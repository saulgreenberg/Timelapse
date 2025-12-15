using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Dialog for populating Note and Multiline fields from metadata.
    /// Includes option to clear fields if metadata is not found.
    /// </summary>
    public partial class FileMetadataPopulateAll
    {
        #region Private variables
        private bool clearIfNoMetadata;
        #endregion

        #region Constructor
        public FileMetadataPopulateAll(Window owner, FileDatabase fileDatabase, DataEntryHandler dataHandler, string filePath)
            : base(owner, fileDatabase, dataHandler, filePath)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(PopulateAllMessage);
        }
        #endregion

        #region Abstract method implementations
        protected override Task<ObservableCollection<FileMetadataFeedbackRow>> PopulateMetadataAsync(Enums.MetadataToolEnum metadataToolSelected, bool previewOnly)
            => PopulateFieldsAsync(metadataToolSelected, previewOnly);

        protected override System.Windows.Controls.DataGrid GetFeedbackGrid() => FeedbackGrid;
        protected override System.Windows.Controls.Panel GetFeedbackPanel() => FeedbackPanel;
        protected override Controls.FileMetadataGrid GetMetadataGrid() => MetadataGrid;
        protected override Controls.BusyCancelIndicator GetBusyCancelIndicator() => BusyCancelIndicator;
        protected override System.Windows.Controls.TextBlock GetPopulatingMessage() => PopulatingMessage;
        protected override System.Windows.Controls.Button GetStartDoneButton() => StartDoneButton;
        protected override System.Windows.Controls.Button GetPreviewButton() => PreviewButton;
        protected override System.Windows.Controls.Button GetBackButton() => BackButton;
        protected override System.Windows.Controls.Button GetCancelButton() => CancelButton;
        protected override System.Windows.Controls.RadioButton GetShowEverythingRadioButton() => ShowEverythingRadioButton;

        protected override void ShowHideDialogSpecificElements(bool showMetadataGrid)
        {
            SkipIfNoMetadata.Visibility = showMetadataGrid ? Visibility.Visible : Visibility.Collapsed;
            ClearIfNoMetadata.Visibility = showMetadataGrid ? Visibility.Visible : Visibility.Collapsed;
            TBSkipClearLabel.Visibility = showMetadataGrid ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region Window event handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateAllMessage.BuildContentFromProperties();

            // Set up progress handler
            InitializeProgressHandler(BusyCancelIndicator);

            // Configure the metadata grid
            MetadataGrid.viewModel.RootPath = FileDatabase.RootPathToDatabase;
            MetadataGrid.viewModel.FilePath = FilePath;
            MetadataGrid.ImageMetadataFilter = ImageMetadataFiltersEnum.AllMetadata;

            // Construct a dictionary of the available data fields
            // Include Note, MultiLine, numeric types, Flag, and DateTime types
            // The FileMetadataGrid will filter these per-row based on metadata value type
            Dictionary<string, string> collectLabels = [];
            Dictionary<string, string> collectControlTypes = [];
            Dictionary<string, Choices> collectChoices = [];
            foreach (ControlRow control in FileDatabase.Controls)
            {
                if (control.Visible == false)
                {
                    // Don't show controls if they are hidden in the UI
                    continue;
                }
                collectLabels.Add(control.DataLabel, control.Label);
                collectControlTypes.Add(control.DataLabel, control.Type);

                // For FixedChoice and MultiChoice controls, parse and store the choices
                if (control.Type == Constant.Control.FixedChoice || control.Type == Constant.Control.MultiChoice)
                {
                    Choices choices = Choices.ChoicesFromJson(control.List);
                    collectChoices.Add(control.DataLabel, choices);
                }
            }

            // Setting DictDataLabel_Label will result in desired side effects in the FileMetadataGrid user control
            MetadataGrid.DictDataLabel_Label = collectLabels;
            MetadataGrid.SetControlTypeMapping(collectControlTypes);
            MetadataGrid.SetChoicesMapping(collectChoices);

            // Refresh the grid to apply the filtering with the control type mapping
            MetadataGrid.Refresh();

            MetadataGrid.SelectedMetadata.CollectionChanged += SelectedMetadata_CollectionChanged;
        }

        #endregion

        #region Change Notifications
        private void SelectedMetadata_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Enable or disable the Populate and Preview buttons to match if any items are in the selectedMetadataList
            bool hasSelection = MetadataGrid.SelectedMetadata is { Count: > 0 };
            StartDoneButton.IsEnabled = hasSelection;
            PreviewButton.IsEnabled = hasSelection;
        }
        #endregion

        #region Button callbacks - implemented in base class
        // Preview_Click, Start_Click, Back_Click, Done_Click, CancelButton_Click are in FileMetadataPopulateBase
        #endregion

        #region Radio button callbacks
        private void MetadataWriteRadioButton_Changed(object sender, RoutedEventArgs e)
        {
            clearIfNoMetadata = (ClearIfNoMetadata?.IsChecked == true);
        }

        // FeedbackDisplayRadioButton_Changed is implemented in base class
        #endregion

        #region Populate fields from metadata
        private async Task<ObservableCollection<FileMetadataFeedbackRow>> PopulateFieldsAsync(MetadataToolEnum metadataToolSelected, bool previewOnly = false)
        {
            ObservableCollection<FileMetadataFeedbackRow> feedbackData = [];

            if (MetadataGrid.SelectedMetadata.Count == 0)
            {
                feedbackData.Clear();
                feedbackData.Add(new("Nothing was selected", "", "No changes were made", isSuccessRow: false));
                return feedbackData;
            }

            return await Task.Run(() =>
            {
                List<ColumnTuplesWithWhere> imagesToUpdate = [];
                double totalImages = FileDatabase.CountAllCurrentlySelectedFiles;
                string[] tags = MetadataGrid.SelectedTags;

                for (int imageIndex = 0; imageIndex < totalImages; ++imageIndex)
                {
                    if (IsCancellationRequested())
                    {
                        feedbackData.Clear();
                        feedbackData.Add(new("Cancelled", "", "No changes were made", isSuccessRow: false));
                        return feedbackData;
                    }

                    ImageRow image = FileDatabase.FileTable[imageIndex];

                    var metadata = metadataToolSelected == MetadataToolEnum.MetadataExtractor
                        ? ImageMetadataDictionary.LoadMetadata(image.GetFilePath(FileDatabase.RootPathToImages))
                        : MetadataGrid.ExifToolManager.FetchExifFrom(image.GetFilePath(FileDatabase.RootPathToImages), tags);

                    if (IsReadyToRefresh())
                    {
                        int percentDone = Convert.ToInt32(imageIndex / totalImages * 100.0);
                        ReportProgress(new(percentDone, $"{imageIndex}/{totalImages} images. Processing {image.File}", true, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);
                    }

                    foreach (var item in MetadataGrid.SelectedMetadata)
                    {
                        string metadataTag = item.MetadataTag;
                        string dataLabelToUpdate = item.DataLabel;
                        string controlType = item.Type;

                        if (!metadata.TryGetValue(metadataTag, out var value))
                        {
                            // Metadata not found in the file
                            if (clearIfNoMetadata)
                            {
                                if (!previewOnly)
                                {
                                    List<ColumnTuple> clearField = [new(dataLabelToUpdate, string.Empty)];
                                    imagesToUpdate.Add(new(clearField, image.ID));
                                }
                                feedbackData.Add(new(image.File, metadataTag, "⚠No metadata found - data field cleared", isSuccessRow: false));
                            }
                            else
                            {
                                feedbackData.Add(new(image.File, metadataTag, "⚠No metadata found - data field unchanged", isSuccessRow: false));
                            }
                            continue;
                        }

                        string metadataValue = value.Value;
                        if (false == CheckDataType(metadataValue, controlType, dataLabelToUpdate))
                        {
                            if (clearIfNoMetadata)
                            {
                                if (!previewOnly)
                                {
                                    List<ColumnTuple> clearField = [new(dataLabelToUpdate, string.Empty)];
                                    imagesToUpdate.Add(new(clearField, image.ID));
                                }
                                feedbackData.Add(new(image.File, metadataTag, $"⚠Metadata value {metadataValue} is not {controlType} - data field cleared", isSuccessRow: false));
                            }
                            else
                            {
                                feedbackData.Add(new(image.File, metadataTag, $"⚠Metadata value {metadataValue} is not {controlType} - data field unchanged", isSuccessRow: false));
                            }
                            continue;
                        }


                        if (!previewOnly)
                        {
                            ColumnTuplesWithWhere imageUpdate = new([new(dataLabelToUpdate, metadataValue)], image.ID);
                            imagesToUpdate.Add(imageUpdate);
                        }
                        feedbackData.Add(new(image.File, metadataTag, metadataValue, isSuccessRow: true));
                    }
                }

                if (!previewOnly)
                {
                    isAnyDataUpdated = true;
                    ReportProgress(new(100, $"Writing metadata for {totalImages} files. Please wait...", false, true));
                    Thread.Sleep(ThrottleValues.RenderingBackoffTime);
                    FileDatabase.UpdateFiles(imagesToUpdate);
                }
                return feedbackData;
            }, GetCancellationToken()).ConfigureAwait(true);
        }

        private bool CheckDataType(string value, string controlType, string dataLabel)
        {
            if (controlType is Constant.Control.Note or Constant.Control.MultiLine)
            {
                return true;
            }

            if (controlType == Constant.Control.AlphaNumeric)
            {
                return IsCondition.IsAlphaNumeric(value);
            }

            if (controlType is Constant.Control.IntegerAny or Constant.Control.IntegerPositive or Constant.Control.Counter)
            {
                if (value == string.Empty)
                {
                    // We allow empty values in integer fields
                    return true;
                }
                if (false == int.TryParse(value, out int intValue))
                {
                    return false;
                }
                if (controlType is Constant.Control.IntegerPositive or Constant.Control.Counter && intValue < 0)
                {
                    return false;
                }

                return true;
            }

            if (controlType is Constant.Control.DecimalAny or Constant.Control.DecimalPositive)
            {
                if (value == string.Empty)
                {
                    // We allow empty values in decimal fields
                    return true;
                }
                if (false == double.TryParse(value, out double doubleValue))
                {
                    return false;
                }
                if (controlType == Constant.Control.DecimalPositive && doubleValue < 0)
                {
                    return false;
                }

                return true;
            }

            if (controlType == Constant.Control.Flag)
            {
                return value is "True" or "False";
            }

            if (controlType is DatabaseColumn.DateTime or Constant.Control.DateTime_ or Constant.Control.Date_ or Constant.Control.Time_)
            {
                return DateTime.TryParse(value, out _);
            }

            if (controlType is Constant.Control.FixedChoice or Constant.Control.MultiChoice)
            {
                // Get the control to access its choice list
                ControlRow control = FileDatabase.Controls.FirstOrDefault(c => c.DataLabel == dataLabel);
                if (control == null)
                {
                    return false;
                }

                // Parse the choice list from the control's List property (JSON)
                Choices choices = Choices.ChoicesFromJson(control.List);

                // If empty strings are allowed and the value is empty, return true
                if (choices.IncludeEmptyChoice && string.IsNullOrEmpty(value))
                {
                    return true;
                }

                // Check if the value is in the choice list
                return choices.ChoiceList.Contains(value);
            }

            return true;
        }
        #endregion
    }
}
