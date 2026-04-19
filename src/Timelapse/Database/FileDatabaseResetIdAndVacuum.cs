using System.Collections.Generic;
using System.Threading.Tasks;
using Timelapse.Constant;

namespace Timelapse.Database
{
    // These static methods reset the IDs and detectionIDs in the database to be sequential
    // starting at 1, while maintaining referential integrity for foreign keys, then vacuum
    // the database to reclaim space and optimize performance.
    //
    // Needed after:
    //   - a merge operation, which can produce very large non-sequential ID values
    //   - mass file deletions, which leave gaps in the ID sequence
    //
    // ResetIDsAndVacuum is normally invoked when:
    //   - a large ID value is detected when loading the database
    //   - a merge check-in completes, to keep master IDs compact and sequential
    public partial class FileDatabase
    {
        #region Reset IDs and detectionIDs, then Vacuum

        // Resets DataTable and detection IDs to sequential values starting at 1, then vacuums.
        //
        // All ID remapping runs inside a single transaction so that either every table is
        // updated or nothing is — the database is never left in a partially renumbered state.
        // Rollback on failure is handled by ExecuteNonQueryWithRollback.
        //
        // Three statement lists are passed to a single connection via the pre/post overload:
        //
        //   Pre-transaction  — pragmas that SQLite silently ignores inside a transaction, and
        //                      temp-table cleanup from any previous interrupted run. These must
        //                      run before BeginTransaction on the same connection.
        //
        //   Transactional    — all structural changes: temp tables, index drops, ID remaps,
        //                      index rebuilds. Rolled back atomically on any failure.
        //
        //   Post-transaction — PRAGMA foreign_keys = ON, which SQLite also ignores inside a
        //                      transaction and must be restored after Commit on the same connection.
        //
        // VACUUM is called separately afterward because SQLite forbids it inside any transaction.
        public static void ResetIDsAndVacuum(SQLiteWrapper database)
        {
            // Snapshot table existence once so all three lists use a consistent view.
            bool detectionsExist = database.TableExistsAndNotEmpty(DBTables.Detections);
            bool detectionsVideoExist = database.TableExistsAndNotEmpty(DBTables.DetectionsVideo);
            bool detectionsVideoTableExists = detectionsVideoExist || database.TableExists(DBTables.DetectionsVideo);

            // Single connection, three phases:
            //   pre-transaction  → pragmas off + temp-table cleanup
            //   transactional    → all ID remapping, rolled back atomically on failure
            //   post-transaction → pragmas restored
            SqlOperationResult result = database.ExecuteNonQueryWithRollback(
                GetPreTransactionStatements(),
                GetTransactionalStatements(detectionsExist, detectionsVideoExist, detectionsVideoTableExists),
                GetPostTransactionStatements(detectionsExist));

            if (!result.Success)
            {
                // Surface the failure — the catch block in ExecuteNonQueryWithRollbackCore
                // only calls Debug.Print (invisible outside the debugger). This call shows
                // the full exception and failing SQL statement in the exception dialog.
                SqlOperationResult.GenerateExceptionDialog(result, nameof(ResetIDsAndVacuum));
                return;
            }

            // VACUUM is forbidden inside a transaction and opens its own connection internally.
            database.Vacuum();
        }

        public static Task ResetIDsAndVacuumAsync(SQLiteWrapper database)
        {
            return Task.Run(() => ResetIDsAndVacuum(database));
        }

        // Statements that must run on the same connection as the transaction but BEFORE
        // BeginTransaction. SQLite silently ignores PRAGMA foreign_keys = OFF inside a
        // transaction, so it must be set here — before the transaction begins — to actually
        // disable FK enforcement during the ID remapping UPDATEs.
        private static List<string> GetPreTransactionStatements()
        {
            return
            [
                // Must precede BeginTransaction — ignored by SQLite inside a transaction.
                $"{Sql.PragmaForeignKeysOff}",

                // Cannot be changed inside a transaction.
                $"{Sql.PragmaJournalModeWall}",

                // Performance pragmas — safe to set any time, placed here for clarity.
                $"{Sql.PragmaSynchronousNormal}",
                $"{Sql.PragmaTempStoreMemory}",
                $"{Sql.PragmaCacheSize} = -64000",

                // Remove any temp tables left over from a previous interrupted run so that
                // the CREATE TEMPORARY TABLE statements below start with a clean slate.
                $"{Sql.DropTableIfExists} tmpIDMapping",
                $"{Sql.DropTableIfExists} tmpDetectionIDMapping",
            ];
        }

