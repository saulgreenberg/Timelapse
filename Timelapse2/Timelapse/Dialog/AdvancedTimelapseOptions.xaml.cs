using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AdvancedTimelapseOptions
    {
        #region Private Variables
        private readonly MarkableCanvas markableCanvas;
        private readonly TimelapseState timelapseState;
        //private readonly bool detectionsLoaded;
        #endregion

        #region Constructor and Loaded
        public AdvancedTimelapseOptions(TimelapseState timelapseState, MarkableCanvas markableCanvas, Window owner, bool detectionsLoaded)
        {
            this.InitializeComponent();
            this.Owner = owner;
            //this.detectionsLoaded = detectionsLoaded;

            // Check the arguments for null 
            ThrowIf.IsNullArgument(timelapseState, nameof(timelapseState));
            ThrowIf.IsNullArgument(markableCanvas, nameof(markableCanvas));

            this.markableCanvas = markableCanvas;
            this.timelapseState = timelapseState;

            // Metadata Populate On Load
            this.CheckBoxEnablePopulateMetadataOnLoad.IsChecked = this.timelapseState.MetadataAskOnLoad;

            // Tab Order - set to current state.
            this.CheckBoxTabOrderDateTime.IsChecked = this.timelapseState.TabOrderIncludeDateTime;
            this.CheckBoxTabOrderDeleteFlag.IsChecked = this.timelapseState.TabOrderIncludeDeleteFlag;

            // Deletion Management
            this.RadioButtonDeletionManagement_Set(this.timelapseState.DeleteFolderManagement);

            // CSV Include Folder column option

            // CSV DateTime options
            if (this.timelapseState.CSVDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
            {
                this.RadioButtonCSVDateAndTimeColumns.IsChecked = true;
            }
            else if (this.timelapseState.CSVDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator)
            {
                this.RadioButtonCSVLocalDateTimeColumn.IsChecked = true;
            }
            else if (this.timelapseState.CSVDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn)
            {
                this.RadioButtonCSVLocalDateTimeColumnWithoutT.IsChecked = true;
            }

            this.CheckBoxCSVInsertSpaceBeforeDates.IsChecked = this.timelapseState.CSVInsertSpaceBeforeDates;
            this.CheckBoxCSVIncludeFolderColumn.IsChecked = this.timelapseState.CSVIncludeFolderColumn;

            // Throttles
            this.ImageRendersPerSecond.Minimum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            this.ImageRendersPerSecond.Maximum = Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ValueChanged += this.ImageRendersPerSecond_ValueChanged;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;

            // The EpisodeMaxRangeToSearch Threshold values 
            this.SliderSetEpisodeMaxRange.Value = this.timelapseState.EpisodeMaxRangeToSearch;
            this.SetSliderSetEpisodeMaxRangeFeedack(this.timelapseState.EpisodeMaxRangeToSearch);
            this.SliderSetEpisodeMaxRange.Maximum = Constant.EpisodeDefaults.MaximumRangeToSearch;
            this.SliderSetEpisodeMaxRange.Minimum = 50;

            // The Max Zoom Value
            this.MaxZoom.Value = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.Maximum = Constant.MarkableCanvas.ImageZoomMaximumRangeAllowed;
            this.MaxZoom.Minimum = 2;

            // Image Differencing Thresholds
            this.DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.Maximum = Constant.ImageValues.DifferenceThresholdMax;
            this.DifferenceThreshold.Minimum = Constant.ImageValues.DifferenceThresholdMin;

            // Detections
            //this.CheckBoxBoundingBoxAnnotate.IsChecked = this.timelapseState.BoundingBoxAnnotate;
            //this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked = this.timelapseState.BoundingBoxColorBlindFriendlyColors;
            //this.AutomatedImageRecognitionPanel.IsEnabled = detectionsLoaded;

            //this.CheckBoxBoundingBoxAnnotate.IsEnabled = detectionsLoaded;
            //this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsEnabled = detectionsLoaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            //this.BoundingBoxDisplayThresholdSlider.IsEnabled = true;
            //this.BoundingBoxDisplayThresholdSlider.Value = this.timelapseState.BoundingBoxDisplayThreshold;
            //this.CheckBoxBoundingBoxAnnotate.IsEnabled = true;
            //this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsEnabled = true;
        }
        #endregion

        #region Callbacks - Populate Metadata on Load
        private void ResetPopulateMetadataDefaults_Click(object sender, RoutedEventArgs e)
        {
            this.CheckBoxEnablePopulateMetadataOnLoad.IsChecked = false;
            this.timelapseState.MetadataAskOnLoad = false;
        }
        private void CheckBoxEnablePopulateMetadataOnLoad_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.MetadataAskOnLoad = this.CheckBoxEnablePopulateMetadataOnLoad.IsChecked == true;
        }
        #endregion

        #region Callbacks + Helper: Delete Folder Management
        // Check the appropriate radio button to match the state


        // Set the state to match the radio button selection
        private void DeletedFileManagement_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = (RadioButton)sender;
            switch (rb.Name)
            {
                case "RadioButtonManualDelete":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ManualDelete;
                    break;
                case "RadioButtonAskToDelete":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.AskToDeleteOnExit;
                    break;
                case "RadioButtonAutoDeleteOnExit":
                    this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.AutoDeleteOnExit;
                    break;
                default:
                    break;
            }
        }

        // Reset to the Default, i.e. manual deletion
        private void ResetDeletedFileManagement_Click(object sender, RoutedEventArgs e)
        {
            this.RadioButtonManualDelete.IsChecked = true;
            this.timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ManualDelete;
        }

        private void RadioButtonDeletionManagement_Set(DeleteFolderManagementEnum deleteFolderManagement)
        {
            switch (deleteFolderManagement)
            {
                case DeleteFolderManagementEnum.ManualDelete:
                    this.RadioButtonManualDelete.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.AskToDeleteOnExit:
                    this.RadioButtonAskToDelete.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.AutoDeleteOnExit:
                    this.RadioButtonAutoDeleteOnExit.IsChecked = true;
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Callback - Reset CSV File Defaults
        private void ResetCSVDefaults_Click(object sender, RoutedEventArgs e)
        {
            this.RadioButtonCSVLocalDateTimeColumnWithoutT.IsChecked = true;
            this.CheckBoxCSVInsertSpaceBeforeDates.IsChecked = true;
            this.CheckBoxCSVIncludeFolderColumn.IsChecked = true;
            this.SetCSVOptions();
        }

        private void RadioButtonCSVOptions_Click(object sender, RoutedEventArgs e)
        {
            this.SetCSVOptions();
        }

        private void CheckBoxCSVInsertSpaceBeforeDate_Click(object sender, RoutedEventArgs e)
        {
            this.SetCSVOptions();
        }

        private void CheckBoxIncludeFolderColumn_Click(object sender, RoutedEventArgs e)
        {
            this.SetCSVOptions();
        }

        private void SetCSVOptions()
        {
            // Note that some of these are now defunct
            if (this.RadioButtonCSVDateAndTimeColumns.IsChecked == true)
            {
                this.timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateAndTimeColumns;
            }
            else if (this.RadioButtonCSVLocalDateTimeColumn.IsChecked == true)
            {
                this.timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator;
            }
            else if (this.RadioButtonCSVLocalDateTimeColumnWithoutT.IsChecked == true)
            {
                this.timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn;
            }
            else //if (this.RadioButtonCSVUTCWithOffsetDateTimeColumn.IsChecked IsChecked == true)
            {
                // This is now defunct and should not be activated
                this.timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateTimeUTCWithOffset;
            }
            this.timelapseState.CSVInsertSpaceBeforeDates = this.CheckBoxCSVInsertSpaceBeforeDates.IsChecked == true;
            this.timelapseState.CSVIncludeFolderColumn = this.CheckBoxCSVIncludeFolderColumn.IsChecked == true;
        }
        #endregion

        #region Callbacks - Tab Controls to Include / Exclude
        private void CheckBoxTabOrder_Click(object sender, RoutedEventArgs e)
        {
            this.SetTabOrder();
        }

        private void ResetTabOrder_Click(object sender, RoutedEventArgs e)
        {
            this.CheckBoxTabOrderDateTime.IsChecked = false;
            this.CheckBoxTabOrderDeleteFlag.IsChecked = false;
            this.SetTabOrder();
        }

        private void SetTabOrder()
        {
            this.timelapseState.TabOrderIncludeDateTime = this.CheckBoxTabOrderDateTime.IsChecked == true;
            this.timelapseState.TabOrderIncludeDeleteFlag = this.CheckBoxTabOrderDeleteFlag.IsChecked == true;
        }
        #endregion

        #region Callbacks - Throttles
        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.Throttles.SetDesiredImageRendersPerSecond(this.ImageRendersPerSecond.Value);
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        private void ResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.Throttles.ResetToDefaults();
            this.ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            this.ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
        }
        #endregion

        #region Callbacks - Differencing
        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.DifferenceThreshold = Constant.ImageValues.DifferenceThresholdDefault;
            this.DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
        }

        private void DifferenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.DifferenceThreshold = (byte)this.DifferenceThreshold.Value;
            this.DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
        }
        #endregion

        //#region Callbacks - Detection and Bounding Boxsettings
        //private void ResetDetections_Click(object sender, RoutedEventArgs e)
        //{
        //    this.CheckBoxBoundingBoxAnnotate.IsChecked = true;
        //    this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked = false;
        //    this.BoundingBoxDisplayThresholdSlider.IsEnabled = true;
        //    this.timelapseState.BoundingBoxDisplayThresholdResetToDefault();
        //    this.BoundingBoxDisplayThresholdSlider.Value = this.timelapseState.BoundingBoxDisplayThreshold;
        //}

        //private void BoundingBoxDisplayThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    if (!(sender is Slider slider))
        //    {
        //        return;
        //    }
        //    this.BoundingBoxThresholdDisplayValue.Text = slider.Value.ToString("0.00");
        //    if (slider.Value == 0)
        //    {
        //        this.BoundingBoxThresholdDisplayText.Text = "This setting will display all bounding boxes";
        //    }
        //    else if (slider.Value == 1)
        //    {
        //        this.BoundingBoxThresholdDisplayText.Text = "This setting will never display bounding boxes";
        //    }
        //    else
        //    {
        //        this.BoundingBoxThresholdDisplayText.Text = "Always display bounding boxes above this confidence threshold";
        //    }
        //    this.timelapseState.BoundingBoxDisplayThreshold = slider.Value;
        //}

        //private void CheckBoxBounidngBoxColorBlindRinedlyColors_Click(object sender, RoutedEventArgs e)
        //{
        //    this.timelapseState.BoundingBoxColorBlindFriendlyColors = this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked == true;
        //}

        //private void CheckBoxBounidngBoxAnnotate_Click(object sender, RoutedEventArgs e)
        //{
        //    this.timelapseState.BoundingBoxAnnotate = this.CheckBoxBoundingBoxAnnotate.IsChecked == true;
        //}
        //#endregion

        #region Callbacks - Episode searching threshold
        private void SliderSetEpisodeMaxRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.timelapseState.EpisodeMaxRangeToSearch = Convert.ToInt32(this.SliderSetEpisodeMaxRange.Value);
            this.SetSliderSetEpisodeMaxRangeFeedack(this.timelapseState.EpisodeMaxRangeToSearch);
            Episodes.Reset();
        }

        // Reset theEpisode searching threshold to the amount specified below;
        private void ResetSliderSetEpisodeMaxRange_Click(object sender, RoutedEventArgs e)
        {
            // As a side effect, this will invoke the above ValueChanged method which sets the state and provides feedback
            this.SliderSetEpisodeMaxRange.Value = Constant.EpisodeDefaults.DefaultRangeToSearch;
            Episodes.Reset();
        }

        private void SetSliderSetEpisodeMaxRangeFeedack(int episodeThreshold)
        {
            TextEpisodeFeedback.Text = String.Format("Check up to {0} surrounding files to determine the episode range", episodeThreshold);
            this.SliderSetEpisodeMaxRange.ToolTip = episodeThreshold;
        }

        #endregion

        #region Callbacks - Maxium zoom
        // Callback: The user has changed the maximum zoom value
        private void MaxZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.markableCanvas.ZoomMaximum = (int)this.MaxZoom.Value;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.ResetMaximumZoom();
            this.MaxZoom.Value = this.markableCanvas.ZoomMaximum;
            this.MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
        }
        #endregion

        #region Callback - Dialog Buttons

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }


        #endregion

    }
}
