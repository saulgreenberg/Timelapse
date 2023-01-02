using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Controls;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;

namespace Timelapse.Database
{
    // This static class will try to merge various .ddb database files into a single database.
    public static class MergeDatabases
    {
        #region Public Static Method - TryMergeDatabasesAsync

        // Given 
        // - a path to a .tdb file  (specifying the root folder)
        // - a list of ddb Files (which must be located in sub-folders relative to the root folder)
        // create a .ddb File in the root folder that merges data found in the .ddbFiles into it, in particular, the tables: 
        // - DataTable
        // - Detections 
        // If fatal errors occur in the merge, abort 
        // Return the relevant error messages in the ErrorsAndWarnings object.
        // Note: if a merged .ddb File already exists in that root folder, it will be backed up and then over-written
        public async static Task<ErrorsAndWarnings> TryMergeDatabasesAsync(
           string templateddbFilePath,
           List<string> sourceddbFilePaths,
           string rootFolderPath,
           string rootFolderName,
           string destinationddbFilePath,
           string destinationddbFileName,
           IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            ErrorsAndWarnings errorMessages = new ErrorsAndWarnings();

            // At this point, we have one or more databases  in the source sourceddbFilePaths that we can try merging
            // Check to see if we can actually open the template. 
            // As we can't have out parameters in an async method, we return the state and the desired templateDatabase as a tuple
            // Original form: if (!(await TemplateDatabase.TryCreateOrOpenAsync(templateDatabasePath, out this.templateDatabase).ConfigureAwait(true))
            Tuple<bool, TemplateDatabase> tupleResult = await TemplateDatabase.TryCreateOrOpenAsync(templateddbFilePath).ConfigureAwait(true);
            TemplateDatabase templateDatabase = tupleResult.Item2;
            if (!tupleResult.Item1)
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                errorMessages.Errors.Add("Could not open the template .tdb file: " + templateddbFilePath);
                return errorMessages;
            }

            // if the merge file exists, move it to the backup folder as we will be overwriting it.
            bool backupMade = false;
            if (File.Exists(destinationddbFilePath))
            {
                // Backup the old merge file by moving it to the backup folder 
                // Note that we do the move instead of copy as we will be overwriting the file anyways
                backupMade = FileBackup.TryCreateBackup(destinationddbFilePath, true);
                File.Delete(destinationddbFilePath);
            }

            FileDatabase fd = await FileDatabase.CreateEmptyDatabase(destinationddbFilePath, templateDatabase).ConfigureAwait(true);
            fd.Dispose();
            fd = null;

            // Open the database
            SQLiteWrapper destinationddb = new SQLiteWrapper(destinationddbFilePath);

            // Get the DataLabels from the DataTable in the main database.
            // We will later check to see if they match their counterparts in each database to merge in
            List<string> mergedddbDataLabels = destinationddb.SchemaGetColumns(Constant.DBTables.FileData);

