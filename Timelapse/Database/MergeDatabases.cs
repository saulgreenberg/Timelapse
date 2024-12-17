using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using Timelapse.Constant;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;

namespace Timelapse.Database
{
    // This static class will try to merge various .ddb database files into a single database.
    public static class MergeDatabases
    {
        #region Public Static Method - TryCreateEmptyDatabaseFromTemplateAsync
        // Given 
        // - a path to a .tdb file  (specifying the root folder)
        // - a path to the ddb File we want to create
        // Create an empty .ddb File in the root folder 
        // If fatal error occurs, abort 
        // Preconditions:
        // - the template file should exist
        // - the destinationDdbPath should be tested to make sure it doesn't already exists.
        public static async Task<bool> TryCreateEmptyDatabaseFromTemplateAsync(
            string templateTdbPath,
            string destinationDdbPath)
        {
            // Check to see if we can actually open the template. 
            // As we can't have out parameters in an async method, we return the state and the desired templateDatabase as a tuple
            Tuple<bool, CommonDatabase> tupleResult = await CommonDatabase.TryCreateOrOpenAsync(templateTdbPath).ConfigureAwait(true);

            if (!tupleResult.Item1)
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                return false;
            }

            // Create the empty database based on the provided template
            CommonDatabase templateDatabase = tupleResult.Item2;
            FileDatabase fd = await FileDatabase.CreateEmptyDatabase(destinationDdbPath, templateDatabase).ConfigureAwait(true);
            fd.Dispose();
            // ReSharper disable once RedundantAssignment
            fd = null;

            //
            // At this point, we should have an empty database
            // 
            return true;
        }

        #endregion

        #region Check: CheckIfDatabaseTemplatesAreMergeCompatable
        // TODO This is not a fully robust way of checking the templates. E.g., does not check for differences in defaults, choice lists, etc.
        // Note that we already have robust template-checking code that we could reuse. Just have to check to see how we can use it.
        // It also may not be really needed, as merges only updates the data (although changes in data types can cause lead to mismatched data, eg. text to number).

        public static DatabaseFileErrorsEnum CheckIfDatabaseTemplatesAreMergeCompatable(SQLiteWrapper sourceDdb, SQLiteWrapper destinationDdb, int levelsToIgnore)
        {
            bool metadataCompatable = CheckIfMetadataTemplatesAreMergeCompatable(sourceDdb, destinationDdb, levelsToIgnore);

            // We only check to see if the elements differ, regardless of order. 
            // We could, perhaps, make this more robust by creating a union of elements if they don't have name/value conflicts. However, this would introduce other issues 
            return metadataCompatable
                ? DatabaseFileErrorsEnum.Ok
                : DatabaseFileErrorsEnum.MetadataLevelsDiffer;
        }
        #endregion

        #region CheckIfMetadataTemplatesAreMergeCompatable
        public static bool CheckIfMetadataTemplatesAreMergeCompatable(SQLiteWrapper sourceDdb,
            SQLiteWrapper destinationDdb, int levelsToIgnore)
        {
            // Check if both have MetadataInfo tables
            bool sourceDdbMetadataInfoExists = sourceDdb.TableExists(DBTables.MetadataInfo);
            bool destinationDdbMetadataInfoExists = destinationDdb.TableExists(DBTables.MetadataInfo);

            // Check: Does the sourceDdb contain metadata tables
            if (false == sourceDdbMetadataInfoExists)
            {
                // We can always merge if there is no metadata info in the source Ddb 
                return true;
            }

            // We know that the source has some metadata structures, so lets load them
            using (CommonDatabase sourceDdbTemplate = new CommonDatabase(sourceDdb.FilePath))
            {
                sourceDdbTemplate.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();

                // Check: Does the source have one or more metadata levels ?
                if (null == sourceDdbTemplate.MetadataInfo || sourceDdbTemplate.MetadataInfo.RowCount == 0)
                {
                    // It doesn't, so we can ignore Metadata comparisons
                    return true;
                }

                // At this point, we know that the sourceDdb MetadataInfo table exists with some levels in it.

                // Check: Does the DestinationDdb has some metadata info in it?
                if (false == destinationDdbMetadataInfoExists)
                {
                    // We cannot merge if the sourceDdb has metadata but the destination does not, as it would result in a loss of data
                    return false;
                }

                int sourceRowCount = sourceDdbTemplate.MetadataInfo.RowCount;
                using (CommonDatabase destinationDdbTemplate = new CommonDatabase(destinationDdb.FilePath))
                {
                    destinationDdbTemplate.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
                    // Check: Does the destination have the equivalent metadata levels as the source  (after factoring in the level differences)?

                    if (null == destinationDdbTemplate.MetadataInfo || destinationDdbTemplate.MetadataInfo.RowCount - levelsToIgnore != sourceRowCount)
                    {
                        // Level numbers are not compatable.
                        return false;
                    }

                    // Check: as the levels appear to be the same, are the controls in each level the same?
                    int maxDestRowIndex = destinationDdbTemplate.MetadataInfo.RowCount;
                    for (int rowIndex = 0, destRowIndex = levelsToIgnore; rowIndex < sourceRowCount; rowIndex++, destRowIndex++)
                    {
                        if (destRowIndex > maxDestRowIndex)
                        {
                            // Mismatched number of expected row Indexes between the src and destination
                            return false;
                        }

                        // Check: Are metadata Guids or Aliases compatable? We do this by comparing the Guid, and if that's not equal, the Alias
                        if (false == (destinationDdbTemplate.MetadataInfo[destRowIndex].Guid == sourceDdbTemplate.MetadataInfo[rowIndex].Guid ||
                                      destinationDdbTemplate.MetadataInfo[destRowIndex].Alias == sourceDdbTemplate.MetadataInfo[rowIndex].Alias))
                        {
                            // If neither Guid or Alias match, assume they are not equal and thus incompatible
                            return false;
                        }

                        // Check: Are metadata controls compatable? We do this by comparing the count and the data labels
                        //int levelDifference = maxDestRowIndex - sourceRowCount;
                        //int srcLevel = rowIndex + 1;
                        //int destLevel = srcLevel + destRowIndex + 1;
                        int srcLevel = sourceDdbTemplate.MetadataInfo[rowIndex].Level;
                        int destLevel = destinationDdbTemplate.MetadataInfo[destRowIndex].Level;
                        if (false == sourceDdbTemplate.MetadataControlsByLevel.ContainsKey(srcLevel) && false == destinationDdbTemplate.MetadataControlsByLevel.ContainsKey(destLevel))
                        {
                            // neither have controls at this level, so we can just skip it i.e., its an empty level
                            continue;
                        }

                        if (false == sourceDdbTemplate.MetadataControlsByLevel.ContainsKey(srcLevel) ||
                            false == destinationDdbTemplate.MetadataControlsByLevel.TryGetValue(destLevel, out var value))
                        {
                            // one has controls but the other is an empty level. So not compatable.
                            return false;
                        }
                        if (sourceDdbTemplate.MetadataControlsByLevel[srcLevel].RowCount != value.RowCount)
                        {
                            // if there are not the same number of controls in each, the are incompatible
                            return false;
                        }
                        // Check:  are datalabels the same?
                        List<string> srcDataLabels = new List<string>();
                        List<string> destinationDataLabels = new List<string>();
                        int controlCount = sourceDdbTemplate.MetadataControlsByLevel[srcLevel].RowCount;
                        for (int controlIndex = 0; controlIndex < controlCount; controlIndex++)
                        {
                            srcDataLabels.Clear();
                            destinationDataLabels.Clear();
                            foreach (MetadataControlRow srcRow in sourceDdbTemplate.MetadataControlsByLevel[srcLevel])
                            {
                                srcDataLabels.Add(srcRow.DataLabel);
                            }
                            foreach (MetadataControlRow destinationRow in destinationDdbTemplate.MetadataControlsByLevel[destLevel])
                            {
                                destinationDataLabels.Add(destinationRow.DataLabel);
                            }
                        }
                        if (Compare.CompareLists(destinationDataLabels, srcDataLabels) == ListComparisonEnum.ElementsDiffer)
                        {
                            // There is at least one difference in the data label naming.
                            // Note that this does NOT check other aspects of the control, e.g., defaults, etc.
                            return false;
                        }
                    }
                }
                return true;
            }
        }
        #endregion

