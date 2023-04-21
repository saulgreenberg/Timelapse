using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
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
            Tuple<bool, TemplateDatabase> tupleResult = await TemplateDatabase.TryCreateOrOpenAsync(templateTdbPath).ConfigureAwait(true);

            if (!tupleResult.Item1)
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                return false;
            }

            // Create the empty database based on the provided template
            TemplateDatabase templateDatabase = tupleResult.Item2;
            FileDatabase fd = await FileDatabase.CreateEmptyDatabase(destinationDdbPath, templateDatabase)
                .ConfigureAwait(true);

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

        // XXXXX
        // This is not a robust way of checking the templates. E.g., does not check for differences in defaults, choice lists, etc.
        // Note that we already have robust template-checking code. Just have to check to see how we can use it.

        public static DatabaseFileErrorsEnum CheckIfDatabaseTemplatesAreMergeCompatable(SQLiteWrapper sourceDdb,
            SQLiteWrapper destinationDdb)
        {
            // Retrieve the soure cand destination dataLabel columns
            List<string> dataLabelsFromDestinationDdb = destinationDdb.SchemaGetColumns(Constant.DBTables.FileData);
            List<string> dataLabelsFromsourceddb = sourceDdb.SchemaGetColumns(Constant.DBTables.FileData);

            ListComparisonEnum listComparisonEnum =
                Compare.CompareLists(dataLabelsFromsourceddb, dataLabelsFromDestinationDdb);

            // We only check to see if the elements differ, regardless of order. 
            // We could, perhaps, make this more robust by creating a union of elements if they don't have name/value conflicts. However, this would introduce other issues 
            return (listComparisonEnum == ListComparisonEnum.ElementsDiffer)
                ? DatabaseFileErrorsEnum.TemplateElementsDiffer
                : DatabaseFileErrorsEnum.Ok;
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
                MergeDatabases.GetDistinctRootsFromRelativePath(Ddb, pathPrefixFromRootToSourceDdb);

            //    If its empty, then the destination ddb does not have any images in the sourceDdb folder
            //    If one is returned, then the destination ddb has one or more images in the sourceDdb folder or its subfolders
            //    If more than one is returned, this is a bug.

            return relativePathsInDdb.Count > 0;
        }
        #endregion

        #region Checkout a relative path database
        public static void CheckoutDatabaseWithRelativePath(FileDatabase sourceDdbFileDatabase, string sourceDdbPath, string destinationDdbPath, string relativePath)
        {

            SQLiteWrapper destinationDdb = new SQLiteWrapper(destinationDdbPath);
            SQLiteWrapper sourceDdb = sourceDdbFileDatabase.Database;

            // a. We need several temporary tables 
            string attachedSourceDB = "attachedSourceDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";

            // Part 1. Initiate the query phrase with a transaction
            string query = Sql.BeginTransactionSemiColon + Environment.NewLine;

            // Part 2. Create the DataTable in the destination, where it contains only those entries that match the relative path folder and subfolder
            query += QueryCheckoutMergeDataTable(query, destinationDdb, sourceDdbPath, attachedSourceDB, tempDataTable, relativePath) + Environment.NewLine;

            // Part 3. Create the Markers table, where where it contains only those entries that match the entries in the datatable
            query += QueryCheckoutMarkersTable(attachedSourceDB, tempMarkersTable, relativePath) + Environment.NewLine;

            // Part 4. Handle the various Recognition Tables portion
            if (sourceDdb.TableExists(Constant.DBTables.Detections))
            {
                query += Sql.PragmaForeignKeysOff + Sql.Semicolon + Environment.NewLine;
                RecognitionDatabases.PrepareRecognitionTablesAndColumns(destinationDdb, false);
                query += QueryCheckoutRecognitionTable(attachedSourceDB, relativePath) + Environment.NewLine;
                query += Sql.PragmaForeignKeysOn + Sql.Semicolon + Environment.NewLine;
            }
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
        private static string QueryCheckoutMergeDataTable(string query, SQLiteWrapper destinationDdb, string sourceDdbPath, string attachedSourceDB, string tempDataTable, string relativePath)
        {
            string queryPhrase = string.Empty;
            List<string> currentDataLabels = destinationDdb.SchemaGetColumns(Constant.DBTables.FileData);
            queryPhrase += QueryAttachDatabaseAs(sourceDdbPath, attachedSourceDB) + Environment.NewLine;
            queryPhrase += QueryCheckoutCreateTemporaryTableFromRelativePathInExistingTable(tempDataTable, attachedSourceDB, Constant.DBTables.FileData, relativePath) + Environment.NewLine;
            queryPhrase += QueryCheckoutTrimRelativePath(tempDataTable, relativePath) + Environment.NewLine;
            queryPhrase += QueryCheckoutInsertTable2DataIntoTable1(tempDataTable, Constant.DBTables.FileData, currentDataLabels, relativePath) + Environment.NewLine;
            return queryPhrase;
        }

        // Form (see above):
        // CREATE TEMPORARY TABLE tempDataTable AS SELECT * FROM dataBaseName.tableName
        //    WHERE RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>\%' ; 
        private static string QueryCheckoutCreateTemporaryTableFromRelativePathInExistingTable(string tempDataTable, string dataBaseName, string tableName, string relativePath)
        {
            return Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + dataBaseName + Sql.Dot +
                   tableName + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath)
                   + Sql.Or
                   + Constant.DatabaseColumn.RelativePath + Sql.Like + Sql.Quote(relativePath + "\\%")
                   + Sql.Semicolon;
        }

        // Insert columns from one table where rows must match only the relative paths
        // Form (see above): 
        // INSERT INTO table2 SELECT <comma separated data labels> FROM table1
        private static string QueryCheckoutInsertTable2DataIntoTable1(string table1, string table2, List<string> listDataLabels, string relativePath)
        {
            //// Turn the list into a comma-separated string
            //string dataLabels = String.Join(",", listDataLabels);
            //dataLabels = dataLabels.TrimEnd(',');

            return Sql.InsertInto + table2 + Sql.Select + String.Join(",", listDataLabels) + Sql.From + table1 + Sql.Semicolon;
        }
        // Form:
        // Update DataTable Set RelativePath = '' where RelativePath = '<relativePath';
        private static string QueryCheckoutTrimRelativePath(string table, string relativePath)
        {
            string queryPhrase = string.Empty;
            queryPhrase += Sql.Update + table + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + "''" 
                + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath) + Sql.Semicolon;

            queryPhrase += Sql.Update + table + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal 
                + Sql.Substr + Sql.OpenParenthesis + Constant.DatabaseColumn.RelativePath + Sql.Comma
                + Sql.Length + Sql.OpenParenthesis + Sql.Quote(relativePath + "\\") + Sql.CloseParenthesis + Sql.Plus + "1" + Sql.CloseParenthesis
                + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Like + Sql.Quote(relativePath + "\\%")
                + Sql.Semicolon;
            return queryPhrase;
        }

        // Form: CREATE TEMPORARY TABLE tempMarkersTable AS  Select MarkersTable.* from AttachedSourceDb.MarkersTable  JOIN DataTable on MarkersTable.Id=DataTable.Id
        //       And (RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>)\%' ;
        private static string QueryCheckoutMarkersTable(string attachedSourceDB, string tempMarkersTable, string relativePath)
        {
            string attachedMarkersTable = attachedSourceDB + Sql.Dot + Constant.DBTables.Markers;
            string attachedDataTable = attachedSourceDB + Sql.Dot + Constant.DBTables.FileData;
            string queryPhrase = string.Empty;
            queryPhrase += Sql.CreateTemporaryTable + tempMarkersTable + Sql.As
                + Sql.Select + Constant.DBTables.Markers + Sql.DotStar + Sql.From + attachedMarkersTable
                + Sql.Join + attachedDataTable + Sql.On 
                + attachedMarkersTable + Sql.Dot + Constant.DatabaseColumn.ID + Sql.Equal + attachedDataTable + Sql.Dot + Constant.DatabaseColumn.ID
                + Sql.And + Sql.OpenParenthesis
                + attachedDataTable + Sql.Dot + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath)
                + Sql.Or
                + attachedDataTable + Sql.Dot + Constant.DatabaseColumn.RelativePath + Sql.Like + Sql.Quote(relativePath + "\\%") 
                + Sql.CloseParenthesis + Sql.Semicolon;
            queryPhrase += QueryInsertTable2DataIntoTable1(Constant.DBTables.Markers, tempMarkersTable) + Environment.NewLine;
            return queryPhrase;
        }

        // Form: CREATE TEMPORARY TABLE tempMarkersTable AS  Select MarkersTable.* from AttachedSourceDb.MarkersTable  JOIN DataTable on MarkersTable.Id=DataTable.Id
        //       And (RelativePath = '<relativePath>' OR RelativePath LIKE '<relativePath>)\%' ;
        private static string QueryCheckoutRecognitionTable(string attachedSourceDB, string relativePath)
        {
            string attachedDataTable = attachedSourceDB + Sql.Dot + Constant.DBTables.FileData;

            // Copy the Detections table matching the ids and relative paths
            string tmpDetections = "tmpDetections"; 
            string attachedDetections = attachedSourceDB + Sql.Dot + Constant.DBTables.Detections;
            string queryPhrase = string.Empty;
            queryPhrase += Sql.CreateTemporaryTable + tmpDetections + Sql.As
                           + Sql.Select + Constant.DBTables.Detections + Sql.DotStar + Sql.From + attachedDetections
                           + Sql.Join + attachedDataTable + Sql.On
                           + attachedDetections + Sql.Dot + Constant.DatabaseColumn.ID + Sql.Equal + attachedDataTable + Sql.Dot + Constant.DatabaseColumn.ID
                           + Sql.And + Sql.OpenParenthesis
                           + attachedDataTable + Sql.Dot + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath)
                           + Sql.Or
                           + attachedDataTable + Sql.Dot + Constant.DatabaseColumn.RelativePath + Sql.Like + Sql.Quote(relativePath + "\\%")
                           + Sql.CloseParenthesis + Sql.Semicolon + Environment.NewLine;
            queryPhrase += QueryInsertTable2DataIntoTable1(Constant.DBTables.Detections, tmpDetections) + Environment.NewLine;

            // Copy the Classifications table matching the detection ids of the just copied detection table
            string tmpClassifications = "tmpClassifications";
            string attachedClassifications = attachedSourceDB + Sql.Dot + Constant.DBTables.Classifications;

            //queryPhrase += Sql.CreateTemporaryTable + tmpClassifications + Sql.As
            //               + Sql.Select + Constant.DBTables.Classifications + Sql.DotStar + Sql.From + attachedClassifications
            //               + Sql.Where
            //               + attachedClassifications + Sql.Dot + Constant.ClassificationColumns.DetectionID
            //               + Sql.Equal + Constant.DBTables.Detections + Sql.Dot + Constant.DetectionColumns.DetectionID
            //               + Sql.Semicolon + Environment.NewLine;

            queryPhrase += Sql.CreateTemporaryTable + tmpClassifications + Sql.As
                           + Sql.Select + Constant.DBTables.Classifications + Sql.DotStar + Sql.From + attachedClassifications
                           + Sql.Join + tmpDetections + Sql.On
                           + attachedClassifications + Sql.Dot + Constant.ClassificationColumns.DetectionID
                           + Sql.Equal + tmpDetections + Sql.Dot + Constant.DetectionColumns.DetectionID
                           + Sql.Semicolon + Environment.NewLine;

            queryPhrase += QueryInsertTable2DataIntoTable1(Constant.DBTables.Classifications, tmpClassifications) + Environment.NewLine;

            // Copy the Detection Categories table
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpDetectionCategories", Constant.DBTables.DetectionCategories, attachedSourceDB);

            // Copy the Classification Categories table 
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpClassificationCategories", Constant.DBTables.ClassificationCategories, attachedSourceDB);

            // Copy the Info table 
            queryPhrase += QueryCheckoutCopyCompleteTable("tmpInfo", Constant.DBTables.Info, attachedSourceDB);
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
        #endregion

        #region Remove: Remove all entries in the database whose RelativePath matches the given folder or its subfolders

        // XXX NEED TO FIX:
        // XXX DOES NOT REMOVE MARKERS
        // XXX CHECK IF RECOGNITIONS ARE REMOVED WITH EACH ENTRY
        // Before calling this, these checks should have been done :
        // - the database is a valid SQL database
        // - the templates match
        public static DatabaseFileErrorsEnum RemoveEntriesFromDestinationDdbMatchingPath(
        SQLiteWrapper destinationDdb,
        string sourceDdbPath,
        string relativePathDifference)
        {
            // Delete entries from the DataTable whose RelativePath begins with the (folder) path from the root folder.
            // This returns the Ids of all deleted entries
            string where = Constant.DatabaseColumn.RelativePath
                           + Sql.Like
                           + Sql.Quote(relativePathDifference + @"\" + "%") // like root/*
                           + Sql.Or
                           + Constant.DatabaseColumn.RelativePath + Sql.Equal
                           + Sql.Quote(relativePathDifference); // or equals root 
            DataTable dtDeletedIds = destinationDdb.DeleteRowsReturningIds(Constant.DBTables.FileData, where, Constant.DatabaseColumn.ID);

            // Use the Ids of the deleted entries to remove corresponding entries (if they exist) from the Markers table
            if (dtDeletedIds.Rows.Count > 0)
            {
                List<string> idClauses = new List<string>();
                foreach (DataRow row in dtDeletedIds.Rows)
                {
                    idClauses.Add(Constant.DatabaseColumn.ID + " = " + row[0]);
                }
                // XXX WHAT IF THERE ARE A HUGE NUMBER? WRAP IN BEGIN END?
                destinationDdb.Delete(Constant.DBTables.Markers, idClauses);
                Debug.Print($"Ddb deleted {idClauses.Count} entries in the folder {relativePathDifference}");
            }
            else
            {
                Debug.Print($"Ddb does not reference the folder: {relativePathDifference}. Nothing deleted");
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
            string relativePathDifference)
        {

            // a. We need several temporary tables 
            string attachedSourceDB = "attachedSourceDB";
            string tempDataTable = "tempDataTable";
            string tempMarkersTable = "tempMarkersTable";

            // Part 1. Initiate the query phrase with a transaction
            string query = Sql.BeginTransactionSemiColon + Environment.NewLine;

            // Part 2. Calculate an ID offset (the current max Id), where we will be adding that to all Ids for the entries inserted into the destinationDdb 
            //    This will guarantee that there are no duplicate primary keys.
            //    Note: if there are no entries in the database, this function returns 0 
            long offsetId = destinationDdb.ScalarGetMaxIntValue(Constant.DBTables.FileData, Constant.DatabaseColumn.ID);

            // Part 3. Handle the DataTable portion
            query = QueryPhraseMergeDataTable(query, offsetId, destinationDdb, sourceDdbPath, attachedSourceDB, tempDataTable, relativePathDifference);

            // Part 4. Handle the Markers Table portion
            query = QueryPhraseMergeMarkersTable(query, offsetId, destinationDdb, attachedSourceDB, tempMarkersTable);

            // Part 5. Handle the various Recognition Tables portion
            bool destinationRecognitionsExist = destinationDdb.TableExists(Constant.DBTables.Detections);
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
                query = QueryPhraseMergeRecognitionTables(query, offsetId, destinationDdb, attachedSourceDB, destinationRecognitionsExist);
            }

            // Part 7. The query is now done, so end the transaction and execute it
            query += Sql.EndTransactionSemiColon;
            destinationDdb.ExecuteNonQuery(query);

            // Part 8. Check if there are any Detections. If not, delete all the recognition tables as they are no longer relevant
            if (destinationDdb.TableExistsAndEmpty(Constant.DBTables.Detections))
            {
                destinationDdb.DropTable(Constant.DBTables.Detections);
                destinationDdb.DropTable(Constant.DBTables.Classifications);
                destinationDdb.DropTable(Constant.DBTables.DetectionCategories);
                destinationDdb.DropTable(Constant.DBTables.ClassificationCategories);
                destinationDdb.DropTable(Constant.DBTables.Info);
            }
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
                ddb.GetDataTableFromSelect(Sql.SelectDistinct + Constant.DatabaseColumn.RelativePath + Sql.From +
                                           Constant.DBTables.FileData
                                           + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal +
                                           Sql.Quote(pathPrefix)
                                           + Sql.Or + Constant.DatabaseColumn.RelativePath + Sql.Like +
                                           Sql.Quote(pathPrefix + "\\%"));

            List<string> relativePathRoots = new List<string>();
            foreach (DataRow row in dtRelativePathsInDdb.Rows)
            {
                string rootFolder = MergeDatabases.GetRootFolder((string)row[0]);
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
            return Sql.CreateTemporaryTable + tempDataTable + Sql.As + Sql.SelectStarFrom + dataBaseName + Sql.Dot +
                   tableName + Sql.Semicolon;
        }

        // Form: UPDATE dataTable SET IDColumnName = (offset + dataTable.Id);
        private static string QueryAddOffsetToIDInTable(string tableName, string IDColumnName, long offset)
        {
            return Sql.Update + tableName + Sql.Set + IDColumnName + Sql.Equal + Sql.OpenParenthesis + offset + Sql.Plus +
                   tableName + Sql.Dot + IDColumnName + Sql.CloseParenthesis + Sql.Semicolon;
        }

        //Form:  UPDATE tableName SET RelativePath = CASE WHEN RelativePath = '' THEN ("PrefixPath" || RelativePath) ELSE ("PrefixPath\\" || RelativePath) EMD
        private static string QueryAddPrefixToRelativePathInTable(string tableName, string pathPrefixToAdd)
        {
            // A longer query, so split into three lines
            // Note that tableName must be a DataTable for this to work
            string query = Sql.Update + tableName + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal +
                           Sql.CaseWhen + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(string.Empty);
            query += Sql.Then + Sql.OpenParenthesis + Sql.Quote(pathPrefixToAdd) + Sql.Concatenate +
                     Constant.DatabaseColumn.RelativePath + Sql.CloseParenthesis;
            query += Sql.Else + Sql.OpenParenthesis + Sql.Quote(pathPrefixToAdd + "\\") + Sql.Concatenate +
                     Constant.DatabaseColumn.RelativePath + Sql.CloseParenthesis + " END " + Sql.Semicolon;
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
            return Sql.Update + Constant.DBTables.Info + Sql.Set
                   + Constant.InfoColumns.Detector + Sql.Equal + Sql.Quote(detector) + Sql.Comma
                   + Constant.InfoColumns.DetectorVersion + Sql.Equal + Sql.Quote(megadetector_version) + Sql.Comma
                   + Constant.InfoColumns.DetectionCompletionTime + Sql.Equal + Sql.Quote(detection_completion_time) +
                   Sql.Comma
                   + Constant.InfoColumns.Classifier + Sql.Equal + Sql.Quote(classifier) + Sql.Comma
                   + Constant.InfoColumns.ClassificationCompletionTime + Sql.Equal +
                   Sql.Quote(classification_completion_time) + Sql.Comma
                   + Constant.InfoColumns.TypicalDetectionThreshold + Sql.Equal +
                   (Math.Round(typical_detection_threshold * 100) / 100) + Sql.Comma
                   + Constant.InfoColumns.ConservativeDetectionThreshold + Sql.Equal +
                   (Math.Round(conservative_detection_threshold * 100) / 100) + Sql.Comma
                   + Constant.InfoColumns.TypicalClassificationThreshold + Sql.Equal +
                   (Math.Round(typical_classification_threshold * 100) / 100)
                   + Sql.Semicolon;
        }

        private static string UpdateImageSetTableWithUndefinedBoundingBox()
        {
            return Sql.Update + Constant.DBTables.ImageSet + Sql.Set
                   + Constant.DatabaseColumn.BoundingBoxDisplayThreshold + Sql.Equal +
                   Constant.RecognizerValues.BoundingBoxDisplayThresholdDefault + Sql.Semicolon;
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
        private static string QueryPhraseMergeDataTable(string query, long offsetId, SQLiteWrapper destinationDdb, string sourceDdbPath, string attachedSourceDB, string tempDataTable, string relativePathDifference)
        {
            List<string> currentDataLabels = destinationDdb.SchemaGetColumns(Constant.DBTables.FileData);
            query += QueryAttachDatabaseAs(sourceDdbPath, attachedSourceDB) + Environment.NewLine;
            query += QueryCreateTemporaryTableFromExistingTable(tempDataTable, attachedSourceDB, Constant.DBTables.FileData) + Environment.NewLine;
            if (offsetId > 0)
            {
                // Add the offset to the IDs to make sure IDs don't conflict with existing IDs
                query += QueryAddOffsetToIDInTable(tempDataTable, Constant.DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            query += QueryAddPrefixToRelativePathInTable(tempDataTable, relativePathDifference) + Environment.NewLine;
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.FileData, tempDataTable, currentDataLabels) + Environment.NewLine;
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
        private static string QueryPhraseMergeMarkersTable(string query, long offsetId, SQLiteWrapper destinationDdb,
            string attachedSourceDB, string tempMarkersTable)
        {
            // Get the columns in order
            // Form: select name from pragma_table_info('MarkersTable')  as tblInfo 
            string queryGetColumnName = Sql.SelectNameFromPragmaTableInfo + Sql.OpenParenthesis +
                                        Sql.Quote(Constant.DBTables.Markers) + Sql.CloseParenthesis + Sql.As +
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
                     attachedSourceDB + Sql.Dot + Constant.DBTables.Markers + Sql.Semicolon + Environment.NewLine;
            if (offsetId > 0)
            {
                // Add the offset to the IDd to make sure IDs don't conflict with existing IDs
                query += QueryAddOffsetToIDInTable(tempMarkersTable, Constant.DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Markers, tempMarkersTable) + Environment.NewLine;
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
            bool sourceDetectionsExists = sourceDdb.TableExists(Constant.DBTables.Detections);

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
                query += QueryCreateTemporaryTableFromExistingTable(tempInfoTable, attachedSourceDB, Constant.DBTables.Info) + Environment.NewLine;
                query += QueryCreateTemporaryTableFromExistingTable(tempDetectionCategoriesTable, attachedSourceDB,
                    Constant.DBTables.DetectionCategories) + Environment.NewLine;
                query += QueryCreateTemporaryTableFromExistingTable(tempClassificationCategoriesTable, attachedSourceDB,
                             Constant.DBTables.ClassificationCategories) + Environment.NewLine;

                // Now we insert those tables into the current database
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.DetectionCategories, attachedSourceDB) + Environment.NewLine;
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.ClassificationCategories, attachedSourceDB) + Environment.NewLine;
                query += QueryInsertTableDataFromAnotherDatabase(Constant.DBTables.Info, attachedSourceDB) + Environment.NewLine;

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
                (string)mergedInfoDictionary[Constant.InfoColumns.Detector],
                (string)mergedInfoDictionary[Constant.InfoColumns.DetectorVersion],
                (string)mergedInfoDictionary[Constant.InfoColumns.DetectionCompletionTime],
                (string)mergedInfoDictionary[Constant.InfoColumns.Classifier],
                (string)mergedInfoDictionary[Constant.InfoColumns.ClassificationCompletionTime],
                Convert.ToDouble(mergedInfoDictionary[Constant.InfoColumns.TypicalDetectionThreshold]),
                Convert.ToDouble(mergedInfoDictionary[Constant.InfoColumns.ConservativeDetectionThreshold]),
                Convert.ToDouble(mergedInfoDictionary[Constant.InfoColumns.TypicalClassificationThreshold]));
            query += UpdateImageSetTableWithUndefinedBoundingBox();

            // Update the various dictionaries to reflect their current values
            // DictionaryReplaceSecondDictWithFirstDictElements(mergedInfoDictionary, infoDictionary);

            // B: Generate a new detection category db if the categories in the current and to be merged dictionary can be merged together. 
            if (sourceDetectionCategories.Count > 0 || detectionCategories.Count > 0)
            {
                // There is something to add
                if (Util.Dictionaries.MergeDictionaries(sourceDetectionCategories, destinationDetectionCategories,
                        out Dictionary<string, string> mergedDetectionCategories))
                {
                    // Clear the DetectionCategories table as we will be completely replacing it
                    query += Sql.DeleteFrom + Constant.DBTables.DetectionCategories + Sql.Semicolon + Environment.NewLine;

                    // update the classification categories in the table.
                    query += Sql.InsertInto + Constant.DBTables.DetectionCategories
                                            + Sql.OpenParenthesis + Constant.DetectionCategoriesColumns.Category +
                                            Sql.Comma + Constant.DetectionCategoriesColumns.Label +
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
                if (Util.Dictionaries.MergeDictionaries(sourceClassificationCategories,
                        classificationCategories,
                        out Dictionary<string, string> mergedClassificationCategories))
                {
                    // Clear the ClassificationCategories table as we will be completely replacing it
                    query += Sql.DeleteFrom + Constant.DBTables.ClassificationCategories + Sql.Semicolon + Environment.NewLine;

                    // update the classification categories in the table.
                    query += Sql.InsertInto + Constant.DBTables.ClassificationCategories
                                            + Sql.OpenParenthesis + Constant.ClassificationCategoriesColumns.Category +
                                            Sql.Comma + Constant.ClassificationCategoriesColumns.Label +
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
        private static string QueryPhraseMergeRecognitionTables(string query, long offsetId, SQLiteWrapper destinationDdb,
            string attachedSourceDB, bool destinationRecognitionsExist)
        {
            string tempDetectionsTable = "tempDetectionsTable";
            string tempClassificationsTable = "tempClassificationsTable";

            // Calculate an offset (the max DetectionIDs), where we will be adding that to all detectionIds in the ddbFile to merge. 
            // The offeset should be 0 if there are no detections in the main DB, as we will be creating the detection table and then just adding to it.
            int offsetDetectionId = destinationRecognitionsExist
                ? destinationDdb.ScalarGetMaxIntValue(Constant.DBTables.Detections, Constant.DetectionColumns.DetectionID)
                : 0;

            query += QueryCreateTemporaryTableFromExistingTable(tempDetectionsTable, attachedSourceDB, Constant.DBTables.Detections) + Environment.NewLine;
            if (offsetId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DatabaseColumn.ID, offsetId) + Environment.NewLine;
            }
            if (offsetDetectionId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempDetectionsTable, Constant.DetectionColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Detections, tempDetectionsTable) + Environment.NewLine;

            // Similar to the above, we also update the classifications
            // XXXX: IS THIS NEEDED IF THERE ARE NOT RECOGNITIONS IN THE TABLE???
            int offsetClassificationId = (destinationRecognitionsExist)
                ? destinationDdb.ScalarGetMaxIntValue(Constant.DBTables.Classifications, Constant.ClassificationColumns.ClassificationID)
                : 0;
            query += QueryCreateTemporaryTableFromExistingTable(tempClassificationsTable, attachedSourceDB, Constant.DBTables.Classifications) + Environment.NewLine;
            if (offsetClassificationId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.ClassificationID, offsetClassificationId) + Environment.NewLine;
            }

            if (offsetDetectionId > 0)
            {
                query += QueryAddOffsetToIDInTable(tempClassificationsTable, Constant.ClassificationColumns.DetectionID, offsetDetectionId) + Environment.NewLine;
            }
            query += QueryInsertTable2DataIntoTable1(Constant.DBTables.Classifications, tempClassificationsTable) + Environment.NewLine;
            return query;
        }
    }
    #endregion
}
