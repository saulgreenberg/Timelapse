using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using Timelapse.Util;
using Constant=Timelapse.Constant;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        // COPY EVERYTHING BETWEEEN BRACKETS FROM  TimelapseMenuCallbacks | TimelapseMenuHelp.cs file}
        // NOTE: COMMENT OUT FILEPLAYER_STOP AS NOT USED IN THE EDITOR
        #region Help sub-menu opening
        private void Help_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            // This method is a no-op
            // this.FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

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

        #region FAQ page
        private void MenuItemFAQ_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri(Constant.ExternalLinks.TimelapseFAQPage));
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

    }
}

