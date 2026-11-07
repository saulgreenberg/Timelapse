using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region Duplicate the records selected in the overview
        // Ask for confirmation only if more than this number of records would be duplicated
        private const int DuplicateInOverviewConfirmAboveCount = 5;

        // True while records are being duplicated in the overview, when positions in the file table are changing and input is ignored
        private bool IsDuplicatingInOverview;

        // The result is the same as duplicating each selected record in turn, except that the view stays where it is and nothing is left selected.
        // Duplicates shift the file table positions used by the selection and everything else in the overview. To keep those from being used:
        // - the selection is cleared, so that edits have no target, and the overview and keyboard ignore input until done
        // - all other positions are then updated, or rebuilt, from what the positions become.
        private async Task DuplicateSelectedRecordsInOverviewAsync(bool useCurrentValues)
        {
            ThumbnailGridVirtualized grid = MarkableCanvas.ThumbnailGridVirtualized;
            FilePlayer_Stop();

            // An edit just typed is posted to the dispatcher, and must reach the selected records before the selection is cleared
            await Dispatcher.Yield(DispatcherPriority.Background);

            List<int> selected = [.. grid.GetSelected().Order()];
            if (selected.Count == 0)
            {
                return;
            }
            if (selected.Count > DuplicateInOverviewConfirmAboveCount
                && true != Dialogs.MenuEditDuplicateSelectedRecordsConfirmDialog(this, selected.Count, useCurrentValues))
            {
                return;
            }

            IsDuplicating = true;
            IsDuplicatingInOverview = true;
            Mouse.OverrideCursor = Cursors.Wait;
            grid.IsEnabled = false;
            grid.CancelUpdate();
            grid.ClearSelection();
            BusyCancelIndicator.ResetAndEnableImmediately();
            BusyCancelIndicator.CancelButtonIsEnabled = false;
            BusyCancelIndicator.CancelButtonText = "Please wait...";
            BusyCancelIndicator.Message = $"Duplicating {selected.Count:N0} record(s)...";
            try
            {
                (DuplicateFilesOutcome outcome, string errorMessage) = await DataHandler.FileDatabase.DuplicateFilesAsync(selected, useCurrentValues);
                if (outcome == DuplicateFilesOutcome.Duplicated && false == TryUpdatePositionsAfterDuplicatesInserted(selected))
                {
                    outcome = DuplicateFilesOutcome.FileTableOutOfStep;
                    errorMessage = "The display could not be updated.";
                }
                if (outcome == DuplicateFilesOutcome.FileTableOutOfStep)
                {
                    // The database is correct, so get the file table from it
                    await FilesSelectAndShowAsync();
                }
                if (outcome != DuplicateFilesOutcome.Duplicated)
                {
                    BusyCancelIndicator.Reset(false);
                    Dialogs.MenuEditDuplicateSelectedRecordsProblemDialog(this, outcome == DuplicateFilesOutcome.DatabaseUnchanged, errorMessage);
                }
            }
            finally
            {
                BusyCancelIndicator.Reset(false);
                grid.IsEnabled = true;
                Mouse.OverrideCursor = null;
                IsDuplicatingInOverview = false;
                IsDuplicating = false;
            }
        }

        // Update whatever holds a file table position to where that file now is. Returns false if that couldn't be done.
        private bool TryUpdatePositionsAfterDuplicatesInserted(List<int> originalPositions)
        {
            int NewPosition(int position)
            {
                // Each original before this position has inserted one row before it
                int index = originalPositions.BinarySearch(position);
                return position + (index >= 0 ? index : ~index);
            }

            int currentRow = NewPosition(DataHandler.ImageCache.CurrentRow);
            if (false == DataHandler.ImageCache.TryMoveToFile(currentRow))
            {
                return false;
            }
            MarkableCanvas.ThumbnailGridVirtualized.RefreshAfterFileTableInsertions(NewPosition);
            ImageDogear?.UpdatePositions(NewPosition);

            FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            FileNavigatorSlider.Maximum = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;
            FileNavigatorSlider.Value = currentRow + 1;
            FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            StatusBar.SetCurrentFile(currentRow + 1);
            StatusBar.SetCount(DataHandler.FileDatabase.CountAllCurrentlySelectedFiles);
            return true;
        }
        #endregion
    }
}
