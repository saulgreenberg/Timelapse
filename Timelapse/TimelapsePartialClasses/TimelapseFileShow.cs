using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Showing Files
    public partial class TimelapseWindow
    {
        #region File Show - invoking versions
        // FileShow is invoked here from a 1-based slider, so we need to correct it to the 0-base index
        // By default, don't force the update
        private void FileShow(Slider fileNavigatorSlider)
        {
            this.FileShow((int)fileNavigatorSlider.Value - 1, true, false);
        }

        // FileShow is invoked from elsewhere than from the slider. 
        // By default, don't force the update
        private void FileShow(int fileIndex)
        {
            this.FileShow(fileIndex, false, false);
        }

        // FileShow is invoked from elsewhere than from the slider. 
        // The argument specifies whether we should force the update
        private void FileShow(int fileIndex, bool forceUpdate)
        {
            this.FileShow(fileIndex, false, forceUpdate);
        }
        #endregion

        #region FileShow - Full version
        // Show the image / video file for the specified row, but only if its different from what is currently being displayed.
        private void FileShow(int fileIndex, bool isInSliderNavigation, bool forceUpdate)
        {
            // If there is no image set open, or if there is no image to show, then show an image indicating the empty image set.
            bool isFileDatabaseAvailable = this.IsFileDatabaseAvailable();
            if (isFileDatabaseAvailable == false || this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles < 1)
            {
                this.MarkableCanvas.SetNewImage(
                    isFileDatabaseAvailable ? Constant.ImageValues.NoFilesAvailable.Value : Constant.ImageValues.LoadAnImageSet.Value,
                    null);
                this.markersOnCurrentFile = null;
                this.MarkableCanvas_UpdateMarkers();
                this.MarkableCanvas.SwitchToImageView();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                if (this.DataHandler != null)
                {
                    this.DataHandler.IsProgrammaticControlUpdate = false;
                }

                // We also need to do a bit of cleanup of UI elements that make no sense when there are no images to show.
                this.QuickPasteWindowHide();
                return;
            }

            // If we are already showing the desired file, and if we are not forcing an update, 
            // then abort as there is no need to redisplay the image.
            if (this.DataHandler.ImageCache.CurrentRow == fileIndex && forceUpdate == false)
            {
                return;
            }

            this.DataEntryControls.AutocompletionUpdateWithCurrentRowValues();
            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            if (this.DataHandler.ImageCache.TryMoveToFile(fileIndex, forceUpdate, out bool newFileToDisplay) == false)
            {
                if (this.DataHandler != null)
                {
                    this.DataHandler.IsProgrammaticControlUpdate = false;
                }
                // We used to throw a new exception, but lets see what happens if we just return instead.
                // i.e., lets just abort.
                // throw new Exception(String.Format("in FileShow: possible problem with fileIndex value is {0}, where its not a valid row index in the image table.", fileIndex));
                Debug.Print(
                    $"in FileShow: possible problem with fileIndex (value is {fileIndex}, where its not a valid row index in the image table.");
                return;
            }

            // Get the current image in the image cache. If we can't, abort.
            ImageRow imageCacheCurrent = this.DataHandler.ImageCache.Current;

            if (imageCacheCurrent == null)
            {
                // This should not happen.
                TracePrint.NullException(nameof(imageCacheCurrent));
                return;
            }

            // Reset the ThumbnailGrid to the current image
            this.MarkableCanvas.ThumbnailGrid.FolderPath = this.FolderPath;
            this.MarkableCanvas.ThumbnailGrid.FileTableStartIndex = fileIndex;
            this.MarkableCanvas.ThumbnailGrid.FileTable = this.DataHandler.FileDatabase.FileTable;

            // Update each control with the data for the now current image
            // This is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            this.DataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.DataEntryControls.ControlsByDataLabel)
            {
                // update value
                string controlType = this.DataHandler.FileDatabase.FileTableColumnsByDataLabel[control.Key].ControlType;
                control.Value.SetContentAndTooltip(imageCacheCurrent.GetValueDisplayString(control.Value.DataLabel));

                // for note controls, update the autocomplete list if an edit occurred
                if (controlType == Constant.Control.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control.Value;
                    if (noteControl.ContentChanged)
                    {
                        noteControl.ContentChanged = false;
                    }
                }
                else if (controlType == Constant.Control.AlphaNumeric)
                {
                    DataEntryAlphaNumeric alphaNumericControl = (DataEntryAlphaNumeric)control.Value;
                    if (alphaNumericControl.ContentChanged)
                    {
                        alphaNumericControl.ContentChanged = false;
                    }
                }
                else if (controlType == Constant.DatabaseColumn.RelativePath)
                {
                    // Inform the MetadataUI about the current relative path
                    this.MetadataUI.RelativePathToCurrentImage = control.Value.Content;
                }
            }


            this.DataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current selection
            // the total is always refreshed as it's not known if FileShow() is being called due to a change in the selection
            this.StatusBar.SetCurrentFile(fileIndex + 1); // Add one because indexes are 0-based
            this.StatusBar.SetCount(this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles);
            this.StatusBar.ClearMessage();

            this.FileNavigatorSlider.Value = fileIndex + 1;

            // Get the bounding boxes and markers (if any) for the current image;
            BoundingBoxes bboxes = this.GetBoundingBoxesForCurrentFile(imageCacheCurrent.ID);
            this.markersOnCurrentFile = this.DataHandler.FileDatabase.MarkersGetMarkersForCurrentFile(imageCacheCurrent.ID);
            List<Marker> displayMarkers = this.GetDisplayMarkers();

            // Display new file if the file changed
            // This avoids unnecessary image reloads and refreshes in cases where FileShow() is just being called to refresh controls
            if (newFileToDisplay)
            {
                if (imageCacheCurrent.IsVideo)
                {
                    this.MarkableCanvas.SetNewVideo(imageCacheCurrent.GetFileInfo(this.DataHandler.FileDatabase.FolderPath), displayMarkers);
                    this.EnableImageManipulationMenus(false);
                }
                else
                {
                    this.MarkableCanvas.SetNewImage(this.DataHandler.ImageCache.GetCurrentImage, displayMarkers);
                    // Draw markers for this file
                    this.MarkableCanvas_UpdateMarkers();
                    this.MarkableCanvas.BoundingBoxes = bboxes;
                    this.EnableImageManipulationMenus(true);
                }
            }
            else if (this.IsDisplayingSingleImage())
            {
                if (imageCacheCurrent.IsVideo)
                {
                    this.MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    this.MarkableCanvas.SwitchToImageView();
                    this.MarkableCanvas_UpdateMarkers();
                }
            }

            this.DataGridSelectionsTimer_Reset();

            // Set the file player status
            this.FilePlayer.BackwardsControlsEnabled(this.DataHandler.ImageCache.CurrentRow != 0);

            this.FilePlayer.ForwardsControlsEnabled(this.DataHandler.ImageCache.CurrentRow != this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles - 1);

            // Refresh the Magnifier if needed
            if (this.IsDisplayingSingleImage())
            {
                if (imageCacheCurrent.IsVideo)
                {
                    this.MarkableCanvas.SetMagnifiersAccordingToCurrentState(false, true);
                }
                else
                {
                    this.MarkableCanvas.SetMagnifiersAccordingToCurrentState(true, false);
                }
            }

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Refresh the QuickPasteEntry previews if needed
            if (this.IsDisplayingSingleImage() && this.quickPasteWindow != null)
            {
                this.quickPasteWindow.RefreshQuickPasteWindowPreviewAsNeeded();
            }

            // Refresh the markable canvas if needed
            this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(isInSliderNavigation);

            // Display the episode and duplicate text as needed
            this.DisplayEpisodeTextInImageIfWarranted(fileIndex);
            this.DuplicateDisplayIndicatorInImageIfWarranted();
        }
        #endregion

        #region TryFileShow Without Slider Callback - various forms
        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool TryFileShowWithoutSliderCallback(DirectionEnum direction)
        {
            // Check to see if there are any images to show, 
            if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles <= 0)
            {
                return false;
            }
            // determine how far to move and in which direction
            int increment = 1;
            return this.TryFileShowWithoutSliderCallback(direction, increment);
        }

        private bool TryFileShowWithoutSliderCallback(DirectionEnum direction, int increment)
        {
            int desiredRow = 0;
            // Check to see if there are any images to show, 
            if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles <= 0)
            {
                return false;
            }

            switch (direction)
            {
                case DirectionEnum.Next:
                    desiredRow = this.DataHandler.ImageCache.CurrentRow + increment;
                    break;
                case DirectionEnum.Previous:
                    desiredRow = this.DataHandler.ImageCache.CurrentRow - increment;
                    break;
                case DirectionEnum.None:
                    desiredRow = this.DataHandler.ImageCache.CurrentRow;
                    break;
            }

            // Set the desiredRow to either the maximum or minimum row if it exceeds the bounds,
            if (desiredRow >= this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles)
            {
                desiredRow = this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles - 1;
            }
            else if (desiredRow < 0)
            {
                desiredRow = 0;
            }

            // If the desired row is the same as the current row, the image is already being displayed
            if (desiredRow != this.DataHandler.ImageCache.CurrentRow || direction == DirectionEnum.None)
            {
                // Move to the desired row, forcing an update if there is no change in direction
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.FileShow(desiredRow, direction == DirectionEnum.None);
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
            return true;
        }
        #endregion
    }
}
