using System;
using System.Collections.Generic;
using System.Data;
using Timelapse.Constant;
using Timelapse.Enums;

namespace Timelapse.Database
{
    public static partial class MergeDatabases
    {
        // Generate various SQL phrases for merging as needed. Used by MergeDatabases only

        #region Update the column (normally relative path) to include the indicated prefix
        // Form: columnName should be RelativePath)
        //   UPDATE tableName SET RelativePath = CASE WHEN RelativePath = '' THEN ("pathPrefixToAdd" || RelativePath) ELSE ("pathPrefixToAdd"\\ || RelativePath) END;
        private static string SqlQueryUpdateTablesColumnByIncludingPathPrefix(string tableName, string pathPrefixToAdd, string columnName)
        {
            if (string.IsNullOrWhiteSpace(pathPrefixToAdd))
            {
                // No need to construct a new relative path if there is nothing to add to it
                return string.Empty;
            }
            // Note that tableName must be a DataTable for this to work
            return $"{Sql.Update} {tableName} {Sql.Set} {columnName} {Sql.Equal} " +
                   $"{Sql.CaseWhen} {columnName} {Sql.Equal} {Sql.Quote(string.Empty)} {Sql.Then} " +
                   $"{Sql.OpenParenthesis} {Sql.Quote(pathPrefixToAdd)} {Sql.Concatenate} {columnName} {Sql.CloseParenthesis} " +
                   $"{Sql.Else} {Sql.OpenParenthesis} {Sql.Quote(pathPrefixToAdd + "\\")} {Sql.Concatenate} {columnName} {Sql.CloseParenthesis} {Sql.End} {Sql.Semicolon} {Environment.NewLine}";
        }
        #endregion

        #region Update the Info table with the new values
        // Form: UPDATE Info SET Detector = 'detector', DetectorVersion = 'megadetector_version', DetectionCompletionTime = 'detection_completion_time', etc
        private static string SqlQueryUpdateInfoTableWithValues(string detector, string megadetector_version,
            string detection_completion_time, string classifier, string classification_completion_time,
            double typical_detection_threshold, double conservative_detection_threshold,
            double typical_classification_threshold)
        {
            return $"{Sql.Update} {DBTables.Info} {Sql.Set} " +
                   $"{InfoColumns.Detector} {Sql.Equal} {Sql.Quote(detector)} {Sql.Comma} " +
                   $"{InfoColumns.DetectorVersion} {Sql.Equal} {Sql.Quote(megadetector_version)} {Sql.Comma} " +
                   $"{InfoColumns.DetectionCompletionTime} {Sql.Equal} {Sql.Quote(detection_completion_time)} {Sql.Comma} " +
                   $"{InfoColumns.Classifier} {Sql.Equal} {Sql.Quote(classifier)} {Sql.Comma} " +
                   $"{InfoColumns.ClassificationCompletionTime} {Sql.Equal} {Sql.Quote(classification_completion_time)} {Sql.Comma}" +
                   $"{InfoColumns.TypicalDetectionThreshold} {Sql.Equal} {(Math.Round(typical_detection_threshold * 100) / 100)} {Sql.Comma} " +
                   $"{InfoColumns.ConservativeDetectionThreshold} {Sql.Equal} {(Math.Round(conservative_detection_threshold * 100) / 100)} {Sql.Comma} " +
                   $"{InfoColumns.TypicalClassificationThreshold} {Sql.Equal} {(Math.Round(typical_classification_threshold * 100) / 100)} {Sql.Semicolon} {Environment.NewLine}";
        }
        #endregion

