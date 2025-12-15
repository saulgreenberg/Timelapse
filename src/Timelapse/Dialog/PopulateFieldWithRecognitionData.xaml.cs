using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly Dictionary<string, string> dataLabelByLabel;

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
            dataLabelByLabel = [];
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);
            Debug.Print(dataFieldLabel);
            ComboBoxSelectNoteField.SelectedItem = null;
            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type is Control.Note or Control.MultiLine)
                {
                    // We only set the combobox to the remembered data field if it is present in the controls 
                    if (dataFieldLabel == control.Label)
                    {
                        ComboBoxSelectNoteField.SelectedItem = dataFieldLabel;
                    }
                    dataLabelByLabel.Add(control.Label, control.DataLabel);
                    ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }
            dataFieldLabel = string.IsNullOrWhiteSpace((string)ComboBoxSelectNoteField.SelectedItem)
                ? string.Empty
                : (string)ComboBoxSelectNoteField.SelectedItem;

            if (ComboBoxSelectNoteField.Items.Count == 0)
            {
                PrimaryPanel.Visibility = Visibility.Collapsed;
                FeedbackPanel.Visibility = Visibility.Visible;
            }
            else if (ComboBoxSelectNoteField.Items.Count == 1)
            {
                ComboBoxSelectNoteField.SelectedIndex = 0;
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
            RBClassificationOny.IsChecked = useClassificationOnly;
            RBChooseFields.IsChecked = useClassificationOnly == false;
            EnableOrDisableGroupBoxes(RBChooseFields.IsChecked == true);

            RBClassificationOny.Checked += RBClassificationOnly_Checked;
            RBChooseFields.Checked += RBChooseFields_Checked;

            SetPopulateButtonEnableState();
        }
        #endregion

        #region Event Handlers

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                dataFieldLabel = ((string)cb.SelectedValue).Trim();
                SetPopulateButtonEnableState();
            }
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
            string dataLabelToUpdate = dataLabelByLabel[dataFieldLabel];
            RecognitionFormatEnum recognitionFormat = RBAsFormattedJson.IsChecked == true
                ? RecognitionFormatEnum.FormattedJson
                : RBAsUnformattedJson.IsChecked == true
                    ? RecognitionFormatEnum.UnformattedJson
                    : RecognitionFormatEnum.PlainText;
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

            CBBoundingBoxCoordinates.Foreground = brush;
            CBClassificationCategory.Foreground = brush;
            CBClassificationConfidence.Foreground = brush;
            CBDetectionCategory.Foreground = brush;
            CBDetectionConfidence.Foreground = brush;

            SetPopulateButtonEnableState();
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

            useRecognitionFormat = RBAsFormattedJson.IsChecked == true
                ? RecognitionFormatEnum.FormattedJson
                : RBAsUnformattedJson.IsChecked == true
                    ? RecognitionFormatEnum.UnformattedJson
                    : RecognitionFormatEnum.PlainText;
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

            // Enable the Start/Done button if at least one of the checkboxes is selected
            StartDoneButton.IsEnabled = CBBoundingBoxCoordinates.IsChecked == true ||
                                        CBDetectionCategory.IsChecked == true || CBDetectionConfidence.IsChecked == true ||
                                        CBClassificationCategory.IsChecked == true || CBClassificationConfidence.IsChecked == true;
        }
        #endregion
    }
}
