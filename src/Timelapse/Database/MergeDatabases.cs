using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
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
            query += MergeDatabasesSqlPhrases.QueryCheckoutMergeDataTable(destinationDdb, sourceDdbPath, attachedSourceDB, tempDataTable, relativePath) + Environment.NewLine;

            // Part 3. Create the Markers table, where where it contains only those entries that match the entries in the datatable
            query += MergeDatabasesSqlPhrases.QueryCheckoutMarkersTable(attachedSourceDB, tempMarkersTable, relativePath) + Environment.NewLine;

            // Part 4. Update the ImageSetTable by importing the values for Quickpaste and BBDisplayThreshold
            query += MergeDatabasesSqlPhrases.QueryCheckoutImageSetTable(attachedSourceDB) + Environment.NewLine;

            // Part 5. Handle the various Recognition Tables portion
            // Note that the two classification tables are only included in the checkout process if there is something in them
            if (sourceDdb.TableExists(DBTables.Detections))
            {
                RecognitionDatabases.PrepareRecognitionTablesAndColumns(destinationDdb, false);
                bool checkoutClassificationCategoriesTable = sourceDdb.TableExistsAndNotEmpty(DBTables.ClassificationCategories);
                query += MergeDatabasesSqlPhrases.QueryCheckoutRecognitionTables(attachedSourceDB, relativePath, checkoutClassificationCategoriesTable) + Environment.NewLine;
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
                        query += MergeDatabasesSqlPhrases.QueryCheckoutLevelTable(sourceDdbFileDatabase, attachedSourceDB, srcLevelTableName, destLevelTableName, relativePath) + Environment.NewLine;
                    }
                }
            }
            // Part 6. We are done.
            query += Sql.EndTransactionSemiColon;
            destinationDdb.ExecuteNonQueryWithRollback(query);
        }
        #endregion

        #region Merge a source database into the destinaton database
        public static DatabaseFileErrorsEnum MergeSourceIntoDestinationDdb(
                                         SQLiteWrapper destinationDdb,
                                         string sourceDdbPath,
                                         string relativePathDifference, int levelsToIgnore,
                                         List<string> deletionQueries)
        {
            string nl = Environment.NewLine;
            // 1 Preparation 
            // 1A Source database wrapper, used throughout this method
            //    Start with an initial check for version compatability of the source Database for backwards Compatability to the destination Database.
            //    If its a problem, abort.
            SQLiteWrapper sourceDdb = new(sourceDdbPath);
            DatabaseFileErrorsEnum databaseFileErrors = FilesFolders.IsDatabaseVersionMergeCompatabileWithTimelapseVersion(sourceDdb, destinationDdb);
            if (databaseFileErrors != DatabaseFileErrorsEnum.Ok)
            {
                // Oops, can't do the merge as there is a version compatability issue.
                return databaseFileErrors;
            }

            // 1B Calculate an ID offset, which will be just one above the current max Id).
            // We will be adding that to all Ids for the entries inserted into the destinationDdb 
            //    This will guarantee that there are no duplicate primary keys.
            //    Note: if there are no entries in the database, this function returns 0 
            long destinationOffsetId = 1 + destinationDdb.ScalarGetMaxValueAsLong(DBTables.FileData, DatabaseColumn.ID);

            // 1C We need the actual column names of these tables to do an insert
            List<string> destinationDataTableColumns = destinationDdb.SchemaGetColumns(Constant.DBTables.FileData);
            List<string> destinationMarkersTableColumns = destinationDdb.SchemaGetColumns(Constant.DBTables.Markers);

            // 1D A check to see if the destinationDdb has a DetectionTable 
            bool destinationRecognitionsExist = destinationDdb.TableExists(DBTables.Detections);
            bool sourceRecognitionsExist = sourceDdb.TableExists(DBTables.Detections);

            // 2. Initiate the query phrase:
            //  - Turn off foreign keys as some of the tables we will be clearing/updating contain foreign keys.
            string query = string.Empty;

            // 3. Delete all destintaion rows in the DataTable, MarkersTable, and Levels that will be replaced by rows in the source data
            // This deletion query was prepared ahead of time.
            foreach (string q in deletionQueries)
            {
                query += $"{q}{Environment.NewLine}";
            }

            // 3. Attach the Source DB
            string attachedSourceDB = "attachedSourceDB";
            query += $"{SqlLine.AttachDatabaseAs(sourceDdbPath, attachedSourceDB)} {nl}";


            // 4.DataTable: Insert the columns from the source DB nto the destination DB, correcting for ID and RelativePath
            query += $"{MergeDatabasesSqlPhrases.GetQueryDataTableInsertColumnsFromSourcWithCorrectedID(attachedSourceDB, destinationDataTableColumns, destinationOffsetId, relativePathDifference)} {nl}";

            // 5. MarkersTable: Insert the columns from the source DB nto the destination DB, correcting for ID and RelativePath
            query += $"{MergeDatabasesSqlPhrases.GetQueryMarkersTableInsertColumnsFromSourceWithCorrectedID(attachedSourceDB, destinationMarkersTableColumns, destinationOffsetId)} {nl}";

            // ====================================================== UPDATED TO HERE ======================================================
            // Merge recognitions only if either the source and/or destination have recognitions
            if (destinationRecognitionsExist || sourceRecognitionsExist)
            {
                // Part 5. Handle the various Recognition Tables portion
                Tuple<DatabaseFileErrorsEnum, string, bool> resultTuple = MergeDatabasesSqlPhrases.GetQueryMergeRecognitionTablesAsNeeded(
                query,
                destinationDdb,
                sourceDdb,
                attachedSourceDB,
                destinationRecognitionsExist,
                sourceRecognitionsExist,
                destinationOffsetId);

                if (resultTuple.Item1 != DatabaseFileErrorsEnum.Ok)
                {
                    // Oops, can't do the merge.
                    return resultTuple.Item1;
                }
                query = resultTuple.Item2;
            }

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
                        query += MergeDatabasesSqlPhrases.GetQueryMergeLevelsTable(destinationDdb, attachedSourceDB, srcTableName, destTableName, relativePathDifference);
                    }
                }
            }

            // Part 7. The query is now done, execute it
            SqlOperationResult result = destinationDdb.ExecuteNonQueryWithRollback(query);
            if (!result.Success)
            {
                return DatabaseFileErrorsEnum.MergeFailedDueToSQLiteQueryError;
            }

            // Part 8. Create the indexes for the detections and detectionsVideo table if they don't already exist
            if (destinationRecognitionsExist)
            {
                FileDatabase.IndexCreateForDetectionsIfNeeded(destinationDdb);
            }
            return DatabaseFileErrorsEnum.Ok;
        }
        #endregion

        #region Private: Check If Metadata Templates Are Merge Compatable
        private static bool CheckIfMetadataTemplatesAreMergeCompatable(SQLiteWrapper sourceDdb,
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

    }
}