            int sourceddbFilePathsCount = sourceddbFilePaths.Count;
            Dictionary<string, string> detectionCategories = new Dictionary<string, string>();
            Dictionary<string, string> classificationCategories = new Dictionary<string, string>();
            Dictionary<string, object> infoDictionary = new Dictionary<string, object>();
            for (int i = 0; i < sourceddbFilePathsCount; i++)
            {

                // Try to merge each database into the merged database
                await Task.Run(() =>
                {
                    if (cancelTokenSource.IsCancellationRequested)
                    {
                       return;
                    }
                    // Report progress, introducing a delay to allow the UI thread to update and to make the progress bar linger on the display
                    progress.Report(new ProgressBarArguments((int)((i + 1) / (double)sourceddbFilePathsCount * 100.0),
                        String.Format("Merging {0}/{1} databases. Please wait...", i + 1, sourceddbFilePathsCount),
                        "Merging...",
                        true, false));
                    Thread.Sleep(250);
                    string message = String.Empty;
                    string trimmedPath = sourceddbFilePaths[i].Substring(rootFolderPath.Length + 1);
                    DatabaseFileErrorsEnum databaseFileErrorsEnum;

                    // Check each database file to see if its ok, or determine its error type.
                    // First, lets do a quick check to catch common db errors.
                    databaseFileErrorsEnum = Util.FilesFolders.QuickCheckDatabaseFile(sourceddbFilePaths[i]);
                    if (databaseFileErrorsEnum == DatabaseFileErrorsEnum.Ok || databaseFileErrorsEnum == DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion)
                    {
                        // Things look ok so far. So lets try the merge, which may (or may not) find other errors
                        databaseFileErrorsEnum = MergeDatabases.InsertSourceDataBaseTablesintoDestinationDatabase(destinationddb, sourceddbFilePaths[i], rootFolderPath, mergedddbDataLabels, infoDictionary, detectionCategories, classificationCategories);
                    }
                    // Now check the error state and generate a message if needed.
                    switch (databaseFileErrorsEnum)
                    {
                        case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                            message = "Its template uses different data labels compared to other just-merged databases";
                            break;
                        case DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent:
                            message = "Its template has the same data labels, but in a different order from other just-merged databases";
                            break;
                        case DatabaseFileErrorsEnum.ClassificationDictionaryDiffers:
                            message = "Image recognition classification categories differ from other just-merged databases";
                            break;
                        case DatabaseFileErrorsEnum.DetectionCategoriesDiffers:
                            message = "Image recognition detection categories differ from other just-merged databases";
                            break;
                        case DatabaseFileErrorsEnum.InvalidDatabase:
                            message = "The file does not appear to contain a valid Timelapse database";
                            break;
                        case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                            message = "The file is in a system or hidden folder, which is not allowed";
                            break;
                        case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                            message = "The file is in a disk's top-level root folder, which is not allowed";
                            break;
                        case DatabaseFileErrorsEnum.PathTooLong:
                            message = "The file's path is too long to handle";
                            break;
                        case DatabaseFileErrorsEnum.DoesNotExist:
                            message = "The file does not exist anymore";
                            break;
                        case DatabaseFileErrorsEnum.PreVersion2300:
                            message = "The file needs to be upgraded (see File|Upgrade Timelapse files (.tdb/.ddb) to latest version...)";
                            break;
                        case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                            message = "The file needs to be upgraded again as it was opened with an earlier Timelapse version (see File|Upgrade Timelapse files (.tdb/.ddb) to latest version...)";
                            break;
                        case DatabaseFileErrorsEnum.Ok:
                        default:
                            errorMessages.MergedFiles.Add(trimmedPath);
                            break;
                    }
                    if (!String.IsNullOrWhiteSpace(message))
                    {
                        errorMessages.Warnings.Add(String.Format("{0}: {1}        - {2}", trimmedPath, Environment.NewLine, message));
                    }

                }).ConfigureAwait(true);
            }
            if (cancelTokenSource.IsCancellationRequested)
            {
                errorMessages.MergedFiles.Clear();
                errorMessages.Errors.Clear();
                errorMessages.Warnings.Clear();
                errorMessages.Warnings.Add("Merge cancelled.");
                if (File.Exists(destinationddbFilePath))
                {
                    File.Delete(destinationddbFilePath);
                }
                return errorMessages;
            }

            // After the merged database is constructed, set the Folder column to the current root folder
            if (!String.IsNullOrEmpty(rootFolderName))
            {
                destinationddb.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.RootFolder + Sql.Equal + Sql.Quote(rootFolderName));
            }

