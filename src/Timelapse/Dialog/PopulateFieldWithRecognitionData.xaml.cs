using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithRecognitionData.xaml
    /// </summary>
    public partial class PopulateFieldWithRecognitionData
    {
        #region Private Variables

        private readonly FileDatabase fileDatabase;
        // The Note, MultiLine and Decimal controls that could hold the recognition data, in template order
        private readonly List<ControlRow> dataFieldControls;

        // Whether the data field combobox currently lists the Decimal fields (null if not yet filled)
        private bool? dataFieldComboBoxIncludesDecimalFields;

        // Used to remember the selections between invocations of the dialog
        // These are the default initial values, which will be modified as the user makes selections
        private static string dataFieldLabel = string.Empty;
        private static bool useBoundingBoxCoordinates = true;
        private static bool useDetectionConfidence = true;
        private static bool useDetectionCategory = true;
        private static bool useClassificationConfidence = true;
        private static bool useClassificationCategory = true;
        private static bool useClassificationOnly = true;
        private static RecognitionFormatEnum useRecognitionFormat = RecognitionFormatEnum.FormattedJson;

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
            Debug.Print(dataFieldLabel);
            // Collect the fields that could hold the recognition data.
            // Decimal fields are only listed in the combobox when a single Confidence is output as raw data
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type is Control.Note or Control.MultiLine or Control.DecimalAny or Control.DecimalPositive)
                {
                    dataFieldControls.Add(control);
                }
            }

            if (false == dataFieldControls.Any(control => control.Type is Control.Note or Control.MultiLine))
            {
                PrimaryPanel.Visibility = Visibility.Collapsed;
                FeedbackPanel.Visibility = Visibility.Visible;
            }

            // Recall the initial or last used settings of the various options
            CBBoundingBoxCoordinates.IsChecked = useBoundingBoxCoordinates;
            CBDetectionConfidence.IsChecked = useDetectionConfidence;
            CBDetectionCategory.IsChecked = useDetectionCategory;
            CBClassificationConfidence.IsChecked = useClassificationConfidence;
            CBClassificationCategory.IsChecked = useClassificationCategory;

            RBAsFormattedJson.IsChecked = useRecognitionFormat == RecognitionFormatEnum.FormattedJson;
            RBAsUnformattedJson.IsChecked = useRecognitionFormat == RecognitionFormatEnum.UnformattedJson;
            RBAsPlainText.IsChecked = useRecognitionFormat == RecognitionFormatEnum.PlainText;
            RBAsRaw.IsChecked = useRecognitionFormat == RecognitionFormatEnum.Raw;
            RBClassificationOny.IsChecked = useClassificationOnly;
            RBChooseFields.IsChecked = useClassificationOnly == false;
            EnableOrDisableGroupBoxes(RBChooseFields.IsChecked == true);

            RBClassificationOny.Checked += RBClassificationOnly_Checked;
            RBChooseFields.Checked += RBChooseFields_Checked;
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

        private void RBClassificationOnly_Checked(object sender, RoutedEventArgs e)
        {
            EnableOrDisableGroupBoxes(false);
        }

        private void RBChooseFields_Checked(object sender, RoutedEventArgs e)
        {
            EnableOrDisableGroupBoxes(true);
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
            bool isCompleted = await PopulateAsync().ConfigureAwait(true);
            DialogResult = isCompleted;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RememberButtonState(); // So we remember this button state, as the dialog may be reused
            DialogResult = false;
        }
        #endregion

        #region Populate Logic
        private async Task<bool> PopulateAsync()
        {
            bool result = false;
            BusyCancelIndicator.IsBusy = true;

            // We need to set up the parameters for the call to PopulateFieldWithRecognitionData as we can't access the UI elements from the background thread
            string dataLabelToUpdate = dataFieldControls.First(control => control.Label == dataFieldLabel).DataLabel;
            RecognitionFormatEnum recognitionFormat = GetSelectedRecognitionFormat();
            bool classificationOny = RBClassificationOny.IsChecked == true;
            bool boundingBoxCoordinates = CBBoundingBoxCoordinates.IsChecked == true;
            bool detectionConfidence = CBDetectionConfidence.IsChecked == true;
            bool detectionCategory = CBDetectionCategory.IsChecked == true;
            bool classificationConfidence = CBClassificationConfidence.IsChecked == true;
            bool classificationCategory = CBClassificationCategory.IsChecked == true;

            await Task.Run(() =>
            {
                result = fileDatabase.RecognitionsPopulateFieldWithData(dataLabelToUpdate,
                        recognitionFormat,
                        classificationOny,
                        boundingBoxCoordinates,
                        detectionConfidence,
                        detectionCategory,
                        classificationConfidence,
                        classificationCategory,
                        Progress);
            }, Token).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;
            return result;
        }

        #endregion

        #region Helpers

        private void EnableOrDisableGroupBoxes(bool enabled)
        {
            Brush brush = enabled ? Brushes.Black : Brushes.DarkGray;
            GroupBox.IsEnabled = enabled;
            GroupBox.Foreground = brush;

            GroupBoxOutputFormat.IsEnabled = enabled;
            GroupBoxOutputFormat.Foreground = brush;

            RBAsPlainText.Foreground = brush;
            RBAsUnformattedJson.Foreground = brush;
            RBAsFormattedJson.Foreground = brush;
            RBAsRaw.Foreground = brush;

            CBBoundingBoxCoordinates.Foreground = brush;
            CBClassificationCategory.Foreground = brush;
            CBClassificationConfidence.Foreground = brush;
            CBDetectionCategory.Foreground = brush;
            CBDetectionConfidence.Foreground = brush;

            UpdateRawDataStateAndDataFields();
        }

        // Raw data can only be output when a single data checkbox is selected in the mix of recognition data.
        // Decimal fields can only hold raw data from a single Confidence, so only then are they listed as data fields.
        private void UpdateRawDataStateAndDataFields()
        {
            bool isChooseFields = RBChooseFields.IsChecked == true;
            int selectedCount = GetSelectedDataCheckboxCount();
            RBAsRaw.IsEnabled = isChooseFields && selectedCount == 1;
            if (isChooseFields)
            {
                RBAsRaw.Foreground = RBAsRaw.IsEnabled ? Brushes.Black : Brushes.DarkGray;
            }

            // Raw data no longer applies once several checkboxes are selected
            if (RBAsRaw.IsChecked == true && isChooseFields && selectedCount > 1)
            {
                RBAsFormattedJson.IsChecked = true;
            }

            bool includeDecimalFields = RBAsRaw.IsEnabled && RBAsRaw.IsChecked == true &&
                                        (CBDetectionConfidence.IsChecked == true || CBClassificationConfidence.IsChecked == true);
            FillDataFieldComboBox(includeDecimalFields);
            TextBlockDecimalFieldsNote.Visibility = includeDecimalFields && dataFieldControls.Any(control => control.Type is Control.DecimalAny or Control.DecimalPositive)
                ? Visibility.Visible
                : Visibility.Collapsed;
            SetPopulateButtonEnableState();
        }

        // Fill the combobox with the Note and MultiLine fields, and optionally the Decimal fields,
        // keeping the current data field selected if it is still listed
        private void FillDataFieldComboBox(bool includeDecimalFields)
        {
            if (dataFieldComboBoxIncludesDecimalFields == includeDecimalFields)
            {
                return;
            }
            dataFieldComboBoxIncludesDecimalFields = includeDecimalFields;

            string previousDataFieldLabel = dataFieldLabel;
            ComboBoxSelectNoteField.Items.Clear();
            foreach (ControlRow control in dataFieldControls)
            {
                if (includeDecimalFields || control.Type is Control.Note or Control.MultiLine)
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
            useClassificationOnly = RBClassificationOny.IsChecked == true;
            useBoundingBoxCoordinates = CBBoundingBoxCoordinates.IsChecked == true;
            useDetectionConfidence = CBDetectionConfidence.IsChecked == true;
            useDetectionCategory = CBDetectionCategory.IsChecked == true;
            useClassificationConfidence = CBClassificationConfidence.IsChecked == true;
            useClassificationCategory = CBClassificationCategory.IsChecked == true;

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

            // Enable the Start/Done button if the Classification Only option is selected
            if (RBClassificationOny.IsChecked == true)
            {
                StartDoneButton.IsEnabled = true;
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
