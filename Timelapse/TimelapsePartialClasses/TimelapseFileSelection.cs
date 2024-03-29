﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // File Selection which includes showing the current file
    public partial class TimelapseWindow
    {
        #region Partial Methods - FilesSelectAndShow, various invokingforms
        private async Task FilesSelectAndShowAsync()
        {
            if (this.DataHandler == null || this.DataHandler.FileDatabase == null)
            {
                TracePrint.PrintMessage("FilesSelectAndShow: Expected a file database to be available.");
                return;
            }
            await this.FilesSelectAndShowAsync(this.DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);
        }

        private async Task FilesSelectAndShowAsync(FileSelectionEnum selection)
        {
            long fileID = Constant.DatabaseValues.DefaultFileID;
            if (this.DataHandler != null && this.DataHandler.ImageCache != null && this.DataHandler.ImageCache.Current != null)
            {
                fileID = this.DataHandler.ImageCache.Current.ID;
            }
            await this.FilesSelectAndShowAsync(fileID, selection).ConfigureAwait(true);
        }

        #endregion

        #region FilesSelectAndShow: Full version
        private async Task<bool> FilesSelectAndShowAsync(long imageID, FileSelectionEnum selection)
        {
            // change selection
            // if the data grid is bound the file database automatically updates its contents on SelectFiles()
            if (this.DataHandler?.FileDatabase == null)
            {
                TracePrint.PrintMessage("FilesSelectAndShow() should not be reachable with a null data handler.  Is a menu item wrongly enabled?");
                return false;
            }

            // Select the files according to the given selection, where Missing files is treated as a special case.
            Mouse.OverrideCursor = Cursors.Wait;
            if (selection == FileSelectionEnum.Missing)
            {
                this.BusyCancelIndicator.Reset(true);
                bool result = await FilesSelectAndShowMissingAsync();
                this.BusyCancelIndicator.Reset(false);
                if (false == result )
                {
                    // Either cancelled or no missing files were found. Dialog message for no missing file is handled in the above method
                    return false;
                }
                // As missing files were found, we just fall through as the remaining code will update the UI
            }
            else
            {
                // A  selection other than Missing files.
                // Select Files is a slow operation as it runs a query over all files and returns everything it finds as datatables stored in memory.
                // As we don't know how long it will take, we display an indeterminate progress bar
                this.BusyCancelIndicator.EnableForSelection(true);
                await this.DataHandler.FileDatabase.SelectFilesAsync(selection).ConfigureAwait(true);
                this.BusyCancelIndicator.EnableForSelection(false);
                this.DataHandler.FileDatabase.BindToDataGrid();
            }
            Mouse.OverrideCursor = null;

            if ((this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles < 1) && (selection != FileSelectionEnum.All))
            {
                // A final check that there is actually some files returned in the above selection process.
                // If not, the reset the selection to all files.
                // Tell the user that we are resetting the selection to all files
                Dialogs.FileSelectionResettngSelectionToAllFilesDialog(this, selection);

                this.StatusBar.SetMessage("Resetting selection to All files.");
                selection = FileSelectionEnum.All;

                // PEFORMANCE: The standard select files operation in FilesSelectAndShow
                this.BusyCancelIndicator.EnableForSelection(true);
                await this.DataHandler.FileDatabase.SelectFilesAsync(selection).ConfigureAwait(true);

                this.BusyCancelIndicator.EnableForSelection(false);
                this.DataHandler.FileDatabase.BindToDataGrid();
            }

            // Change the selection to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the selection.
            string status;
            switch (selection)
            {
                case FileSelectionEnum.All:
                    status = "All files";
                    break;
                case FileSelectionEnum.Custom:
                    status = "Custom selection";
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    status = "Files marked for deletion";
                    break;
                case FileSelectionEnum.Folders:
                    status = "Files in a specific folder";
                    break;
                case FileSelectionEnum.Missing:
                    status = "Missing files";
                    break;
                default:
                    throw new NotSupportedException($"Unhandled file selection {selection}.");
            }
            // Show feedback of the status description in both the status bar and the data entry control panel title
            this.StatusBar.SetView(status);
            this.DataEntryControlPanel.Title = "Data entry for " + status;

            // Reset the Episodes, as it may change based on the current selection
            Episodes.Episodes.Reset();

            // Display the specified file or, if it's no longer selected, the next closest one
            // FileShow() handles empty image sets, so those don't need to be checked for here.
            // After a selection changes, set the slider to represent the index and the count of the current selection
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.FileNavigatorSlider.Maximum = this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;  // Reset the slider to the size of images in this set
            if (this.FileNavigatorSlider.Maximum <= 50)
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = true;
                this.FileNavigatorSlider.TickFrequency = 1.0;
            }
            else
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = false;
                this.FileNavigatorSlider.TickFrequency = 0.02 * this.FileNavigatorSlider.Maximum;
            }

            // Reset the ThumbnailGrid selection after every change in the selection
            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.ThumbnailGrid.SelectInitialCellOnly();
            }

            this.DataEntryControls.AutocompletionPopulateAllNotesWithFileTableValues(this.DataHandler.FileDatabase);

            // Always force an update after a selection
            this.FileShow(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(imageID), true);

            // Update the status bar accordingly
            this.StatusBar.SetCurrentFile(this.DataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            this.StatusBar.SetCount(this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.DataHandler.FileDatabase.FileSelectionEnum = selection;    // Remember the current selection
            return true;
        }
        #endregion

        #region Private Helpers
        // Special case selection for missing files
        private async Task<bool> FilesSelectAndShowMissingAsync()
        {
            // Note that the missing files check is slow if there are many files, as it checks every single file in the current database selection to see if it exists
            // To mitigate this, we use a cancellable progress handler that will update the progress bar during this check
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            SelectMissingFilesResultEnum resultEnum = await this.DataHandler.FileDatabase.SelectMissingFilesFromCurrentlySelectedFiles(progressHandler, GlobalReferences.CancelTokenSource);

            if (SelectMissingFilesResultEnum.NoMissingFiles == resultEnum)
            {
                // No files were missing. Inform the user, and don't change anything.
                Mouse.OverrideCursor = null;
                Dialogs.FileSelectionNoFilesAreMissingDialog(this);
                return false;
            }
            if (SelectMissingFilesResultEnum.Cancelled == resultEnum)
            {
                Mouse.OverrideCursor = null;
                return false;
            }
            // must be SelectMissingFilesResultEnum.MissingFilesFound 
            return true;
        }

        #endregion
    }
}
