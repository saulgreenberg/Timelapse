using System;
using System.Collections.Generic;
using Timelapse.Constant;

namespace Timelapse.Database
{
    public static partial class MergeDatabases
    {

        // Generate various SQL phrases as needed. Used by MergeDatabases only

        #region Simple phrases that updates tables

        //Form:  UPDATE tableName SET RelativePath = CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
        private static string SqlQueryAddPrefixToRelativePathInTable(string tableName, string pathPrefixToAdd, string columnName)
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
        #endregion

        #region Update table with the new values
        private static string SqlQueryUpdateInfoTableWithValues(string detector, string megadetector_version,
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

        private static string SqlQueryUpdateImageSetTableWithUndefinedBoundingBox()
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
            query = query.Substring(0, query.LastIndexOf(Sql.Comma, StringComparison.Ordinal));
            query += Sql.Semicolon + Environment.NewLine;
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

            query += SqlQueryAddPrefixToRelativePathInTable(tmpLevelsTable, relativePathDifference, DatabaseColumn.FolderDataPath) + Environment.NewLine;
            query += SqlLine.InsertTable2DataIntoTable1(destTableName, tmpLevelsTable);
            return query;
        }
        #endregion

        #region Merge detections
        // The database to merge in has recognitions. This SQL query should update the detections table and detectionsVideo table.
        private static string SqlQueryPhraseMergeRecognitionTablesOLD(long offsetId, SQLiteWrapper destinationDdb,
            string attachedSourceDB, bool destinationRecognitionsExist, bool sourceDetectionsVideoTableExists)
        {
            string query = string.Empty;
            string tempDetectionsTable = "tempDetectionsTable";
            string tempDetectionsVideoTable = "tempDetectionsVideoTable";

            // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
            // The offeset should be 0 if there are no detections in the main DB, as we will be creating the detection table and then just adding to it.
            long offsetDetectionId = destinationRecognitionsExist
                ? destinationDdb.ScalarGetMaxValueAsLong(DBTables.Detections, DetectionColumns.DetectionID)
                : 0;

            // Update the Detections table, including adjusting the ID and DetectionID offset as needed
            query += SqlLine.CreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedSourceDB, DBTables.Detections);
            // Add the offset to the IDd to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
               query += SqlLine.AddOffsetToColumnInTable(tempDetectionsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;
            
            if (offsetDetectionId > 0)
            {
                query += SqlLine.AddOffsetToColumnInTable(tempDetectionsTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += SqlLine.InsertTable2DataIntoTable1(DBTables.Detections, tempDetectionsTable);

            // Now update the DetectionsVideo table (but only if it exists), including adjusting the DetectionID offset as needed
            if (sourceDetectionsVideoTableExists)
            {
                query += SqlLine.CreateTemporaryTableFromExistingTable(tempDetectionsVideoTable, attachedSourceDB, DBTables.DetectionsVideo);
                // Add the offset to the IDd to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
                query += SqlLine.AddOffsetToColumnInTable(tempDetectionsVideoTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
                
                query += SqlLine.InsertTable2DataIntoTable1(DBTables.DetectionsVideo, tempDetectionsVideoTable);
            }

            query += $"{Sql.DropTableIfExists} {tempDetectionsTable} {Sql.Semicolon} {Environment.NewLine}";
            if (sourceDetectionsVideoTableExists)
            {
                query += SqlLine.DropTableIfExists(tempDetectionsVideoTable);
            }
            return query;
        }

        private static string SqlQueryPhraseMergeDetectionTable(long offsetId, long offsetDetectionId, SQLiteWrapper destinationDdb,
                 string tempDetectionsTable)
        {
            // Should only be invoked if detections exist
            string query = string.Empty;


            // Add the offset to the IDd to make sure IDs don't conflict with existing IDs. (A zero offset is a noop).
            query += SqlLine.AddOffsetToColumnInTable(tempDetectionsTable, DatabaseColumn.ID, offsetId) + Environment.NewLine;

            if (offsetDetectionId > 0)
            {
                query += SqlLine.AddOffsetToColumnInTable(tempDetectionsTable, DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += SqlLine.InsertTable2DataIntoTable1(DBTables.Detections, tempDetectionsTable);

            return query;
        }

        #endregion
    }
}
