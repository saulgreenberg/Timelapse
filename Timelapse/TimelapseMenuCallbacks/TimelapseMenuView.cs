using System.Windows;
using System.Windows.Input;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // View Menu Callbacks
    public partial class TimelapseWindow
    {
        #region View sub-menu opening
        private void View_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going

            bool filesSelected = IsDisplayingActiveSingleImageOrVideo(); //IsFileDatabaseAvailable() && DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0; // A non-empty image set is loaded
            bool filesSelectedAndSingleImage = filesSelected && (this.MarkableCanvas?.ImageToDisplay != null && this.MarkableCanvas.ImageToDisplay.IsVisible);

            MenuItemViewDifferencesCycleThrough.IsEnabled = filesSelected;
            MenuItemViewDifferencesCombined.IsEnabled = filesSelected;

            // Currently, bookmarks are enabled only if a single image is being displayed.
            MenuItemBookmarkDefaultPanZoom.IsEnabled = filesSelectedAndSingleImage;
            MenuItemBookmarkSavePanZoom.IsEnabled = filesSelectedAndSingleImage;
            MenuItemBookmarkSetPanZoom.IsEnabled = filesSelectedAndSingleImage;
            MenuItemShowInExplorer.IsEnabled =
                filesSelectedAndSingleImage &&
                true == DataHandler?.ImageCache?.Current?.FileExists(DataHandler?.FileDatabase?.RootPathToImages);

        }
        #endregion

        #region View next / previous file
        // View next file in this image set
        private void MenuItemShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            TryFileShowWithoutSliderCallback(DirectionEnum.Next);
        }

        // View previous file in this image set
        private void MenuItemShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            TryFileShowWithoutSliderCallback(DirectionEnum.Previous);
        }
        #endregion

        #region  View next / previous episode
        // View next episode in this image set
        private void MenuItemShowNextEpisode_Click(object sender, RoutedEventArgs e)
        {
            EpisodeShowNextOrPrevious_Click(DirectionEnum.Next);
        }
        private void MenuItemShowPreviousEpisode_Click(object sender, RoutedEventArgs e)
        {
            EpisodeShowNextOrPrevious_Click(DirectionEnum.Previous);
        }

        // View Episode helper function
        private void EpisodeShowNextOrPrevious_Click(DirectionEnum direction)
        {
            if (DataHandler?.ImageCache?.Current == null)
            {
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }
            long currentFileID = DataHandler.ImageCache.Current.ID;
            bool result = Episodes.Episodes.GetIncrementToNextEpisode(DataHandler.FileDatabase.FileTable, DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID), direction, out int increment);
            if (result)
            {
                if (Episodes.Episodes.ShowEpisodes == false)
                {
                    // turn on Episode display if its not already on
                    EpisodeShowHide(true);
                }
                // At this point, the episodes should be showing and the increment amount should be reset (see the out parameter above)
                TryFileShowWithoutSliderCallback(direction, increment);
            }
        }
        #endregion

        #region Zoom in / out
        // Zoom in
        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point imageMousePosition = Mouse.GetPosition(MarkableCanvas.ImageToDisplay);
            Point videoMousePosition = Mouse.GetPosition(MarkableCanvas.VideoPlayer.MediaElement);
            MarkableCanvas.TryZoomInOrOut(true, imageMousePosition, videoMousePosition);
        }

        // Zoom out
        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point imageMousePosition = Mouse.GetPosition(MarkableCanvas.ImageToDisplay);
            Point videoMousePosition = Mouse.GetPosition(MarkableCanvas.VideoPlayer.MediaElement);
            MarkableCanvas.TryZoomInOrOut(false, imageMousePosition, videoMousePosition);
        }
        #endregion

        #region Zoom Bookmarks
        // Save a Bookmark of the current pan / zoom region 
        private void MenuItem_BookmarkSavePanZoom(object sender, RoutedEventArgs e)
        {
            MarkableCanvas.SetBookmark();
        }

        // Zoom to bookmarked _region: restores the zoom level / pan coordinates of the bookmark
        private void MenuItem_BookmarkSetPanZoom(object sender, RoutedEventArgs e)
        {
            MarkableCanvas.ApplyBookmark();
        }

        // Zoom out all the way: restores the level to an image that fills the space
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            MarkableCanvas.ZoomOutAllTheWay();
        }
        #endregion

        #region View Image differences
        // Cycle through the image differences
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            TryViewPreviousOrNextDifference();
        }

        // View  combined image differences
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            TryViewCombinedDifference();
        }
        #endregion

        #region Show in Explorer
        private void MenuItemShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            // Note that the menu item is only selectable if the file actually exists
            // Thus the empty/null test is likely not needed, but...
            string path = DataHandler?.ImageCache?.Current?.GetFilePath(DataHandler?.FileDatabase?.RootPathToImages);
            if (false == string.IsNullOrWhiteSpace(path))
            {
                ProcessExecution.TryProcessStartUsingFileExplorerToSelectFile(path);
            }
        }
        #endregion
    }
}