        #region Check: CheckIfDdbContainsThisPath

        // Before calling this, these checks should have been done :
        // - the database is a valid SQL database
        public static bool CheckIfDdbContainsThisPath(
            SQLiteWrapper Ddb,
            string pathPrefixFromRootToSourceDdb)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(Ddb, nameof(Ddb));

            // Get a list of  relative paths in the Ddb that have the pathPrefix
            List<string> relativePathsInDdb =
                GetDistinctRootsFromRelativePath(Ddb, pathPrefixFromRootToSourceDdb);

            //    If its empty, then the destination ddb does not have any images in the sourceDdb folder
            //    If one is returned, then the destination ddb has one or more images in the sourceDdb folder or its subfolders
            //    If more than one is returned, this is a bug.

            return relativePathsInDdb.Count > 0;
        }
        #endregion

        #region Checkout a relative path database
        public static void CheckoutDatabaseWithRelativePath(FileDatabase sourceDdbFileDatabase, string sourceDdbPath, string destinationDdbPath, string relativePath)
        {
            // TODO DetectionsVideo
            SQLiteWrapper destinationDdb = new SQLiteWrapper(destinationDdbPath);
            SQLiteWrapper sourceDdb = sourceDdbFileDatabase.Database;

            // a. We need several temporary tables 
            string attachedSourceDB = "attachedSourceDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";

            // Part 1. Initiate the query phrase with a transaction
            string query = Sql.BeginTransactionSemiColon + Environment.NewLine;

            // Part 2. Create the DataTable in the destination, where it contains only those entries that match the relative path folder and subfolder
            query += QueryCheckoutMergeDataTable(destinationDdb, sourceDdbPath, attachedSourceDB, tempDataTable, relativePath) + Environment.NewLine;

            // Part 3. Create the Markers table, where where it contains only those entries that match the entries in the datatable
            query += QueryCheckoutMarkersTable(attachedSourceDB, tempMarkersTable, relativePath) + Environment.NewLine;

            // Part 4. Update the ImageSetTable by importing the values for Quickpaste and BBDisplayThreshold
            query += QueryCheckoutImageSetTable(attachedSourceDB) + Environment.NewLine;

            // Part 5. Handle the various Recognition Tables portion
            // Note that the two classification tables are only included in the checkout process if there is something in them
            if (sourceDdb.TableExists(DBTables.Detections))
            {
                RecognitionDatabases.PrepareRecognitionTablesAndColumns(destinationDdb, false);
                bool checkoutClassificationsTable = sourceDdb.TableExistsAndNotEmpty(DBTables.Classifications);
                bool checkoutClassificationCategoriesTable = sourceDdb.TableExistsAndNotEmpty(DBTables.Classifications);
                query += QueryCheckoutRecognitionTables(attachedSourceDB, relativePath, checkoutClassificationsTable, checkoutClassificationCategoriesTable) + Environment.NewLine;
                query += Sql.PragmaForeignKeysOn + Sql.Semicolon + Environment.NewLine;
            }

            // Export the MetadataTables, if any
            if (null != sourceDdbFileDatabase.MetadataInfo && sourceDdbFileDatabase.MetadataInfo.RowCount > 0)
            {
                // Rename the datatables to reflect their new level numbers and export them into the destination
                int startingLevel = string.IsNullOrWhiteSpace(relativePath) ? 1 : relativePath.Split(Path.DirectorySeparatorChar).Length + 1;
                int destinationLevel = 1;
                for (int level = startingLevel; level <= sourceDdbFileDatabase.MetadataInfo.RowCount; level++)
                {
                    string srcLevelTableName = FileDatabase.MetadataComposeTableNameFromLevel(level);
                    string destLevelTableName = FileDatabase.MetadataComposeTableNameFromLevel(destinationLevel++);
                    if (sourceDdbFileDatabase.Database.TableExists(srcLevelTableName))
                    {
                        // Rename the table to match its new level name, and then copy the original table's contents from the src master to the destination child
                        query += QueryCheckoutLevelTable(sourceDdbFileDatabase, attachedSourceDB, srcLevelTableName, destLevelTableName, relativePath) + Environment.NewLine;
                    }
                }
            }
            // Part 6. We are done.
            query += Sql.EndTransactionSemiColon;
            destinationDdb.ExecuteNonQuery(query);
        }
        #endregion

        #region QueryPhrases used to checkout a database subset (based on a relative path) from a source database
        // DataTable Checkout create merge query
        //  Create the first part of the query to:
        //  - Attach the ddbFile
        //  - Create a temporary DataTable mirroring the one in the toBeMergedDDB (so updates to that don't affect the original ddb)
        //  - Update the DataTable with the modified Ids
        //  - Update the DataTable with the path prefix
        //  - Insert the DataTable  into the main db's DataTable
        // Form example: 
        // ATTACH DATABASE '<sourceDdbPath>' AS attachedSourceDB;
        // CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM dataBaseName.tableName
        //    WHERE RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>\%' 
        // INSERT INTO Constant.DBTables.FileData SELECT <comma separated data labels> FROM tempDataTable
        private static string QueryCheckoutMergeDataTable(SQLiteWrapper destinationDdb, string sourceDdbPath, string attachedSourceDB, string tempDataTable, string relativePath)
        {
            string queryPhrase = string.Empty;
            List<string> currentDataLabels = destinationDdb.SchemaGetColumns(DBTables.FileData);
            queryPhrase += QueryAttachDatabaseAs(sourceDdbPath, attachedSourceDB) + Environment.NewLine;
            queryPhrase += QueryCheckoutCreateTableFromRelativePathInExistingTable(tempDataTable, attachedSourceDB, DBTables.FileData, relativePath, DatabaseColumn.RelativePath) + Environment.NewLine;
            queryPhrase += QueryCheckoutTrimRelativePath(tempDataTable, relativePath, DatabaseColumn.RelativePath) + Environment.NewLine;
            queryPhrase += QueryCheckoutInsertTable2DataIntoTable1(tempDataTable, DBTables.FileData, currentDataLabels) + Environment.NewLine;
            return queryPhrase;
        }

        // Form (see above):
        // CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM dataBaseName.tableName
        //    WHERE RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>\%' ; 
        private static string QueryCheckoutCreateTableFromRelativePathInExistingTable(string tempDataTable, string dataBaseName, string tableName, string relativePath, string RelativePathColumnName, bool useTemporaryTable = true)
        {
            return $"{(useTemporaryTable ? Sql.CreateTemporaryTable : Sql.CreateTable)}  {tempDataTable}  {Sql.As} {Sql.SelectStarFrom}  {dataBaseName}{Sql.Dot}{tableName} {Sql.Where} {RelativePathColumnName} {Sql.Equal} {Sql.Quote(relativePath)}" +
                   $"{Sql.Or} {RelativePathColumnName} {Sql.Like} + {Sql.Quote(relativePath + "\\%")} {Sql.Semicolon}";

            //return Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + dataBaseName + Sql.Dot +
            //       tableName + Sql.Where + RelativePathColumnName + Sql.Equal + Sql.Quote(relativePath)
            //       + Sql.Or
            //       + RelativePathColumnName + Sql.Like + Sql.Quote(relativePath + "\\%")
            //       + Sql.Semicolon;
        }

        private static string QueryCheckoutPopulateTableFromRelativePathInExistingTable(string dataTable, string dataBaseName, string tableName, string relativePath, string RelativePathColumnName)
        {
            return $"{Sql.InsertInto}  {dataTable} {Sql.SelectStarFrom}  {dataBaseName}{Sql.Dot}{tableName} {Sql.Where} {RelativePathColumnName} {Sql.Equal} {Sql.Quote(relativePath)}" +
                   $"{Sql.Or} {RelativePathColumnName} {Sql.Like} + {Sql.Quote(relativePath + "\\%")} {Sql.Semicolon}";
        }

        // Insert columns from one table where rows must match only the relative paths
        // Form (see above): 
        // INSERT INTO table2 SELECT <comma separated data labels> FROM table1
        private static string QueryCheckoutInsertTable2DataIntoTable1(string table1, string table2, List<string> listDataLabels)
        {
            return Sql.InsertInto + table2 + Sql.Select + String.Join(",", listDataLabels) + Sql.From + table1 + Sql.Semicolon;
        }
        // Form:
        // Update DataTable Set RelativePath = '' where RelativePath = '<relativePath';
        private static string QueryCheckoutTrimRelativePath(string table, string relativePath, string RelativePathColumnName)
        {
            string queryPhrase = string.Empty;
            queryPhrase += Sql.Update + table + Sql.Set + RelativePathColumnName + Sql.Equal + "''"
                + Sql.Where + RelativePathColumnName + Sql.Equal + Sql.Quote(relativePath) + Sql.Semicolon;

            queryPhrase += Sql.Update + table + Sql.Set + RelativePathColumnName + Sql.Equal
                + Sql.Substr + Sql.OpenParenthesis + RelativePathColumnName + Sql.Comma
                + Sql.Length + Sql.OpenParenthesis + Sql.Quote(relativePath + "\\") + Sql.CloseParenthesis + Sql.Plus + "1" + Sql.CloseParenthesis
                + Sql.Where + RelativePathColumnName + Sql.Like + Sql.Quote(relativePath + "\\%")
                + Sql.Semicolon;
            return queryPhrase;
        }

        // Form: CREATE TEMPORARY TABLE tempMarkersTable AS  Select MarkersTable.* from AttachedSourceDb.MarkersTable  JOIN DataTable on MarkersTable.Id=DataTable.Id
        //       And (RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>)\%' ;
        private static string QueryCheckoutMarkersTable(string attachedSourceDB, string tempMarkersTable, string relativePath)
        {
            string attachedMarkersTable = attachedSourceDB + Sql.Dot + DBTables.Markers;
            string attachedDataTable = attachedSourceDB + Sql.Dot + DBTables.FileData;
            string queryPhrase = string.Empty;
            queryPhrase += Sql.CreateTemporaryTable + tempMarkersTable + Sql.As
                + Sql.Select + DBTables.Markers + Sql.DotStar + Sql.From + attachedMarkersTable
                + Sql.Join + attachedDataTable + Sql.On
                + attachedMarkersTable + Sql.Dot + DatabaseColumn.ID + Sql.Equal + attachedDataTable + Sql.Dot + DatabaseColumn.ID
                + Sql.And + Sql.OpenParenthesis
                + attachedDataTable + Sql.Dot + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath)
                + Sql.Or
                + attachedDataTable + Sql.Dot + DatabaseColumn.RelativePath + Sql.Like + Sql.Quote(relativePath + "\\%")
                + Sql.CloseParenthesis + Sql.Semicolon;
            queryPhrase += QueryInsertTable2DataIntoTable1(DBTables.Markers, tempMarkersTable) + Environment.NewLine;
            return queryPhrase;
        }

        // Update the ImageSetTable by importing the values for Quickpaste and BBDisplayThreshold
        private static string QueryCheckoutImageSetTable(string attachedSourceDB)
        {
            string queryPhrase = string.Empty;
            queryPhrase += Sql.Update + DBTables.ImageSet + Sql.Set + DatabaseColumn.QuickPasteTerms + Sql.Equal
                                      + Sql.OpenParenthesis + Sql.Select + DatabaseColumn.QuickPasteTerms +
                                      Sql.From + attachedSourceDB + Sql.Dot + DBTables.ImageSet + Sql.Where +
                                      DatabaseColumn.ID + Sql.Equal + Sql.Quote("1") + Sql.CloseParenthesis + Sql.Semicolon;
            queryPhrase += Sql.Update + DBTables.ImageSet + Sql.Set + DatabaseColumn.BoundingBoxDisplayThreshold + Sql.Equal
                           + Sql.OpenParenthesis + Sql.Select + DatabaseColumn.BoundingBoxDisplayThreshold +
                           Sql.From + attachedSourceDB + Sql.Dot + DBTables.ImageSet + Sql.Where +
                           DatabaseColumn.ID + Sql.Equal + Sql.Quote("1") + Sql.CloseParenthesis + Sql.Semicolon;
            return queryPhrase;
        }

        // Form: CREATE TEMPORARY TABLE tempMarkersTable AS  Select MarkersTable.* from AttachedSourceDb.MarkersTable  JOIN DataTable on MarkersTable.Id=DataTable.Id
        //       And (RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>)\%' ;
        private static string QueryCheckoutRecognitionTables(string attachedSourceDB, string relativePath, bool checkoutClassificationsTable, bool checkoutClassificationCategoriesTable)
        {
            // TODO DetectionsVideo
            string attachedDataTable = attachedSourceDB + Sql.Dot + DBTables.FileData;

            // Copy the Detections table matching the ids and relative paths
            string tmpDetections = "tmpDetections";
            string attachedDetections = attachedSourceDB + Sql.Dot + DBTables.Detections;
            string queryPhrase = string.Empty;
            queryPhrase += Sql.CreateTemporaryTable + tmpDetections + Sql.As
                           + Sql.Select + DBTables.Detections + Sql.DotStar + Sql.From + attachedDetections
                           + Sql.Join + attachedDataTable + Sql.On
                           + attachedDetections + Sql.Dot + DatabaseColumn.ID + Sql.Equal + attachedDataTable + Sql.Dot + DatabaseColumn.ID
                           + Sql.And + Sql.OpenParenthesis
                           + attachedDataTable + Sql.Dot + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath)
                           + Sql.Or
                           + attachedDataTable + Sql.Dot + DatabaseColumn.RelativePath + Sql.Like + Sql.Quote(relativePath + "\\%")
                           + Sql.CloseParenthesis + Sql.Semicolon + Environment.NewLine;
            queryPhrase += QueryInsertTable2DataIntoTable1(DBTables.Detections, tmpDetections) + Environment.NewLine;

            string tmpDetectionsVideo = "tmpDetectionsVideos";
            string attachedDetectionsVideos = attachedSourceDB + Sql.Dot + DBTables.DetectionsVideo;
            queryPhrase += Sql.CreateTemporaryTable + tmpDetectionsVideo + Sql.As
                + Sql.Select + DetectionColumns.FrameNumber + Sql.Comma + DetectionColumns.FrameRate + Sql.Comma + DetectionColumns.DetectionID + Sql.From + attachedDetectionsVideos
                + Sql.Join + tmpDetections  + Sql.Using + Sql.OpenParenthesis + DetectionColumns.DetectionID + Sql.CloseParenthesis + Sql.Semicolon + Environment.NewLine;
            queryPhrase += QueryInsertTable2DataIntoTable1(DBTables.DetectionsVideo, tmpDetectionsVideo) + Environment.NewLine;

            // Copy the Detection Categories table
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpDetectionCategories", DBTables.DetectionCategories, attachedSourceDB);

            // Copy the Info table 
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpInfo", DBTables.Info, attachedSourceDB);

            // Copy the Classifications table matching the detection ids of the just copied detection table if not empty
            if (checkoutClassificationsTable)
            {
                string tmpClassifications = "tmpClassifications";
                string attachedClassifications = attachedSourceDB + Sql.Dot + DBTables.Classifications;
                queryPhrase += Sql.CreateTemporaryTable + tmpClassifications + Sql.As
                               + Sql.Select + DBTables.Classifications + Sql.DotStar + Sql.From + attachedClassifications
                               + Sql.Join + tmpDetections + Sql.On
                               + attachedClassifications + Sql.Dot + ClassificationColumns.DetectionID
                               + Sql.Equal + tmpDetections + Sql.Dot + DetectionColumns.DetectionID
                               + Sql.Semicolon + Environment.NewLine;

                queryPhrase += QueryInsertTable2DataIntoTable1(DBTables.Classifications, tmpClassifications) + Environment.NewLine;
            }

            // Copy the Classification Categories table if not empty
            if (checkoutClassificationCategoriesTable)
            {
                queryPhrase += QueryCheckoutCopyCompleteTable("tmpClassificationCategories", DBTables.ClassificationCategories, attachedSourceDB);
            }
            return queryPhrase;
        }

        private static string QueryCheckoutCopyCompleteTable(string tmpTableName, string TableName, string attachedSourceDB)
        {
            string queryPhrase = Sql.CreateTemporaryTable + tmpTableName + Sql.As
                           + Sql.Select + TableName + Sql.DotStar
                           + Sql.From + attachedSourceDB + Sql.Dot + TableName + Sql.Semicolon + Environment.NewLine;
            queryPhrase += QueryInsertTable2DataIntoTable1(TableName, tmpTableName) + Environment.NewLine;
            return queryPhrase;
        }

        // Copy the source Level Table to the dest Level Table, renaming that table as needed and adjusting the relative path in the destination to trim the unneeded levels from it.
        private static string QueryCheckoutLevelTable(FileDatabase sourceDdbFileDatabase, string attachedSourceDB, string srcLevelTableName, string destLevelTableName, string relativePath)
        {
            // Create the tables in the destination 
            // To do this, we need to retrieve the schema from the source tables and manipulate it (as it includes the old table name in it)
            string queryPhrase = string.Empty;
            DataTable dtschema = sourceDdbFileDatabase.Database.GetDataTableFromSelect($"{SqlPhrase.GetSchemaFromTable(srcLevelTableName)}");
            if (dtschema.Rows.Count != 0)
            {
                string createTableWithSchema = dtschema.Rows[0].ItemArray[0].ToString();
                // Substitute the new table name into the creation string
                createTableWithSchema = Regex.Replace(createTableWithSchema, @"CREATE .*\(", $"CREATE TABLE {destLevelTableName} (", RegexOptions.IgnoreCase);
                queryPhrase += createTableWithSchema + Sql.Semicolon + Environment.NewLine;
            }

            // The source database should already be attached by previous calls, named attachedSourceDB.
            // Populate the just created table with the contents of the old table that match the relative path
            // and then trim the relative path to remove unneeded levels
            queryPhrase += QueryCheckoutPopulateTableFromRelativePathInExistingTable(
                destLevelTableName, attachedSourceDB, srcLevelTableName, relativePath,
                            DatabaseColumn.FolderDataPath);
            queryPhrase += QueryCheckoutTrimRelativePath(destLevelTableName, relativePath, DatabaseColumn.FolderDataPath) + Environment.NewLine;
            return queryPhrase;
        }
        #endregion

        #region Remove: Remove all FileData entries in the database whose RelativePath matches the given folder or its subfolders

        // TODO NEED TO FIX: NOT SURE IF THE NOTES BELOW RE CORRECT
        // XXX DOES NOT REMOVE MARKERS
        // XXX CHECK IF RECOGNITIONS ARE REMOVED WITH EACH ENTRY
        // Before calling this, these checks should have been done :
        // - the database is a valid SQL database
        // - the templates match
        public static DatabaseFileErrorsEnum RemoveEntriesFromDestinationDdbMatchingPath(
        SQLiteWrapper destinationDdb,
        string relativePathDifference)
        {
            // Delete entries from the DataTable whose RelativePath begins with the (folder) path from the root folder.
            // This returns the Ids of all deleted entries
            string where = DatabaseColumn.RelativePath
                           + Sql.Like
                           + Sql.Quote(relativePathDifference + @"\" + "%") // like root/*
                           + Sql.Or
                           + DatabaseColumn.RelativePath + Sql.Equal
                           + Sql.Quote(relativePathDifference); // or equals root 
            DataTable dtDeletedIds = destinationDdb.DeleteRowsReturningIds(DBTables.FileData, where, DatabaseColumn.ID);

            // Use the Ids of the deleted entries to remove corresponding entries (if they exist) from the Markers table
            if (dtDeletedIds.Rows.Count > 0)
            {
                List<string> idClauses = new List<string>();
                foreach (DataRow row in dtDeletedIds.Rows)
                {
                    idClauses.Add(DatabaseColumn.ID + " = " + row[0]);
                }
                destinationDdb.Delete(DBTables.Markers, idClauses);
                // Debug.Print($"Ddb deleted {idClauses.Count} entries in the folder {relativePathDifference}");
            }
            return DatabaseFileErrorsEnum.Ok;
        }

        public static DatabaseFileErrorsEnum RemoveMetadataEntriesFromDestinationDdbMatchingPath(
            SQLiteWrapper destinationDdb,
            string relativePathDifference, int levelsToIgnore)
        {
            // For each levels table in the destination...
            // We know that the source has some metadata structures, so lets load them
            using (CommonDatabase destinationDdbTemplate = new CommonDatabase(destinationDdb.FilePath))
            {
                destinationDdbTemplate.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
                for (int rowIndex = levelsToIgnore; rowIndex < destinationDdbTemplate.MetadataInfo.RowCount; rowIndex++)
                {
                    MetadataInfoRow infoRow = destinationDdbTemplate.MetadataInfo[rowIndex];
                    string metadataTable = FileDatabase.MetadataComposeTableNameFromLevel(infoRow.Level);
                    if (false == destinationDdb.TableExists(metadataTable))
                    {
                        // This shouldn't happen, but just in case...
                        continue;
                    }

                    // Delete entries from each Levels table whose RelativePath begins with the (folder) path from the root folder.
                    // This returns the Ids of all deleted entries
                    string where = DatabaseColumn.FolderDataPath
                                   + Sql.Like
                                   + Sql.Quote(relativePathDifference + @"\" + "%") // like root/*
                                   + Sql.Or
                                   + DatabaseColumn.FolderDataPath + Sql.Equal
                                   + Sql.Quote(relativePathDifference); // or equals root 
                    destinationDdb.DeleteRowsReturningIds(metadataTable, where, DatabaseColumn.ID);
                }
            }
            return DatabaseFileErrorsEnum.Ok;
        }
        #endregion

        #region Merge a source database into the destinaton database

        // Before calling this, checks should have been done to:
        // - see if both databases are valid SQL database
        // - the templates match
        public static DatabaseFileErrorsEnum MergeSourceIntoDestinationDdb(
            SQLiteWrapper destinationDdb,
            string sourceDdbPath,
            string relativePathDifference, int levelsToIgnore)
        {
            // a. We need several temporary tables 
            string attachedSourceDB = "attachedSourceDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";

            // Part 1. Initiate the query phrase with a transaction
            string query = Sql.BeginTransactionSemiColon + Environment.NewLine;

            // Turn Foreign keys off for this transaction as we will be clearing and updating tables with foreign keys.
            query += Sql.PragmaForeignKeysOff + Sql.Semicolon + Environment.NewLine; 

            // Part 2. Calculate an ID offset (the current max Id), where we will be adding that to all Ids for the entries inserted into the destinationDdb 
            //    This will guarantee that there are no duplicate primary keys.
            //    Note: if there are no entries in the database, this function returns 0 
            long offsetId = destinationDdb.ScalarGetMaxValueAsLong(DBTables.FileData, DatabaseColumn.ID);

            // Part 3. Handle the DataTable portion
            query += QueryPhraseMergeDataTable(offsetId, destinationDdb, sourceDdbPath, attachedSourceDB, tempDataTable, relativePathDifference);

            // Part 4. Handle the Markers Table portion
            query += QueryPhraseMergeMarkersTable(offsetId, destinationDdb, attachedSourceDB, tempMarkersTable);

            // Part 5. Handle the various Recognition Tables portion
            bool destinationRecognitionsExist = destinationDdb.TableExists(DBTables.Detections);
            Tuple<DatabaseFileErrorsEnum, string, bool> resultTuple = QueryPhrasePrepareMergeRecognitionTablesAsNeeded(
                query,
                destinationDdb,
                sourceDdbPath,
                attachedSourceDB,
                destinationRecognitionsExist);

            if (resultTuple.Item1 != DatabaseFileErrorsEnum.Ok)
            {
                // Oops, can't do the merge.
                return resultTuple.Item1;
            }

            // Part 6. If the sourceDdb has recognitions, the SQL query should update the destination recognition tables as needed
            bool updateRecognitions = resultTuple.Item3;
            if (updateRecognitions)
            {
                query = resultTuple.Item2;

                query += QueryPhraseMergeRecognitionTables(offsetId, destinationDdb, attachedSourceDB, destinationRecognitionsExist);

            }

            // Part 7. Now update the various MetadataTables if and as needed
            // If the MetadataTableInfo exists in the destination
            if (destinationDdb.TableExists(DBTables.MetadataInfo))
            {
                SQLiteWrapper sourceDdb = new SQLiteWrapper(sourceDdbPath);
                DataTable destInfoTable = destinationDdb.GetDataTableFromSelect($"{Sql.SelectStarFrom} {DBTables.MetadataInfo} {Sql.OrderBy} {Control.Level}");
                int maxLevel = destInfoTable.Rows.Count;
                int startLevel = levelsToIgnore + 1;
                for (int srcLevel = 1, destLevel = startLevel; destLevel <= maxLevel; srcLevel++, destLevel++)
                {

                    string srcTableName = FileDatabase.MetadataComposeTableNameFromLevel(srcLevel);
                    string destTableName = FileDatabase.MetadataComposeTableNameFromLevel(destLevel);
                    if (sourceDdb.TableExists(srcTableName))
                    {
                        query += QueryPhraseMergeLevelsTable(destinationDdb, attachedSourceDB, srcTableName, destTableName, relativePathDifference);
                    }
                }
            }

            // Part 8. The query is now done, so end the transaction and execute it
            // Turn Foreign keys back on again
            query += Sql.PragmaForeignKeysOn + Sql.Semicolon + Environment.NewLine;
            query += Sql.EndTransactionSemiColon;


            destinationDdb.ExecuteNonQuery(query);

            // Part 9. Check if there are any Detections. If not, delete all the recognition tables as they are no longer relevant
            if (destinationDdb.TableExistsAndEmpty(DBTables.Detections))
            {

                destinationDdb.DropTable(DBTables.Detections);
                destinationDdb.DropTable(DBTables.DetectionsVideo);
                destinationDdb.DropTable(DBTables.Classifications);
                destinationDdb.DropTable(DBTables.DetectionCategories);
                destinationDdb.DropTable(DBTables.ClassificationCategories);
                destinationDdb.DropTable(DBTables.Info);
            }
            FileDatabase.IndexCreateForDetectionsAndClassificationsIfNotExists(destinationDdb);
            return DatabaseFileErrorsEnum.Ok;
        }

        #endregion

        #region Private

        // Given a path, get the first folder in it (or Empty if there isn't one)
        private static string GetRootFolder(string path)
        {
            while (true)
            {
                string temp = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(temp))
                {
                    break;
                }

                path = temp;
            }

            return path;
        }

        // Get a list of all unique relative paths in the source DDB
        // Form: SELECT DISTINCT RelativePath FROM DataTable
        private static List<string> GetDistinctRootsFromRelativePath(SQLiteWrapper ddb, string pathPrefix)
        {
            // Get the relative paths
            DataTable dtRelativePathsInDdb =
                ddb.GetDataTableFromSelect(Sql.SelectDistinct + DatabaseColumn.RelativePath + Sql.From +
                                           DBTables.FileData
                                           + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal +
                                           Sql.Quote(pathPrefix)
                                           + Sql.Or + DatabaseColumn.RelativePath + Sql.Like +
                                           Sql.Quote(pathPrefix + "\\%"));

            List<string> relativePathRoots = new List<string>();
            foreach (DataRow row in dtRelativePathsInDdb.Rows)
            {
                string rootFolder = GetRootFolder((string)row[0]);
                if (false == relativePathRoots.Contains(rootFolder))
                {
                    relativePathRoots.Add(rootFolder);
                }
            }

            return relativePathRoots;
        }
        #endregion

        #region Query phrases

        // Form: ATTACH DATABASE 'databasePath' AS alias;
        private static string QueryAttachDatabaseAs(string databasePath, string alias)
        {
            return Sql.AttachDatabase + Sql.Quote(databasePath) + Sql.As + alias + Sql.Semicolon;
        }

        // Form: CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM dataBaseName.tableName;
        private static string QueryCreateTemporaryTableFromExistingTable(string tempDataTable, string dataBaseName,
            string tableName)
        {
            string query = $"{Sql.DropTableIfExists} {tempDataTable} {Sql.Semicolon} {Environment.NewLine}";
            query += $"{Sql.CreateTemporaryTable} {tempDataTable}  {Sql.As} {Sql.SelectStarFrom} {dataBaseName}{Sql.Dot}{tableName} {Sql.Semicolon} {Environment.NewLine}";
            return query;
        }

        // Form: UPDATE dataTable SET IDColumnName = (offset + dataTable.Id);
        private static string QueryAddOffsetToIDInTable(string tableName, string IDColumnName, long offset)
        {
            return Sql.Update + tableName + Sql.Set + IDColumnName + Sql.Equal + Sql.OpenParenthesis + offset + Sql.Plus +
                   tableName + Sql.Dot + IDColumnName + Sql.CloseParenthesis + Sql.Semicolon;
        }

        //Form:  UPDATE tableName SET RelativePath = CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
        private static string QueryAddPrefixToRelativePathInTable(string tableName, string pathPrefixToAdd, string columnName)
        {
            if (string.IsNullOrWhiteSpace(pathPrefixToAdd))
            {
                // No need to construct a new relative path if there is nothing to add to it
                return string.Empty;
            }
            // A longer query, so split into three lines
            // Note that tableName must be a DataTable for this to work
            string query = Sql.Update + tableName + Sql.Set + columnName + Sql.Equal +
                           Sql.CaseWhen + columnName + Sql.Equal + Sql.Quote(string.Empty);
            query += Sql.Then + Sql.OpenParenthesis + Sql.Quote(pathPrefixToAdd) + Sql.Concatenate +
                     columnName + Sql.CloseParenthesis;
            query += Sql.Else + Sql.OpenParenthesis + Sql.Quote(pathPrefixToAdd + "\\") + Sql.Concatenate +
                     columnName + Sql.CloseParenthesis + " END " + Sql.Semicolon;
            return query;
        }

        //  Two Forms: INSERT INTO table1 SELECT * FROM table2;
        private static string QueryInsertTable2DataIntoTable1(string table1, string table2)
        {
            return Sql.InsertInto + table1 + Sql.SelectStarFrom + table2 + Sql.Semicolon;
        }

        private static string QueryInsertTable2DataIntoTable1(string table1, string table2, List<string> listDataLabels)
        {
            string dataLabels = string.Empty;
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


        private static string UpdateInfoTableWithValues(string detector, string megadetector_version,
            string detection_completion_time, string classifier, string classification_completion_time,
            double typical_detection_threshold, double conservative_detection_threshold,
            double typical_classification_threshold)
        {
            return Sql.Update + DBTables.Info + Sql.Set
                   + InfoColumns.Detector + Sql.Equal + Sql.Quote(detector) + Sql.Comma
                   + InfoColumns.DetectorVersion + Sql.Equal + Sql.Quote(megadetector_version) + Sql.Comma
                   + InfoColumns.DetectionCompletionTime + Sql.Equal + Sql.Quote(detection_completion_time) +
                   Sql.Comma
                   + InfoColumns.Classifier + Sql.Equal + Sql.Quote(classifier) + Sql.Comma
                   + InfoColumns.ClassificationCompletionTime + Sql.Equal +
                   Sql.Quote(classification_completion_time) + Sql.Comma
                   + InfoColumns.TypicalDetectionThreshold + Sql.Equal +
                   (Math.Round(typical_detection_threshold * 100) / 100) + Sql.Comma
                   + InfoColumns.ConservativeDetectionThreshold + Sql.Equal +
                   (Math.Round(conservative_detection_threshold * 100) / 100) + Sql.Comma
                   + InfoColumns.TypicalClassificationThreshold + Sql.Equal +
                   (Math.Round(typical_classification_threshold * 100) / 100)
                   + Sql.Semicolon;
        }

        private static string UpdateImageSetTableWithUndefinedBoundingBox()
        {
            return Sql.Update + DBTables.ImageSet + Sql.Set
                   + DatabaseColumn.BoundingBoxDisplayThreshold + Sql.Equal +
                   RecognizerValues.BoundingBoxDisplayThresholdDefault + Sql.Semicolon;
        }

        private static void DictionaryReplaceSecondDictWithFirstDictElements(Dictionary<string, string> first,
            Dictionary<string, string> second)
        {
            second.Clear();
            foreach (KeyValuePair<string, string> kvp in first)
            {
                second.Add(kvp.Key, kvp.Value);
            }
        }

        #endregion

        #region QueryPhrases used to merge various datatables

        // DataTable create merge query
        //  Create the first part of the query to:
        //  - Attach the ddbFile
        //  - Create a temporary DataTable mirroring the one in the toBeMergedDDB (so updates to that don't affect the original ddb)
        //  - Update the DataTable with the modified Ids
        //  - Update the DataTable with the path prefix
        //  - Insert the DataTable  into the main db's DataTable
        // Form: ATTACH DATABASE 'sourceDdbPath' AS attachedSourceDB; 
        //       CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM attachedSourceDB.DataTable;
        //       UPDATE tempDataTable SET Id = (offsetID + tempDataTable.Id);  // This line only inserted if offset > 0
        //       UPDATE TempDataTable SET RelativePath =  CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) END
        //       INSERT INTO DataTable SELECT * FROM tempDataTable;
        private static string QueryPhraseMergeDataTable(long offsetId, SQLiteWrapper destinationDdb, string sourceDdbPath, string attachedSourceDB, string tempDataTable, string relativePathDifference)
        {
            string query = string.Empty;
            List<string> currentDataLabels = destinationDdb.SchemaGetColumns(DBTables.FileData);
            query += QueryAttachDatabaseAs(sourceDdbPath, attachedSourceDB) + Environment.NewLine;
            query += QueryCreateTemporaryTableFromExistingTable(tempDataTable, attachedSourceDB, DBTables.FileData) + Environment.NewLine;
            if (offsetId > 0)
            {
                // Add the offset to the IDs to make sure IDs don't conflict with existing IDs
                query += QueryAddOffsetToIDInTable(tempDataTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            query += QueryAddPrefixToRelativePathInTable(tempDataTable, relativePathDifference, DatabaseColumn.RelativePath) + Environment.NewLine;
            query += QueryInsertTable2DataIntoTable1(DBTables.FileData, tempDataTable, currentDataLabels) + Environment.NewLine;
            return query;
        }

        // Markers Table create merge query
        //  Create the second part of the query to:
        //  - Create a temporary Markers Table mirroring the one in the toBeMergedDDB (so updates to that don't affect the original ddb)
        //  - Note that we have to ensure that the columns in both markers table are in the same order. Consequently, we
        //    get the ordered column names from the main database, and then construct a query that creates the tempTable
        //    by selecting on those column names (in order)
        //  - Update the Markers Table with the modified Ids
        //  - Insert the Markers Table  into the main db's Markers Table
        //  Form: select name from pragma_table_info('MarkersTable')  as tblInfo  - return a list of columns
        //  CREATE TEMPORARY TABLE tempMarkers AS SELECT <comma-spearated column list> FROM attachedSourceDB.Markers;
        //       UPDATE tempMarkers SET Id = (offsetID + tempMarkers.Id); // This line only inserted if offset > 0
        //       INSERT INTO Markers SELECT * FROM tempMarkers;
        private static string QueryPhraseMergeMarkersTable(long offsetId, SQLiteWrapper destinationDdb,
            string attachedSourceDB, string tempMarkersTable)
        {
            string query = string.Empty;
            // Get the columns in order
            // Form: select name from pragma_table_info('MarkersTable')  as tblInfo 
            string queryGetColumnName = Sql.SelectNameFromPragmaTableInfo + Sql.OpenParenthesis +
                                        Sql.Quote(DBTables.Markers) + Sql.CloseParenthesis + Sql.As +
                                        Sql.TBLINFO;
            DataTable dataTable = destinationDdb.GetDataTableFromSelect(queryGetColumnName);
            string columns = string.Empty;
            int i = 0;
            foreach (DataRow row in dataTable.Rows)
            {
                if (i++ != 0)
                {
                    columns += Sql.Comma + " ";
                }

                columns += row[0] + " ";
            }

            query += Sql.CreateTemporaryTable + tempMarkersTable + Sql.As + Sql.Select + columns + Sql.From +
                     attachedSourceDB + Sql.Dot + DBTables.Markers + Sql.Semicolon + Environment.NewLine;
            if (offsetId > 0)
            {
                // Add the offset to the IDd to make sure IDs don't conflict with existing IDs
                query += QueryAddOffsetToIDInTable(tempMarkersTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(DBTables.Markers, tempMarkersTable) + Environment.NewLine;
            return query;
        }

        private static Tuple<DatabaseFileErrorsEnum, string, bool> QueryPhrasePrepareMergeRecognitionTablesAsNeeded(
            string query,
            SQLiteWrapper destinationDdb,
            string sourceDdbPath,
            string attachedSourceDB,
            bool destinationDetectionsExists)
        {
            string tempInfoTable = "tempInfoTable";
            string tempDetectionCategoriesTable = "tempDetectionCategoriesTable";
            string tempClassificationCategoriesTable = "tempClassificationCategoriesTable";

            Dictionary<string, string> detectionCategories = new Dictionary<string, string>();
            Dictionary<string, string> classificationCategories = new Dictionary<string, string>();

            // Create a connection to the sourceDdbPath
            // and check if the sourceDdb file has any recognitions (by seeing if a Detections table exists)
            SQLiteWrapper sourceDdb = new SQLiteWrapper(sourceDdbPath);
            bool sourceDetectionsExists = sourceDdb.TableExists(DBTables.Detections);

            // Condition 1. The sourceDdb doesn't have recognitions. So no need to update detections
            //              regardless of whether the destination has or doesn't have recognitions.
            if (false == sourceDetectionsExists)
            {
                // Don't need to do anything with the varous recognition tables as
                // either neither have them, or the one to be merged in doesn't have them
                return new Tuple<DatabaseFileErrorsEnum, string, bool>(DatabaseFileErrorsEnum.Ok, query, false);
            }

            // A. Generate several dictionaries reflecting the contents of the info and category tables as held in the source and destination database
            Dictionary<string, string> sourceDetectionCategories = new Dictionary<string, string>();
            Dictionary<string, string> sourceClassificationCategories = new Dictionary<string, string>();
            Dictionary<string, object> sourceInfoDictionary = new Dictionary<string, object>();
            RecognitionUtilities.GenerateRecognitionDictionariesFromDB(sourceDdbPath, sourceInfoDictionary,
                sourceDetectionCategories, sourceClassificationCategories);

            // B. Generate several dictionaries reflecting the contents of the info and category tables as held in the destination database
            Dictionary<string, string> destinationDetectionCategories = new Dictionary<string, string>();
            Dictionary<string, string> destinationClassificationCategories = new Dictionary<string, string>();
            Dictionary<string, object> destinationInfoDictionary = new Dictionary<string, object>();
            RecognitionUtilities.GenerateRecognitionDictionariesFromDB(destinationDdb, destinationInfoDictionary,
                destinationDetectionCategories, destinationClassificationCategories);

            // Condition 1. The destinationDdb doesn't have detections, but we now know that the sourceDdb does,
            // Thus we need to create the detection tables in the destination database.
            if (false == destinationDetectionsExists)
            {
                RecognitionDatabases.PrepareRecognitionTablesAndColumns(destinationDdb, false);

                // Import the Detection Categories, Classification Categories and Info from the sourceDdb
                // To do this, we first create temporary tables from the sourceDdb 
                query += QueryCreateTemporaryTableFromExistingTable(tempInfoTable, attachedSourceDB, DBTables.Info) + Environment.NewLine;
                query += QueryCreateTemporaryTableFromExistingTable(tempDetectionCategoriesTable, attachedSourceDB,
                    DBTables.DetectionCategories) + Environment.NewLine;
                query += QueryCreateTemporaryTableFromExistingTable(tempClassificationCategoriesTable, attachedSourceDB,
                             DBTables.ClassificationCategories) + Environment.NewLine;

                // Now we insert those tables into the current database
                query += QueryInsertTableDataFromAnotherDatabase(DBTables.DetectionCategories, attachedSourceDB) + Environment.NewLine;
                query += QueryInsertTableDataFromAnotherDatabase(DBTables.ClassificationCategories, attachedSourceDB) + Environment.NewLine;
                query += QueryInsertTableDataFromAnotherDatabase(DBTables.Info, attachedSourceDB) + Environment.NewLine;

                //// Update the various dictionaries to reflect their current values
                //DictionaryReplaceSecondDictWithFirstDictElements(sourceInfoDictionary, infoDictionary);
                //DictionaryReplaceSecondDictWithFirstDictElements(sourceDetectionCategories,
                //    detectionCategories);
                //DictionaryReplaceSecondDictWithFirstDictElements(sourceClassificationCategories,
                //    classificationCategories);

                // At this point,we have 
                // - created the recognition database tables in the destination, 
                // - filled in the info and the two detection/classification category tables
                return new Tuple<DatabaseFileErrorsEnum, string, bool>(DatabaseFileErrorsEnum.Ok, query, true);
            }

            // Condition 3.  Both the current database and the database to merged have recognition tables

            // A. Generate a new info structure that is a best effort combination of the db and json info structure,
            //    and then update the jsonRecognizer to match that. Note the we do it even if no update is really needed, as its lightweight

            Dictionary<string, object> mergedInfoDictionary =
                RecognitionUtilities.GenerateBestRecognitionInfoFromTwoInfos(destinationInfoDictionary, sourceInfoDictionary);

            query += UpdateInfoTableWithValues(
                (string)mergedInfoDictionary[InfoColumns.Detector],
                (string)mergedInfoDictionary[InfoColumns.DetectorVersion],
                (string)mergedInfoDictionary[InfoColumns.DetectionCompletionTime],
                (string)mergedInfoDictionary[InfoColumns.Classifier],
                (string)mergedInfoDictionary[InfoColumns.ClassificationCompletionTime],
                Convert.ToDouble(mergedInfoDictionary[InfoColumns.TypicalDetectionThreshold]),
                Convert.ToDouble(mergedInfoDictionary[InfoColumns.ConservativeDetectionThreshold]),
                Convert.ToDouble(mergedInfoDictionary[InfoColumns.TypicalClassificationThreshold]));
            query += UpdateImageSetTableWithUndefinedBoundingBox();

            // Update the various dictionaries to reflect their current values
            // DictionaryReplaceSecondDictWithFirstDictElements(mergedInfoDictionary, infoDictionary);

            // B: Generate a new detection category db if the categories in the current and to be merged dictionary can be merged together. 
            if (sourceDetectionCategories.Count > 0 || detectionCategories.Count > 0)
            {
                // There is something to add
                if (Dictionaries.MergeDictionaries(sourceDetectionCategories, destinationDetectionCategories,
                        out Dictionary<string, string> mergedDetectionCategories))
                {
                    // Clear the DetectionCategories table as we will be completely replacing it
                    query += Sql.DeleteFrom + DBTables.DetectionCategories + Sql.Semicolon + Environment.NewLine;

                    // update the classification categories in the table.
                    query += Sql.InsertInto + DBTables.DetectionCategories
                                            + Sql.OpenParenthesis + DetectionCategoriesColumns.Category +
                                            Sql.Comma + DetectionCategoriesColumns.Label +
                                            Sql.CloseParenthesis + Sql.Values;
                    foreach (KeyValuePair<string, string> kvp in mergedDetectionCategories)
                    {
                        query += Sql.OpenParenthesis + Sql.Quote(kvp.Key) + Sql.Comma + Sql.Quote(kvp.Value) +
                                 Sql.CloseParenthesis + Sql.Comma;
                    }
                    // Replace the last comma with a semicolon
                    query = query.Substring(0, query.LastIndexOf(Sql.Comma, StringComparison.Ordinal));
                    query += Sql.Semicolon + Environment.NewLine;

                    // Update the various dictionaries to reflect their current values
                    DictionaryReplaceSecondDictWithFirstDictElements(mergedDetectionCategories,
                        detectionCategories);
                }
                else
                {
                    // The merge failed because the detection categories differ
                    return new Tuple<DatabaseFileErrorsEnum, string, bool>(
                        DatabaseFileErrorsEnum.DetectionCategoriesDiffer, query, false);
                }
            }

            // C: Generate a new classification category db if the categories in the destination and source dictionary can be merged together. 
            if (sourceClassificationCategories.Count > 0 || destinationClassificationCategories.Count > 0)
            {
                // There is something to add
                if (Dictionaries.MergeDictionaries(sourceClassificationCategories,
                        classificationCategories,
                        out Dictionary<string, string> mergedClassificationCategories))
                {
                    // Clear the ClassificationCategories table as we will be completely replacing it
                    query += Sql.DeleteFrom + DBTables.ClassificationCategories + Sql.Semicolon + Environment.NewLine;

                    // update the classification categories in the table.
                    query += Sql.InsertInto + DBTables.ClassificationCategories
                                            + Sql.OpenParenthesis + ClassificationCategoriesColumns.Category +
                                            Sql.Comma + ClassificationCategoriesColumns.Label +
                                            Sql.CloseParenthesis + Sql.Values;
                    foreach (KeyValuePair<string, string> kvp in mergedClassificationCategories)
                    {
                        query += Sql.OpenParenthesis + Sql.Quote(kvp.Key) + Sql.Comma + Sql.Quote(kvp.Value) +
                                 Sql.CloseParenthesis + Sql.Comma;
                    }

                    // Replace the last comma with a semicolon
                    query = query.Substring(0, query.LastIndexOf(Sql.Comma, StringComparison.Ordinal))
                            + Sql.Semicolon + Environment.NewLine;

                    // Update the various dictionaries to reflect their current values
                    DictionaryReplaceSecondDictWithFirstDictElements(mergedClassificationCategories,
                        classificationCategories);
                }
                else
                {
                    // Debug.Print("merged failed for classification categories");
                    return new Tuple<DatabaseFileErrorsEnum, string, bool>(
                        DatabaseFileErrorsEnum.ClassificationCategoriesDiffer, query, false);
                }
            }

            // Condition 6: All is fine, so we now have a query that works for updating various recognition tables
            return new Tuple<DatabaseFileErrorsEnum, string, bool>(DatabaseFileErrorsEnum.Ok, query, true);
        }

        // The database to merge in has recognitions, so the SQL query should update the detections table and (if needed) the recognition table.
        private static string QueryPhraseMergeRecognitionTables(long offsetId, SQLiteWrapper destinationDdb,
            string attachedSourceDB, bool destinationRecognitionsExist)
        {
            string query = string.Empty;
            string tempDetectionsTable = "tempDetectionsTable";
            string tempDetectionsVideoTable = "tempDetectionVideoTable";
            string tempClassificationsTable = "tempClassificationsTable";

            // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
            // The offeset should be 0 if there are no detections in the main DB, as we will be creating the detection table and then just adding to it.
            long offsetDetectionId = destinationRecognitionsExist
                ? destinationDdb.ScalarGetMaxValueAsLong(DBTables.Detections, DetectionColumns.DetectionID)
                : 0;

            // Update the Detections table, including adjusting the ID and DetectionID offset as needed
            query += QueryCreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedSourceDB, DBTables.Detections) + Environment.NewLine;
            if (offsetId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            if (offsetDetectionId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(DBTables.Detections, tempDetectionsTable) + Environment.NewLine;

            // Now update the DetectionsVideo table, including adjusting the DetectionID offset as needed
            query += QueryCreateTemporaryTableFromExistingTable(tempDetectionsVideoTable, attachedSourceDB, DBTables.DetectionsVideo) + Environment.NewLine;
            if (offsetDetectionId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempDetectionsVideoTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(DBTables.DetectionsVideo, tempDetectionsVideoTable) + Environment.NewLine; ;

            // Update the classifications table, , including adjusting the ID and DetectionID offset as needed
            // TODO: IS THIS NEEDED IF THERE ARE NO RECOGNITIONS IN THE TABLE???
            long offsetClassificationId = (destinationRecognitionsExist)
                ? destinationDdb.ScalarGetMaxValueAsLong(DBTables.Classifications, ClassificationColumns.ClassificationID)
                : 0;
            query += QueryCreateTemporaryTableFromExistingTable(tempClassificationsTable, attachedSourceDB, DBTables.Classifications) + Environment.NewLine;
            if (offsetClassificationId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, ClassificationColumns.ClassificationID, offsetClassificationId) + Environment.NewLine;
            }

            if (offsetDetectionId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, ClassificationColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(DBTables.Classifications, tempClassificationsTable) + Environment.NewLine;
            return query;
        }

        private static string QueryPhraseMergeLevelsTable(SQLiteWrapper destinationDdb,
            string attachedSourceDB, string srcTableName, string destTableName, string relativePathDifference)
        {
            string tmpLevelsTable = $"tmp{srcTableName}";
            long offsetId = destinationDdb.ScalarGetMaxValueAsLong(destTableName, DatabaseColumn.ID);
            string query = QueryCreateTemporaryTableFromExistingTable(tmpLevelsTable, attachedSourceDB, srcTableName) + Environment.NewLine;
            if (offsetId > 0)
            {
                query += QueryAddOffsetToIDInTable(tmpLevelsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            query += QueryAddPrefixToRelativePathInTable(tmpLevelsTable, relativePathDifference, DatabaseColumn.FolderDataPath) + Environment.NewLine;
            query += QueryInsertTable2DataIntoTable1(destTableName, tmpLevelsTable) + Environment.NewLine;
            return query;
        }
    }
    #endregion
}