            // After the merged database is constructed, reset fields in the ImageSetTable to the defaults i.e., first row, selection all, 
            if (!String.IsNullOrEmpty(rootFolderName))
            {
                destinationddb.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.MostRecentFileID + Sql.Equal + "1");
                destinationddb.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.SearchTerms + Sql.Equal + Sql.Quote(Constant.DatabaseValues.DefaultSearchTerms));
                destinationddb.ExecuteNonQuery(Sql.Update + Constant.DBTables.ImageSet + Sql.Set + Constant.DatabaseColumn.SortTerms + Sql.Equal + Sql.Quote(Constant.DatabaseValues.DefaultSortTerms));
            }
            if (backupMade && (errorMessages.Errors.Any() || errorMessages.Warnings.Any()))
            {
                errorMessages.BackupMessages.Add(String.Format("Note: A backup of your original {0} can be found in the {1} folder", destinationddbFileName, Constant.File.BackupFolder));
            }
            return errorMessages;
        }
        #endregion

        #region Private internal methods

        // Merge a .ddb file specified in the toBeMergedDDBPath path into the currentDDB database.
        // - current: the current database / data structures that contains all the information merged so far
        // - to be merged: the database / data structures to be merged into the current database
        // Also update the Relative path to reflect the new location of the toBeMergedDDB paths as defined in the rootFolderPath
        private static DatabaseFileErrorsEnum InsertSourceDataBaseTablesintoDestinationDatabase(SQLiteWrapper currentDDB, string toBeMergedDDBPath, string rootFolderPath, List<string> currentDataLabels, Dictionary<string, object> currentInfoDictionary, Dictionary<string, string> currentDetectionCategories, Dictionary<string, string> currentClassificationCategories)

        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(currentDDB, nameof(currentDDB));


            bool updateDetections = false;
            bool updateClassifications = false;
            //
            // Part 1. Verify if templates are compatable
            //

            // Check to see if the datalabels in the toBeMergedDDBPath matches those in the currentDataLabels.
            // If not, generate a warning and abort the merge
            SQLiteWrapper toBeMergedDDB = new SQLiteWrapper(toBeMergedDDBPath);
            List<string> toBeMergedDataLabels = toBeMergedDDB.SchemaGetColumns(Constant.DBTables.FileData);
            ListComparisonEnum listComparisonEnum = Compare.CompareLists(currentDataLabels, toBeMergedDataLabels);

            // Both IdenticalToSet2 and different ordered lists are ok. 
            // If the order differs, it uses the order in the primary template. 
            // We could, perhaps, make this more robust by creating a union of elements if they don't have name/value conflicts. However, this would introduce other issues 
            if (listComparisonEnum == ListComparisonEnum.ElementsDiffer)
            {
                return DatabaseFileErrorsEnum.TemplateElementsDiffer;
            }

            //
            // Part 2. Preparation
            //

            // a. We need several temporary tables 
            string attachedDB = "attachedDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";
            string tempDetectionsTable = "tempDetectionsTable";
            string tempInfoTable = "tempInfoTable";
            string tempClassificationsTable = "tempClassificationsTable";
            string tempDetectionCategoriesTable = "tempDetectionCategoriesTable";
            string tempClassificationCategoriesTable = "tempClassificationCategoriesTable";

            // b. Determine the path prefix to add to the Relative Path i.e., the difference between the .tdb root folder and the path to the ddb file
            string pathPrefixToAdd = FilesFolders.GetDifferenceBetweenPathAndSubPath(toBeMergedDDBPath, rootFolderPath);

            // c. Calculate an ID offset (the current max Id), where we will be adding that to all Ids in the ddbFile to merge. 
            //    This will guarantee that there are no duplicate primary keys 
            int offsetId = currentDDB.ScalarGetCountFromSelect(QueryGetMax(Constant.DatabaseColumn.ID, Constant.DBTables.FileData));

            //
            // Part 3. DataTable create merge query
            //

            // Create the first part of the query to:
            // - Attach the ddbFile
            // - Create a temporary DataTable mirroring the one in the toBeMergedDDB (so updates to that don't affect the original ddb)
            // - Update the DataTable with the modified Ids
            // - Update the DataTable with the path prefix
            // - Insert the DataTable  into the main db's DataTable
            // Form: ATTACH DATABASE 'toBeMergedDDB' AS attachedDB; 
            //       CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM attachedDB.DataTable;
            //       UPDATE tempDataTable SET Id = (offsetID + tempDataTable.Id);
            //       UPDATE TempDataTable SET RelativePath =  CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
            //       INSERT INTO DataTable SELECT * FROM tempDataTable;
            string query = Sql.BeginTransactionSemiColon;
            query += QueryAttachDatabaseAs(toBeMergedDDBPath, attachedDB);
            query += QueryCreateTemporaryTableFromExistingTable(tempDataTable, attachedDB, Constant.DBTables.FileData);
            query += QueryAddOffsetToIDInTable(tempDataTable, Constant.DatabaseColumn.ID, offsetId);
            query += QueryAddPrefixToRelativePathInTable(tempDataTable, pathPrefixToAdd);
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.FileData, tempDataTable, currentDataLabels);

            //
            // Part 4.Markers Table create merge query
            //

            // Create the second part of the query to:
            // - Create a temporary Markers Table mirroring the one in the toBeMergedDDB (so updates to that don't affect the original ddb)
            // - Update the Markers Table with the modified Ids
            // - Insert the Markers Table  into the main db's Markers Table
            // Form: CREATE TEMPORARY TABLE tempMarkers AS SELECT * FROM attachedDB.Markers;
            //       UPDATE tempMarkers SET Id = (offsetID + tempMarkers.Id);
            //       INSERT INTO Markers SELECT * FROM tempMarkers;
            query += QueryCreateTemporaryTableFromExistingTable(tempMarkersTable, attachedDB, Constant.DBTables.Markers);
            query += QueryAddOffsetToIDInTable(tempMarkersTable, Constant.DatabaseColumn.ID, offsetId);
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Markers, tempMarkersTable);

            //
            // Part 5. Detection Table merge query
            //

            // Now we need to see if we have to handle detection table updates.
            // Check to see if the currentDDB file and the toBeMergedDDB file each have a Detections table.
            bool toBeMergedDetectionsExists = FileDatabase.TableExists(Constant.DBTables.Detections, toBeMergedDDBPath);
            bool currentDetectionsExists = currentDDB.TableExists(Constant.DBTables.Detections);

            // A. Generate several dictionaries reflecting the contents of the info and category tables as held in the to be merged database
            Dictionary<string, string> toBeMergedDetectionCategories = new Dictionary<string, string>();
            Dictionary<string, string> toBeMergedClassificationCategories = new Dictionary<string, string>();
            Dictionary<string, object> toBeMergedInfoDictionary = new Dictionary<string, object>();
            RecognitionUtilities.GenerateRecognitionDictionariesFromDB(toBeMergedDDBPath, toBeMergedInfoDictionary, toBeMergedDetectionCategories, toBeMergedClassificationCategories);

            if ((false == currentDetectionsExists && false == toBeMergedDetectionsExists)
                || (currentDetectionsExists && false == toBeMergedDetectionsExists))
            {
                // Don't need to do anything with the varous recognition tables as
                // either neither have them, or the one to be merged in doesn't have them
                // triggerUpdate remains false
            }
            else if (false == currentDetectionsExists && toBeMergedDetectionsExists)
            {
                updateDetections = true;
                updateClassifications = true;
                // Since the current DB doesn't 
                // The current database doesn't have detections, but the database to merged does,
                // Thus we need to create the detection tables in the current database.
                RecognitionDatabases.PrepareRecognitionTablesAndColumns(currentDDB, false);

                // As its the first time we see a database with detections, import the Detection Categories, Classification Categories and Info 
                // This becomes the base comparison against which future databases will be compared to,
                // in terms of generating best fit info, and whether detection/classification categories conflict or can be merged together.

                // To do this, we first create temporary tables from the toBeMerged db 
                query += QueryCreateTemporaryTableFromExistingTable(tempInfoTable, attachedDB, Constant.DBTables.Info);
                query += QueryCreateTemporaryTableFromExistingTable(tempDetectionCategoriesTable, attachedDB, Constant.DBTables.DetectionCategories);
                query += QueryCreateTemporaryTableFromExistingTable(tempClassificationCategoriesTable, attachedDB, Constant.DBTables.ClassificationCategories);

                // Now we insert those tables into the current database
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.DetectionCategories, attachedDB);
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.ClassificationCategories, attachedDB);
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.Info, attachedDB);

                // Update the various dictionaries to reflect their current values
                MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(toBeMergedInfoDictionary, currentInfoDictionary);
                MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(toBeMergedDetectionCategories, currentDetectionCategories);
                MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(toBeMergedClassificationCategories, currentClassificationCategories);

                // At this point, we have all the recognition database tables created, 
                // with filled in info and category tables
            }
            else // if (currentDetectionsExists && toBeMergedDetectionsExists)
            {
                updateDetections = true;
                updateClassifications = true;

                // Both the current database and the database to merged have recognition tables

                // A. Generate a new info structure that is a best effort combination of the db and json info structure,
                //    and then update the jsonRecognizer to match that. Note the we do it even if no update is really needed, as its lightweight

                Dictionary<string, object> mergedInfoDictionary = RecognitionUtilities.GenerateBestRecognitionInfoFromTwoInfos(currentInfoDictionary, toBeMergedInfoDictionary);

                // Clear the info table as we will be completely replacing it
                // query += Sql.DeleteFrom + Constant.DBTables.Info + Sql.Semicolon;

                // Update the info table completely with these new values, and reset the ImageSetTable bounding box threshold
                query += MergeDatabases.UpdateInfoTableWithValues(
                    (string)mergedInfoDictionary[Constant.InfoColumns.Detector],
                    (string)mergedInfoDictionary[Constant.InfoColumns.DetectorVersion],
                    (string)mergedInfoDictionary[Constant.InfoColumns.DetectionCompletionTime],
                    (string)mergedInfoDictionary[Constant.InfoColumns.Classifier],
                    (string)mergedInfoDictionary[Constant.InfoColumns.ClassificationCompletionTime],
                    (float)mergedInfoDictionary[Constant.InfoColumns.TypicalDetectionThreshold],
                    (float)mergedInfoDictionary[Constant.InfoColumns.ConservativeDetectionThreshold],
                    (float)mergedInfoDictionary[Constant.InfoColumns.TypicalClassificationThreshold]);
                query += UpdateImageSetTableWithUndefinedBoundingBox();

                // Update the various dictionaries to reflect their current values
                MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(mergedInfoDictionary, currentInfoDictionary);

                // B: Generate a new detection category db if the categories in the current and to be merged dictionary can be merged together. 
                if (toBeMergedDetectionCategories.Count > 0 || currentDetectionCategories.Count > 0)
                {
                    // There is something to add
                    if (Util.Dictionaries.MergeDictionaries(toBeMergedDetectionCategories, currentDetectionCategories, out Dictionary<string, string> mergedDetectionCategories))
                    {
                        // Clear the DetectionCategories table as we will be completely replacing it
                        query += Sql.DeleteFrom + Constant.DBTables.DetectionCategories + Sql.Semicolon;

                        // update the classification categories in the table.
                        query += Sql.InsertInto + Constant.DBTables.DetectionCategories
                            + Sql.OpenParenthesis + Constant.DetectionCategoriesColumns.Category + Sql.Comma + Constant.DetectionCategoriesColumns.Label + Sql.CloseParenthesis + Sql.Values;
                        foreach (KeyValuePair<string, string> kvp in mergedDetectionCategories)
                        {
                            query += Sql.OpenParenthesis + Sql.Quote(kvp.Key) + Sql.Comma + Sql.Quote(kvp.Value) + Sql.CloseParenthesis + Sql.Comma;
                        }
                        query = query.Substring(0, query.LastIndexOf(Sql.Comma));
                        query += Sql.Semicolon;
                        // Update the various dictionaries to reflect their current values
                        MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(mergedDetectionCategories, currentDetectionCategories);
                    }
                    else
                    {
                        // System.Diagnostics.Debug.Print("merged failed for detection categories");
                        return DatabaseFileErrorsEnum.DetectionCategoriesDiffers;
                    }
                }
                // D: Generate a new classification category db if the categories in the current and to be merged dictionary can be merged together. 
                if (toBeMergedClassificationCategories.Count > 0 || currentClassificationCategories.Count > 0)
                {
                    // There is something to add
                    if (Util.Dictionaries.MergeDictionaries(toBeMergedClassificationCategories, currentClassificationCategories, out Dictionary<string, string> mergedClassificationCategories))
                    {
                        // Clear the ClassificationCategories table as we will be completely replacing it
                        query += Sql.DeleteFrom + Constant.DBTables.ClassificationCategories + Sql.Semicolon;

                        // update the classification categories in the table.
                        query += Sql.InsertInto + Constant.DBTables.ClassificationCategories
                            + Sql.OpenParenthesis + Constant.ClassificationCategoriesColumns.Category + Sql.Comma + Constant.ClassificationCategoriesColumns.Label + Sql.CloseParenthesis + Sql.Values;
                        foreach (KeyValuePair<string, string> kvp in mergedClassificationCategories)
                        {
                            query += Sql.OpenParenthesis + Sql.Quote(kvp.Key) + Sql.Comma + Sql.Quote(kvp.Value) + Sql.CloseParenthesis + Sql.Comma;
                        }
                        query = query.Substring(0, query.LastIndexOf(Sql.Comma));
                        query += Sql.Semicolon;

                        // Update the various dictionaries to reflect their current values
                        MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(mergedClassificationCategories, currentClassificationCategories);
                    }
                    else
                    {
                        // System.Diagnostics.Debug.Print("merged failed for classification categories");
                        return DatabaseFileErrorsEnum.ClassificationDictionaryDiffers;
                    }
                }
            }


            // E. The database to merge in has detections, so the SQL query also updates the Detections table.

            if (updateClassifications && updateDetections)
            {
                // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
                // The offeset should be 0 if there are no detections in the main DB, as we will be creating the detection table and then just adding to it.
                int offsetDetectionId = currentDetectionsExists
                ? currentDDB.ScalarGetCountFromSelect(QueryGetMax(Constant.DetectionColumns.DetectionID, Constant.DBTables.Detections))
                : 0;

                query += QueryCreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedDB, Constant.DBTables.Detections);
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DatabaseColumn.ID, offsetId);
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DetectionColumns.DetectionID, offsetDetectionId);
                query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Detections, tempDetectionsTable);

                // Similar to the above, we also update the classifications
                int offsetClassificationId = (currentDetectionsExists)
                    ? currentDDB.ScalarGetCountFromSelect(QueryGetMax(Constant.ClassificationColumns.ClassificationID, Constant.DBTables.Classifications))
                    : 0;
                query += QueryCreateTemporaryTableFromExistingTable(tempClassificationsTable, attachedDB, Constant.DBTables.Classifications);
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.ClassificationID, offsetClassificationId);
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.DetectionID, offsetDetectionId);
                query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Classifications, tempClassificationsTable);
            }
            query += Sql.EndTransactionSemiColon;
            currentDDB.ExecuteNonQuery(query);

            return DatabaseFileErrorsEnum.Ok;
        }
        #endregion

        #region Private Methods - Query formation helpers
        // Form: "Select Max(columnName) from tableName"
        private static string QueryGetMax(string columnName, string tableName)
        {
            return Sql.Select + Sql.Max + Sql.OpenParenthesis + columnName + Sql.CloseParenthesis + Sql.From + tableName;
        }

        // Form: ATTACH DATABASE 'databasePath' AS alias;
        private static string QueryAttachDatabaseAs(string databasePath, string alias)
        {
            return Sql.AttachDatabase + Sql.Quote(databasePath) + Sql.As + alias + Sql.Semicolon;
        }

        // Form: CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM dataBaseName.tableName;
        private static string QueryCreateTemporaryTableFromExistingTable(string tempDataTable, string dataBaseName, string tableName)
        {
            return Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + dataBaseName + Sql.Dot + tableName + Sql.Semicolon;
        }

        // Form: UPDATE dataTable SET IDColumn = (offset + dataTable.Id);
        private static string QueryAddOffsetToIDInTable(string tableName, string IDColumn, int offset)
        {
            return Sql.Update + tableName + Sql.Set + IDColumn + Sql.Equal + Sql.OpenParenthesis + offset.ToString() + Sql.Plus + tableName + Sql.Dot + IDColumn + Sql.CloseParenthesis + Sql.Semicolon;
        }

        //Form:  UPDATE tableName SET RelativePath = CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
        private static string QueryAddPrefixToRelativePathInTable(string tableName, string pathPrefixToAdd)
        {
            // A longer query, so split into three lines
            // Note that tableName must be a DataTable for this to work
            string query = Sql.Update + tableName + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.CaseWhen + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(String.Empty);
            query += Sql.Then + Sql.OpenParenthesis + Sql.Quote(pathPrefixToAdd) + Sql.Concatenate + Constant.DatabaseColumn.RelativePath + Sql.CloseParenthesis;
            query += Sql.Else + Sql.OpenParenthesis + Sql.Quote(pathPrefixToAdd + "\\") + Sql.Concatenate + Constant.DatabaseColumn.RelativePath + Sql.CloseParenthesis + " END " + Sql.Semicolon;
            return query;
        }

        //  Form: INSERT INTO table1 SELECT * FROM table2;
        private static string QueryInsertTable2DataIntoTable1(string table1, string table2)
        {
            return Sql.InsertInto + table1 + Sql.SelectStarFrom + table2 + Sql.Semicolon;
        }

        private static string QueryInsertTable2DataIntoTable1(string table1, string table2, List<string> listDataLabels)
        {
            string dataLabels = String.Empty;
            foreach (string datalabels in listDataLabels)
            {
                dataLabels += datalabels + ",";
            }
            dataLabels = dataLabels.TrimEnd(',');
            return Sql.InsertInto + table1 + Sql.Select + dataLabels + Sql.From + table2 + Sql.Semicolon;
        }

        //  Form: INSERT INTO table SELECT * FROM dataBase.table;
        private static string QueryInsertTableDataFromAnotherDatabase(string table, string fromDatabase)
        {
            return Sql.InsertInto + table + Sql.SelectStarFrom + fromDatabase + Sql.Dot + table + Sql.Semicolon;
        }
        #endregion

        private static string UpdateInfoTableWithValues(string detector, string megadetector_version, string detection_completion_time, string classifier, string classification_completion_time, double typical_detection_threshold, double conservative_detection_threshold, double typical_classification_threshold)
        {
            return Sql.Update + Constant.DBTables.Info + Sql.Set
                + Constant.InfoColumns.Detector + Sql.Equal + Sql.Quote(detector) + Sql.Comma
                + Constant.InfoColumns.DetectorVersion + Sql.Equal + Sql.Quote(megadetector_version) + Sql.Comma
                + Constant.InfoColumns.DetectionCompletionTime + Sql.Equal + Sql.Quote(detection_completion_time) + Sql.Comma
                + Constant.InfoColumns.Classifier + Sql.Equal + Sql.Quote(classifier) + Sql.Comma
                + Constant.InfoColumns.ClassificationCompletionTime + Sql.Equal + Sql.Quote(classification_completion_time) + Sql.Comma
                + Constant.InfoColumns.TypicalDetectionThreshold + Sql.Equal + (Math.Round(typical_detection_threshold * 100) / 100).ToString() + Sql.Comma
                + Constant.InfoColumns.ConservativeDetectionThreshold + Sql.Equal + (Math.Round(conservative_detection_threshold * 100) / 100).ToString() + Sql.Comma
                + Constant.InfoColumns.TypicalClassificationThreshold + Sql.Equal + (Math.Round(typical_classification_threshold * 100) / 100).ToString()
                + Sql.Semicolon;
        }

        private static string UpdateImageSetTableWithUndefinedBoundingBox()
        {
            return Sql.Update + Constant.DBTables.ImageSet + Sql.Set
                + Constant.DatabaseColumn.BoundingBoxDisplayThreshold + Sql.Equal + Constant.RecognizerValues.BoundingBoxDisplayThresholdDefault + Sql.Semicolon;
        }

        private static void DictionaryReplaceSecondDictWithFirstDictElements(Dictionary<string, object> first, Dictionary<string, object> second)
        {
            second.Clear();
            foreach (KeyValuePair<string, object> kvp in first)
            {
                second.Add(kvp.Key, kvp.Value);
            }
        }
        private static void DictionaryReplaceSecondDictWithFirstDictElements(Dictionary<string, string> first, Dictionary<string, string> second)
        {
            second.Clear();
            foreach (KeyValuePair<string, string> kvp in first)
            {
                second.Add(kvp.Key, kvp.Value);
            }
        }
    }
}