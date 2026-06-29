using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Util;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Help Menu Callbacks
    public partial class TimelapseWindow
    {
        #region Help sub-menu opening
        private void Help_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        private void MenuItem_KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            Dialogs.ShowKeyboardShortcutsForTimelapse(this);
        }

        #region Timelapse web site: home, tutorial manual, sample images
            // Timelapse web page (via your browser): Timelapse home page
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseHomePage));
        }

        // Tutorial guides (via your browser) 
        private void MenuItemGuidesAndManuals_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                switch (mi.Name)
                {
                    case "MenuItemGuidesPage":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuidesPage));
                        break;
                    case "MenuItemQuickStartGuide":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuideQuickStart));
                        break;
                    case "MenuItemReferenceGuide":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuideReference));
                        break;
                    case "MenuItemTemplateGuide":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuideTemplate));
                        break;
                    case "MenuItemImageRecognitionGuide":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuideImageRecognition));
                        break;
                    case "MenuItemMetadataGuide":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuideMetadata));
                        break;
                    case "MenuItemDatabaseGuide":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseGuideDatabase));
                        break;
                }
            }
        }
        #endregion

        #region FAQ page
        private void MenuItemFAQ_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseFAQPage));
        }
        #endregion

        #region Timelapse web site: videos
        public void MenuVideoPlay_Click(object sender, RoutedEventArgs e)
        {
            string prefix = "https://saul.cpsc.ucalgary.ca/timelapse/uploads/Videos/";
            if (sender is MenuItem mi)
            {
                switch (mi.Name)
                {
                    // Companion Videos
                    case "MenuItemVideosPage":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosPage));
                        break;

                    case "MenuItemVideoCompanionQuickStart":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosQuickStart));
                        break;
                    case "MenuItemVideoCompanionTemplateEditor":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosTemplateEditor));
                        break;
                    case "MenuItemVideoCompanionImageRecognition":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosImageRecognition));
                        break;

                    // Presentations
                    case "MenuItemVideoWhirlwindTour":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosWhirlwindTourOfTimelapse));
                        break;

                    case "MenuItemVideoImageRecognitionForCameraTraps":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideoImageRecognitionPresentation));
                        break;

                    // Lessons
                    case "MenuItemVideoInstallingTimelapse":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosInstallingTimelapse));
                        break;
                    case "MenuItemVideoAddingImagesOverTime":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosIncrementallyAddingImages));
                        break;
                    case "MenuItemVideoViewingVideos":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosViewingVideos));
                        break;

                    case "MenuItemVideoQuickPaste":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosQuickPaste));
                        break;
                    case "MenuItemVideoDuplicatingRecords":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosDuplicatingRecords));
                        break;

                    case "MenuItemVideoUsingAddaxAI":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosUsingAddaxAI));
                        break;
                    case "MenuItemImageRecognitionForVideos":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosImageRecognitionForVideos));
                        break;
                        
                    case "MenuItemVideoUsingOverview":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosUsingTheOverview));
                        break;
                    case "MenuItemVideoRandomSampling":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosRandomSampling));
                        break;

                    case "MenuItemVideoImageRecnWebinar":
                        ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseVideosImageRecognitionTalk));
                        break;
                    // OLDER
                    case "MenuItemVideoClassifyingDarkImages":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "Options-DarkThresholds.mp4"));
                        break;
                    case "MenuItemVideoRepositionDataEntryPanel":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "RepositioningTabsAndPanels.mp4"));
                        break;

                    case "MenuItemVideoPopulateEpisodeData":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "PopulateEpisodeData.mp4"));
                        break;
                    case "MenuItemVideoViewingPopups":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "EpisodePopups.mp4"));
                        break;
                }
            }
        }

        #endregion

        #region Timelapse mailing list - Join and/or send email
        // Timelapse mailing list - Join it(via your web browser)
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l"));
        }

        // Timelapse mailing list - Send email
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("mailto:timelapse-l@mailman.ucalgary.ca"));
        }
        #endregion

        #region Mail the timelapse developers
        private void MenuMailToTimelapseDevelopers_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("mailto:saul@ucalgary.ca"));
        }
        #endregion

        #region About: Display a message describing the version,check for updates etc.
        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutTimelapse about = new(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                State.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }
        #endregion

        #region Open Error Log
        private void MenuItemOpenErrorLog_Click(object sender, RoutedEventArgs e)
        {
            string logPath = AppLog.DefaultLogFilePath;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            {
                MessageBox.Show(
                    "No error log file exists yet. A log is only created if certain (but not all) warnings or errors have occurred.",
                    "No Error Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ProcessExecution.TryProcessStart(logPath);
        }
        #endregion

        #region Email the Timelapse developer - submenu opened
        private void MenuEmailTimelapseDeveloper_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            string logPath = AppLog.DefaultLogFilePath;
            MenuItemEmailErrorLog.IsEnabled = !string.IsNullOrEmpty(logPath)
                && File.Exists(logPath)
                && new FileInfo(logPath).Length > 0;
        }
        #endregion

        #region Email Error Log
        private void MenuItemEmailErrorLog_Click(object sender, RoutedEventArgs e)
        {
            string logPath = AppLog.DefaultLogFilePath;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            {
                MessageBox.Show(
                    "No error log file exists yet. A log is only created if certain (but not all) warnings or errors have occurred.",
                    "No Error Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string logContents;
            try { logContents = File.ReadAllText(logPath); }
            catch { logContents = "(Could not read log file contents)"; }

            string body = "Add details describing the problem you are having, as that will help the Timelapse developer try to figure out what is going on."
                        + Environment.NewLine + Environment.NewLine
                        + "--- Timelapse Error Log ---" + Environment.NewLine
                        + logContents;

            Uri uri = new($"mailto:{Constant.ExternalLinks.EmailAddress}?subject=Timelapse error log&body={Uri.EscapeDataString(body)}");
            if (!ProcessExecution.TryProcessStart(uri))
            {
                MessageBox.Show(
                    $"Could not open your email client. You can manually send the error log file to {Constant.ExternalLinks.EmailAddress}{Environment.NewLine}Log file: {logPath}",
                    "Email Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion
    }
}
