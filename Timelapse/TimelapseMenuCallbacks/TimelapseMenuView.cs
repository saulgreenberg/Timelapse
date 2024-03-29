﻿using System.Windows;
using System.Windows.Input;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // View Menu Callbacks
    public partial class TimelapseWindow
    {
        #region View sub-menu opening
        private void View_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going

            bool state = this.IsDisplayingActiveSingleImage();
            this.MenuItemViewDifferencesCycleThrough.IsEnabled = state;
            this.MenuItemViewDifferencesCombined.IsEnabled = state;
            this.MenuItemZoomIn.IsEnabled = state;
            this.MenuItemZoomOut.IsEnabled = state;
            this.MenuItemBookmarkDefaultPanZoom.IsEnabled = state;
            this.MenuItemBookmarkSavePanZoom.IsEnabled = state;
            this.MenuItemBookmarkSetPanZoom.IsEnabled = state;
            this.MenuItemShowInExplorer.IsEnabled = 
                state &&
                true == this.DataHandler?.ImageCache?.Current?.FileExists(this.DataHandler?.FileDatabase?.FolderPath);
        }
        #endregion

        #region View next / previous file
        // View next file in this image set
        private void MenuItemShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            this.TryFileShowWithoutSliderCallback(DirectionEnum.Next);
        }

        // View previous file in this image set
        private void MenuItemShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            this.TryFileShowWithoutSliderCallback(DirectionEnum.Previous);
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
            if (this.DataHandler?.ImageCache?.Current == null)
            {
                TracePrint.NullException(nameof(this.DataHandler.ImageCache.Current));
                return;
            }
            long currentFileID = this.DataHandler.ImageCache.Current.ID;
            bool result = Episodes.Episodes.GetIncrementToNextEpisode(this.DataHandler.FileDatabase.FileTable, this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID), direction, out int increment);
            if (result)
            {
                if (Episodes.Episodes.ShowEpisodes == false)
                {
                    // turn on Episode display if its not already on
                    this.EpisodeShowHide(true);
                }
                // At this point, the episodes should be showing and the increment amount should be reset (see the out parameter above)
                this.TryFileShowWithoutSliderCallback(direction, increment);
            }
        }
        #endregion

        #region Zoom in / out
        // Zoom in
        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point imageMousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            Point videoMousePosition = Mouse.GetPosition(this.MarkableCanvas.VideoPlayer.Video);
            this.MarkableCanvas.TryZoomInOrOut(true, imageMousePosition, videoMousePosition);
        }

        // Zoom out
        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point imageMousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            Point videoMousePosition = Mouse.GetPosition(this.MarkableCanvas.VideoPlayer.Video);
            this.MarkableCanvas.TryZoomInOrOut(false, imageMousePosition, videoMousePosition);
        }
        #endregion

        #region Zoom Bookmarks
        // Save a Bookmark of the current pan / zoom region 
        private void MenuItem_BookmarkSavePanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.SetBookmark();
        }

        // Zoom to bookmarked _region: restores the zoom level / pan coordinates of the bookmark
        private void MenuItem_BookmarkSetPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ApplyBookmark();
        }

        // Zoom out all the way: restores the level to an image that fills the space
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ZoomOutAllTheWay();
        }
        #endregion

        #region View Image differences
        // Cycle through the image differences
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewPreviousOrNextDifference();
        }

        // View  combined image differences
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewCombinedDifference();
        }
        #endregion

        #region Show in Explorer
        private void MenuItemShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            // Note that the menu item is only selectable if the file actually exists
            // Thus the empty/null test is likely not needed, but...
            string path = this.DataHandler?.ImageCache?.Current?.GetFilePath(this.DataHandler?.FileDatabase?.FolderPath);
            if (false == string.IsNullOrWhiteSpace(path))
            {
                Util.ProcessExecution.TryProcessStartUsingFileExplorerToSelectFile(path);
            }
        }
        #endregion
    }
}
