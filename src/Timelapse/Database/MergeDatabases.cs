using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;

namespace Timelapse.Database
{
    // This static class will try to merge various .ddb database files into a single database.
    public static partial class MergeDatabases
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

            //
            // At this point, we should have an empty database
            // 
            return true;
        }
        #endregion

        #region Check: Check If Database Templates Are Merge Compatable
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

        #region Check If Metadata Templates Are Merge Compatable
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
            using CommonDatabase sourceDdbTemplate = new(sourceDdb.FilePath);
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
            using CommonDatabase destinationDdbTemplate = new(destinationDdb.FilePath);
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
                List<string> srcDataLabels = [];
                List<string> destinationDataLabels = [];
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

            return true;
        }
        #endregion

        #region Checkout a relative path database
        public static void CheckoutDatabaseWithRelativePath(FileDatabase sourceDdbFileDatabase, string sourceDdbPath, string destinationDdbPath, string relativePath)
        {
            SQLiteWrapper destinationDdb = new(destinationDdbPath);
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
                bool checkoutClassificationCategoriesTable = sourceDdb.TableExistsAndNotEmpty(DBTables.ClassificationCategories);
                query += QueryCheckoutRecognitionTables(attachedSourceDB, relativePath, checkoutClassificationCategoriesTable) + Environment.NewLine;
                query += Sql.PragmaForeignKeysOn + Sql.Semicolon + Environment.NewLine;
            }

            // Export the MetadataTables, if any
            if (sourceDdbFileDatabase.MetadataInfo is { RowCount: > 0 })
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
            queryPhrase += SqlLine.AttachDatabaseAs(sourceDdbPath, attachedSourceDB);
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
            queryPhrase += SqlLine.InsertTable2DataIntoTable1(DBTables.Markers, tempMarkersTable);
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
        private static string QueryCheckoutRecognitionTables(string attachedSourceDB, string relativePath, bool checkoutClassificationCategoriesTable)
        {
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
            queryPhrase += SqlLine.InsertTable2DataIntoTable1(DBTables.Detections, tmpDetections);

            string tmpDetectionsVideo = "tmpDetectionsVideos";
            string attachedDetectionsVideos = attachedSourceDB + Sql.Dot + DBTables.DetectionsVideo;
            queryPhrase += Sql.CreateTemporaryTable + tmpDetectionsVideo + Sql.As
                + Sql.Select + DetectionColumns.FrameNumber + Sql.Comma + DetectionColumns.FrameRate + Sql.Comma + DetectionColumns.DetectionID + Sql.From + attachedDetectionsVideos
                + Sql.Join + tmpDetections + Sql.Using + Sql.OpenParenthesis + DetectionColumns.DetectionID + Sql.CloseParenthesis + Sql.Semicolon + Environment.NewLine;
            queryPhrase += SqlLine.InsertTable2DataIntoTable1(DBTables.DetectionsVideo, tmpDetectionsVideo);

            // Copy the Detection Categories table
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpDetectionCategories", DBTables.DetectionCategories, attachedSourceDB);

            // Copy the Info table 
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpInfo", DBTables.Info, attachedSourceDB);

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
            queryPhrase += SqlLine.InsertTable2DataIntoTable1(TableName, tmpTableName);
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
                string createTableWithSchema = dtschema.Rows[0].ItemArray[0]!.ToString();
                // Substitute the new table name into the creation string
                createTableWithSchema = Regex.Replace(createTableWithSchema!, @"CREATE .*\(", $"CREATE TABLE {destLevelTableName} (", RegexOptions.IgnoreCase);
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
                List<string> idClauses = [];
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
            using CommonDatabase destinationDdbTemplate = new(destinationDdb.FilePath);
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
            // So we can check the source database for various things
            SQLiteWrapper sourceDdb = new(sourceDdbPath);

            // 1. Check version compatability of the source Database with the backwards Compatability of the destination Database. If its a problem, abort.
            DatabaseFileErrorsEnum databaseFileErrors = FilesFolders.IsDatabaseVersionMergeCompatabileWithTimelapseVersion(sourceDdb, destinationDdb);
            if (databaseFileErrors != DatabaseFileErrorsEnum.Ok)
            {
                // Oops, can't do the merge as there is a version compatability issue.
                return databaseFileErrors;
            }

            // a. We need several temporary tables 
            string attachedSourceDB = "attachedSourceDB";

            // Part 1. Initiate the query phrase:
            //  - begin transaction, turn off foreign keys as we will be clearing and updating tables with foreign keys, and attach the sourceDdb.
            string query = SqlLine.BeginTransaction();
            query += SqlLine.ForeignKeyOff();
            query += SqlLine.AttachDatabaseAs(sourceDdbPath, attachedSourceDB) + Environment.NewLine;

            // Part 2. Calculate an ID offset (the current max Id).
            // We will be adding that to all Ids for the entries inserted into the destinationDdb 
            //    This will guarantee that there are no duplicate primary keys.
            //    Note: if there are no entries in the database, this function returns 0 
            long offsetId = 1 + destinationDdb.ScalarGetMaxValueAsLong(DBTables.FileData, DatabaseColumn.ID);

            // Part 3. Merge the DataTable query. This will modify the source Datatable by:
            // - updating its IDs by adding the offsetID amount to each ID
            // - adjusting the relative path to add a prefix to it correctly identifying its sub-folder location
            string tempDataTable = "tempDataTable";
            query += QueryPhraseMergeDataTable(offsetId, destinationDdb, attachedSourceDB, tempDataTable, relativePathDifference) + Environment.NewLine;

            // Part 4. Handle the Markers Table portion
            string tempMarkersTable = "tempMarkersTable";
            query += QueryPhraseMergeMarkersTable(offsetId, destinationDdb, attachedSourceDB, tempMarkersTable) + Environment.NewLine;

            // Part 5. Handle the various Recognition Tables portion
            // TODO: Test the case where Detections is empty - should we check that Detections have count > 0? If so, would it try to create the table?
            bool destinationRecognitionsExist = destinationDdb.TableExists(DBTables.Detections);
            Tuple<DatabaseFileErrorsEnum, string, bool> resultTuple = QueryPhraseMergeRecognitionTablesAsNeeded(
                query,
                destinationDdb,
                sourceDdbPath,
                attachedSourceDB,
                destinationRecognitionsExist,
                offsetId);

            if (resultTuple.Item1 != DatabaseFileErrorsEnum.Ok)
            {
                // Oops, can't do the merge.
                return resultTuple.Item1;
            }
            query = resultTuple.Item2;

            // Part 6. Now update the various MetadataTables if and as needed
            // If the MetadataTableInfo exists in the destination
            if (destinationDdb.TableExists(DBTables.MetadataInfo))
            {
                DataTable destInfoTable = destinationDdb.GetDataTableFromSelect($"{Sql.SelectStarFrom} {DBTables.MetadataInfo} {Sql.OrderBy} {Control.Level}");
                int maxLevel = destInfoTable.Rows.Count;
                int startLevel = levelsToIgnore + 1;
                for (int srcLevel = 1, destLevel = startLevel; destLevel <= maxLevel; srcLevel++, destLevel++)
                {

                    string srcTableName = FileDatabase.MetadataComposeTableNameFromLevel(srcLevel);
                    string destTableName = FileDatabase.MetadataComposeTableNameFromLevel(destLevel);
                    if (sourceDdb.TableExists(srcTableName))
                    {
                        query += SqlQueryPhraseMergeLevelsTable(destinationDdb, attachedSourceDB, srcTableName, destTableName, relativePathDifference);
                    }
                }
            }

            // Part 7. The query is now done,
            // turn Foreign keys back on again, end the transaction and execute it
            // Turn Foreign keys back on again 
            query += SqlLine.ForeignKeyOn();
            query += SqlLine.EndTransaction();
            destinationDdb.ExecuteNonQuery(query);

            // Part 8. Create the indexes for the detections and detectionsVideo table if they don't already exist
            FileDatabase.IndexCreateForDetectionsIfNotExists(destinationDdb);
            return DatabaseFileErrorsEnum.Ok;
        }
        #endregion

        #region Helpers: GetRootFolder, GetDistinctRootsFromRelativePath



        // Unused but keep for now in case it becomes useful at some point

        // Given a path, get the first folder in it (or Empty if there isn't one)
        //private static string GetRootFolder(string path)
        //{
        //    while (true)
        //    {
        //        string temp = Path.GetDirectoryName(path);
        //        if (string.IsNullOrEmpty(temp))
        //        {
        //            break;
        //        }

        //        path = temp;
        //    }

        //    return path;
        //}

        // Get a list of all unique relative paths in the source DDB
        // Form: SELECT DISTINCT RelativePath FROM DataTable
        //private static List<string> GetDistinctRootsFromRelativePath(SQLiteWrapper ddb, string pathPrefix)
        //{
        //    // Get the relative paths
        //    DataTable dtRelativePathsInDdb =
        //        ddb.GetDataTableFromSelect(Sql.SelectDistinct + DatabaseColumn.RelativePath + Sql.From +
        //                                   DBTables.FileData
        //                                   + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal +
        //                                   Sql.Quote(pathPrefix)
        //                                   + Sql.Or + DatabaseColumn.RelativePath + Sql.Like +
        //                                   Sql.Quote(pathPrefix + "\\%"));

        //    List<string> relativePathRoots = new List<string>();
        //    foreach (DataRow row in dtRelativePathsInDdb.Rows)
        //    {
        //        string rootFolder = GetRootFolder((string)row[0]);
        //        if (false == relativePathRoots.Contains(rootFolder))
        //        {
        //            relativePathRoots.Add(rootFolder);
        //        }
        //    }

        //    return relativePathRoots;
        //}
        #endregion

        #region QueryPhrases used to merge various datatables
        private static SQLiteWrapper GetDatabaseIfTableExists(string sourceDatabasePath, string tableName)
        {
            // Return a connection to the database at the path only if the table exists.
            SQLiteWrapper sourceDdb = new(sourceDatabasePath);
            return sourceDdb.TableExists(tableName) ? sourceDdb : null;
        }

        // Insert various recognition tables found in the attachedSourceDB into the destinationDdb
        private static string QueryPhraseInsertRecognitionTablesFromOneDBIntoAnother(
            string query,
            SQLiteWrapper destinationDdb,
            string attachedSourceDB)
        {
            // Condition 1. The destinationDdb doesn't have detections, but we now know that the sourceDdb does,
            // Thus we need to create and copy the detection tables in the destination database.
            // Since we are copying the tables in their entirety, we don't have to create temporary tables.

            RecognitionDatabases.PrepareRecognitionTablesAndColumns(destinationDdb, false);

            // Import the Detection Categories, Classification Categories and Info from the sourceDdb

            // Now we insert those tables into the current database
            query += SqlLine.InsertTableDataFromAnotherDatabase(DBTables.DetectionCategories, attachedSourceDB);
            query += SqlLine.InsertTableDataFromAnotherDatabase(DBTables.ClassificationCategories, attachedSourceDB);
            query += SqlLine.InsertTableDataFromAnotherDatabase(DBTables.Info, attachedSourceDB);

            // At this point,we have 
            // - created the recognition database tables in the destination, 
            // - filled in the info and the two detection/classification category tables
            return query;
        }

        private static Tuple<DatabaseFileErrorsEnum, string, bool> QueryPhraseMergeRecognitionTablesAsNeeded(
            string query,
            SQLiteWrapper destinationDdb,
            string sourceDdbPath,
            string attachedSourceDB,
            bool destinationDetectionsExists, long offsetId)
        {
            string tempDetectionsTable = "tempDetectionTable";
            //
            // Step 1. Create a connection to the sourceDdbPath so we can check a few things.
            //
            SQLiteWrapper sourceDdb = GetDatabaseIfTableExists(sourceDdbPath, DBTables.Detections);

            //
            // Step 2. Case: sourceDdb does not contain reconitions 
            //         Abort merging recognitions if a recognition table isn't present, as there are no recognitions to import
            if (null == sourceDdb)
            {
                return new(DatabaseFileErrorsEnum.Ok, query, false);
            }

            //
            // Step 3. Case: destinationDdb doesn't have detections, sourceDdb has detections,
            //

            // Create and copy the detection tables in the destination database in their entirety, correction the ID offset as needed
            if (false == destinationDetectionsExists)
            {
                query = QueryPhraseInsertRecognitionTablesFromOneDBIntoAnother(query, destinationDdb, attachedSourceDB);
                query += QueryPhraseMergeDetectionsTable(offsetId, attachedSourceDB, tempDetectionsTable);
                query += SqlLine.InsertTable2DataIntoTable1(DBTables.DetectionsVideo, $"{attachedSourceDB}.{Constant.DBTables.DetectionsVideo}");
                return new(DatabaseFileErrorsEnum.Ok, query, true);
            }

            //
            // From here on, both the destinationDdb and sourceDdb have recognitions so we have to merge those structures
            // Note that we assume, perhaps incorrectly, that the source and destination databases have some detection categories.

            // Step 4. Dictionaries: Populate various dictionaries with selected source and destination table values
            // Source database dictionaries 
            Dictionary<string, object> sourceInfoDictionary = [];
            Dictionary<string, string> sourceDetectionCategories = [];
            Dictionary<string, string> sourceClassificationCategories = [];
            Dictionary<string, string> sourceClassificationDescriptions = [];

            // Destination database database dictionaries 
            Dictionary<string, object> destinationInfoDictionary = [];
            Dictionary<string, string> destinationDetectionCategories = [];
            Dictionary<string, string> destinationClassificationCategories = [];
            Dictionary<string, string> destinationClassificationDescriptions = [];

            // Fill them in
            RecognitionUtilities.GenerateRecognitionDictionariesFromDB(sourceDdbPath, sourceInfoDictionary,
                sourceDetectionCategories, sourceClassificationCategories, sourceClassificationDescriptions);
            RecognitionUtilities.GenerateRecognitionDictionariesFromDB(destinationDdb, destinationInfoDictionary,
                destinationDetectionCategories, destinationClassificationCategories, destinationClassificationDescriptions);
            //
            // Step 5. Info: Generate a new info structure that is a best effort combination of the db and json info structure,
            //     Then Update the destinationDdb Info table with those values.
            //     Note the we do it even if no update is really needed, as its lightweight
            Dictionary<string, object> mergedInfoDictionary =
                RecognitionUtilities.GenerateBestRecognitionInfoFromTwoInfos(destinationInfoDictionary, sourceInfoDictionary);
            query += SqlQueryUpdateInfoTableWithValues(
                (string)mergedInfoDictionary[InfoColumns.Detector],
                (string)mergedInfoDictionary[InfoColumns.DetectorVersion],
                (string)mergedInfoDictionary[InfoColumns.DetectionCompletionTime],
                (string)mergedInfoDictionary[InfoColumns.Classifier],
                (string)mergedInfoDictionary[InfoColumns.ClassificationCompletionTime],
                Convert.ToDouble(mergedInfoDictionary[InfoColumns.TypicalDetectionThreshold]),
                Convert.ToDouble(mergedInfoDictionary[InfoColumns.ConservativeDetectionThreshold]),
                Convert.ToDouble(mergedInfoDictionary[InfoColumns.TypicalClassificationThreshold]));

            //
            // Step 6. ImageSetTable: Update the destinationDdb ImageSet table BoundingBoxDisplayThreshold to its undefined value.
            //
            query += SqlLine.UpdateColumnInTable(DBTables.ImageSet, DatabaseColumn.BoundingBoxDisplayThreshold, Constant.RecognizerValues.BoundingBoxDisplayThresholdDefault);

            //
            // Step 7. detectionCategory table. Replace the detectionCategory table in the destinationDB with a table that merges the current destination and updated src table:
            //     If category numbers differ, ensure that the source and destination category numbers are compatible by:
            //     - creating a temporary table in the source database that contains the detection categories, 
            //     - updating (remapping) its category numbers to match those in the destination ddb,
            //     - updating the src detections table (via a temporary table) to match the new category numbers
            query += SqlLine.CreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedSourceDB, DBTables.Detections);

            if (FileDatabase.RemapAndReplaceCategoryNumbersIfNeeded(destinationDetectionCategories, sourceDetectionCategories,
                out Dictionary<string, string> remappedDetectionCategoryDict, out Dictionary<string, string> detectionCategoryLookupMappingDict))
            {
                // 7a: Remapping detection categories and corresponding values in the detection table is needed,
                // - Replace the detection categories table contents with the pairs found in the remappedDetectionCategoryDict
                //   which we generate by merging the updated source and destination dictionaries
                // - Remap the source dictionary's detection category number to match those in the destination dictionary
                // The lookup dictionary contains the mapping of the old to the new category numbers as old/new number pairs

                Tuple<DatabaseFileErrorsEnum, string, bool> tuple = SqlPhraseUpdateCategory(
                    query, tempDetectionsTable, DBTables.DetectionCategories, DetectionCategoriesColumns.Category, DetectionCategoriesColumns.Label,
                    destinationDetectionCategories, remappedDetectionCategoryDict, detectionCategoryLookupMappingDict, true);
                if (tuple.Item1 != DatabaseFileErrorsEnum.Ok)
                {
                    return tuple;
                }
                query = tuple.Item2;
            }

            // 7b. Simlar to above but with classification categories
            // TODO: Changed to difft method that includes Categore Descriptions. See Importing recognitions for difft way to do it.
            if (FileDatabase.RemapAndReplaceCategoryNumbersIfNeeded(destinationClassificationCategories, sourceClassificationCategories,
                    out Dictionary<string, string> remappedClassificationCategoryDict, out Dictionary<string, string> classificationCategoryLookupMappingDict))
            {
                // TODO: SEE 15- THIS ONE DOESN"T WORK PROPERLY IF DESTINATONCLASSDESCR HAS EMPTY?
                FileDatabase.RemapAndReplaceDescriptonNumbersIfNeeded(sourceClassificationDescriptions, classificationCategoryLookupMappingDict,
                    out Dictionary<string, string> remappedClassificationDescriptionsDict);

                // The merged dictionary will be category number, category label, description
                MergeSourceAndDestinationClassificationDictionaries(
                    destinationClassificationCategories, remappedClassificationCategoryDict,
                    destinationClassificationDescriptions, remappedClassificationDescriptionsDict,
                    out Dictionary<string, Tuple<string, string>> mergedClassificationColumns);

                Tuple<DatabaseFileErrorsEnum, string, bool> tuple = SqlPhraseUpdateClassificationCategory(
                    query, tempDetectionsTable, DBTables.ClassificationCategories, ClassificationCategoriesColumns.Category, ClassificationCategoriesColumns.Label, ClassificationCategoriesColumns.Description,
                    mergedClassificationColumns, classificationCategoryLookupMappingDict);

                if (tuple.Item1 != DatabaseFileErrorsEnum.Ok)
                {
                    return tuple;
                }
                query = tuple.Item2;
            }
            else if (sourceClassificationCategories.Count > 0 && destinationClassificationCategories.Count == 0)
            {
                // Classification categories were found in the source but not in the destination. 
                // So just copy the table over.
                query += SqlLine.InsertTable2DataIntoTable1(DBTables.ClassificationCategories, $"{attachedSourceDB}.{Constant.DBTables.ClassificationCategories}");
            }
            // There are no further else statements, as the only way to get here is if the source and destination classification categories are the same, or if the source has no classification categories
            // So nothing needs to be done.

            // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
            long offsetDetectionId = destinationDdb.ScalarGetMaxValueAsLong(DBTables.Detections, DetectionColumns.DetectionID);

            // Merge the Detections table into the destination database
            query += SqlQueryPhraseMergeDetectionTableUsingOffsets(offsetId, offsetDetectionId, tempDetectionsTable);
            query += SqlLine.DropTableIfExists(tempDetectionsTable);

            // 5th: Merge the temporary detectionsVidoes table into the destination database
            if (sourceDdb.TableExists(Constant.DBTables.DetectionsVideo))
            {
                string tempDetectionsVideoTable = "tempDetectionsVideoTable";
                query += SqlLine.CreateTemporaryTableFromExistingTable(tempDetectionsVideoTable, attachedSourceDB, DBTables.DetectionsVideo);
                query += SqlLine.AddOffsetToColumnInTable(tempDetectionsVideoTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
                query += SqlLine.InsertTable2DataIntoTable1(DBTables.DetectionsVideo, tempDetectionsVideoTable);
                query += SqlLine.DropTableIfExists(tempDetectionsTable);
            }

            return new(DatabaseFileErrorsEnum.Ok, query, true);
        }

        // This should always work, as the source and destination databases should have the same values before this is invoked
        private static void MergeSourceAndDestinationClassificationDictionaries(
            Dictionary<string, string> destinationCategories, Dictionary<string, string> remappedCategories,
            Dictionary<string, string> destinationDescriptions, Dictionary<string, string> remappedDescriptions,
            out Dictionary<string, Tuple<string, string>> mergedClassificationColumns)
        {
            mergedClassificationColumns = [];


            if ((destinationCategories == null || destinationCategories.Count == 0) && (remappedCategories == null || remappedCategories.Count == 0))
            {
                // No categories exist
                // As no categories exist at all, definitions (even if present) aren't relevant
                // so return an empty merged dictionary 
                // so mergedDictionary has 0 items
                return;
            }

            if (destinationCategories == null || destinationCategories.Count == 0)
            {
                // As the destination categories don't exist, just return a merge of the (remapped) source categories and its corresponding descriptions
                // even if there is a destination description
                foreach (KeyValuePair<string, string> kvp in remappedCategories)
                {
                    if (remappedDescriptions.TryGetValue(kvp.Key, out string description) && false == string.IsNullOrWhiteSpace(description))
                    {
                        mergedClassificationColumns.Add(kvp.Key, new(kvp.Value, description));
                    }
                    else
                    {
                        mergedClassificationColumns.Add(kvp.Key, new(kvp.Value, string.Empty));
                    }
                }
                return;
            }

            if (remappedCategories == null || remappedCategories.Count == 0)
            {
                foreach (KeyValuePair<string, string> kvp in destinationCategories)
                {
                    if (destinationDescriptions.TryGetValue(kvp.Key, out string description) && false == string.IsNullOrWhiteSpace(description))
                    {
                        mergedClassificationColumns.Add(kvp.Key, new(kvp.Value, description));
                    }
                    else
                    {
                        mergedClassificationColumns.Add(kvp.Key, new(kvp.Value, string.Empty));
                    }
                }
                return;
            }

            // At this point, there is something in both destination and merged categories
            // His is a just in case bit of code for debuggingto check if the dictionaries are compatable, i.e., that the common keys have the same values.
            //foreach (KeyValuePair<string, string> pair in destinationCategories)
            //{
            //    if (remappedCategories.TryGetValue(pair.Key, out string value))
            //    {
            //        // If the key is in remappedCategories and it has a different value, we've failed for some unusual reason
            //        // This shouldn't happen, but just in case
            //        if (value != pair.Value)
            //        {
            //            Debug.Print("In MergeSourceAndDestinationClassificationDictionaries: Problem as values don't match");
            //        }
            //    }
            //}
            //foreach (KeyValuePair<string, string> pair in remappedCategories)
            //{
            //    if (destinationCategories.TryGetValue(pair.Key, out string value))
            //    {
            //        // If the key is in destinationCategories and it has a different value, we've failed
            //        if (value != pair.Value)
            //        {
            //           Debug.Print("In MergeSourceAndDestinationClassificationDictionaries: Problem as values don't match");
            //        }
            //    }
            //}

            // At this point we know that the dictionaries are compatable. 
            // Start by adding all KVP in destinationCategories to the merged dictionary
            // By default, this means that the destination descriptions are also populated
            foreach (KeyValuePair<string, string> kvp in destinationCategories)
            {
                if (destinationDescriptions.TryGetValue(kvp.Key, out string description) && false == string.IsNullOrWhiteSpace(description))
                {
                    mergedClassificationColumns.Add(kvp.Key, new(kvp.Value, description));
                }
                else
                {
                    mergedClassificationColumns.Add(kvp.Key, new(kvp.Value, string.Empty));
                }
            }

            // Now add the KVP in remappedDescriptions that aren't present in destinationCategories
            // At the same time, test if there are empty descriptions and if so use the non-empty description
            foreach (KeyValuePair<string, string> kvp in remappedCategories)
            {
                if (remappedDescriptions.TryGetValue(kvp.Key, out string description) && false == string.IsNullOrWhiteSpace(description))
                {
                    // We have remapped description. Use that if the destination's description is empty, otherwise stick with the destination's description
                    if (destinationDescriptions.TryGetValue(kvp.Key, out string destinationDescription) && false == string.IsNullOrWhiteSpace(destinationDescription))
                    {
                        MergedClassificationUpsert(mergedClassificationColumns, kvp.Key, kvp.Value, destinationDescription);
                    }
                    else
                    {
                        // This pair is in the source but not in the destination
                        MergedClassificationUpsert(mergedClassificationColumns, kvp.Key, kvp.Value, description);
                    }
                }
                else
                {
                    // the remapped description is empty. Try to get it from the destination
                    if (destinationDescriptions.TryGetValue(kvp.Key, out string destinationDescription) && false == string.IsNullOrWhiteSpace(destinationDescription))
                    {
                        // Use destination description. If the key already exists, just update the value
                        MergedClassificationUpsert(mergedClassificationColumns, kvp.Key, kvp.Value, destinationDescription);
                    }
                    else
                    {
                        // There are no descriptions in either, so use empty descriptions. If the key already exists, just update the value
                        MergedClassificationUpsert(mergedClassificationColumns, kvp.Key, kvp.Value, string.Empty);
                    }
                }
            }
        }
        private static void MergedClassificationUpsert(Dictionary<string, Tuple<string, string>> mergedClassificationColumns, string key, string value, string description)
        {
            if (mergedClassificationColumns.ContainsKey(key))
            {
                mergedClassificationColumns[key] = new(value, description);
            }
            else
            {
                // Empty description
                mergedClassificationColumns.Add(key, new(value, description));
            }
        }
    }


    #endregion

}
