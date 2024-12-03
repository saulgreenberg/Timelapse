using System.Windows;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Options Menu Callbacks
    public partial class TimelapseWindow
    {
        #region Options sub-menu opening
        private void Options_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        #region Audio feedback: toggle on / off
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            State.AudioFeedback = !State.AudioFeedback;
            MenuItemAudioFeedback.IsChecked = State.AudioFeedback;
        }
        #endregion

        #region Magnifier
        // Display Magnifier: toggle on / off
        private void MenuItemDisplayMagnifyingGlass_Click(object sender, RoutedEventArgs e)
        {
            State.MagnifyingGlassOffsetLensEnabled = !State.MagnifyingGlassOffsetLensEnabled;
            MarkableCanvas.MagnifiersEnabled = State.MagnifyingGlassOffsetLensEnabled;
            MenuItemDisplayMagnifyingGlass.IsChecked = State.MagnifyingGlassOffsetLensEnabled;
        }

        // Increase magnification of the magnifying glass. 
        private void MenuItemMagnifyingGlassIncrease_Click(object sender, RoutedEventArgs e)
        {
            // Increase the magnification by several steps to make
            // the effect more visible through a menu option versus the keyboard equivalent
            for (int i = 0; i < 6; i++)
            {
                MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomIn);
            }
        }

        // Decrease the magnification of the magnifying glass. 
        private void MenuItemMagnifyingGlassDecrease_Click(object sender, RoutedEventArgs e)
        {
            // Decrease the magnification by several steps to make
            // the effect more visible through a menu option versus the keyboard equivalent
            for (int i = 0; i < 6; i++)
            {
                MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomOut);
            }
        }
        #endregion

        #region Image Adjuster
        // Create or Show the Image Adjuster window
        private void MenuItemImageAdjuster_Click(object sender, RoutedEventArgs e)
        {
            if (ImageAdjuster == null)
            {
                ImageAdjuster = new ImageAdjuster(this);
                ImageAdjuster.ImageProcessingParametersChanged += MarkableCanvas.AdjustImage_EventHandler;
            }
            if (ImageAdjuster.IsVisible == false)
            {
                ImageAdjuster.Show();
                MarkableCanvas.GenerateImageStateChangeEventToReflectCurrentStatus();
            }
        }
        #endregion

        #region Adjust FilePlayer playback speeds
        private void MenuItemFilePlayerOptions_Click(object sender, RoutedEventArgs e)
        {
            FilePlayerOptions filePlayerOptions = new FilePlayerOptions(State, this);
            filePlayerOptions.ShowDialog();
        }
        #endregion

        #region Episode ShowHide / AdjustThreshold
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

        private void MenuItemEpisodeOptions_Click(object sender, RoutedEventArgs e)
        {
            EpisodeOptions episodeOptions = new EpisodeOptions(State.EpisodeTimeThreshold, this);
            bool? result = episodeOptions.ShowDialog();
            if (result == true)
            {
                // the time threshold has changed, so save its new state
                State.EpisodeTimeThreshold = episodeOptions.EpisodeTimeThreshold;
                Episodes.Episodes.TimeThreshold = State.EpisodeTimeThreshold; // so we don't have to pass it as a parameter
                Episodes.Episodes.Reset();
            }

            if (IsDisplayingMultipleImagesInOverview())
            {
                MarkableCanvas.RefreshIfMultipleImagesAreDisplayed(false);
            }
            else
            {
                DisplayEpisodeTextInImageIfWarranted(DataHandler.ImageCache.CurrentRow);
            }
        }
        #endregion

        #region Show / Hide various informational dialogs"
        private void MenuItemDialogsOnOrOff_Click(object sender, RoutedEventArgs e)
        {
            DialogsHideOrShow dialog = new DialogsHideOrShow(State, this);
            dialog.ShowDialog();
        }
        #endregion

        #region Preferences
        /// <summary>Show Timelapse Preference dialog</summary>
        private void MenuItemPreferences_Click(object sender, RoutedEventArgs e)
        {
            AdvancedTimelapseOptions advancedTimelapseOptions = new AdvancedTimelapseOptions(State, MarkableCanvas, this);
            advancedTimelapseOptions.ShowDialog();

            // Reset how some controls appear depending upon the current options
            EnableOrDisableMenusAndControls();

            if (DataHandler != null && DataHandler.FileDatabase != null)
            {
                // If we aren't using detections, then hide their existence even if detection data may be present
                GlobalReferences.DetectionsExists = DataHandler.FileDatabase.DetectionsExists();
            }
            else
            {
                GlobalReferences.DetectionsExists = false;
            }

            // redisplay the file as the options may change how bounding boxes should be displayed
            if (DataHandler != null)
            {
                FileShow(DataHandler.ImageCache.CurrentRow, true);
            }
        }
        #endregion
    }
}
