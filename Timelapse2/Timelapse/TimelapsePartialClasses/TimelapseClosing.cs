using Newtonsoft.Json;
using System.Windows;
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
            if (this.IsFileDatabaseAvailable())
            {
                // persist image set and registry properties if an image set has been opened
                if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
                {
                    this.CloseTimelapseAndSaveState(false);
                }

                // Dispose the image set, templateDatabase and DataHandler as they will be reset if another next image set is opened. 
                if (this.DataHandler.ImageCache != null)
                {
                    this.DataHandler.ImageCache.Dispose();
                }
                if (this.DataHandler != null)
                {
                    this.DataHandler.Dispose();
                }
                this.DataHandler = null;
                this.templateDatabase = null;
            }

            // As we are starting afresh, no detections should now exists.
            // As well, we want to reset the current detection threshold to a value that indicates it is undefined.
            GlobalReferences.DetectionsExists = false;
            if (this.DataHandler?.FileDatabase?.CustomSelection?.DetectionSelections != null)
            {
                this.DataHandler.FileDatabase.CustomSelection.DetectionSelections.CurrentDetectionThreshold = -1; // this forces it to use the default in the new JSON
            }

            // Clear the data grid
            this.DataGrid.ItemsSource = null;

            // Clear the arguments, in case we want to start a new session.
            this.Arguments = new Arguments(null);

            // Reset the rest of the user interface 
            if (this.MarkableCanvas.ThumbnailGrid != null)
            {
                this.MarkableCanvas.ThumbnailGrid.Reset();
            }
            this.MenuItemSelectByRelativePath_ClearAllCheckmarks();
            this.State.Reset();
            this.DataEntryControlPanel.IsVisible = false;
            this.MarkableCanvas.ZoomOutAllTheWay();
            this.FileNavigatorSliderReset();
            this.EnableOrDisableMenusAndControls();
            this.CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
            this.DataEntryControlPanel.IsVisible = false;
            this.FilePlayer.Visibility = Visibility.Collapsed;
            this.InstructionPane.IsActive = true;
            this.DataGridSelectionsTimer.Stop();
            this.lastControlWithFocus = null;
            this.QuickPasteWindowTerminate();
            if (this.ImageAdjuster != null)
            {
                this.ImageAdjuster.Hide();
            }
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

            this.FilePlayer_Stop();

            if ((this.DataHandler != null) &&
                (this.DataHandler.FileDatabase != null) &&
                (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0))
            {
                // save image set properties to the database
                this.DataHandler.FileDatabase.FileSelectionEnum = FileSelectionEnum.All;

                // sync image set properties
                if (this.MarkableCanvas != null)
                {
                    this.State.MagnifyingGlassOffsetLensEnabled = this.MarkableCanvas.MagnifiersEnabled;
                }

                // Persist the current ID in the database image set, so we can go back to that image when restarting timelapse
                if (this.DataHandler.ImageCache != null && this.DataHandler.ImageCache.Current != null)
                {
                    this.DataHandler.FileDatabase.ImageSet.MostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                }

                this.DataHandler.FileDatabase.ImageSet.SearchTermsAsJSON = JsonConvert.SerializeObject(this.DataHandler.FileDatabase.CustomSelection, Formatting.Indented);
                this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();

                // ensure custom filter operator is synchronized in state for writing to user's registry
                this.State.CustomSelectionTermCombiningOperator = this.DataHandler.FileDatabase.CustomSelection.TermCombiningOperator;

                // Check if we should delete the DeletedFiles folder, and if so do it.
                // Note that we can only do this if we know where the DeletedFolder is,
                // i.e. because the datahandler and datahandler.FileDatabae is not null
                // That is why its in this if statement.
                if (this.State.DeleteFolderManagement != DeleteFolderManagementEnum.ManualDelete)
                {
                    this.DeleteTheDeletedFilesFolderIfNeeded();
                }
                // Save selection state
            }

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.State.TimelapseWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.State.TimelapseWindowSize = new Size(this.Width, this.Height);

            // Save the layout only if we are really closing Timelapse and the DataEntryControlPanel is visible, as otherwise it would be hidden
            // the next time Timelapse is started
            if (isCompleteShutdown && this.DataEntryControlPanel.IsVisible)
            {
                this.AvalonLayout_TrySave(Constant.AvalonLayoutTags.LastUsed);
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
                this.AvalonLayout_TrySaveWindowPositionAndSizeAndMaximizeState(Constant.AvalonLayoutTags.LastUsed);
            }

            // persist user specific state to the registry
            // note that we have to set the bookmark scale and transform in the state, as it is not done elsewhere
            if (this.MarkableCanvas != null)
            {
                this.State.BookmarkScale = this.MarkableCanvas.GetBookmarkScale();
                this.State.BookmarkTranslation = this.MarkableCanvas.GetBookmarkTranslation();
            }
            
            // Clear the QuickPasteEntries from the ImageSet table and save its state, including the QuickPaste window position
            this.quickPasteEntries = null;
            if (this.quickPasteWindow != null)
            {
                this.State.QuickPasteWindowPosition = this.quickPasteWindow.Position;
            }

            // Save the state by writing it to the registry
            // Note that this gets called whenever we close an image set, and again when we exit Timelapse
            this.State.WriteSettingsToRegistry();

            if (false == isCompleteShutdown)
            {
                // This is just a close of the image set, so lets clean up some data structures

                // Clear the arguments, as we are starting a new session.
                this.Arguments = new DataStructures.Arguments(null);

                // SAULXXX
                // There was a memory leak somewhere. It was most evident when you open/close a large data set repeatedly, where
                // the amount of memory used went up every time. It seemed to be related to DataTables
                // The code below appeared to take care of a subset of the memory leak issues, but not everything.
                // FOr example, the memory profiler (dotMemory by JetBrains) suggests that far too many strings are allocated
                // e.g., the many RelativePaths are repeatedly allocated when opening a database
                // Needs to be investigated further
                this.DataHandler?.FileDatabase?.DisposeAsNeeded();
                this.DataHandler?.DisposeAsNeeded();
                this.DataEntryControls.DisposeAsNeeded();
            }
        }
        #endregion
    }
}
