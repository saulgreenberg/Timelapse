using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;
using RecognizerValues = Timelapse.Constant.RecognizerValues;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithRecognitionData.xaml
    /// </summary>
    public partial class PopulateFieldWithRecognitionData
    {
        #region Private Variables

        private readonly FileDatabase fileDatabase;
        // The Note, MultiLine, Decimal and FixedChoice controls that could hold the recognition data, in template order
        private readonly List<ControlRow> dataFieldControls;

        // Whether the data field combobox currently lists the Decimal fields and the FixedChoice fields (null if not yet filled)
        private (bool Decimal, bool Choice)? dataFieldComboBoxIncludes;

        // Whether the recognition data includes classification categories. If not, the Classification row is disabled
        private bool classificationCategoriesExist = true;

        // The bounding box used to show an example of the output: from the current file if possible, otherwise another selected file, otherwise made up
        private BoundingBox exampleBoundingBox;

        // Each lookup of a file's bounding boxes searches the detections, so only this many of the selected files are searched for an example
        private const int MaxFilesToSearchForExample = 100;

        // Used to remember the selections between invocations of the dialog
        // These are the default initial values, which will be modified as the user makes selections
        private static string dataFieldLabel = string.Empty;
        private static bool useBoundingBoxCoordinates = true;
        private static bool useDetectionConfidence = true;
        private static bool useDetectionCategory = true;
        private static bool useClassificationConfidence = true;
        private static bool useClassificationCategory = true;
        private static RecognitionFormatEnum useRecognitionFormat = RecognitionFormatEnum.PlainText;

        #endregion

        #region Constructor / Loaded
        public PopulateFieldWithRecognitionData(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            dataFieldControls = [];
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);
            // Collect the fields that could hold the recognition data.
            // Decimal fields are only listed in the combobox when a single Confidence is output as raw data,
            // and FixedChoice fields only when a single Detection or Classification category is output as raw data
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type is Control.Note or Control.MultiLine or Control.DecimalAny or Control.DecimalPositive or Control.FixedChoice)
                {
                    dataFieldControls.Add(control);
                }
            }

            if (false == dataFieldControls.Any(control => control.Type is Control.Note or Control.MultiLine))
            {
                PrimaryPanel.Visibility = Visibility.Collapsed;
                FeedbackPanel.Visibility = Visibility.Visible;
            }

            classificationCategoriesExist = fileDatabase.ClassificationCategoriesExist();

            // Recall the initial or last used settings of the various options
            // (the Classification checkboxes are cleared when there are no classification categories, as they can't be selected)
            CBBoundingBoxCoordinates.IsChecked = useBoundingBoxCoordinates;
            CBDetectionConfidence.IsChecked = useDetectionConfidence;
            CBDetectionCategory.IsChecked = useDetectionCategory;
            CBClassificationConfidence.IsChecked = classificationCategoriesExist && useClassificationConfidence;
            CBClassificationCategory.IsChecked = classificationCategoriesExist && useClassificationCategory;

            RBAsFormattedJson.IsChecked = useRecognitionFormat == RecognitionFormatEnum.FormattedJson;
            RBAsUnformattedJson.IsChecked = useRecognitionFormat == RecognitionFormatEnum.UnformattedJson;
            RBAsPlainText.IsChecked = useRecognitionFormat == RecognitionFormatEnum.PlainText;
            RBAsRaw.IsChecked = useRecognitionFormat == RecognitionFormatEnum.Raw;

            // The Classification row can't be used when the recognition data has no classification categories
            if (false == classificationCategoriesExist)
            {
                CBClassificationCategory.IsEnabled = false;
                CBClassificationConfidence.IsEnabled = false;
                CBClassificationCategory.Foreground = Brushes.DarkGray;
                CBClassificationConfidence.Foreground = Brushes.DarkGray;
                TextBlockClassificationLabel.Foreground = Brushes.DarkGray;
                TextBlockClassificationLabel.ToolTip = "Not available, as the recognition data has no classification categories";
            }

            SetExampleBoundingBox();
            UpdateRawDataStateAndDataFields();
        }
        #endregion

        #region Event Handlers

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore the null selection that occurs when the combobox items are cleared and refilled
            if (sender is ComboBox { SelectedItem: string label })
            {
                dataFieldLabel = label.Trim();
                SetPopulateButtonEnableState();
            }
        }

        // A data checkbox was clicked, which may change whether raw data can be output
        private void DataToOutput_Click(object sender, RoutedEventArgs e)
        {
            UpdateRawDataStateAndDataFields();
        }

        // An output format was clicked, which may change whether decimal fields can be selected
        private void OutputFormat_Click(object sender, RoutedEventArgs e)
        {
            UpdateRawDataStateAndDataFields();
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Start_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Start_ClickAsync()
        {
            RememberButtonState(); // So we remember this button state, as the dialog may be reused
            bool? isCompleted = await PopulateAsync().ConfigureAwait(true);
            if (isCompleted == null)
            {
                // The user cancelled the overwrite or Choice field mismatch warning, so stay in this dialog to choose differently
                return;
            }
            DialogResult = isCompleted;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RememberButtonState(); // So we remember this button state, as the dialog may be reused
            DialogResult = false;
        }
        #endregion

        #region Populate Logic
        // Returns true if the field was populated, false if it wasn't, or null if the user cancelled
        // the warning that existing values would be overwritten, or that some raw categories don't match a FixedChoice field's menu items
        private async Task<bool?> PopulateAsync()
        {
            bool result = false;
            Dictionary<long, string> fieldData = null;
            int overwriteCount = 0;
            BusyCancelIndicator.IsBusy = true;

            // We need to set up the parameters for the call to RecognitionsComputeFieldData as we can't access the UI elements from the background thread
            ControlRow controlToUpdate = dataFieldControls.First(control => control.Label == dataFieldLabel);
            string dataLabelToUpdate = controlToUpdate.DataLabel;
            RecognitionFormatEnum recognitionFormat = GetSelectedRecognitionFormat();
            bool boundingBoxCoordinates = CBBoundingBoxCoordinates.IsChecked == true;
            bool detectionConfidence = CBDetectionConfidence.IsChecked == true;
            bool detectionCategory = CBDetectionCategory.IsChecked == true;
            bool classificationConfidence = CBClassificationConfidence.IsChecked == true;
            bool classificationCategory = CBClassificationCategory.IsChecked == true;

            // For a Choice field, categories that match a menu item, ignoring case, get the menu's own spelling,
            // so they aren't reported as mismatches, nor counted as overwrites of the same item
            Choices choices = controlToUpdate.Type == Control.FixedChoice ? Choices.ChoicesFromJson(controlToUpdate.List) : null;
            List<string> mismatchedCategories = [];
            await Task.Run(() =>
            {
                fieldData = fileDatabase.RecognitionsComputeFieldData(dataLabelToUpdate,
                        recognitionFormat,
                        boundingBoxCoordinates,
                        detectionConfidence,
                        detectionCategory,
                        classificationConfidence,
                        classificationCategory,
                        Progress);
                if (choices != null && fieldData != null)
                {
                    mismatchedCategories = choices.UseChoiceSpellingAndGetMismatches(fieldData);
                }
                overwriteCount = fileDatabase.RecognitionsCountValuesToOverwrite(dataLabelToUpdate, fieldData);
            }, Token).ConfigureAwait(true);
            if (fieldData == null)
            {
                BusyCancelIndicator.IsBusy = false;
                return false;
            }

            // Warn the user if existing, different values in the field would be overwritten.
            // This comes before the Choice field warning, so cancelling it never leaves the template updated
            if (overwriteCount > 0)
            {
                BusyCancelIndicator.IsBusy = false;
                if (false == Dialogs.PopulateFieldOverwriteWarningDialog(this, controlToUpdate.Label, overwriteCount, fileDatabase.FileTable.RowCount))
                {
                    return null;
                }
                BusyCancelIndicator.IsBusy = true;
            }

            // A FixedChoice field only gets a single raw category: warn the user if some of them don't match its menu items
            if (controlToUpdate.Type == Control.FixedChoice)
            {
                string categoryKind = detectionCategory ? "detection" : "classification";
                if (mismatchedCategories.Count > 0)
                {
                    BusyCancelIndicator.IsBusy = false;
                    if (false == Dialogs.PopulateChoiceFieldWithMismatchedCategoriesDialog(this, controlToUpdate.Label, categoryKind, mismatchedCategories, out bool updateTemplate))
                    {
                        return null;
                    }
                    if (updateTemplate && false == GlobalReferences.MainWindow.TryAddChoicesToTemplate(dataLabelToUpdate, mismatchedCategories))
                    {
                        // Shouldn't happen, but if the template's choice list can't be read, it is left as is and the field is still populated
                        TracePrint.PrintMessage($"Could not add the recognition categories to the {controlToUpdate.Label} menu");
                    }
                    BusyCancelIndicator.IsBusy = true;
                }
            }

            await Task.Run(() =>
            {
                result = fileDatabase.RecognitionsWriteFieldData(dataLabelToUpdate, fieldData, Progress);
            }, Token).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;
            return result;
        }

        #endregion

        #region Example output
        // Find the bounding box to use in the example output. So that every checkbox visibly changes the example, the box must have
        // all the selectable data, i.e., a classification too when the recognition data has classification categories.
        // Use the current file's highest-confidence bounding box if it qualifies, otherwise that of the first selected file
        // (of those searched) whose box qualifies, otherwise a made-up one
        private void SetExampleBoundingBox()
        {
            ImageRow currentFile = GlobalReferences.MainWindow?.DataHandler?.ImageCache?.Current;
            if (currentFile != null && TryGetCompleteBoundingBox(currentFile, out exampleBoundingBox))
            {
                TextBlockExampleSource.Text = $"From the current file ({currentFile.File}), using your current selections:";
                return;
            }

            foreach (ImageRow file in fileDatabase.FileTable.Take(MaxFilesToSearchForExample))
            {
                if (TryGetCompleteBoundingBox(file, out exampleBoundingBox))
                {
                    TextBlockExampleSource.Text = $"From the file {file.File}, using your current selections:";
                    return;
                }
            }

            // None of the files searched has a qualifying bounding box, so make one up from this image set's categories
            fileDatabase.CreateDetectionCategoriesDictionaryIfNeeded();
            string detectionCategory = (fileDatabase.detectionCategoriesDictionary ?? [])
                .Select(category => category.Key)
                .FirstOrDefault(category => category != RecognizerValues.NoDetectionCategory) ?? "1";
            List<KeyValuePair<string, string>> classifications = [];
            if (classificationCategoriesExist)
            {
                fileDatabase.CreateClassificationCategoriesDictionaryIfNeeded();
                string classificationCategory = (fileDatabase.classificationCategoriesDictionary ?? [])
                    .Select(category => category.Value)
                    .FirstOrDefault(category => false == string.IsNullOrWhiteSpace(category)) ?? "deer";
                classifications.Add(new(classificationCategory, "0.912"));
            }
            exampleBoundingBox = new(0.125f, 0.340f, 0.250f, 0.180f, 0.987f, 0, detectionCategory, string.Empty, classifications);
            TextBlockExampleSource.Text = classificationCategoriesExist
                ? "A made-up example (no bounding box with a classification was found in your selected files), using your current selections:"
                : "A made-up example (no bounding boxes were found in your selected files), using your current selections:";
        }

        // Get the file's highest-confidence bounding box, returning true only if it has all the selectable data
        private bool TryGetCompleteBoundingBox(ImageRow file, out BoundingBox boundingBox)
        {
            boundingBox = TimelapseWindow.GetHighestConfidenceBoundingBoxForCurrentFile(file.ID);
            return boundingBox != null && (false == classificationCategoriesExist || boundingBox.Classifications.Count >= 1);
        }

        // Show the output that the current selections would record for the example bounding box
        private void UpdateExampleOutput()
        {
            if (exampleBoundingBox == null)
            {
                return;
            }
            if (GetSelectedDataCheckboxCount() == 0)
            {
                TextBoxExample.Text = "(Nothing to output: select at least one item in Choose data to output)";
                return;
            }
            string example = fileDatabase.RecognitionsFormatBoundingBox(exampleBoundingBox,
                GetSelectedRecognitionFormat(),
                CBBoundingBoxCoordinates.IsChecked == true,
                CBDetectionConfidence.IsChecked == true,
                CBDetectionCategory.IsChecked == true,
                CBClassificationConfidence.IsChecked == true,
                CBClassificationCategory.IsChecked == true);
            TextBoxExample.Text = string.IsNullOrEmpty(example)
                ? "(Empty, as this file's bounding box has no data for the selected items, e.g., no classification)"
                : example;
        }
        #endregion

        #region Helpers

        // Raw data can only be output when a single data checkbox is selected.
        // Decimal fields can only hold raw data from a single Confidence, so only then are they listed as data fields.
        // Likewise, FixedChoice fields are only listed when the output is raw data from a single Detection or Classification category.
        private void UpdateRawDataStateAndDataFields()
        {
            int selectedCount = GetSelectedDataCheckboxCount();
            RBAsRaw.IsEnabled = selectedCount == 1;
            RBAsRaw.Foreground = RBAsRaw.IsEnabled ? Brushes.Black : Brushes.DarkGray;

            // Raw data no longer applies once several checkboxes are selected, so revert to the default label:value format
            if (RBAsRaw.IsChecked == true && selectedCount > 1)
            {
                RBAsPlainText.IsChecked = true;
            }

            bool isRawData = RBAsRaw.IsEnabled && RBAsRaw.IsChecked == true;
            bool includeDecimalFields = isRawData && (CBDetectionConfidence.IsChecked == true || CBClassificationConfidence.IsChecked == true);
            bool includeChoiceFields = isRawData && (CBDetectionCategory.IsChecked == true || CBClassificationCategory.IsChecked == true);
            FillDataFieldComboBox(includeDecimalFields, includeChoiceFields);
            // Hidden rather than Collapsed, so the notes keep their space and the layout doesn't shift as they come and go
            TextBlockDecimalFieldsNote.Visibility = includeDecimalFields && dataFieldControls.Any(control => control.Type is Control.DecimalAny or Control.DecimalPositive)
                ? Visibility.Visible
                : Visibility.Hidden;
            TextBlockChoiceFieldsNote.Visibility = includeChoiceFields && dataFieldControls.Any(control => control.Type is Control.FixedChoice)
                ? Visibility.Visible
                : Visibility.Hidden;
            SetPopulateButtonEnableState();
            UpdateExampleOutput();
        }

        // Fill the combobox with the Note and MultiLine fields, and optionally the Decimal fields or the FixedChoice fields,
        // keeping the current data field selected if it is still listed
        private void FillDataFieldComboBox(bool includeDecimalFields, bool includeChoiceFields)
        {
            if (dataFieldComboBoxIncludes == (includeDecimalFields, includeChoiceFields))
            {
                return;
            }
            dataFieldComboBoxIncludes = (includeDecimalFields, includeChoiceFields);

            string previousDataFieldLabel = dataFieldLabel;
            ComboBoxSelectNoteField.Items.Clear();
            foreach (ControlRow control in dataFieldControls)
            {
                if (control.Type is Control.Note or Control.MultiLine ||
                    (includeDecimalFields && control.Type is Control.DecimalAny or Control.DecimalPositive) ||
                    (includeChoiceFields && control.Type is Control.FixedChoice))
                {
                    ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }

            if (ComboBoxSelectNoteField.Items.Contains(previousDataFieldLabel))
            {
                ComboBoxSelectNoteField.SelectedItem = previousDataFieldLabel;
            }
            else if (ComboBoxSelectNoteField.Items.Count == 1)
            {
                ComboBoxSelectNoteField.SelectedIndex = 0;
            }
            else
            {
                dataFieldLabel = string.Empty;
            }
        }

        private int GetSelectedDataCheckboxCount()
        {
            return new[] { CBBoundingBoxCoordinates, CBDetectionCategory, CBDetectionConfidence, CBClassificationCategory, CBClassificationConfidence }
                .Count(checkBox => checkBox.IsChecked == true);
        }

        private RecognitionFormatEnum GetSelectedRecognitionFormat()
        {
            return RBAsFormattedJson.IsChecked == true
                ? RecognitionFormatEnum.FormattedJson
                : RBAsUnformattedJson.IsChecked == true
                    ? RecognitionFormatEnum.UnformattedJson
                    : RBAsRaw.IsChecked == true
                        ? RecognitionFormatEnum.Raw
                        : RecognitionFormatEnum.PlainText;
        }

        // Record the current button states to the static variables so that they can be recalled next time the dialog is opened
        private void RememberButtonState()
        {
            useBoundingBoxCoordinates = CBBoundingBoxCoordinates.IsChecked == true;
            useDetectionConfidence = CBDetectionConfidence.IsChecked == true;
            useDetectionCategory = CBDetectionCategory.IsChecked == true;
            // Keep the remembered Classification settings when they couldn't be used, for the next file with classifications
            if (classificationCategoriesExist)
            {
                useClassificationConfidence = CBClassificationConfidence.IsChecked == true;
                useClassificationCategory = CBClassificationCategory.IsChecked == true;
            }

            useRecognitionFormat = GetSelectedRecognitionFormat();
        }

        // Enable or disable the Start/Done button based on the current selections
        private void SetPopulateButtonEnableState()
        {
            // Disable the Start/Done button if there is no selected data field
            if (string.IsNullOrEmpty(dataFieldLabel))
            {
                StartDoneButton.IsEnabled = false;
                return;
            }

            // Enable the Start/Done button if at least one of the checkboxes is selected, or exactly one if outputting raw data
            int selectedCount = GetSelectedDataCheckboxCount();
            StartDoneButton.IsEnabled = RBAsRaw.IsChecked == true
                ? selectedCount == 1
                : selectedCount >= 1;
        }
        #endregion
    }
}
