using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithBoundingBoxCoordinates.xaml
    /// Populates a data field with the coordinates of each file's highest-confidence bounding box.
    /// This does the same as the PopulateFieldWithRecognitionData dialog when only the Bounding box is output as raw data.
    /// </summary>
    public partial class PopulateFieldWithBoundingBoxCoordinates
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        // The Note and MultiLine controls that could hold the bounding box coordinates, keyed by their label
        private readonly Dictionary<string, ControlRow> controlByLabel;
        private string dataFieldLabel = string.Empty;
        #endregion

        public PopulateFieldWithBoundingBoxCoordinates(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            controlByLabel = [];
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Construct a list showing the available text fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type is Control.Note or Control.MultiLine)
                {
                    controlByLabel.Add(control.Label, control);
                    ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }
            if (ComboBoxSelectNoteField.Items.Count == 0)
            {
                PrimaryPanel.Visibility = Visibility.Collapsed;
                FeedbackPanel.Visibility = Visibility.Visible;
            }
            else if (ComboBoxSelectNoteField.Items.Count == 1)
            {
                ComboBoxSelectNoteField.SelectedIndex = 0;
            }
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
            bool? isCompleted = await PopulateAsync().ConfigureAwait(true);
            if (isCompleted == null)
            {
                // The user cancelled the overwrite warning, so stay in this dialog to choose differently
                return;
            }
            DialogResult = isCompleted;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedItem: string label })
            {
                dataFieldLabel = label.Trim();
                StartDoneButton.IsEnabled = !string.IsNullOrEmpty(dataFieldLabel);
            }
        }

        // Returns true if the field was populated, false if it wasn't, or null if the user cancelled
        // the warning that existing values would be overwritten
        private async Task<bool?> PopulateAsync()
        {
            bool result = false;
            Dictionary<long, string> fieldData = null;
            int overwriteCount = 0;
            BusyCancelIndicator.IsBusy = true;

            ControlRow controlToUpdate = controlByLabel[dataFieldLabel];
            string dataLabelToUpdate = controlToUpdate.DataLabel;

            // The bounding box coordinates alone, output as raw data
            await Task.Run(() =>
            {
                fieldData = fileDatabase.RecognitionsComputeFieldData(dataLabelToUpdate,
                    RecognitionFormatEnum.Raw,
                    useBboxCoords: true,
                    useDetectionConf: false,
                    useDetectionCategory: false,
                    useClassConf: false,
                    useClassCat: false,
                    Progress);
                overwriteCount = fileDatabase.RecognitionsCountValuesToOverwrite(dataLabelToUpdate, fieldData);
            }, Token).ConfigureAwait(true);
            if (fieldData == null)
            {
                BusyCancelIndicator.IsBusy = false;
                return false;
            }

            // Warn the user if existing, different values in the field would be overwritten
            if (overwriteCount > 0)
            {
                BusyCancelIndicator.IsBusy = false;
                if (false == Dialogs.PopulateFieldOverwriteWarningDialog(this, controlToUpdate.Label, overwriteCount, fileDatabase.FileTable.RowCount))
                {
                    return null;
                }
                BusyCancelIndicator.IsBusy = true;
            }

            await Task.Run(() =>
            {
                result = fileDatabase.RecognitionsWriteFieldData(dataLabelToUpdate, fieldData, Progress);
            }, Token).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;
            return result;
        }
    }
}
