using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Enums;
using Timelapse.State;
using Timelapse.Util;
using MarkableCanvas = Timelapse.Images.MarkableCanvas;

namespace Timelapse.Dialog
{
    public partial class AdvancedTimelapseOptions
    {
        #region Private Variables
        private readonly MarkableCanvas markableCanvas;
        private readonly TimelapseState timelapseState;
        #endregion

        #region Constructor and Loaded
        public AdvancedTimelapseOptions(TimelapseState timelapseState, MarkableCanvas markableCanvas, Window owner)
        {
            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            Message.BuildContentFromProperties();

            // Check the arguments for null 
            ThrowIf.IsNullArgument(timelapseState, nameof(timelapseState));
            ThrowIf.IsNullArgument(markableCanvas, nameof(markableCanvas));

            this.markableCanvas = markableCanvas;
            this.timelapseState = timelapseState;

            // Metadata Populate On Load
            CheckBoxEnablePopulateMetadataOnLoad.IsChecked = this.timelapseState.ImageMetadataAskOnLoad;

            // Tab Order - set to current state.
            CheckBoxTabOrderDateTime.IsChecked = this.timelapseState.TabOrderIncludeDateTime;
            CheckBoxTabOrderDeleteFlag.IsChecked = this.timelapseState.TabOrderIncludeDeleteFlag;

            // Deletion Management
            RadioButtonDeletionManagement_Set(this.timelapseState.DeleteFolderManagement);

            // CSV Include Folder column option
            switch (this.timelapseState.CSVDateTimeOptions)
            {
                // CSV DateTime options
                case CSVDateTimeOptionsEnum.DateAndTimeColumns:
                    RadioButtonCSVDateAndTimeColumns.IsChecked = true;
                    break;
                case CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator:
                    RadioButtonCSVLocalDateTimeColumn.IsChecked = true;
                    break;
                case CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn:
                    RadioButtonCSVLocalDateTimeColumnWithoutT.IsChecked = true;
                    break;
            }

            CheckBoxCSVInsertSpaceBeforeDates.IsChecked = this.timelapseState.CSVInsertSpaceBeforeDates;
            CheckBoxCSVIncludeFolderColumn.IsChecked = this.timelapseState.CSVIncludeFolderColumn;

            // Throttles
            ImageRendersPerSecond.Minimum = ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound;
            ImageRendersPerSecond.Maximum = ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound;
            ImageRendersPerSecond.Value = this.timelapseState.Throttles.DesiredImageRendersPerSecond;
            ImageRendersPerSecond.ValueChanged += ImageRendersPerSecond_ValueChanged;
            ImageRendersPerSecond.ToolTip = this.timelapseState.Throttles.DesiredImageRendersPerSecond;

            // The EpisodeMaxRangeToSearch Threshold values 
            SliderSetEpisodeMaxRange.Value = this.timelapseState.EpisodeMaxRangeToSearch;
            SetSliderSetEpisodeMaxRangeFeedack(this.timelapseState.EpisodeMaxRangeToSearch);
            SliderSetEpisodeMaxRange.Maximum = EpisodeDefaults.MaximumRangeToSearch;
            SliderSetEpisodeMaxRange.Minimum = 50;

            // The Max Zoom Value
            MaxZoom.Value = this.markableCanvas.ZoomMaximum;
            MaxZoom.ToolTip = this.markableCanvas.ZoomMaximum;
            MaxZoom.Maximum = Constant.MarkableCanvas.ImageZoomMaximumRangeAllowed;
            MaxZoom.Minimum = 2;

            // Image Differencing Thresholds
            DifferenceThreshold.Value = this.timelapseState.DifferenceThreshold;
            DifferenceThreshold.ToolTip = this.timelapseState.DifferenceThreshold;
            DifferenceThreshold.Maximum = ImageValues.DifferenceThresholdMax;
            DifferenceThreshold.Minimum = ImageValues.DifferenceThresholdMin;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region Callbacks - Populate Metadata on Load
        private void ResetPopulateMetadataDefaults_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxEnablePopulateMetadataOnLoad.IsChecked = false;
            timelapseState.ImageMetadataAskOnLoad = false;
        }
        private void CheckBoxEnablePopulateMetadataOnLoad_Click(object sender, RoutedEventArgs e)
        {
            timelapseState.ImageMetadataAskOnLoad = CheckBoxEnablePopulateMetadataOnLoad.IsChecked == true;
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
                    timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ManualDelete;
                    break;
                case "RadioButtonAskToDelete":
                    timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.AskToDeleteOnExit;
                    break;
                case "RadioButtonAutoDeleteOnExit":
                    timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.AutoDeleteOnExit;
                    break;
                case "RadioButtonImmediatelyDelete":
                    timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ImmediatelyDelete;
                    break;
            }
        }

        // Reset to the Default, i.e. manual deletion
        private void ResetDeletedFileManagement_Click(object sender, RoutedEventArgs e)
        {
            RadioButtonManualDelete.IsChecked = true;
            timelapseState.DeleteFolderManagement = DeleteFolderManagementEnum.ManualDelete;
        }

        private void RadioButtonDeletionManagement_Set(DeleteFolderManagementEnum deleteFolderManagement)
        {
            switch (deleteFolderManagement)
            {
                case DeleteFolderManagementEnum.ManualDelete:
                    RadioButtonManualDelete.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.AskToDeleteOnExit:
                    RadioButtonAskToDelete.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.AutoDeleteOnExit:
                    RadioButtonAutoDeleteOnExit.IsChecked = true;
                    break;
                case DeleteFolderManagementEnum.ImmediatelyDelete:
                    RadioButtonImmediatelyDelete.IsChecked = true;
                    break;
            }
        }
        #endregion

