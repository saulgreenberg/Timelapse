using System;
using System.Threading.Tasks;
using Timelapse.Constant;

namespace Timelapse.Database
{

    // These static methods are used to reset the IDs and detectionIDs in the database to be sequential starting at 1, incluidng maintaining referential integrity for foreign keys,
    // They also vacuum the database to reclaim space and optimize performance.
    // This is needed for:
    // - after a merge operation that can create extremely large ID values that potentially go beyond the maximum value of a long.
    // - cleaning up the database after a large number of files have been deleted, which can leave gaps in the ID sequence.
    // ResetIDsAndVacuum is normally invoked when:
    // - a large ID value is detected when loading the database
    // - whenever a merge checkin operation is performed, to avoid
    //   the master having very large non-sequential ID values,
    //   the child having non-sequential Ids starting at a value other than one
    public partial class FileDatabase
    {
        #region Reset IDs and detectionIDs, then Vacuum 
        // Do this while maintaining foreign key dependencies to begin at 1, then vacuum
        public static void ResetIDsAndVacuum(SQLiteWrapper database)
        {
            database.ExecuteNonQuery(ResetIDsGetQuery(database));
        }

        public static Task ResetIDsAndVacuumAsync(SQLiteWrapper database)
        {
            return Task.Run(() => ResetIDsAndVacuum(database));
        }

