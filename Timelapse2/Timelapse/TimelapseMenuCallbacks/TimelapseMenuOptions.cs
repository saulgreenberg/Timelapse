using System;
using System.Windows;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse
{
    // Options Menu Callbacks
    public partial class TimelapseWindow
    {
        #region Options sub-menu opening
        private void Options_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        #region Audio feedback: toggle on / off
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.State.AudioFeedback = !this.State.AudioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.State.AudioFeedback;
        }
        #endregion

        #region Magnifier
        // Display Magnifier: toggle on / off
        private void MenuItemDisplayMagnifyingGlass_Click(object sender, RoutedEventArgs e)
        {
            this.State.MagnifyingGlassOffsetLensEnabled = !this.State.MagnifyingGlassOffsetLensEnabled;
            this.MarkableCanvas.MagnifiersEnabled = this.State.MagnifyingGlassOffsetLensEnabled;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = this.State.MagnifyingGlassOffsetLensEnabled;
        }

        // Increase magnification of the magnifying glass. 
        private void MenuItemMagnifyingGlassIncrease_Click(object sender, RoutedEventArgs e)
        {
            // Increase the magnification by several steps to make
            // the effect more visible through a menu option versus the keyboard equivalent
            for (int i = 0; i < 6; i++)
            {
                this.MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomIn);
            }
        }

        // Decrease the magnification of the magnifying glass. 
        private void MenuItemMagnifyingGlassDecrease_Click(object sender, RoutedEventArgs e)
        {
            // Decrease the magnification by several steps to make
            // the effect more visible through a menu option versus the keyboard equivalent
            for (int i = 0; i < 6; i++)
            {
                this.MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomOut);
            }
        }
        #endregion

        #region Image Adjuster
        // Create or Show the Image Adjuster window
        private void MenuItemImageAdjuster_Click(object sender, RoutedEventArgs e)
        {
            if (ImageAdjuster == null)
            {
                this.ImageAdjuster = new ImageAdjuster(this);
                this.ImageAdjuster.ImageProcessingParametersChanged += this.MarkableCanvas.AdjustImage_EventHandler;
            }
            if (ImageAdjuster.IsVisible == false)
            {
                this.ImageAdjuster.Show();
                this.MarkableCanvas.GenerateImageStateChangeEventToReflectCurrentStatus();
            }
        }
        #endregion

        #region Adjust FilePlayer playback speeds
        private void MenuItemFilePlayerOptions_Click(object sender, RoutedEventArgs e)
        {
            FilePlayerOptions filePlayerOptions = new FilePlayerOptions(this.State, this);
            filePlayerOptions.ShowDialog();
        }
        #endregion

        #region Episode ShowHide / AdjustThreshold
        private void MenuItemEpisodeShowHide_Click(object sender, RoutedEventArgs e)
        {
            this.EpisodeShowHide(!Episodes.ShowEpisodes);
            return;
        }
        public void EpisodeShowHide(bool show)
        {
            Episodes.ShowEpisodes = show;
            this.MenuItemEpisodeShowHide.IsChecked = Episodes.ShowEpisodes;

            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.DisplayEpisodeTextInThumbnailGridIfWarranted();
            }
            else
            {
                this.DisplayEpisodeTextInImageIfWarranted(this.DataHandler.ImageCache.CurrentRow);
            }
        }

        private void MenuItemEpisodeOptions_Click(object sender, RoutedEventArgs e)
        {
            EpisodeOptions episodeOptions = new EpisodeOptions(this.State.EpisodeTimeThreshold, this);
            bool? result = episodeOptions.ShowDialog();
            if (result == true)
            {
                // the time threshold has changed, so save its new state
                this.State.EpisodeTimeThreshold = episodeOptions.EpisodeTimeThreshold;
                Episodes.TimeThreshold = this.State.EpisodeTimeThreshold; // so we don't have to pass it as a parameter
                Episodes.Reset();
            }

            if (this.IsDisplayingMultipleImagesInOverview())
            {
                this.MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(false);
            }
            else
            {
                this.DisplayEpisodeTextInImageIfWarranted(this.DataHandler.ImageCache.CurrentRow);
            }
        }
        #endregion

        #region Show / Hide various informational dialogs"
        private void MenuItemDialogsOnOrOff_Click(object sender, RoutedEventArgs e)
        {
            DialogsHideOrShow dialog = new DialogsHideOrShow(this.State, this);
            dialog.ShowDialog();
        }
        #endregion

        #region Preferences
        /// <summary>Show Timelapse Preference dialog</summary>
        private void MenuItemPreferences_Click(object sender, RoutedEventArgs e)
        {
            bool detectionsAvailable = this.DataHandler?.FileDatabase != null && this.DataHandler.FileDatabase.DetectionsExists();
            AdvancedTimelapseOptions advancedTimelapseOptions = new AdvancedTimelapseOptions(this.State, this.MarkableCanvas, this, detectionsAvailable);
            advancedTimelapseOptions.ShowDialog();

            // Reset how some controls appear depending upon the current options
            this.EnableOrDisableMenusAndControls();

            if (this.DataHandler != null && this.DataHandler.FileDatabase != null)
            {
                // If we aren't using detections, then hide their existence even if detection data may be present
                GlobalReferences.DetectionsExists = this.DataHandler.FileDatabase.DetectionsExists();
            }
            else
            {
                GlobalReferences.DetectionsExists = false;
            }

            // redisplay the file as the options may change how bounding boxes should be displayed
            if (this.DataHandler != null)
            {
                this.FileShow(this.DataHandler.ImageCache.CurrentRow, true);
            }
        }
        #endregion
    }
}