        #region Replace old table values (assuming only two columns are in the table) with new values
        // Generate a query to clear the given table and to populate its columns as needed
        // with the values in the dictionary. The dictionary pairs order must match the column1/2 order
        // FORM example (using DetectionCategories and its category/label columns): 
        // DELETE FROM DetectionCategories;
        // INSERT INTO  DetectionCategories(category, label)   VALUES('0', 'Empty');
        // ('1', 'animal'),
        // ('5', 'train5'),
        // ('6', 'car6'),
        // ('7', 'newEntity7');
        private static string SqlQueryReplaceValuesInTwoColumnTable(string tableName, Dictionary<string, string> dictionaryOfNewValues, string column1, string column2)
        {
            string query = string.Empty;

            // Clear the table
            query += $"{Sql.DeleteFrom}  {tableName} {Sql.Semicolon} {Environment.NewLine}";

            // Update the two columns in the table with the dictionary values.
            query += $"{Sql.InsertInto} {tableName} " +
                     $"{Sql.OpenParenthesis} {column1} {Sql.Comma} {column2} {Sql.CloseParenthesis} {Sql.Values}";
            foreach (KeyValuePair<string, string> kvp in dictionaryOfNewValues)
            {
                query += $"{Sql.OpenParenthesis} {Sql.Quote(kvp.Key)} {Sql.Comma} {Sql.Quote(kvp.Value)} {Sql.CloseParenthesis} {Sql.Comma}";
            }

            // Replace the last comma with a semicolon
            query = query[..query.LastIndexOf(Sql.Comma, StringComparison.Ordinal)];
            query += Sql.Semicolon + Environment.NewLine;
            return query;
        }
        #endregion

        #region Replace old table values (assuming only two columns are in the table) with new values
        // Generate a query to clear the given table and to populate its columns as needed
        // with the values in the dictionary. The dictionary pairs order must match the column1/2 order
        // FORM example (using DetectionCategories and its category/label columns): 
        // DELETE FROM DetectionCategories;
        // INSERT INTO  DetectionCategories(category, label)   VALUES('0', 'Empty');
        // ('1', 'animal', 'some description1'),
        // ('5', 'train5', 'some description2'),
        // ('6', 'car6',   'some description3'),
        // ('7', 'newEntity7','some description4');
        private static string SqlQueryReplaceValuesInTheeColumnTable(string tableName, Dictionary<string, Tuple<string, string>> dictionaryOfNewValues, string column1, string column2, string column3)
        {
            string query = string.Empty;

            // Clear the table
            query += $"{Sql.DeleteFrom}  {tableName} {Sql.Semicolon} {Environment.NewLine}";

            // Update the three columns in the table with the dictionary values.
            query += $"{Sql.InsertInto} {tableName} " +
                     $"{Sql.OpenParenthesis} {column1} {Sql.Comma} {column2} {Sql.Comma} {column3} {Sql.CloseParenthesis} {Sql.Values}";
            foreach (KeyValuePair<string, Tuple<string,string>> kvp in dictionaryOfNewValues)
            {
                query += $"{Sql.OpenParenthesis} {Sql.Quote(kvp.Key)} {Sql.Comma} {Sql.Quote(kvp.Value.Item1)} {Sql.Comma} {Sql.Quote(kvp.Value.Item2)} {Sql.CloseParenthesis} {Sql.Comma}";
            }

            // Replace the last comma with a semicolon
            query = query[..query.LastIndexOf(Sql.Comma, StringComparison.Ordinal)];
            query += Sql.Semicolon + Environment.NewLine;
            return query;
        }
        #endregion

        #region Merge DataTable
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
        private static string QueryPhraseMergeDataTable(long offsetId, SQLiteWrapper destinationDdb, string attachedSourceDB, string tempDataTable, string relativePathDifference)
        {
            string query = string.Empty;
            List<string> currentDataLabels = destinationDdb.SchemaGetColumns(DBTables.FileData);


            // Create a temporary table from the markers table, where we will update that table
            query += SqlLine.CreateTemporaryTableFromExistingTable(tempDataTable, attachedSourceDB, DBTables.FileData);

            // Add the offset to the IDs to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
            query += SqlLine.AddOffsetToColumnInTable(tempDataTable, DatabaseColumn.ID, offsetId);

            // Add the prefix to the relative path
            query += SqlQueryUpdateTablesColumnByIncludingPathPrefix(tempDataTable, relativePathDifference, DatabaseColumn.RelativePath) + Environment.NewLine;

            // Insert the modified DataTable into the current database's datatable
            query += SqlLine.InsertTable2DataIntoTable1(DBTables.FileData, tempDataTable, currentDataLabels);

            // We no longer need the temporary data table, so drop it.
            query += $"{Sql.DropTableIfExists} {tempDataTable} {Sql.Semicolon} {Environment.NewLine}";
            return query;
        }
        #endregion

