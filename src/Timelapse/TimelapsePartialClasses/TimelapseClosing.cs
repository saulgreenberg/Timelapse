using System.Windows;
using Newtonsoft.Json;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Extensions;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    /// <summary>
    /// Persists and (if needed) resets the UI and various states when the image set and/or Timelapse is closing or is shutting down
    /// </summary>
    public partial class TimelapseWindow
    {
        #region Private Methods - CloseImageSet
        /// <summary>
        /// Close the current image set. Resets the UI, states, datahandler,  filedatabase etc. and saves the state
        /// </summary>
        private void CloseImageSet()
        {
            // if we are actually viewing any files, reset the items below
            if (IsFileDatabaseAvailable())
            {

                // persist image set and registry properties if an image set has been opened
                if (DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
                {
                    CloseTimelapseAndSaveState(false);
                }

                // Dispose the image set, templateDatabase and DataHandler as they will be reset if another next image set is opened. 
                DataHandler.ImageCache?.Dispose();
                DataHandler?.Dispose();
                DataHandler = null;
                templateDatabase = null;
                this.ImageDogear = null;

                // Reinitializing the MetadataUI  on an empty data handler will clear all existing metadata tabs, if any.
                MetadataUI.InitalizeFolderMetadataTabs();

            }

            // As we are starting afresh, no detections should now exists.
            // As well, we want to reset the current detection threshold to a value that indicates it is undefined.
            GlobalReferences.DetectionsExists = false;
            if (DataHandler?.FileDatabase?.CustomSelection?.RecognitionSelections != null)
            {
                DataHandler.FileDatabase.CustomSelection.RecognitionSelections.DetectionConfidenceLowerForUI = -1; // this forces it to use the default in the new JSON
            }

            // Clear the data grid
            DataGrid.ItemsSource = null;

            // Clear the arguments, in case we want to start a new session.
            Arguments = new(null);

            // Reset the rest of the user interface 
            MarkableCanvas.ThumbnailGrid?.Reset();
            State.Reset();
            DataEntryControlPanel.IsVisible = false;
            MarkableCanvas.ZoomOutAllTheWay();
            FileNavigatorSliderReset();
            EnableOrDisableMenusAndControls();
            CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
            DataEntryControlPanel.IsVisible = false;
            FilePlayer.Visibility = Visibility.Collapsed;
            InstructionPane.IsSelected = true;
            State.AlreadyWarnedAboutOpenWithOlderVersionOfTimelapse = false;

            DataGridSelectionsTimer.Stop();
            lastControlWithFocus = null;
            QuickPasteWindowTerminate();
            ImageAdjuster?.Hide();
        }
        #endregion

        #region Private Methods - Close Timelapse and Save State
        /// <summary>
        /// When Timelapse closes, save its state. 
        /// Note that this may be invoked when an image set is closed but Timelapse is not shut down (as indicated by the isCompleteShutdonw flag). 
        /// If its not a complete shutdown, we leave a few things out as the UI is still up and running.
        /// </summary>
        private void CloseTimelapseAndSaveState(bool isCompleteShutdown)
        {

            FilePlayer_Stop();

            // Removes the controls from the DataEntryPanel
            DataEntryControls.Reset();

            if (DataHandler is { FileDatabase.CountAllCurrentlySelectedFiles: > 0 })
            {
                // save image set properties to the database
                DataHandler.FileDatabase.FileSelectionEnum = FileSelectionEnum.All;

                // sync image set properties
                if (MarkableCanvas != null)
                {
                    State.MagnifyingGlassOffsetLensEnabled = MarkableCanvas.MagnifiersEnabled;
                }

                // Persist the current ID in the database image set, so we can go back to that image when restarting timelapse
                if (DataHandler.ImageCache is { Current: not null })
                {
                    DataHandler.FileDatabase.ImageSet.MostRecentFileID = DataHandler.ImageCache.Current.ID;
                }

                DataHandler.FileDatabase.ImageSet.SearchTermsAsJSON = JsonConvert.SerializeObject(DataHandler.FileDatabase.CustomSelection, Formatting.Indented);
                DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();

                // ensure custom filter operator is synchronized in state for writing to user's registry
                State.CustomSelectionTermCombiningOperator = DataHandler.FileDatabase.CustomSelection.TermCombiningOperator;

                // Check if we should delete the DeletedFiles folder, and if so do it.
                // Note that we can only do this if we know where the DeletedFolder is,
                // i.e. because the datahandler and datahandler.FileDatabae is not null
                // That is why its in this if statement.
                if (State.DeleteFolderManagement != DeleteFolderManagementEnum.ManualDelete)
                {
                    DeleteTheDeletedFilesFolderIfNeeded();
                }
                // Save selection state
            }

            // persist user specific timelapse and template editor window position state to the registry
            if (Top > -10 && Left > -10)
            {
                State.TimelapseWindowPosition = new(new(Left, Top), new Size(Width, Height));
            }
            State.TimelapseWindowSize = new(Width, Height);

            //if (isCompleteShutdown && this.DataEntryControlPanel.IsVisible) // using the isCompleteShutdown flag wasn't working...
            if (DataEntryControlPanel.IsVisible)
            {
                // Save the layout only when we close an image set and the DataEntryControlPanel is visible, 
                // as otherwise it would be hidden the next time Timelapse is started
                this.AvalonLayout_TrySave(AvalonLayoutTags.LastUsed);
            }
            else if (isCompleteShutdown)
            {
                // If the data entry control panel is not visible, we should do a reduced layut save i.e.,
                // where we save ony the position and size of the main window and whether its maximized
                // This is useful for the situation where:
                // - the user has opened timelapse but not loaded an image set
                // - they moved/resized/ maximized the window
                // - they exited without loading an image set.
                // On reload, it will show the timelapse window at the former place/size/maximize state
                // The catch is that if there is a flaoting data entry window, that window will appear at its original place, i.e., where it was when
                // last used to analyze an image set. That is, it may be in an awkward position as it is not moved relative to the timelapse window. 
                // There is no real easy solution for that, except to make the (floating) data entry window always visible on loading (which I don't really want to do). But I don't expect it to be a big problem.
                this.AvalonLayout_TrySaveWindowPositionAndSizeAndMaximizeState(AvalonLayoutTags.LastUsed);
            }

            // persist user specific state to the registry
            // note that we have to set the bookmark scale and transform in the state, as it is not done elsewhere
            if (MarkableCanvas != null)
            {
                State.BookmarkScale = MarkableCanvas.GetBookmarkScale();
                State.BookmarkTranslation = MarkableCanvas.GetBookmarkTranslation();
            }
            
            // Clear the QuickPasteEntries from the ImageSet table and save its state, including the QuickPaste window position
            quickPasteEntries = null;
            if (quickPasteWindow != null)
            {
                State.QuickPasteWindowPosition = quickPasteWindow.Position;
            }

            // Save the state by writing it to the registry
            // Note that this gets called whenever we close an image set, and again when we exit Timelapse
            State.WriteSettingsToRegistry();

            if (false == isCompleteShutdown)
            {
                // This is just a close of the image set, so lets clean up some data structures

                // Clear the arguments, as we are starting a new session.
                Arguments = new(null);

                // SAULXXX
                // There was a memory leak somewhere. It was most evident when you open/close a large data set repeatedly, where
                // the amount of memory used went up every time. It seemed to be related to DataTables
                // The code below appeared to take care of a subset of the memory leak issues, but not everything.
                // FOr example, the memory profiler (dotMemory by JetBrains) suggests that far too many strings are allocated
                // e.g., the many RelativePaths are repeatedly allocated when opening a database
                // Needs to be investigated further
                DataHandler?.FileDatabase?.DisposeAsNeeded();
                DataHandler?.DisposeAsNeeded();
                DataEntryControls.DisposeAsNeeded();
            }
        }
        #endregion
    }
}
