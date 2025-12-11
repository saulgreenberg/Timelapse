using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Timelapse.Constant;
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
            if (DataHandler == null || DataHandler.FileDatabase == null)
            {
                TracePrint.PrintMessage("FilesSelectAndShow: Expected a file database to be available.");
                return;
            }
            await FilesSelectAndShowAsync(DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);
        }

        private async Task FilesSelectAndShowAsync(FileSelectionEnum selection)
        {
            long fileID = DatabaseValues.DefaultFileID;
            if (DataHandler is { ImageCache.Current: not null })
            {
                fileID = DataHandler.ImageCache.Current.ID;
            }
            await FilesSelectAndShowAsync(fileID, selection).ConfigureAwait(true);
        }

        #endregion

        #region FilesSelectAndShow: Full version
        private async Task<bool> FilesSelectAndShowAsync(long imageID, FileSelectionEnum selection)
        {
            // change selection
            // if the data grid is bound the file database automatically updates its contents on SelectFiles()
            if (DataHandler?.FileDatabase == null)
            {
                TracePrint.PrintMessage("FilesSelectAndShow() should not be reachable with a null data handler.  Is a menu item wrongly enabled?");
                return false;
            }

            // Set a string to indicate the current selection
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

            // Allow random sampling after a new selection
            this.MenuItemSelectRandomSample.IsEnabled = true;

            // Select the files according to the given selection, where Missing files is treated as a special case.
            Mouse.OverrideCursor = Cursors.Wait;
            BusyCancelIndicator.Reset(true);
            BusyCancelIndicator.EnableForSelection(true);
            BusyCancelIndicator.Message = $"Selecting '{status}' from the database. Please wait...";
            if (selection == FileSelectionEnum.Missing)
            {
                //BusyCancelIndicator.Reset(true);
                //BusyCancelIndicator.Message = $"Selecting {selection} from the database. Please wait...";
                bool result = await FilesSelectAndShowMissingAsync();

                // BusyCancelIndicator.Reset(false);
                if (false == result)
                {
                    // Either cancelled or no missing files were found. Dialog message for no missing file is handled in the above method
                    BusyCancelIndicator.Reset(false);
                    Mouse.OverrideCursor = null;
                    return false;
                }
                // As missing files were found, we just fall through as the remaining code will update the UI
            }
            else
            {
                // A  selection other than Missing files.
                // Select Files is a slow operation as it runs a query over all files and returns everything it finds as datatables stored in memory.
                // As we don't know how long it will take, we display an indeterminate progress bar
                //BusyCancelIndicator.EnableForSelection(true);
                //BusyCancelIndicator.Message = "Selecting files from the database. Please wait...";
                await DataHandler.FileDatabase.SelectFilesAsync(selection).ConfigureAwait(true);
                //BusyCancelIndicator.EnableForSelection(false);
                DataHandler.FileDatabase.BindToDataGrid();
            }
            //Mouse.OverrideCursor = null;

            if ((DataHandler.FileDatabase.CountAllCurrentlySelectedFiles < 1) && (selection != FileSelectionEnum.All))
            {
                // A final check that there is actually some files returned in the above selection process.
                // If not, the reset the selection to all files.
                // Tell the user that we are resetting the selection to all files
                Dialogs.FileSelectionResettngSelectionToAllFilesDialog(this, selection);

                StatusBar.SetMessage("Resetting selection to All files.");
                selection = FileSelectionEnum.All;

                // PEFORMANCE: The standard select files operation in FilesSelectAndShow
                //BusyCancelIndicator.EnableForSelection(true);
                BusyCancelIndicator.Message = "Resetting to select All files from the database. Please wait...";
                await DataHandler.FileDatabase.SelectFilesAsync(selection).ConfigureAwait(true);
                //BusyCancelIndicator.EnableForSelection(false);
                DataHandler.FileDatabase.BindToDataGrid();
            }

            // Show feedback of the status description in both the status bar and the data entry control panel title
            StatusBar.SetView(status);
            DataEntryControlPanel.Title = $"Image data ({status} selected)";

            // Reset the Episodes, as it may change based on the current selection
            Episodes.Episodes.Reset();

            // Display the specified file or, if it's no longer selected, the next closest one
            // FileShow() handles empty image sets, so those don't need to be checked for here.
            // After a selection changes, set the slider to represent the index and the count of the current selection
            FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            FileNavigatorSlider.Maximum = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;  // Reset the slider to the size of images in this set
            if (FileNavigatorSlider.Maximum <= 50)
            {
                FileNavigatorSlider.IsSnapToTickEnabled = true;
                FileNavigatorSlider.TickFrequency = 1.0;
            }
            else
            {
                FileNavigatorSlider.IsSnapToTickEnabled = false;
                FileNavigatorSlider.TickFrequency = 0.02 * FileNavigatorSlider.Maximum;
            }

            // Reset the ThumbnailGrid selection after every change in the selection
            if (IsDisplayingMultipleImagesInOverview())
            {
                MarkableCanvas.ThumbnailGrid.SelectInitialCellOnly();
            }

            await DataEntryControls.AutocompletionPopulateAllNotesWithFileTableValuesAsync(DataHandler.FileDatabase);

            // Always force an update after a selection
            //BusyCancelIndicator.EnableForSelection(true);
            BusyCancelIndicator.Message = "Setting up the file to display. Please wait...";
            await FileShowAsync(DataHandler.FileDatabase.GetFileOrNextFileIndex(imageID), true);
            // BusyCancelIndicator.EnableForSelection(false);

            // Update the status bar accordingly
            StatusBar.SetCurrentFile(DataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            StatusBar.SetCount(DataHandler.FileDatabase.CountAllCurrentlySelectedFiles);
            FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            DataHandler.FileDatabase.FileSelectionEnum = selection;    // Remember the current selection
            this.MetadataUI.ResetNavigationButtonsForMetadataTabs();   // Reset the navigation buttons for the MetadataUI (if any) as changed selection may affect what relative paths are present
            BusyCancelIndicator.Reset(false);
            Mouse.OverrideCursor = null;
            return true;
        }
        #endregion

        #region Private Helpers
        // Special case selection for missing files
        private async Task<bool> FilesSelectAndShowMissingAsync()
        {
            // Note that the missing files check is slow if there are many files, as it checks every single file in the current database selection to see if it exists
            // To mitigate this, we use a cancellable progress handler that will update the progress bar during this check
            Progress<ProgressBarArguments> progressHandler = new(value =>
            {
                // Update the progress bar
                FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            //IProgress<ProgressBarArguments> progress = progressHandler;
            SelectMissingFilesResultEnum resultEnum = await DataHandler.FileDatabase.SelectMissingFilesFromCurrentlySelectedFiles(progressHandler, GlobalReferences.CancelTokenSource);

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