        // All structural changes executed as a single atomic transaction.
        // ExecuteNonQueryWithRollback owns the BEGIN/COMMIT; do not include them here.
        // If any statement fails the entire batch rolls back — nothing is partially committed.
        private static List<string> GetTransactionalStatements(bool detectionsExist, bool detectionsVideoExist, bool detectionsVideoTableExists)
        {
            // Temp table and index names used only within this transaction.
            const string tmpIDMapping = "tmpIDMapping";
            const string tmpDetectionIDMapping = "tmpDetectionIDMapping";
            const string idx_TempIDMapping = "idx_tmpIDMapping";
            const string idx_TempDetectionIDMapping = "idx_tmpDetectionIDMapping";

            List<string> statements =
            [
                $@"
                {Sql.CreateTemporaryTable} {tmpIDMapping} {Sql.As}
                    {Sql.Select} ID {Sql.As} old_id,
                    {Sql.Cast} ( {Sql.RowNumberOver} ( {Sql.OrderBy} ID) {Sql.As} {Sql.IntegerType} ) {Sql.As} new_id
                    {Sql.From} DataTable",
                // Index on old_id for fast correlated-subquery lookups in the UPDATE statements below.

                $"{Sql.CreateIndex} {idx_TempIDMapping} {Sql.On} {tmpIDMapping}(old_id)",
                // Drop secondary indexes before the heavy UPDATEs.
                // SQLite must update every secondary index entry for every changed row. Dropping them
                // now and bulk-recreating them at the end of this list is far faster than incremental
                // maintenance across a full-table remap.
                // Note: IndexEpisodeField is intentionally omitted — it is created on demand the first
                // time the user selects "include all files in an episode" in the Custom Select dialog.
                $"{Sql.DropIndexIfExists} IndexEpisodeField",
                $"{Sql.DropIndexIfExists} IndexFile",
                $"{Sql.DropIndexIfExists} IndexRelativePath",
                $"{Sql.DropIndexIfExists} IndexRelativePathDateTimeFile",
                $"{Sql.DropIndexIfExists} IndexRelativePathFile",
                $"{Sql.DropIndexIfExists} IndexDetectionID",
                $"{Sql.DropIndexIfExists} IndexDetectionsClassificationConfidence",
                $"{Sql.DropIndexIfExists} IndexDetectionVideoID",
                // Remap DataTable.Id to the new sequential values, preserving relative order.
                //   UPDATE DataTable
                //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = DataTable.Id)
                $@"
                {Sql.Update} {DBTables.FileData}
                    {Sql.Set} {DatabaseColumn.ID} = (
                        {Sql.Select} new_id {Sql.From} {tmpIDMapping}
                        {Sql.Where} old_id = DataTable.ID)",
                // Reset the AUTOINCREMENT counter to the new MAX(Id).
                // The UPDATE above changes existing row Ids but does not update sqlite_sequence,
                // which tracks the highest Id ever inserted. Without this reset, the next INSERT
                // would use the old large value as its starting point, undoing the renumbering.

                $"UPDATE sqlite_sequence SET seq = (SELECT MAX({DatabaseColumn.ID}) FROM {DBTables.FileData}) WHERE name = {Sql.Quote(DBTables.FileData)}",
                // Remove orphaned marker rows whose DataTable counterpart no longer exists.
                // Without this, the correlated UPDATE subquery returns NULL for those rows,
                // and SQLite rejects NULL on an INTEGER PRIMARY KEY column ("datatype mismatch").
                $"DELETE FROM {DBTables.Markers} WHERE {DatabaseColumn.ID} NOT IN (SELECT old_id FROM {tmpIDMapping})",
                // Remap MarkersTable.Id to match the updated DataTable IDs.
                //   UPDATE MarkersTable
                //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = MarkersTable.Id)
                $@"
                {Sql.Update} {DBTables.Markers}
                    {Sql.Set} {DatabaseColumn.ID} = (
                        {Sql.Select} new_id {Sql.From} {tmpIDMapping}
                        {Sql.Where} old_id = MarkersTable.ID)",
                // Reset the AUTOINCREMENT counter for MarkersTable for the same reason as DataTable above.

                $"UPDATE sqlite_sequence SET seq = (SELECT MAX({DatabaseColumn.ID}) FROM {DBTables.Markers}) WHERE name = {Sql.Quote(DBTables.Markers)}"

            ];

            // Build a temp table mapping each existing DataTable ID to a new sequential ID
            // starting at 1, preserving the original row order.
            //   CREATE TEMPORARY TABLE tmpIDMapping AS
            //     SELECT Id AS old_id,
            //            CAST(ROW_NUMBER() OVER(ORDER BY Id) AS INTEGER) AS new_id
            //     FROM DataTable

            // Index on old_id for fast correlated-subquery lookups in the UPDATE statements below.

            // Drop secondary indexes before the heavy UPDATEs.
            // SQLite must update every secondary index entry for every changed row. Dropping them
            // now and bulk-recreating them at the end of this list is far faster than incremental
            // maintenance across a full-table remap.
            // Note: IndexEpisodeField is intentionally omitted — it is created on demand the first
            // time the user selects "include all files in an episode" in the Custom Select dialog.

            // Remap DataTable.Id to the new sequential values, preserving relative order.
            //   UPDATE DataTable
            //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = DataTable.Id)

            // Reset the AUTOINCREMENT counter to the new MAX(Id).
            // The UPDATE above changes existing row Ids but does not update sqlite_sequence,
            // which tracks the highest Id ever inserted. Without this reset, the next INSERT
            // would use the old large value as its starting point, undoing the renumbering.

            // Remove orphaned marker rows whose DataTable counterpart no longer exists.
            // Without this, the correlated UPDATE subquery returns NULL for those rows,
            // and SQLite rejects NULL on an INTEGER PRIMARY KEY column ("datatype mismatch").

            // Remap MarkersTable.Id to match the updated DataTable IDs.
            //   UPDATE MarkersTable
            //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = MarkersTable.Id)

            // Reset the AUTOINCREMENT counter for MarkersTable for the same reason as DataTable above.

            if (detectionsExist)
            {
                // Remap Detections.Id (FK into DataTable) to the updated DataTable IDs.
                //   UPDATE Detections
                //     SET Id = (SELECT new_id FROM tmpIDMapping WHERE old_id = Detections.Id)
                statements.Add($@"
                    {Sql.Update} {DBTables.Detections}
                        {Sql.Set} {DatabaseColumn.ID} = (
                            {Sql.Select} new_id {Sql.From} {tmpIDMapping}
                            {Sql.Where} old_id = Detections.ID)");

                // Build a second mapping table to remap detectionIDs to sequential values.
                // Built after the Detections.Id remap so ROW_NUMBER reflects the final order.
                //   CREATE TEMPORARY TABLE tmpDetectionIDMapping AS
                //     SELECT detectionID AS old_detection_id,
                //            CAST(ROW_NUMBER() OVER(ORDER BY detectionID) AS INTEGER) AS new_detection_id
                //     FROM Detections
                statements.Add($@"
                    {Sql.CreateTemporaryTable} {tmpDetectionIDMapping} {Sql.As}
                        {Sql.Select} {DetectionColumns.DetectionID} {Sql.As} old_detection_id,
                        {Sql.Cast} ( {Sql.RowNumberOver} ( {Sql.OrderBy} {DetectionColumns.DetectionID}) {Sql.As} {Sql.IntegerType} ) {Sql.As} new_detection_id
                        {Sql.From} {DBTables.Detections}");

                // Index on old_detection_id for fast lookups in the UPDATE below.
                statements.Add($"{Sql.CreateIndex} {idx_TempDetectionIDMapping} {Sql.On} {tmpDetectionIDMapping}(old_detection_id)");

                // Remap Detections.detectionID to the new sequential values.
                //   UPDATE Detections
                //     SET detectionID = (SELECT new_detection_id FROM tmpDetectionIDMapping
                //                        WHERE old_detection_id = Detections.detectionID)
                statements.Add($@"
                    {Sql.Update} {DBTables.Detections}
                        {Sql.Set} {DetectionColumns.DetectionID} = (
                            {Sql.Select} new_detection_id {Sql.From} {tmpDetectionIDMapping}
                            {Sql.Where} old_detection_id = Detections.detectionID)");
            }

            if (detectionsVideoExist)
            {
                // Recreate the index before this UPDATE so SQLite does not generate an automatic
                // index (warning 284). This is the last UPDATE touching DetectionsVideo, so there
                // is no incremental-maintenance cost to having the index present here.
                statements.Add($"{Sql.CreateIndex} IndexDetectionVideoID {Sql.On} DetectionsVideo(detectionID)");

                // Remap DetectionsVideo.detectionID to match the updated Detections.detectionID.
                //   UPDATE DetectionsVideo
                //     SET detectionID = (SELECT new_detection_id FROM tmpDetectionIDMapping
                //                        WHERE old_detection_id = DetectionsVideo.detectionID)
                statements.Add($@"
                    UPDATE DetectionsVideo
                        SET detectionID = (
                            SELECT new_detection_id FROM {tmpDetectionIDMapping}
                            WHERE old_detection_id = DetectionsVideo.detectionID)");
            }
            else if (detectionsVideoTableExists)
            {
                // The table exists but is empty — no UPDATE needed, but the index was dropped
                // above and must be restored so subsequent LEFT JOINs on detectionID don't
                // trigger an automatic index (SQLite warning 284).
                statements.Add($"{Sql.CreateIndex} IndexDetectionVideoID {Sql.On} DetectionsVideo(detectionID)");
            }

            // Recreate secondary indexes in bulk — far faster than the incremental maintenance
            // that would have occurred had the indexes been present during the UPDATEs above.
            statements.Add($"{Sql.CreateIndex} IndexFile {Sql.On} DataTable(File)");
            statements.Add($"{Sql.CreateIndex} IndexRelativePath {Sql.On} DataTable(RelativePath)");
            statements.Add($"{Sql.CreateIndex} IndexRelativePathDateTimeFile {Sql.On} DataTable(RelativePath, DateTime, File)");
            statements.Add($"{Sql.CreateIndex} IndexRelativePathFile {Sql.On} DataTable(RelativePath, File)");

            if (detectionsExist)
            {
                statements.Add($"{Sql.CreateIndex} IndexDetectionID {Sql.On} Detections(Id)");
                statements.Add($"{Sql.CreateIndex} IndexDetectionsClassificationConfidence {Sql.On} Detections(classification, conf, classification_conf, Id)");
            }

            return statements;
        }

        // Statements that must run on the same connection as the transaction but AFTER Commit.
        // PRAGMA foreign_keys = ON is silently ignored by SQLite inside a transaction; it must
        // be restored here, after the transaction has been committed, on the same connection.
        // Temp table cleanup is also done here as explicit hygiene (they would be dropped
        // automatically when the connection closes, but dropping them explicitly is cleaner).
        private static List<string> GetPostTransactionStatements(bool detectionsExist)
        {
            List<string> statements =
            [
                $"{Sql.DropTableIfExists} tmpIDMapping",
            ];

            if (detectionsExist)
            {
                statements.Add($"{Sql.DropTableIfExists} tmpDetectionIDMapping");
            }

            // Must follow Commit on the same connection — ignored by SQLite inside a transaction.
            statements.Add($"{Sql.PragmaForeignKeysOn}");

            return statements;
        }

        #endregion
    }
}
