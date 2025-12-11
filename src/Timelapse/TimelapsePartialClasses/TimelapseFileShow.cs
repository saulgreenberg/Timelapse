using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Control = Timelapse.Constant.Control;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Showing Files
    public partial class TimelapseWindow
    {
        #region FileShow - invoking versions
        // FileShow is invoked here from a 1-based slider, so we need to correct it to the 0-base index
        // By default, don't force the update
        private void FileShow(Slider fileNavigatorSlider)
        {
            FileShow((int)fileNavigatorSlider.Value - 1, true, false);
        }

        // FileShow is invoked from elsewhere than from the slider. 
        // By default, don't force the update
        public void FileShow(int fileIndex)
        {
            FileShow(fileIndex, false, false);
        }

        // FileShow is invoked from elsewhere than from the slider. 
        // The argument specifies whether we should force the update
        public void FileShow(int fileIndex, bool forceUpdate)
        {
            FileShow(fileIndex, false, forceUpdate);
        }
        private async Task FileShowAsync(int fileIndex, bool forceUpdate)
        {
            await FileShowAsync(fileIndex, false, forceUpdate);
        }
        #endregion

        #region FileShow - Full Sync and Async versions
        // Show the image / video file for the specified row, but only if its different from what is currently being displayed.
        private void FileShow(int fileIndex, bool isInSliderNavigation, bool forceUpdate)
        {
            if (false == FileShowHelperPart1(fileIndex, forceUpdate, out bool newFileToDisplay, out ImageRow imageCacheCurrent))
            {
                // We clear IsNewSelections after files are shown
                GlobalReferences.TimelapseState.IsNewSelection = false;
                return;
            }
            // Sync: Get the bounding boxes and markers (if any) for the current image;
            BoundingBoxes bboxes = GetBoundingBoxesForCurrentFile(imageCacheCurrent.ID);
            FileShowHelperPart2(imageCacheCurrent, newFileToDisplay, bboxes, fileIndex, isInSliderNavigation);
        }

        private async Task FileShowAsync(int fileIndex, bool isInSliderNavigation, bool forceUpdate)
        {
            if (false == FileShowHelperPart1(fileIndex, forceUpdate, out bool newFileToDisplay, out ImageRow imageCacheCurrent))
            {
                // We clear IsNewSelections after files are shown
                GlobalReferences.TimelapseState.IsNewSelection = false;
                return;
            }
            // Async Get the bounding boxes and markers (if any) for the current image;
            BoundingBoxes bboxes = await GetBoundingBoxesForCurrentFileAsync(imageCacheCurrent.ID);

            FileShowHelperPart2(imageCacheCurrent, newFileToDisplay, bboxes, fileIndex, isInSliderNavigation);
        }

        // Show the image / video file for the specified row, but only if its different from what is currently being displayed.
        #endregion

        #region FileShowHelper parts
        // These parts essentially allow the same code to be invoked from both FileShow and FileShowAsync
        private bool FileShowHelperPart1(int fileIndex, bool forceUpdate, out bool newFileToDisplay, out ImageRow imageCacheCurrent)
        {
            imageCacheCurrent = null;
            newFileToDisplay = false;
            // If there is no image set open, or if there is no image to show, then show an image indicating the empty image set.
            bool isFileDatabaseAvailable = IsFileDatabaseAvailable();
            if (isFileDatabaseAvailable == false || DataHandler.FileDatabase.CountAllCurrentlySelectedFiles < 1)
            {
                MarkableCanvas.SetNewImage(
                    isFileDatabaseAvailable ? ImageValues.NoFilesAvailable.Value : ImageValues.LoadAnImageSet.Value,
                    null);
                markersOnCurrentFile = null;
                MarkableCanvas_UpdateMarkers();
                MarkableCanvas.SwitchToImageView();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                if (DataHandler != null)
                {
                    DataHandler.IsProgrammaticControlUpdate = false;
                }

                // We also need to do a bit of cleanup of UI elements that make no sense when there are no images to show.
                QuickPasteWindowHide();
                return false;
            }

            // If we are already showing the desired file, and if we are not forcing an update, 
            // then abort as there is no need to redisplay the image.
            if (DataHandler.ImageCache.CurrentRow == fileIndex && forceUpdate == false)
            {
                return false;
            }

            // Add autocompletions of the current file (not the new file) if needed to all autocomplete controls
            DataEntryControls.AutocompletionUpdateWithCurrentRowValues();
            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            if (DataHandler.ImageCache.TryMoveToFile(fileIndex, forceUpdate, out bool newFileToDisplayTemp) == false)
            {
                if (DataHandler != null)
                {
                    DataHandler.IsProgrammaticControlUpdate = false;
                }
                // We used to throw a new exception, but lets see what happens if we just return instead.
                // i.e., lets just abort.
                // throw new Exception(String.Format("in FileShow: possible problem with fileIndex value is {0}, where its not a valid row index in the image table.", fileIndex));
                Debug.Print($"in FileShow: possible problem with fileIndex (value is {fileIndex}, where its not a valid row index in the image table.");
                return false;
            }

            newFileToDisplay = newFileToDisplayTemp;

            // Get the current image in the image cache. If we can't, abort.
            imageCacheCurrent = DataHandler.ImageCache.Current;

            if (imageCacheCurrent == null)
            {
                // This should not happen.
                TracePrint.NullException(nameof(imageCacheCurrent));
                return false;
            }

            // Reset the ThumbnailGrid to the current image
            MarkableCanvas.ThumbnailGrid.RootPathToImages = RootPathToImages;
            MarkableCanvas.ThumbnailGrid.FileTableStartIndex = fileIndex;
            MarkableCanvas.ThumbnailGrid.FileTable = DataHandler.FileDatabase.FileTable;

            // Update each control with the data for the now current image
            // This is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            DataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in DataEntryControls.ControlsByDataLabelThatAreVisible)
            {
                // update value
                string controlType = DataHandler.FileDatabase.FileTableColumnsByDataLabel[control.Key].ControlType;
                control.Value.SetContentAndTooltip(imageCacheCurrent.GetValueDisplayString(control.Value.DataLabel));

                // for note controls, update the autocomplete list if an edit occurred
                if (controlType == Control.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control.Value;
                    if (noteControl.ContentChanged)
                    {
                        noteControl.ContentChanged = false;
                    }
                }
                else if (controlType == Control.AlphaNumeric)
                {
                    DataEntryAlphaNumeric alphaNumericControl = (DataEntryAlphaNumeric)control.Value;
                    if (alphaNumericControl.ContentChanged)
                    {
                        alphaNumericControl.ContentChanged = false;
                    }
                }
                else if (controlType == DatabaseColumn.RelativePath)
                {
                    // Inform the MetadataUI about the current relative path
                    MetadataUI.RelativePathToCurrentImage = control.Value.Content;
                }
            }

            DataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current selection
            // the total is always refreshed as it's not known if FileShow() is being called due to a change in the selection
            StatusBar.SetCurrentFile(fileIndex + 1); // Add one because indexes are 0-based
            StatusBar.SetCount(DataHandler.FileDatabase.CountAllCurrentlySelectedFiles);
            StatusBar.ClearMessage();

            FileNavigatorSlider.Value = fileIndex + 1;
            return true;
        }
        private void FileShowHelperPart2(ImageRow imageCacheCurrent, bool newFileToDisplay, BoundingBoxes bboxes, int fileIndex, bool isInSliderNavigation)
        {
            markersOnCurrentFile = DataHandler.FileDatabase.MarkersGetMarkersForCurrentFile(imageCacheCurrent.ID);
            List<Marker> displayMarkers = GetDisplayMarkers();

            // Display new file if the file changed
            // This avoids unnecessary image reloads and refreshes in cases where FileShow() is just being called to refresh controls
            if (newFileToDisplay)
            {
                if (imageCacheCurrent.IsVideo)
                {
                    if (false == MarkableCanvas.SetNewVideo(imageCacheCurrent.GetFileInfo(DataHandler.FileDatabase.RootPathToImages), displayMarkers, fileIndex))
                    {
                        // the video image is missing. We need to regenerate the bounding boxes for the 'best' detection so it appears over the missing file.
                        bboxes = GetBoundingBoxesForCurrentFile(imageCacheCurrent.ID, true);
                        MarkableCanvas.BoundingBoxes = bboxes;
                    }
                    EnableImageManipulationMenus(false);
                }
                else
                {
                    MarkableCanvas.SetNewImage(DataHandler.ImageCache.GetCurrentImage, displayMarkers);
                    // Draw markers for this file
                    MarkableCanvas_UpdateMarkers();
                    MarkableCanvas.BoundingBoxes = bboxes;
                    EnableImageManipulationMenus(true);
                }
            }
            else if (IsDisplayingSingleImage())
            {
                if (imageCacheCurrent.IsVideo)
                {
                    MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    MarkableCanvas.SwitchToImageView();
                    MarkableCanvas_UpdateMarkers();
                }
            }

            DataGridSelectionsTimer_Reset();

            // Set the file player status
            FilePlayer.BackwardsControlsEnabled(DataHandler.ImageCache.CurrentRow != 0);

            FilePlayer.ForwardsControlsEnabled(DataHandler.ImageCache.CurrentRow != DataHandler.FileDatabase.CountAllCurrentlySelectedFiles - 1);

            // Refresh the Magnifier if needed
            if (IsDisplayingSingleImage())
            {
                if (imageCacheCurrent.IsVideo)
                {
                    MarkableCanvas.SetMagnifiersAccordingToCurrentState(false, true);
                }
                else
                {
                    MarkableCanvas.SetMagnifiersAccordingToCurrentState(true, false);
                }
            }

            // Refresh the CopyPreviousButton and its Previews as needed
            CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Refresh the QuickPasteEntry previews if needed
            if (IsDisplayingSingleImage() && quickPasteWindow != null)
            {
                quickPasteWindow.RefreshQuickPasteWindowPreviewAsNeeded();
            }

            // Refresh the markable canvas if needed
            MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(isInSliderNavigation);

            // Display the episode and duplicate text as needed
            DisplayEpisodeTextInImageIfWarranted(fileIndex);
            DuplicateDisplayIndicatorInImageIfWarranted();
            GlobalReferences.TimelapseState.IsNewSelection = false;
        }
        #endregion

        #region TryFileShow Without Slider Callback - various forms
        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool TryFileShowWithoutSliderCallback(DirectionEnum direction)
        {
            // Check to see if there are any images to show, 
            if (DataHandler.FileDatabase.CountAllCurrentlySelectedFiles <= 0)
            {
                return false;
            }
            // determine how far to move and in which direction
            int increment = 1;
            return TryFileShowWithoutSliderCallback(direction, increment);
        }

        private bool TryFileShowWithoutSliderCallback(DirectionEnum direction, int increment)
        {
            int desiredRow = 0;
            // Check to see if there are any images to show, 
            if (DataHandler.FileDatabase.CountAllCurrentlySelectedFiles <= 0)
            {
                return false;
            }

            switch (direction)
            {
                case DirectionEnum.Next:
                    desiredRow = DataHandler.ImageCache.CurrentRow + increment;
                    break;
                case DirectionEnum.Previous:
                    desiredRow = DataHandler.ImageCache.CurrentRow - increment;
                    break;
                case DirectionEnum.None:
                    desiredRow = DataHandler.ImageCache.CurrentRow;
                    break;
            }

            // Set the desiredRow to either the maximum or minimum row if it exceeds the bounds,
            if (desiredRow >= DataHandler.FileDatabase.CountAllCurrentlySelectedFiles)
            {
                desiredRow = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles - 1;
            }
            else if (desiredRow < 0)
            {
                desiredRow = 0;
            }

            // If the desired row is the same as the current row, the image is already being displayed
            if (desiredRow != DataHandler.ImageCache.CurrentRow || direction == DirectionEnum.None)
            {
                // Move to the desired row, forcing an update if there is no change in direction
                FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                FileShow(desiredRow, direction == DirectionEnum.None);
                FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
            return true;
        }
        #endregion
    }
}
