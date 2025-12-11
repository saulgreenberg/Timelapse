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
            bool filesSelectedAndSingleImage = filesSelected && this.MarkableCanvas?.ImageToDisplay is { IsVisible: true };
            bool overviewIsVisible = this.MarkableCanvas?.ImageToDisplay is { IsVisible: false };

            MenuItemViewDifferencesCycleThrough.IsEnabled = filesSelected;
            MenuItemViewDifferencesCombined.IsEnabled = filesSelected;

            //MenuItemViewInOverview.IsEnabled = overviewIsVisible;
            MenuItemViewNextRowInOverview.IsEnabled = overviewIsVisible;
            MenuItemViewPreviousRowInOverview.IsEnabled = overviewIsVisible;
            MenuItemViewNextPageInOverview.IsEnabled = overviewIsVisible;
            MenuItemViewPreviousPageInOverview.IsEnabled = overviewIsVisible;
            
            // Zoom menu items
            // Note: bookmarks are enabled only if a single image is being displayed.
            MenuItemBookmarkDefaultPanZoom.IsEnabled = this.MarkableCanvas?.isZooming == true;
            MenuItemBookmarkSavePanZoom.IsEnabled = filesSelectedAndSingleImage;
            MenuItemBookmarkSetPanZoom.IsEnabled = filesSelectedAndSingleImage;

            // Show in explorer menu item
            MenuItemShowInExplorer.IsEnabled =
                filesSelected &&
                true == DataHandler?.ImageCache?.Current?.FileExists(DataHandler?.FileDatabase?.RootPathToImages);

            // Dogear menu item
            // The dogear menu items are enabled (and their text) depends on a variety of conditions
            MenuItemDogearSet.IsEnabled = filesSelectedAndSingleImage && this.ImageDogear != null;
            if (this.ImageDogear != null)
            {
                if (false == this.ImageDogear.IsDogearTheCurrentImage())
                {
                    if (this.ImageDogear.DogearExists())
                    {
                        // We can only go to a dogeared image
                        MenuItemDogearSwitch.Header = "Go to the dogeared image";
                        MenuItemDogearSwitch.IsEnabled = filesSelectedAndSingleImage;
                    }
                    else
                    {
                        // but the dogear doesn't exist
                        MenuItemDogearSwitch.IsEnabled = false;
                    }
                }
                else
                {
                    if (this.ImageDogear.LastSeenImageExists())
                    {
                        // We can only go to the last seen image
                        MenuItemDogearSwitch.Header = "Return to the last seen image";
                        MenuItemDogearSwitch.IsEnabled = filesSelectedAndSingleImage;
                    }
                    else
                    {
                        // but the last seen image doesn't exist
                        MenuItemDogearSwitch.Header = "Go to the dogeared image";
                        MenuItemDogearSwitch.IsEnabled = false;
                    }
                }
            }
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

        #region  Show / Hide / Next / Previous episode
        private void MenuItemEpisodeShowHide_Click(object sender, RoutedEventArgs e)
        {
            EpisodeShowHide(!Episodes.Episodes.ShowEpisodes);
        }
        public void EpisodeShowHide(bool show)
        {
            Episodes.Episodes.ShowEpisodes = show;
            MenuItemEpisodeShowHide.IsChecked = Episodes.Episodes.ShowEpisodes;

            if (IsDisplayingMultipleImagesInOverview())
            {
                MarkableCanvas.DisplayEpisodeTextInThumbnailGridIfWarranted();
            }
            else
            {
                DisplayEpisodeTextInImageIfWarranted(DataHandler.ImageCache.CurrentRow);
            }
        }

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

        #region view next/ previous row/page in overview
        private void MenuItemShowInOverview_Click(object sender, RoutedEventArgs e)
        {
            // Since the file player already does this, we just invoke its own handler.
            this.FilePlayer.FilePlayer_Click(sender, e);
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

        // Zoom out all the way: restores the level to an image that fills the space
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            MarkableCanvas.ZoomOutAllTheWay();
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
        #endregion

        #region Dogears
        private void MenuItem_DogearSet(object sender, RoutedEventArgs e)
        {
            if (this.MarkableCanvas.IsThumbnailGridVisible)
            {
                return;
            }
            this.ImageDogear?.TrySetDogearToCurrentImage();
        }

        private void MenuItem_DogearSwitch(object sender, RoutedEventArgs e)
        {
            if (this.ImageDogear != null)
            {
                int index = this.ImageDogear.TryGetDogearOrPreviouslySeenImageIndex();
                if (index != Constant.DatabaseValues.InvalidRow)
                {
                    // Show the image at the bookmark index
                    this.FileShow(index);
                }
            }
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
