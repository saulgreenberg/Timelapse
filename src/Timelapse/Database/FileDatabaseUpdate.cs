using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Database
{
    // Filedatabase : Update methods
    // - Collects FileDatabase methods that updates the database and/or corresponding data structures
    public partial class FileDatabase
    {
        #region Update Files
        /// <summary>
        /// Update the datalabel column in an ImageRow (identified by its fileID) and Database to the provided value 
        /// </summary>
        public void UpdateFile(long fileID, string dataLabel, string value)
        {
            // Get the ImageRow identified by the fileID nad set it to the new value
            ImageRow image = this.FileTable.Find(fileID);
            image.SetValueFromDatabaseString(dataLabel, value);

            this.CreateBackupIfNeeded();

            // update the row in the database
            ColumnTuplesWithWhere columnToUpdate = new();
            columnToUpdate.Columns.Add(new(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(fileID);

            this.Database.Update(DBTables.FileData, columnToUpdate);
        }

        /// <summary>
        /// Set one property on all rows in the selected view to a given value
        /// </summary>
        public async Task UpdateFiles(ImageRow valueSource, DataEntryControl control)
        {
            // We update the the files using the database format for dates (Time is the same across all of them)
            if (control is DataEntryDateTimeCustom dateTimeCustom)
            {
                if (dateTimeCustom.ContentControl.Value != null)
                {
                    string dateTimeAsDatabaseString = DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimeCustom.ContentControl.Value);
                    await UpdateFiles(dateTimeAsDatabaseString, control.DataLabel, 0, CountAllCurrentlySelectedFiles - 1);
                }
            }
            else if (control is DataEntryDate date)
            {
                if (date.ContentControl.Value != null)
                {
                    string dateAsDatabaseString = DateTimeHandler.ToStringDatabaseDate((DateTime)date.ContentControl.Value);
                    await UpdateFiles(dateAsDatabaseString, control.DataLabel, 0, CountAllCurrentlySelectedFiles - 1);
                }
            }
            // No need to do DataEntryTime as the database and display format are the same.
            else
            {
                await UpdateFiles(valueSource, control.DataLabel);
            }
        }

        public async Task UpdateFiles(ImageRow valueSource, DataEntryControl control, int from, int to)
        {
            // We update the the files using the database format for dates (Time is the same across all of them)
            if (control is DataEntryDateTimeCustom dateTimeCustom)
            {
                if (dateTimeCustom.ContentControl.Value != null)
                {
                    string dateTimeAsDatabaseString = DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimeCustom.ContentControl.Value);
                    await UpdateFiles(dateTimeAsDatabaseString, control.DataLabel, from, to);
                }
            }
            else if (control is DataEntryDate date)
            {
                if (date.ContentControl.Value != null)
                {
                    string dateAsDatabaseString = DateTimeHandler.ToStringDatabaseDate((DateTime)date.ContentControl.Value);
                    await UpdateFiles(dateAsDatabaseString, control.DataLabel, from, to);
                }
            }
            else if (control is DataEntryTime time)
            {
                if (time.ContentControl.Value != null)
                {
                    string timeString = DateTimeHandler.ToStringTime((DateTime)time.ContentControl.Value);
                    await UpdateFiles(timeString, control.DataLabel, from, to);
                }
            }
            else
            {
                await UpdateFiles(valueSource, control.DataLabel, from, to);
            }
        }

        public async Task UpdateFiles(ImageRow valueSource, string dataLabel)
        {
            await UpdateFiles(valueSource, dataLabel, 0, CountAllCurrentlySelectedFiles - 1);
        }



        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateFiles(List<ColumnTuplesWithWhere> filesToUpdate)
        {
            CreateBackupIfNeeded();
            Database.Update(DBTables.FileData, filesToUpdate);
        }

        public void UpdateFiles(ColumnTuplesWithWhere filesToUpdate)
        {
            List<ColumnTuplesWithWhere> imagesToUpdateList = [filesToUpdate];
            Database.Update(DBTables.FileData, imagesToUpdateList);
        }

        public void UpdateFiles(ColumnTuple columnToUpdate)
        {
            Database.Update(DBTables.FileData, columnToUpdate);
        }

        // Given a range of selected files, update the field identified by dataLabel with the value from valueSource
        public async Task UpdateFiles(ImageRow valueSource, string dataLabel, int fromIndex, int toIndex)
        {
            ThrowIf.IsNullArgument(valueSource, nameof(valueSource));
            await UpdateFiles(valueSource.GetValueDatabaseString(dataLabel), dataLabel, fromIndex, toIndex);
        }

        // Given a range of selected files, update the field identified by dataLabel with the given value
        public async Task UpdateFiles(string value, string dataLabel, int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            if (toIndex < fromIndex || toIndex > CountAllCurrentlySelectedFiles - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }
            int count = toIndex - fromIndex + 1;
            await UpdateFilesCore(Enumerable.Range(fromIndex, count), count, dataLabel, value);
        }

        // Given a list of selected files, update the field identified by dataLabel with the given value
        public async Task UpdateFiles(List<int> fileIndexes, string dataLabel, string value)
        {
            ThrowIf.IsNullArgument(fileIndexes, nameof(fileIndexes));
            if (fileIndexes.Count == 0)
            {
                return;
            }
            await UpdateFilesCore(fileIndexes, fileIndexes.Count, dataLabel, value);
        }

        // The minimum file count required before the BusyCancelIndicator progress is shown
        private const int ProgressIndicatorThreshold = 10000;

        // Updates both the in-memory FileTable rows and the database for each index in fileIndexes.
        // For large updates (above ProgressIndicatorThreshold) the BusyCancelIndicator is raised and
        // the loop runs on a background thread so the UI and progress bar remain responsive.
        // Performance note: the per-row cost is dominated by DataRow's internal record-versioning
        // machinery (proposed-record allocation + copy on every SetValueFromDatabaseString call);
        // this cannot be eliminated through the public DataRow API.
        private async Task UpdateFilesCore(IEnumerable<int> fileIndexes, int count, string dataLabel, string value)
        {
            // Show the BusyCancelIndicator with a determinate progress bar for large updates
            bool showProgress = count >= ProgressIndicatorThreshold;
            BusyCancelIndicator bci = showProgress ? GlobalReferences.BusyCancelIndicator : null;
            if (bci != null)
            {
                bci.Reset(true);
                bci.UseStaticProgressBar = false;
                bci.IsIndeterminate = false;
                bci.CancelButtonIsEnabled = false;
                bci.CancelButtonText = "Please wait...";
                bci.Percent = 0;
                bci.Message = $"Updating {dataLabel} for {count:N0} files...";
            }

            // Progress<T> captures the calling thread's SynchronizationContext, so reports are
            // automatically marshalled back to the UI thread to update the indicator safely.
            IProgress<ProgressBarArguments> progress = bci != null
                ? new Progress<ProgressBarArguments>(args =>
                {
                    bci.Percent = args.PercentDone;
                    bci.Message = args.Message;
                })
                : null;

            // Run the in-memory update loop on a background thread so the UI and progress bar
            // remain responsive. The FileTable is not modified concurrently during this operation.
            List<long> listOfIDs = await Task.Run(() =>
            {
                List<long> ids = new(count);
                int reportEvery = 25000; //Math.Max(1, count / 100); // report approximately every 1%
                int i = 0;
                foreach (int index in fileIndexes)
                {
                    if (FileTable[index] is not { } image)
                    {
                        TracePrint.StackTrace($"in FileDatabase.UpdateFiles: FileTable returned null as there is no index: {index}");
                        i++;
                        continue;
                    }
                    image.SetValueFromDatabaseString(dataLabel, value);
                    ids.Add(image.ID);
                    i++;
                    if (progress != null && i % reportEvery == 0)
                    {
                        int percent = (int)(100.0 * i / count);
                        progress.Report(new(percent, $"Updating {dataLabel} for {i:N0}/{count:N0} files...", false, false));
                    }
                }
                return ids;
            });

            // Update bci directly — we're on the UI thread here, so direct assignment renders
            // immediately once the await below yields back to the dispatcher.
            if (bci != null)
            {
                bci.Message = $"Updating the database for {count:N0} files...";
            }

            // Wrapping Database.Update in Task.Run serves two purposes:
            // 1. The await yields to the dispatcher, allowing the message above to render.
            // 2. The database update itself no longer blocks the UI thread.
            await Task.Run(() =>
            {
                CreateBackupIfNeeded();
                Database.Update(DBTables.FileData, Constant.DatabaseColumn.ID, listOfIDs, dataLabel, value);
            });

            if (bci != null)
            {
                bci.Reset(false);
            }
        }
        #endregion

        #region Update Sync to database 
        public void UpdateSyncImageSetToDatabase()
        {
            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Timelapse exits.
            Database.Update(DBTables.ImageSet, ImageSet.CreateColumnTuplesWithWhereByID());
        }

        public void UpdateSyncMarkerToDatabase(MarkerRow marker)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(marker, nameof(marker));

            CreateBackupIfNeeded();
            Database.Update(DBTables.Markers, marker.CreateColumnTuplesWithWhereByID());
        }
        #endregion

        #region Update Markers
        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        // ReSharper disable once UnusedMember.Global
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            CreateBackupIfNeeded();
            Database.Update(DBTables.Markers, markersToUpdate);

            // Refresh the markers data table
            RefreshMarkers();
        }
        #endregion

        #region Update File Dates and Times
        // Update all selected files with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment)
        {
            UpdateAdjustedFileTimes(adjustment, 0, CountAllCurrentlySelectedFiles - 1);
        }

        // Update all selected files between the start and end row with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(adjustment), "The current format of the time column does not support milliseconds.");
            }
            UpdateAdjustedFileTimes((_, _, _, imageTime) => imageTime + adjustment, startRow, endRow, CancellationToken.None);
        }

        // Given a time difference in ticks, update all the date/time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever selection is being used..
        public void UpdateAdjustedFileTimes(Func<string, int, int, DateTime, DateTime> adjustment, int startRow, int endRow, CancellationToken token)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(adjustment, nameof(adjustment));

            if (IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // We now have an unselected temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> filesToAdjust = [];
            int count = endRow - startRow + 1;
            int fileIndex = 0;
            for (int row = startRow; row <= endRow; ++row)
            {
                if (token.IsCancellationRequested)
                {
                    // A cancel was requested. Clear all pending changes and abort
                    return;
                }
                ImageRow image = FileTable[row];
                DateTime currentImageDateTime = image.DateTime;

                // adjust the date/time
                fileIndex++;
                DateTime newImageDateTime = adjustment.Invoke(image.File, fileIndex, count, currentImageDateTime);
                TimeSpan mostRecentAdjustment = newImageDateTime - currentImageDateTime;
                if (mostRecentAdjustment.Duration() < TimeSpan.FromSeconds(1))
                {
                    // Ignore changes if it results in less than a 1 second change, 
                    continue;
                }
                image.SetDateTime(newImageDateTime);
                filesToAdjust.Add(image);
            }

            if (token.IsCancellationRequested)
            {
                // Don't update the database, as a cancellation was requested.
                return;
            }

            // update the database with the new date/time values
            List<ColumnTuplesWithWhere> imagesToUpdate = [];
            foreach (ImageRow image in filesToAdjust)
            {
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }

            if (imagesToUpdate.Count > 0)
            {
                CreateBackupIfNeeded();
                Database.Update(DBTables.FileData, imagesToUpdate);
            }
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void UpdateExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = [];
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = FileTable[row];
                DateTime originalDateTime = image.DateTime;

                if (DateTimeHandler.TrySwapDayMonth(originalDateTime, out DateTime reversedDateTime) == false)
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                image.SetDateTime(reversedDateTime);
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }

            if (imagesToUpdate.Count > 0)
            {
                CreateBackupIfNeeded();
                Database.Update(DBTables.FileData, imagesToUpdate);
            }
        }
        #endregion

        #region Update RelativePaths (used mostly by RelativePathEditor)
        // This method will rename all relative paths matching a prefix. The Query is different depending upon
        // whether its an interior node (i.e., a prefix matching any relative paths with a following path) or a leaf (i.e., no subfolders are under it) 
        // Form:Update DataTable
        // Interior nodes:
        //      Update DataTable SET RelativePath =
        //      'newPrefixPath' || Substr(RelativePath, Length(oldPrefixPath) + 1) where Instr (RelativePath, oldPrefixPath\) == 1
        // Leaf nodes:
        //      Update DataTable SET RelativePath = 'newPrefixPath' where RelativePath = 'oldPrefixPath'
        public void UpdateRelativePathByReplacingPrefix(string oldPrefixPath, string newPrefixPath, bool isInteriorNode)
        {
            string query;
            this.ResetAfterPossibleRelativePathChanges();
            if (isInteriorNode)
            {
                query = Sql.Update + DBTables.FileData
                                   + Sql.Set + DatabaseColumn.RelativePath + Sql.Equal
                                   + Sql.Quote(newPrefixPath) + Sql.Concatenate
                                   + Sql.Substr
                                   + Sql.OpenParenthesis
                                   + DatabaseColumn.RelativePath + Sql.Comma
                                   + Sql.Length + Sql.OpenParenthesis + Sql.Quote(oldPrefixPath) +
                                   Sql.CloseParenthesis + Sql.Plus + "1"
                                   + Sql.CloseParenthesis
                                   + Sql.Where
                                   + Sql.Instr
                                   + Sql.OpenParenthesis
                                   + DatabaseColumn.RelativePath + Sql.Comma
                                   + Sql.Quote(oldPrefixPath + '\\')
                                   + Sql.CloseParenthesis
                                   + Sql.BooleanEquals + "1";
            }
            else
            {
                query = Sql.Update + DBTables.FileData
                                   + Sql.Set + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(newPrefixPath)
                                   + Sql.Where
                                   + DatabaseColumn.RelativePath + Sql.BooleanEquals + Sql.Quote(oldPrefixPath);
            }
            Database.ExecuteNonQuery(query);
        }
        #endregion
    }
}
