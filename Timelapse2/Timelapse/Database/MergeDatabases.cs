using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Controls;
using Timelapse.Detection;
using Timelapse.Enums;
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
           IProgress<ProgressBarArguments> progress)
        //string templateddbFilePath, List<string> sourceddbFilePaths, IProgress<ProgressBarArguments> progress)
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
                    // Report progress, introducing a delay to allow the UI thread to update and to make the progress bar linger on the display
                    progress.Report(new ProgressBarArguments((int)((i + 1) / (double)sourceddbFilePathsCount * 100.0),
                        String.Format("Merging {0}/{1} databases. Please wait...", i + 1, sourceddbFilePathsCount),
                        "Merging...",
                        false, false));
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

        // Merge a .ddb file specified in the sourceddbPath path into the destinationddb database.
        // Also update the Relative path to reflect the new location of the sourceddb paths as defined in the rootFolderPath
        private static DatabaseFileErrorsEnum InsertSourceDataBaseTablesintoDestinationDatabase(SQLiteWrapper destinationddb, string SourceddbPath, string rootFolderPath, List<string> sourceDataLabels, Dictionary<string, object> previousInfoDictionary, Dictionary<string, string> previousDetectionCategories, Dictionary<string, string> previousClassificationCategories)

        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(destinationddb, nameof(destinationddb));

            // Check to see if the datalabels in the sourceddb matches those in the destinationDataLabels.
            // If not, generate a warning and abort the merge
            SQLiteWrapper sourceddb = new SQLiteWrapper(SourceddbPath);
            List<string> destinationDataLabels = sourceddb.SchemaGetColumns(Constant.DBTables.FileData);
            ListComparisonEnum listComparisonEnum = Compare.CompareLists(sourceDataLabels, destinationDataLabels);

            // Both Identical and different ordered lists are ok. If the order differs,
            // it uses the order in the primary template. if (listComparisonEnum == ListComparisonEnum.ElementsDiffer)
            if (listComparisonEnum == ListComparisonEnum.ElementsDiffer)
            {
                return DatabaseFileErrorsEnum.TemplateElementsDiffer;
            }

            string attachedDB = "attachedDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";
            string tempDetectionsTable = "tempDetectionsTable";
            string tempClassificationsTable = "tempClassificationsTable";

            // Determine the path prefix to add to the Relative Path i.e., the difference between the .tdb root folder and the path to the ddb file
            string pathPrefixToAdd = GetDifferenceBetweenPathAndSubPath(SourceddbPath, rootFolderPath);

            // Calculate an ID offset (the current max Id), where we will be adding that to all Ids in the ddbFile to merge. 
            // This will guarantee that there are no duplicate primary keys 
            int offsetId = destinationddb.ScalarGetCountFromSelect(QueryGetMax(Constant.DatabaseColumn.ID, Constant.DBTables.FileData));

            // Create the first part of the query to:
            // - Attach the ddbFile
            // - Create a temporary DataTable mirroring the one in the sourceddb (so updates to that don't affect the original ddb)
            // - Update the DataTable with the modified Ids
            // - Update the DataTable with the path prefix
            // - Insert the DataTable  into the main db's DataTable
            // Form: ATTACH DATABASE 'sourceddb' AS attachedDB; 
            //       CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM attachedDB.DataTable;
            //       UPDATE tempDataTable SET Id = (offsetID + tempDataTable.Id);
            //       UPDATE TempDataTable SET RelativePath =  CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
            //       INSERT INTO DataTable SELECT * FROM tempDataTable;
            string query = Sql.BeginTransactionSemiColon;
            query += QueryAttachDatabaseAs(SourceddbPath, attachedDB);
            query += QueryCreateTemporaryTableFromExistingTable(tempDataTable, attachedDB, Constant.DBTables.FileData);
            query += QueryAddOffsetToIDInTable(tempDataTable, Constant.DatabaseColumn.ID, offsetId);
            query += QueryAddPrefixToRelativePathInTable(tempDataTable, pathPrefixToAdd);
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.FileData, tempDataTable, sourceDataLabels);

            // Create the second part of the query to:
            // - Create a temporary Markers Table mirroring the one in the sourceddb (so updates to that don't affect the original ddb)
            // - Update the Markers Table with the modified Ids
            // - Insert the Markers Table  into the main db's Markers Table
            // Form: CREATE TEMPORARY TABLE tempMarkers AS SELECT * FROM attachedDB.Markers;
            //       UPDATE tempMarkers SET Id = (offsetID + tempMarkers.Id);
            //       INSERT INTO Markers SELECT * FROM tempMarkers;
            query += QueryCreateTemporaryTableFromExistingTable(tempMarkersTable, attachedDB, Constant.DBTables.Markers);
            query += QueryAddOffsetToIDInTable(tempMarkersTable, Constant.DatabaseColumn.ID, offsetId);
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Markers, tempMarkersTable);

            // Now we need to see if we have to handle detection table updates.
            // Check to see if the destinationddb file and the sourceddb file each have a Detections table.
            bool sourceDetectionsExists = FileDatabase.TableExists(Constant.DBTables.Detections, SourceddbPath);
            bool destinationDetectionsExists = destinationddb.TableExists(Constant.DBTables.Detections);

            // If the main database doesn't have detections, but the database to merge into it does,
            // then we have to create the detection tables to the main database.
            if (destinationDetectionsExists == false && sourceDetectionsExists)
            {
                DetectionDatabases.CreateOrRecreateTablesAndColumns(destinationddb);

                // As its the first time we see a database with detections, import the Detection Categories, Classification Categories and Info 
                // This assumes (perhaps incorrectly) that all databases the merge in have the same detection/classification categories and info.
                // FORM: INSERT INTO DetectionCategories SELECT * FROM attachedDB.DetectionCategories;
                //              INSERT INTO ClassificationCategories SELECT * FROM attachedDB.ClassifciationCategories;
                //              INSERT INTO Info SELECT * FROM attachedDB.Info;
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.DetectionCategories, attachedDB);
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.ClassificationCategories, attachedDB);
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.Info, attachedDB);
                destinationDetectionsExists = true;
            }

            // Generate several dictionaries reflecting the contents of several detection tables as held in the source (to be merged in) database
            Dictionary<string, string> currentDetectionCategories = new Dictionary<string, string>();
            Dictionary<string, string> currentClassificationCategories = new Dictionary<string, string>();
            Dictionary<string, object> currentInfoDictionary = new Dictionary<string, object>();
            GenerateDetectionDictionariesFromDB(SourceddbPath, currentInfoDictionary, currentDetectionCategories, currentClassificationCategories);

            // Take action if the  ddb to be merged does not have an MD version, or if it has a higher detector version than the merged database being created,
            bool triggerUpdateInfoValues = false;
            if (destinationDetectionsExists && currentInfoDictionary.TryGetValue("megadetector_version", out object currentMegadetector_version))
            {
                if (false == previousInfoDictionary.TryGetValue("megadetector_version", out object prev_megadetector_version))
                {
                    // If we can't get a version from the info table, than just set it to unknown.
                    prev_megadetector_version = Constant.DetectionValues.MDVersionUnknown;
                }
                if (IsMegadetectorVersionHigher((string)prev_megadetector_version, (string)currentMegadetector_version))
                {
                    // The ddb to be merged in has a higher detector version than the merged database being created
                    // Update the previous info dictionary to the current one (so it can be compared in the next iteration)
                    // and set a flag to trigger updating the values in the merged database to the ones in this dictionary
                    // Note that we don't bother if the current dictionary is empty
                    if (currentInfoDictionary.Count > 0)
                    {
                        MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(currentInfoDictionary, previousInfoDictionary);
                    }
                    triggerUpdateInfoValues = true;
                }
            }

            // Abort if the  ddb to be merged has difference detection categories than the merged database being created
            if (destinationDetectionsExists)
            {
                if (previousDetectionCategories.Count == 0 && currentDetectionCategories.Count == 0)
                {
                    // Do nothing, as so far we have not seen any files with detection categories 
                }
                else if (previousDetectionCategories.Count > 0 && currentDetectionCategories.Count == 0)
                {
                    // Do nothing. While the merged database file has detection categories , the database file
                    // to be merged in has none
                }
                else
                {
                    if (previousDetectionCategories.Count == 0 && currentDetectionCategories.Count > 0)
                    {
                        // The merged database file has no detection categories, but the database file to be merged has some.
                        // So we need to add them. Note that any future databases containing detection categories will be
                        // compared to this one for differences. 
                        // Set a flag to trigger updating the categories in the merged database to the ones in this dictionary
                        MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(currentDetectionCategories, previousDetectionCategories);
                    }
                    else if (false == MergeDatabases.CompareDictionaries(previousDetectionCategories, currentDetectionCategories))
                    {
                        // Both the merged database file and the database file to be merged has detection categories.
                        // However, they differ, so abort the merge and indicate the error
                        return DatabaseFileErrorsEnum.DetectionCategoriesDiffers;
                    }
                }
            }

            // Take action if the  ddb to be merged has difference classification categories than the merged database being created
            bool updateClassificationCategories = false;
            if (destinationDetectionsExists)
            {
                if (previousClassificationCategories.Count == 0 && currentClassificationCategories.Count == 0)
                {
                    // Do nothing, as so far we have not seen any files with classification categories 
                }
                else if (previousClassificationCategories.Count > 0 && currentClassificationCategories.Count == 0)
                {
                    // Do nothing. While the merged database file has classification categories , the database file
                    // to be merged in has none
                }
                else
                {
                    if (previousClassificationCategories.Count == 0 && currentClassificationCategories.Count > 0)
                    {
                        // The merged database file has no classification categories, but the database file to be merged has some.
                        // So we need to add them. Note that any future databases containing classification categories will be
                        // compared to this one for differences. 
                        // Update the previous info dictionary to the current one (so it can be compared in the next iteration)
                        // Set a flag to trigger updating the categories in the merged database to the ones in this dictionary
                        MergeDatabases.DictionaryReplaceSecondDictWithFirstDictElements(currentClassificationCategories, previousClassificationCategories);
                        updateClassificationCategories = true;
                    }
                    else if (false == MergeDatabases.CompareDictionaries(previousClassificationCategories, currentClassificationCategories))
                    {
                        // Both the merged database file and the database file to be merged has classification categories.
                        // However, they differ, so abort the merge and indicate the error
                        return DatabaseFileErrorsEnum.ClassificationDictionaryDiffers;
                    }
                }
            }

            // Create the third part of the query only if the toMergeddb contains a detections table
            // (as otherwise we don't have to update the detection table in the main ddb.
            // - Create a temporary Detections table mirroring the one in the toMergeddb (so updates to that don't affect the original ddb)
            // - Update the Detections Table with both the modified Ids and detectionIDs
            // - Insert the Detections Table into the main db's Detections Table
            // Form: CREATE TEMPORARY TABLE tempDetectionsTable AS SELECT * FROM attachedDB.Detections;
            //       UPDATE TempDetectionsTable SET Id = (offsetId + TempDetectionsTable.Id);
            //       UPDATE TempDetectionsTable SET DetectionID = (offsetDetectionId + TempDetectionsTable.DetectionId);
            //       INSERT INTO Detections SELECT * FROM TempDetectionsTable;"
            // The Classifications form is similar, except it used the classification-specific tables, ids, offsets, etc.
            if (sourceDetectionsExists)
            {
                // The database to merge in has detections, so the SQL query also updates the Detections table.
                // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
                // However, the offeset should be 0 if there are no detections in the main DB, so we can just reusue this as is.
                // as we will be creating the detection table and then just adding to it.
                int offsetDetectionId = (destinationDetectionsExists)
                    ? destinationddb.ScalarGetCountFromSelect(QueryGetMax(Constant.DetectionColumns.DetectionID, Constant.DBTables.Detections))
                    : 0;
                query += QueryCreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedDB, Constant.DBTables.Detections);
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DatabaseColumn.ID, offsetId);
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DetectionColumns.DetectionID, offsetDetectionId);
                query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Detections, tempDetectionsTable);

                // Similar to the above, we also update the classifications
                int offsetClassificationId = (destinationDetectionsExists)
                    ? destinationddb.ScalarGetCountFromSelect(QueryGetMax(Constant.ClassificationColumns.ClassificationID, Constant.DBTables.Classifications))
                    : 0;
                query += QueryCreateTemporaryTableFromExistingTable(tempClassificationsTable, attachedDB, Constant.DBTables.Classifications);
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.ClassificationID, offsetClassificationId);
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.DetectionID, offsetDetectionId);
                query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Classifications, tempClassificationsTable);

                if (triggerUpdateInfoValues)
                {
                    // This is triggered whenever a higher Megadetector version was found in the database file to be merged in 
                    // when compared to the merged database file. 
                    // This means that the merged database file will always have info values from the database with the highest Megadetector version
                    // Update the info table completely with these new values, and reset the ImageSetTable bounding box threshold
                    // This is triggered only when the to-be-merged database info table has an MD version higher than what is currently there. 
                    query += MergeDatabases.UpdateInfoTableWithValues(
                        (string)currentInfoDictionary[Constant.InfoColumns.Detector],
                        (string)currentInfoDictionary[Constant.InfoColumns.DetectorVersion],
                        (string)currentInfoDictionary[Constant.InfoColumns.DetectionCompletionTime],
                        (string)currentInfoDictionary[Constant.InfoColumns.Classifier],
                        (string)currentInfoDictionary[Constant.InfoColumns.ClassificationCompletionTime],
                        (double)currentInfoDictionary[Constant.InfoColumns.TypicalDetectionThreshold],
                        (double)currentInfoDictionary[Constant.InfoColumns.ConservativeDetectionThreshold],
                        (double)currentInfoDictionary[Constant.InfoColumns.TypicalClassificationThreshold]);
                    query += UpdateImageSetTableWithUndefinedBoundingBox();
                }
                if (updateClassificationCategories)
                {
                    // This is triggered whenever we have to update the classification categories in the table.
                    // Triggering normally occurs when the merged database has no classification categories, but the database to be merged in does have classification categories.
                    // Because we always check to see if subsequent classification categories are different (and abort if that is the case),
                    // this is usually only done once if at least one database has classifcation categories.

                    // Clear the table
                    query += Sql.DeleteFrom + Constant.DBTables.ClassificationCategories + Sql.Semicolon;
                    // Add the new values
                    query += Sql.InsertInto + Constant.DBTables.ClassificationCategories
                        + Sql.OpenParenthesis + Constant.ClassificationCategoriesColumns.Category + Sql.Comma + Constant.ClassificationCategoriesColumns.Label + Sql.CloseParenthesis + Sql.Values;
                    foreach (KeyValuePair<string, string> kvp in currentClassificationCategories)
                    {
                        query += Sql.OpenParenthesis + Sql.Quote(kvp.Key) + Sql.Comma + Sql.Quote(kvp.Value) + Sql.CloseParenthesis + Sql.Comma;
                    }
                    query = query.Substring(0, query.LastIndexOf(Sql.Comma));
                    query += Sql.Semicolon;
                }
            }
            query += Sql.EndTransactionSemiColon;
            destinationddb.ExecuteNonQuery(query);

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

        #region Private methods
        // Find the difference between two paths (ignoring the file name, if any) and return it
        // For example, given:
        // path1 =    "C:\\Users\\Owner\\Desktop\\Test sets\\MergeLarge\\1\\TimelapseData.ddb"
        // path2 =    "C:\\Users\\Owner\\Desktop\\Test sets\\MergeLarge" 
        // return     "1"
        private static string GetDifferenceBetweenPathAndSubPath(string path1, string path2)
        {
            if (path1.Length > path2.Length)
            {
                return Path.GetDirectoryName(path1).Replace(path2 + "\\", "");
            }
            else
            {
                return Path.GetDirectoryName(path2).Replace(path1 + "\\", "");
            }
        }

        private static void GenerateDetectionDictionariesFromDB(string ddbPath, Dictionary<string, object> infoDictionary, Dictionary<string, string> detectionCategoriesDictionary, Dictionary<string, string> classificationCategoriesDictionary)
        {
            SQLiteWrapper db = new SQLiteWrapper(ddbPath);
            if (false == db.TableExists(Constant.DBTables.Info))
            {
                // There are no detection-based tables in this database
                return;
            }
            using (DataTable dataTable = db.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Info))
            {
                Dictionary<string, object> tmpDict = new Dictionary<string, object>();
                if (dataTable.Rows.Count != 0)
                {
                    DataRow row = dataTable.Rows[0];
                    tmpDict = row.Table.Columns
                            .Cast<DataColumn>()
                            .ToDictionary(c => c.ColumnName, c => row[c.ColumnName]);
                }
                foreach (KeyValuePair<string, object> item in tmpDict)
                {
                    infoDictionary.Add(item.Key, item.Value);
                }
            }

            using (DataTable dataTable = db.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.DetectionCategories))
            {
                int dataTableRowCount = dataTable.Rows.Count;
                for (int i = 0; i < dataTableRowCount; i++)
                {
                    DataRow row = dataTable.Rows[i];
                    detectionCategoriesDictionary.Add((string)row[Constant.DetectionCategoriesColumns.Category], (string)row[Constant.DetectionCategoriesColumns.Label]);
                }
            }
            using (DataTable dataTable = db.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.ClassificationCategories))
            {
                int dataTableRowCount = dataTable.Rows.Count;
                for (int i = 0; i < dataTableRowCount; i++)
                {
                    DataRow row = dataTable.Rows[i];
                    classificationCategoriesDictionary.Add((string)row[Constant.ClassificationCategoriesColumns.Category], (string)row[Constant.ClassificationCategoriesColumns.Label]);
                }
            }
        }

        #endregion

        private static bool CompareDictionaries(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
        {
            bool equal = false;
            if (dict1.Count == dict2.Count) // Require equal count.
            {
                equal = true;
                foreach (var pair in dict1)
                {
                    if (dict2.TryGetValue(pair.Key, out string value))
                    {
                        // Require value be equal.
                        if (value != pair.Value)
                        {
                            equal = false;
                            break;
                        }
                    }
                    else
                    {
                        // Require key be present.
                        equal = false;
                        break;
                    }
                }
            }
            return equal;
        }

        private static string UpdateInfoTableWithValues(string detector, string megadetector_version, string detection_completion_time, string classifier, string classification_completion_time, double typical_detection_threshold, double conservative_detection_threshold, double typical_classification_threshold)
        {
            return Sql.Update + Constant.DBTables.Info + Sql.Set
                + Constant.InfoColumns.Detector + Sql.Equal + Sql.Quote(detector) + Sql.Comma
                + Constant.InfoColumns.DetectorVersion + Sql.Equal + Sql.Quote(megadetector_version) + Sql.Comma
                + Constant.InfoColumns.DetectionCompletionTime + Sql.Equal + Sql.Quote(detection_completion_time) + Sql.Comma
                + Constant.InfoColumns.Classifier + Sql.Equal + Sql.Quote(classifier) + Sql.Comma
                + Constant.InfoColumns.ClassificationCompletionTime + Sql.Equal + Sql.Quote(classification_completion_time) + Sql.Comma
                + Constant.InfoColumns.TypicalDetectionThreshold + Sql.Equal + typical_detection_threshold.ToString() + Sql.Comma
                + Constant.InfoColumns.ConservativeDetectionThreshold + Sql.Equal + conservative_detection_threshold.ToString() + Sql.Comma
                + Constant.InfoColumns.TypicalClassificationThreshold + Sql.Equal + typical_classification_threshold.ToString()
                + Sql.Semicolon;
        }

        private static string UpdateImageSetTableWithUndefinedBoundingBox()
        {
            return Sql.Update + Constant.DBTables.ImageSet + Sql.Set
                + Constant.DatabaseColumn.BoundingBoxDisplayThreshold + Sql.Equal + Constant.DetectionValues.BoundingBoxDisplayThresholdDefault + Sql.Semicolon;
        }

        private static bool IsMegadetectorVersionHigher(string source, string destination)
        {
            if (source == destination)
            {
                return false;
            }
            if (source == Constant.DetectionValues.MDVersionUnknown)
            {
                return true;
            }
            return string.Compare(source, destination) == -1;
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