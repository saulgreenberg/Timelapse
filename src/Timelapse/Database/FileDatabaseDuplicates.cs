using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;

namespace Timelapse.Database
{
    public enum DuplicateFilesOutcome
    {
        // The database and the file table both include the duplicates
        Duplicated,

        // Nothing was changed
        DatabaseUnchanged,

        // The database includes the duplicates, but the file table may not match it, so the files must be selected again
        FileTableOutOfStep
    }

    // Duplicates many files at once, for the overview. The result is the same as duplicating each file in turn from first to last,
    // as is done by TimelapseWindow.DuplicateCurrentRecord, except that:
    // - all the database changes are made together or not at all,
    // - the detections table is reloaded only once.
    public partial class FileDatabase
    {
        #region Public Methods - DuplicateFilesAsync
        // Insert a duplicate of each of the files at these file table positions, immediately after its original.
        // Every position in the file table after the first duplicate changes, so the caller should not keep any positions across this call.
        // A failure leaves the database unchanged unless the outcome says otherwise. Its explanation is returned in errorMessage.
        public async Task<(DuplicateFilesOutcome Outcome, string ErrorMessage)> DuplicateFilesAsync(IEnumerable<int> fileIndexes, bool useCurrentValues)
        {
            ArgumentNullException.ThrowIfNull(fileIndexes);

            // Bulk edits write to file table positions. Those already waiting go first, as their positions are still valid.
            // Any others have to wait, and the caller must ensure that they can't then be using the old positions.
            await _fileTableWriteSemaphore.WaitAsync();
            try
            {
                List<int> positions = [.. fileIndexes.Distinct().Order()];
                if (positions.Count == 0)
                {
                    return (DuplicateFilesOutcome.Duplicated, string.Empty);
                }
                if (positions[0] < 0 || positions[^1] >= FileTable.RowCount)
                {
                    return (DuplicateFilesOutcome.DatabaseUnchanged, "A selected file is no longer in the list of files.");
                }

                List<ImageRow> originals = [.. positions.Select(position => FileTable[position])];
                bool isCamtrapDPStandard = MetadataTablesIsCamtrapDPStandard();
                List<ImageRow> duplicates = [.. originals.Select(original =>
                    original.DuplicateRowWithValues(FileTable.NewRow(new FileInfo(original.File)), useCurrentValues, isCamtrapDPStandard))];
                List<string> insertCommands = ComposeAddFilesCommands(duplicates, null);
                Dictionary<long, List<DataRow>> detectionsByFileID = await GetDetectionsOfFilesAsync(originals);

                CreateBackupIfNeeded();
                DataTable insertedFiles = null;
                SqlOperationResult result = await Database.RunAtomicallyAsync(() => Task.Run(() =>
                    InsertDuplicatesInAtomicScope(originals, insertCommands, detectionsByFileID, out insertedFiles)));
                if (false == result.Success)
                {
                    return (DuplicateFilesOutcome.DatabaseUnchanged, result.ErrorMessage);
                }

                // The database is now committed
                InvalidateAllFoldersCache();
                try
                {
                    InsertDuplicatesInFileTable(positions, insertedFiles);
                    if (detectionsByFileID != null)
                    {
                        await RefreshDetectionsDataTableAsync();
                        IndexCreateForDetectionsIfNeeded();
                    }
                    Episodes.Episodes.Reset();
                }
                catch (Exception exception)
                {
                    return (DuplicateFilesOutcome.FileTableOutOfStep, exception.Message);
                }
                return (DuplicateFilesOutcome.Duplicated, string.Empty);
            }
            finally
            {
                _fileTableWriteSemaphore.Release();
            }
        }
        #endregion

        #region Private Methods
        // The detection rows of each of these files, in the order returned by GetDetectionsFromFileIDAsync. Null if there aren't any recognitions.
        private async Task<Dictionary<long, List<DataRow>>> GetDetectionsOfFilesAsync(List<ImageRow> files)
        {
            if (false == GlobalReferences.DetectionsExists)
            {
                return null;
            }
            if (detectionDataTable == null)
            {
                await RefreshDetectionsDataTableAsync();
            }

            Dictionary<long, List<DataRow>> detectionsByFileID = [];
            foreach (ImageRow file in files)
            {
                detectionsByFileID.TryAdd(file.ID, []);
            }
            // RefreshDetectionsDataTableAsync always sets detectionDataTable (or throws), so it isn't null here
            // ReSharper disable once PossibleNullReferenceException
            foreach (DataRow detection in detectionDataTable.Rows)
            {
                if (detectionsByFileID.TryGetValue(Convert.ToInt64(detection[DatabaseColumn.ID]), out List<DataRow> detections))
                {
                    detections.Add(detection);
                }
            }
            return detectionsByFileID;
        }