        #region Callback - Reset CSV File Defaults
        private void ResetCSVDefaults_Click(object sender, RoutedEventArgs e)
        {
            RadioButtonCSVLocalDateTimeColumnWithoutT.IsChecked = true;
            CheckBoxCSVInsertSpaceBeforeDates.IsChecked = true;
            CheckBoxCSVIncludeFolderColumn.IsChecked = true;
            SetCSVOptions();
        }

        private void RadioButtonCSVOptions_Click(object sender, RoutedEventArgs e)
        {
            SetCSVOptions();
        }

        private void CheckBoxCSVInsertSpaceBeforeDate_Click(object sender, RoutedEventArgs e)
        {
            SetCSVOptions();
        }

        private void CheckBoxIncludeFolderColumn_Click(object sender, RoutedEventArgs e)
        {
            SetCSVOptions();
        }

        private void SetCSVOptions()
        {
            // Note that some of these are now defunct
            if (RadioButtonCSVDateAndTimeColumns.IsChecked == true)
            {
                timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateAndTimeColumns;
            }
            else if (RadioButtonCSVLocalDateTimeColumn.IsChecked == true)
            {
                timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator;
            }
            else if (RadioButtonCSVLocalDateTimeColumnWithoutT.IsChecked == true)
            {
                timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn;
            }
            else //if (this.RadioButtonCSVUTCWithOffsetDateTimeColumn.IsChecked IsChecked == true)
            {
                // This is now defunct and should not be activated
                timelapseState.CSVDateTimeOptions = CSVDateTimeOptionsEnum.DateTimeUTCWithOffset;
            }
            timelapseState.CSVInsertSpaceBeforeDates = CheckBoxCSVInsertSpaceBeforeDates.IsChecked == true;
            timelapseState.CSVIncludeFolderColumn = CheckBoxCSVIncludeFolderColumn.IsChecked == true;
        }
        #endregion

        #region Callbacks - Tab Controls to Include / Exclude
        private void CheckBoxTabOrder_Click(object sender, RoutedEventArgs e)
        {
            SetTabOrder();
        }

        private void ResetTabOrder_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxTabOrderDateTime.IsChecked = false;
            CheckBoxTabOrderDeleteFlag.IsChecked = false;
            SetTabOrder();
        }

        private void SetTabOrder()
        {
            timelapseState.TabOrderIncludeDateTime = CheckBoxTabOrderDateTime.IsChecked == true;
            timelapseState.TabOrderIncludeDeleteFlag = CheckBoxTabOrderDeleteFlag.IsChecked == true;
        }
        #endregion

        #region Callbacks - Throttles
        private void ImageRendersPerSecond_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            timelapseState.Throttles.SetDesiredImageRendersPerSecond(ImageRendersPerSecond.Value);
            ImageRendersPerSecond.ToolTip = timelapseState.Throttles.DesiredImageRendersPerSecond;
        }

        private void ResetThrottle_Click(object sender, RoutedEventArgs e)
        {
            timelapseState.Throttles.ResetToDefaults();
            ImageRendersPerSecond.Value = timelapseState.Throttles.DesiredImageRendersPerSecond;
            ImageRendersPerSecond.ToolTip = timelapseState.Throttles.DesiredImageRendersPerSecond;
        }
        #endregion

        #region Callbacks - Differencing
        private void ResetImageDifferencingButton_Click(object sender, RoutedEventArgs e)
        {
            timelapseState.DifferenceThreshold = ImageValues.DifferenceThresholdDefault;
            DifferenceThreshold.Value = timelapseState.DifferenceThreshold;
            DifferenceThreshold.ToolTip = timelapseState.DifferenceThreshold;
        }

        private void DifferenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            timelapseState.DifferenceThreshold = (byte)DifferenceThreshold.Value;
            DifferenceThreshold.ToolTip = timelapseState.DifferenceThreshold;
        }
        #endregion

        #region Callbacks - Episode searching threshold
        private void SliderSetEpisodeMaxRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            timelapseState.EpisodeMaxRangeToSearch = Convert.ToInt32(SliderSetEpisodeMaxRange.Value);
            SetSliderSetEpisodeMaxRangeFeedack(timelapseState.EpisodeMaxRangeToSearch);
            Episodes.Episodes.Reset();
        }

        // Reset theEpisode searching threshold to the amount specified below;
        private void ResetSliderSetEpisodeMaxRange_Click(object sender, RoutedEventArgs e)
        {
            // As a side effect, this will invoke the above ValueChanged method which sets the state and provides feedback
            SliderSetEpisodeMaxRange.Value = EpisodeDefaults.DefaultRangeToSearch;
            Episodes.Episodes.Reset();
        }

        private void SetSliderSetEpisodeMaxRangeFeedack(int episodeThreshold)
        {
            TextEpisodeFeedback.Text = $"Check up to {episodeThreshold} surrounding files to determine the episode range";
            SliderSetEpisodeMaxRange.ToolTip = episodeThreshold;
        }
        #endregion

        #region Callbacks - Maxium zoom
        // Callback: The user has changed the maximum zoom value
        private void MaxZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            markableCanvas.ZoomMaximum = (int)MaxZoom.Value;
            MaxZoom.ToolTip = markableCanvas.ZoomMaximum;
        }

        // Reset the maximum zoom to the amount specified in Max Zoom;
        private void ResetMaxZoom_Click(object sender, RoutedEventArgs e)
        {
            markableCanvas.ResetMaximumZoom();
            MaxZoom.Value = markableCanvas.ZoomMaximum;
            MaxZoom.ToolTip = markableCanvas.ZoomMaximum;
        }
        #endregion

        #region Callback - Dialog Buttons

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion
    }
}