        #region Merge Markers table
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
                    columns += $"{Sql.Comma} ";
                }
                columns += $"{row[0]} ";
            }

            // Create a temporary table from the markers table, where we will update that table
            query += SqlLine.CreateTemporaryTableFromExistingTable(tempMarkersTable, attachedSourceDB, DBTables.Markers, columns);

            // Add the offset to the IDd to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
            query += SqlLine.AddOffsetToColumnInTable(tempMarkersTable, DatabaseColumn.ID, offsetId);
            query += SqlLine.InsertTable2DataIntoTable1(DBTables.Markers, tempMarkersTable);

            // We no longer need the temporary data table, so drop it.
            query += SqlLine.DropTableIfExists(tempMarkersTable);
            return query;
        }
        #endregion

        #region Remap categories in category table and detection table
        // Remap the desired categories (which are either detection or classificaton categories) and corresponding values in the indicated category table
        //     and the corresponding column values in the detection table.
        // - Replace the categories table contents with the pairs found in the remappedCategoryDict
        //   which we previously generated by merging the updated source and destination dictionaries
        // - Replace the category values in the detection table as needed
        // The lookup dictionary contains the mapping of the old to the new category numbers as old/new number pairs
        private static Tuple<DatabaseFileErrorsEnum, string, bool> SqlPhraseUpdateCategory(
            string query, string tempDetectionsTable, string tableName, string categoryColumn, string labelColumn,
            Dictionary<string, string> destinationCategories, Dictionary<string, string> remappedCategoryDict, Dictionary<string, string> categoryLookupMappingDict, bool updateTempDetectionsTable)
        {
            // Remap detection categories and corresponding values in the detection table is needed,
            // - Replace the given categories table contents with the pairs found in the remappedDetectionCategoryDict
            //   which we generate by merging the updated source and destination dictionaries
            // - Remap the source dictionary's detection category number to match those in the destination dictionary
            // The lookup dictionary contains the mapping of the old to the new category numbers as old/new number pairs
            if (false == Util.Dictionaries.MergeDictionaries(destinationCategories, remappedCategoryDict,
            out Dictionary<string, string> mergedDetectionDictionary, out bool _))
            {
                return new(DatabaseFileErrorsEnum.DetectionCategoriesIncompatible, query, false);
            }
            query += SqlQueryReplaceValuesInTwoColumnTable(tableName, mergedDetectionDictionary, categoryColumn, labelColumn);

            // 7b: Update the temporaryDetections Category table if asked to do so 
            if (updateTempDetectionsTable)
            {
                query += $"{Sql.Update} {tempDetectionsTable} {Sql.Set} {categoryColumn} {Sql.Equal} {Sql.Case}";
                foreach (KeyValuePair<string, string> categoryMap in categoryLookupMappingDict)
                {
                    query += $"{Sql.When} {categoryColumn} {Sql.Equal} {Sql.Quote(categoryMap.Key)} {Sql.Then} {Sql.Quote(categoryMap.Value)}";
                }
                query += $"{Sql.Else} {categoryColumn} {Sql.End} {Sql.Semicolon} {Environment.NewLine}";
            }
            return new(DatabaseFileErrorsEnum.Ok, query, false);
        }

        private static Tuple<DatabaseFileErrorsEnum, string, bool> SqlPhraseUpdateClassificationCategory(
            string query, string tempDetectionsTable, string tableName, string categoryColumn, string labelColumn, string descriptionColumn,
            Dictionary<string, Tuple<string,string>> mergedClassificationColumns, Dictionary<string, string> categoryLookupMappingDict)
        {
            // Remap detection categories and corresponding values in the detection table is needed,
            // - Replace the given categories table contents with the pairs found in the remappedDetectionCategoryDict
            //   which we generate by merging the updated source and destination dictionaries
            // - Remap the source dictionary's detection category number to match those in the destination dictionary
            // The lookup dictionary contains the mapping of the old to the new category numbers as old/new number pairs
           
            query += SqlQueryReplaceValuesInTheeColumnTable(tableName, mergedClassificationColumns, categoryColumn, labelColumn, descriptionColumn);

            // 7b: Update the temporaryDetections Category table if asked to do so 
            if (categoryLookupMappingDict.Count > 0)
            {
                query += $"{Sql.Update} {tempDetectionsTable} {Sql.Set} {categoryColumn} {Sql.Equal} {Sql.Case}";
                foreach (KeyValuePair<string, string> categoryMap in categoryLookupMappingDict)
                {
                    query += $"{Sql.When} {categoryColumn} {Sql.Equal} {Sql.Quote(categoryMap.Key)} {Sql.Then} {Sql.Quote(categoryMap.Value)}";
                }
                query += $"{Sql.Else} {categoryColumn} {Sql.End} {Sql.Semicolon} {Environment.NewLine}";
            }
            return new(DatabaseFileErrorsEnum.Ok, query, false);
        }
        #endregion

        #region Merge Detections table
        private static string QueryPhraseMergeDetectionsTable(long offsetId, string attachedSourceDB, string tempDetectionsTable)
        {
            string query = string.Empty;

            // Just to make sure we are starting with a new temporary table
            query += $"{Sql.DropTableIfExists} {tempDetectionsTable} {Sql.Semicolon} {Environment.NewLine}";

            query += Sql.CreateTemporaryTable + tempDetectionsTable + Sql.As + Sql.SelectStarFrom +
                     attachedSourceDB + Sql.Dot + DBTables.Detections + Sql.Semicolon + Environment.NewLine;

            // Add the offset to the ID to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
            query += SqlLine.AddOffsetToColumnInTable(tempDetectionsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;
            query += SqlLine.InsertTable2DataIntoTable1(DBTables.Detections, tempDetectionsTable);

            // We no longer need the temporary data table, so drop it.
            query += $"{Sql.DropTableIfExists} {tempDetectionsTable} {Sql.Semicolon} {Environment.NewLine}";
            return query;
        }
        #endregion 

        #region Merge levels
        private static string SqlQueryPhraseMergeLevelsTable(SQLiteWrapper destinationDdb,
            string attachedSourceDB, string srcTableName, string destTableName, string relativePathDifference)
        {
            string tmpLevelsTable = $"tmp{srcTableName}";
            long offsetId = destinationDdb.ScalarGetMaxValueAsLong(destTableName, DatabaseColumn.ID);
            string query = SqlLine.CreateTemporaryTableFromExistingTable(tmpLevelsTable, attachedSourceDB, srcTableName);

            // Add the offset to the IDd to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
            query += SqlLine.AddOffsetToColumnInTable(tmpLevelsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;

            query += SqlQueryUpdateTablesColumnByIncludingPathPrefix(tmpLevelsTable, relativePathDifference, DatabaseColumn.FolderDataPath) + Environment.NewLine;
            query += SqlLine.InsertTable2DataIntoTable1(destTableName, tmpLevelsTable);
            query += SqlLine.DropTableIfExists(tmpLevelsTable) + Environment.NewLine;
            return query;
        }
        #endregion

        #region Merge detections - Should only be invoked if detections exist
        private static string SqlQueryPhraseMergeDetectionTableUsingOffsets(long offsetId, long offsetDetectionId, string detectionsTable)
        {
            // Add the offset to the ID to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
            string query = SqlLine.AddOffsetToColumnInTable(detectionsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;

            // Add offset to the DetectionID to make sure DetectionIDs don't conflict with existing IDs. (A zero offset is a noop).
            query += SqlLine.AddOffsetToColumnInTable(detectionsTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;

            // Do the merge
            List<string> columnNames =
            [
                Constant.DetectionColumns.DetectionID,
                Constant.DetectionColumns.Category,
                Constant.DetectionColumns.Conf,
                Constant.DetectionColumns.BBox,
                Constant.DetectionColumns.Classification,
                Constant.DetectionColumns.ClassificationConf,
                Constant.DatabaseColumn.ID
            ];
            query += SqlLine.InsertTable2DataIntoTable1(DBTables.Detections, detectionsTable, columnNames);
            return query;
        }

        #endregion
    }
}