        // Only used by the above.
        // It generates and returns the SQL command to reset the IDs and detectionIDs, and then vacuums the database.
        private static string ResetIDsGetQuery(SQLiteWrapper database)
        {
            // temporary table names and index names used in the script below. 
            const string tmpIDMapping = "tmpIDMapping"; // {tmpIDMapping}
            const string tmpDetectionIDMapping = "tmpDetectionIDMapping"; // {tmpDetectionIDMapping}
            const string idx_TempIDMapping = "idx_tmpIDMapping"; // {indexTempIDMapping}
            const string idx_TempDetectionIDMapping = "idx_tmpDetectionIDMapping";

            // Detection-related tables only exist if there are recognitions. 
            // So we need to check if they exist before we try to do anything with them.
            bool detectionsExist = database.TableExistsAndNotEmpty(Constant.DBTables.Detections);
            bool detectionsVideoExist = database.TableExistsAndNotEmpty(Constant.DBTables.DetectionsVideo);

            //Set up various pragmas for performance
            // PRAGMA foreign_keys = OFF;
            // PRAGMA journal_mode = WAL;
            // PRAGMA synchronous = NORMAL;
            // PRAGMA temp_store = MEMORY;
            // PRAGMA cache_size = -64000;
            string command = @$"    
                {Sql.PragmaForeignKeysOff};
                {Sql.PragmaJournalModeWall};
                {Sql.PragmaSynchronousNormal};
                {Sql.PragmaTempStoreMemory};
                {Sql.PragmaCacheSize} = -64000; 
            ";

            // Clean up existing temp tables if needed.
            // We will be creating temporary tables to map old IDs to new IDs, so we need to make sure that any old temp tables with the same names are dropped first.
            // DROP TABLE IF EXISTS tmpIDMapping;
            // DROP TABLE IF EXISTS tmpDetectionIDMapping;
            command += @$"{Sql.DropTableIfExists} {tmpIDMapping}; {Environment.NewLine}";
            command += @$"{Sql.DropTableIfExists} {tmpDetectionIDMapping}; {Environment.NewLine}";

            // Begin transaction for atomic operation
            // BEGIN TRANSACTION;
            command += $@"{Sql.BeginTransactionSemiColon}";

            // Create a temporary table mapping old IDs (as found in the DataTable to new IDs) to new IDs starting at 1
            // CREATE TEMPORARY TABLE tmpIDMapping AS
            //    SELECT Id AS old_id,
            //    CAST(ROW_NUMBER() OVER(ORDER BY Id) AS INTEGER) AS new_id
            // FROM DataTable;
            command += @$"
                {Sql.CreateTemporaryTable} {tmpIDMapping} {Sql.As}
                    {Sql.Select} ID {Sql.As} old_id,
                    {Sql.Cast} ( {Sql.RowNumberOver} ( {Sql.OrderBy} ID)  {Sql.As} {Sql.IntegerType} ) {Sql.As} new_id
                    {Sql.From} DataTable;{Environment.NewLine}";

            // Then create an index to the above table for better performance before we do the updates.
            // CREATE INDEX idx_tmpIDMapping ON tmpIDMapping(old_id);
            command += @$"{Sql.CreateIndex} {idx_TempIDMapping} {Sql.On} {tmpIDMapping} (old_id);";

            // Drop secondary indexes BEFORE the heavy UPDATEs
            // We do this as we have several secondary indexes/ When you update DataTable.Id, SQLite must update every
            // secondary index entry for every row, because secondary indexes store the rowid as their row pointer.
            // Dropping and bulk-recreating the indexes (which is done at the end of this script) is much faster than updating them incrementally for every row.
            // DROP INDEX IF EXISTS IndexEpisodeField;
            // DROP INDEX IF EXISTS IndexFile;
            // DROP INDEX IF EXISTS IndexRelativePath;
            // DROP INDEX IF EXISTS IndexRelativePathDateTimeFile;
            // DROP INDEX IF EXISTS IndexRelativePathFile;
            // DROP INDEX IF EXISTS IndexDetectionID;
            // DROP INDEX IF EXISTS IndexDetectionsClassificationConfidence;
            // DROP INDEX IF EXISTS IndexDetectionVideoID;
            command += @$" {Sql.DropIndexIfExists} IndexEpisodeField;
                           {Sql.DropIndexIfExists} IndexFile;
                           {Sql.DropIndexIfExists} IndexRelativePath;
                           {Sql.DropIndexIfExists} IndexRelativePathDateTimeFile;
                           {Sql.DropIndexIfExists} IndexRelativePathFile;
                           {Sql.DropIndexIfExists} IndexDetectionID;
                           {Sql.DropIndexIfExists} IndexDetectionsClassificationConfidence;
                           {Sql.DropIndexIfExists} IndexDetectionVideoID;";

            // Update all tables with the new IDs, starting with the main DataTable, and then the dependent tables, while maintaining referential integrity

            // 1. Remap DataTable IDs to new sequential values starting at 1, while maintaining the same relative order
            // UPDATE DataTable
            //    SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = DataTable.Id);
            command += @$" 
                 {Sql.Update} {DBTables.FileData}
                    {Sql.Set} {DatabaseColumn.ID} = ({Sql.Select} new_id {Sql.From} {tmpIDMapping} {Sql.Where} old_id = DataTable.ID); {Environment.NewLine}";

            //  2. Remap MarkersTable to match new DataTable IDs
            //  UPDATE MarkersTable
            //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = MarkersTable.Id);
            command += @$"
                {Sql.Update} {DBTables.Markers}
                      {Sql.Set} {DatabaseColumn.ID} = ({Sql.Select} new_id {Sql.From} {tmpIDMapping} {Sql.Where} old_id = MarkersTable.ID); {Environment.NewLine}";

            // 3. If Detections table exists, then  Remap Detections.ID (the FK column) to match new DataTable IDs (this maintains FK relationship)
            // UPDATE Detections
            //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = Detections.Id);
            if (detectionsExist)
            {
                command += @$"
                {Sql.Update} {DBTables.Detections}
                    {Sql.Set} {DatabaseColumn.ID} = ({Sql.Select} new_id {Sql.From} {tmpIDMapping} {Sql.Where} old_id = Detections.ID); {Environment.NewLine}";


                // 4. Now build the detectionID remapping with proper casting
                // CREATE TEMPORARY TABLE tmpDetectionIDMapping AS
                //    SELECT detectionID AS old_detection_id,
                //    CAST(ROW_NUMBER() OVER(ORDER BY detectionID) AS INTEGER) AS new_detection_id
                // FROM Detections;
                command += @$"
                 {Sql.CreateTemporaryTable} {tmpDetectionIDMapping} {Sql.As}
                 {Sql.Select} {DetectionColumns.DetectionID} {Sql.As} old_detection_id,
                 {Sql.Cast} ( {Sql.RowNumberOver} ( {Sql.OrderBy} {DetectionColumns.DetectionID}) {Sql.As} {Sql.IntegerType}) {Sql.As} new_detection_id
                 {Sql.From} {DBTables.Detections}; {Environment.NewLine}";

                // Then create an index to the above table for better performance before we do the updates.
                // CREATE INDEX idx_tmpDetectionIDMapping ON tmpDetectionIDMapping(old_detection_id);
                command += @$"   
                    {Sql.CreateIndex} {idx_TempDetectionIDMapping} {Sql.On} {tmpDetectionIDMapping}(old_detection_id);{Environment.NewLine}";

                //  and now Remap detectionID
                //  UPDATE Detections
                //  SET detectionID = (SELECT new_detection_id FROM tmpDetectionIDMapping
                //                     WHERE old_detection_id = Detections.detectionID);
                command += @$"
                    {Sql.Update} {DBTables.Detections}
                    {Sql.Set} {DetectionColumns.DetectionID} = ( {Sql.Select} new_detection_id {Sql.From} {tmpDetectionIDMapping}
                    {Sql.Where} old_detection_id = Detections.detectionID);{Environment.NewLine}";
            }

            if (detectionsVideoExist)
            {
                // 5. Update DetectionsVideo if it exists
                // UPDATE DetectionsVideo
                //     SET detectionID = (SELECT new_detection_id FROM tmpDetectionIDMapping
                //                         WHERE old_detection_id = DetectionsVideo.detectionID);
                command += @$"
                UPDATE DetectionsVideo
                SET detectionID = (SELECT new_detection_id FROM tmpDetectionIDMapping
                WHERE old_detection_id = DetectionsVideo.detectionID);";
            }

            // 5. Recreate secondary indexes as needed
            // (bulk build is much faster than incremental maintenance)
            // Note that we don't recreate the IndexEpisodeField index here, as we can take a lazy evaluation approach
            // Instead, it will be created on the fly only if the user selects 'include all files in an episode' in the Custom Select dialog
            // as that is the only time that index is really needed.

            // CREATE INDEX IndexFile ON DataTable(File);
            // CREATE INDEX IndexRelativePath ON DataTable(RelativePath);
            // CREATE INDEX IndexRelativePathDateTimeFile ON DataTable(RelativePath, DateTime, File);
            // CREATE INDEX IndexRelativePathFile ON DataTable(RelativePath, File);
            command += $@"{Sql.CreateIndex} IndexFile {Sql.On} DataTable(File);
                          {Sql.CreateIndex} IndexRelativePath {Sql.On} DataTable(RelativePath);
                          {Sql.CreateIndex} IndexRelativePathDateTimeFile {Sql.On} DataTable(RelativePath, DateTime, File);
                          {Sql.CreateIndex} IndexRelativePathFile {Sql.On} DataTable(RelativePath, File);";

            if (detectionsExist)
            {
                // CREATE INDEX IndexDetectionID ON Detections(Id);
                // CREATE INDEX IndexDetectionsClassificationConfidence ON Detections(classification, conf,classification_conf, Id);
                command += $@"{Sql.CreateIndex} IndexDetectionID {Sql.On} Detections(Id);
                              {Sql.CreateIndex} IndexDetectionsClassificationConfidence {Sql.On} Detections(classification, conf, classification_conf, Id);";
            }

            if (detectionsVideoExist)
            {
                //  CREATE INDEX IndexDetectionVideoID ON DetectionsVideo(detectionID);
                command += $@"{Sql.CreateIndex} IndexDetectionVideoID {Sql.On} DetectionsVideo(detectionID);";
            }

            // 6. Commit transaction
            // COMMIT;
            command += @$"{Sql.Commit};";

            // 7. Clean up temp tables (note that we don't have to clean up temporary indexes, as they are automatically dropped when the temp tables are dropped
            // DROP TABLE tmpIDMapping;
            command += @$"
                {Sql.DropTableIfExists} {tmpIDMapping}; {Environment.NewLine}";

            if (detectionsExist)
            {
                //  DROP TABLE tmpDetectionIDMapping;
                command += @$"
                {Sql.DropTableIfExists} {tmpDetectionIDMapping};{Environment.NewLine}";
            }

            // 8. Vacuum
            command += @$"{Sql.Vacuum}; {Environment.NewLine}";

            // 9. PRAGMA foreign_keys = ON;
            command += @$"{Sql.PragmaForeignKeysOn};{Environment.NewLine}";
            return command;
        }

        #endregion
    }
}