        // This must only be run inside Database.RunAtomicallyAsync: a failure returns its result rather than showing a dialog,
        // which abandons all the changes.
        private SqlOperationResult InsertDuplicatesInAtomicScope(List<ImageRow> originals, List<string> insertCommands, Dictionary<long, List<DataRow>> detectionsByFileID, out DataTable insertedFiles)
        {
            insertedFiles = null;
            long fileCountBefore = Database.ScalarGetScalarFromSelectAsLong($"{Sql.SelectCountStarFrom}{DBTables.FileData}");
            long maxFileIDBefore = Database.ScalarGetMaxValueAsLong(DBTables.FileData, DatabaseColumn.ID);
            SqlOperationResult result = Database.ExecuteNonQueryWithRollback(insertCommands);
            if (false == result.Success)
            {
                return result;
            }

            // The new IDs are autoincremented, so get them from the rows themselves, and confirm that each is a duplicate of its original
            insertedFiles = Database.GetDataTableFromSelect($"{Sql.SelectStarFrom}{DBTables.FileData}{Sql.Where}{DatabaseColumn.ID}>{maxFileIDBefore}{Sql.OrderBy}{DatabaseColumn.ID}");
            if (insertedFiles == null || insertedFiles.Rows.Count != originals.Count)
            {
                return SqlOperationResult.Fail("The database did not add the expected number of records.");
            }
            for (int i = 0; i < originals.Count; i++)
            {
                if ((string)insertedFiles.Rows[i][DatabaseColumn.File] != originals[i].File
                    || (string)insertedFiles.Rows[i][DatabaseColumn.RelativePath] != originals[i].RelativePath)
                {
                    return SqlOperationResult.Fail($"A new record does not match the file it duplicates ({originals[i].File}).");
                }
            }
            if (Database.ScalarGetScalarFromSelectAsLong($"{Sql.SelectCountStarFrom}{DBTables.FileData}") != fileCountBefore + originals.Count)
            {
                return SqlOperationResult.Fail("The number of records in the database is not what was expected.");
            }
            return detectionsByFileID == null
                ? SqlOperationResult.Ok()
                : InsertDuplicateDetections(originals, insertedFiles, detectionsByFileID);
        }

        // Each detection's values are composed in the same way as in TimelapseWindow.DuplicateCurrentRecord.
        // A value it can't convert, such as a video detection without a frame rate, throws an exception there as well, and here abandons all the changes.
        private SqlOperationResult InsertDuplicateDetections(List<ImageRow> originals, DataTable insertedFiles, Dictionary<long, List<DataRow>> detectionsByFileID)
        {
            long detectionCountBefore = Database.ScalarGetScalarFromSelectAsLong($"{Sql.SelectCountStarFrom}{DBTables.Detections}");
            long detectionVideoCountBefore = Database.ScalarGetScalarFromSelectAsLong($"{Sql.SelectCountStarFrom}{DBTables.DetectionsVideo}");
            long detectionID = Database.ScalarGetMaxValueAsLong(DBTables.Detections, DetectionColumns.DetectionID);

            List<List<ColumnTuple>> detectionInsertionStatements = [];
            List<List<ColumnTuple>> detectionVideoInsertionStatements = [];
            for (int i = 0; i < originals.Count; i++)
            {
                long duplicateFileID = Convert.ToInt64(insertedFiles.Rows[i][DatabaseColumn.ID]);
                foreach (DataRow detectionRow in detectionsByFileID[originals[i].ID])
                {
                    detectionID++;
                    List<ColumnTuple> detectionColumns =
                    [
                        new(DetectionColumns.DetectionID, detectionID),
                        new(DetectionColumns.ImageID, duplicateFileID),
                        new(DetectionColumns.Category, (string)detectionRow[DetectionColumns.Category]),
                        new(DetectionColumns.Conf, (float)Convert.ToDouble(detectionRow[DetectionColumns.Conf])),
                        new(DetectionColumns.BBox, (string)detectionRow[DetectionColumns.BBox])
                    ];
                    if (detectionRow[DetectionColumns.Classification] != DBNull.Value)
                    {
                        detectionColumns.Add(new(DetectionColumns.Classification, (string)detectionRow[DetectionColumns.Classification]));
                        detectionColumns.Add(new(DetectionColumns.ClassificationConf, (float)Convert.ToDouble(detectionRow[DetectionColumns.ClassificationConf])));
                    }
                    detectionInsertionStatements.Add(detectionColumns);

                    if (originals[i].IsVideo)
                    {
                        detectionVideoInsertionStatements.Add(
                        [
                            new(DetectionColumns.FrameNumber, Convert.ToInt32(detectionRow[DetectionColumns.FrameNumber])),
                            new(DetectionColumns.FrameRate, (float?)Convert.ToDouble(detectionRow[DetectionColumns.FrameRate])),
                            new(DetectionColumns.DetectionID, detectionID)
                        ]);
                    }
                }
            }

            SqlOperationResult result = Database.Insert(DBTables.Detections, detectionInsertionStatements);
            if (false == result.Success)
            {
                return result;
            }
            result = Database.Insert(DBTables.DetectionsVideo, detectionVideoInsertionStatements);
            if (false == result.Success)
            {
                return result;
            }
            if (Database.ScalarGetScalarFromSelectAsLong($"{Sql.SelectCountStarFrom}{DBTables.Detections}") != detectionCountBefore + detectionInsertionStatements.Count
                || Database.ScalarGetScalarFromSelectAsLong($"{Sql.SelectCountStarFrom}{DBTables.DetectionsVideo}") != detectionVideoCountBefore + detectionVideoInsertionStatements.Count)
            {
                return SqlOperationResult.Fail("The number of recognitions in the database is not what was expected.");
            }
            return SqlOperationResult.Ok();
        }

        // Insert from the last position to the first, so that the positions not yet used are still valid
        private void InsertDuplicatesInFileTable(List<int> positions, DataTable insertedFiles)
        {
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                FileTable.InsertRow(positions[i] + 1, insertedFiles.Rows[i]);
            }

            // Confirm that each duplicate ended up immediately after its original
            for (int i = 0; i < positions.Count; i++)
            {
                if (FileTable[positions[i] + i + 1].ID != Convert.ToInt64(insertedFiles.Rows[i][DatabaseColumn.ID]))
                {
                    throw new InvalidOperationException("A new record is not where it was expected in the list of files.");
                }
            }
        }
        #endregion
    }
}
