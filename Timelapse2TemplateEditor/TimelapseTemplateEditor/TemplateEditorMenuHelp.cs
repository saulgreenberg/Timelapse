using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Editor
{
    public partial class EditorWindow : Window
    {
        // COPY EVERYTHING BETWEEEN BRACKETS FROM  TimelapseMenuCallbacks | TimelapseMenuHelp.cs file}
        // NOTE: COMMENT OUT FILEPLAYER_STOP AS NOT USED IN THE EDITOR
        #region Help sub-menu opening
        private void Help_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            //this.FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        #region Timelapse web site: home, tutorial manual, sample images
        // Timelapse web page (via your browser): Timelapse home page
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://saul.cpsc.ucalgary.ca/timelapse"));
        }

        // Tutorial manual (via your browser) 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf"));
        }

        // Tutorial guides (via your browser) 
        private void MenuItemGuidesAndManuals_Click(object sender, RoutedEventArgs e)
        {
            string prefix = "https://saul.cpsc.ucalgary.ca/timelapse/uploads/Guides/";
            if (sender is MenuItem mi)
            {
                switch (mi.Name)
                {
                    case "MenuItemGoToManualsPage":
                        ProcessExecution.TryProcessStart(new Uri("https://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.UserGuide"));
                        break;
                    case "MenuItemQuickStartGuide":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "TimelapseQuickStartGuide.pdf"));
                        break;
                    case "MenuItemReferenceGuide":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "TimelapseReferenceGuide.pdf"));
                        break;
                    case "MenuItemTemplateGuide":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "TimelapseTemplateGuide.pdf"));
                        break;
                    case "MenuItemImageRecognitionGuide":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "TimelapseImageRecognitionGuide.pdf"));
                        break;
                    case "MenuItemDatabaseGuide":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "TimelapseDatabaseGuide.pdf"));
                        break;
                    default:
                        break;
                }
            }
        }

        // Download tutorial sample images (via your web browser) 
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            ProcessExecution.TryProcessStart(new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.UserGuide"));
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
                    case "MenuItemVideoWhirlwindTour":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "WhirlwindTourOfTimelapse.mp4"));
                        break;
                    case "MenuItemVideoImageRecognition":
                        ProcessExecution.TryProcessStart(new Uri("http://grouplab.cpsc.ucalgary.ca/grouplab/uploads/Publications/Publications/2021-05-ImageRecognition-Video.mp4"));
                        break;
                    case "MenuItemVideoTemplateEditor":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "TemplateEditor.mp4"));
                        break;
                    case "MenuItemVideoPlayer":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "UsingVideo.mp4"));
                        break;
                    case "MenuItemVideoClassifyingDarkImages":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "Options-DarkThresholds.mp4"));
                        break;
                    case "MenuItemVideoRepositionDataEntryPanel":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "RepositioningTabsAndPanels.mp4"));
                        break;
                    case "MenuItemVideoUsingOverview":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "UsingTheOverview.mp4"));
                        break;
                    case "MenuItemVideoPopulateEpisodeData":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "PopulateEpisodeData.mp4"));
                        break;
                    case "MenuItemVideoViewingPopups":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "EpisodePopups.mp4"));
                        break;
                    case "MenuItemVideoRandomSampling":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "RandomSample.mp4"));
                        break;
                    case "MenuItemVideoDuplicatingRecords":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "DuplicateThisRecord.mp4"));
                        break;
                    case "MenuItemVideoCompanionImageRecognition":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "Video-TimelapseImageRecognitionGuide.mp4"));
                        break;
                    case "MenuItemVideoCompanionQuickStart":
                        ProcessExecution.TryProcessStart(new Uri(prefix + "Video-TimelapseQuickStartGuide.mp4"));
                        break;
                    default:
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
            AboutTimelapse about = new AboutTimelapse(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.State.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }
        #endregion
    }
}
