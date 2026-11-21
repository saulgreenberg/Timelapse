using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithDetectionCounts.xaml
    /// </summary>
    public partial class PopulateFieldWithDetectionCounts
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private readonly Dictionary<string, string> dataLabelByLabel;
        private string dataFieldLabel = string.Empty;
        private static double? confidenceValue;
        #endregion

        public PopulateFieldWithDetectionCounts(Window owner, FileDatabase fileDatabase) : base(owner)
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

            // Construct a list showing the available numeric and text fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                switch (control.Type)
                {
                    case Control.Note:
                    case Control.MultiLine:
                    case Control.Counter:
                    case Control.IntegerAny:
                    case Control.IntegerPositive:
                    case Control.DecimalAny:
                    case Control.DecimalPositive:
                        dataLabelByLabel.Add(control.Label, control.DataLabel);
                        ComboBoxSelectNoteField.Items.Add(control.Label);
                        break;
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
            confidenceValue ??= GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold;
            SliderConfidence.Value = (double) confidenceValue;
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // By default, DialogResult is false if the window close button is selected
            // i.e., its the same as cancel. So setting the DialogResult is not needed.
            // DialogResult = true;
        }

        private void SliderConfidence_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBlockSliderValue.Text = SliderConfidence.Value.ToString(("0.00"));
            confidenceValue = SliderConfidence.Value;
        }

        // Reset the slider to its default value, the bounding box display threshold (ValueChanged then updates confidenceValue)
        private void ButtonResetConfidence_Click(object sender, RoutedEventArgs e)
        {
            SliderConfidence.Value = GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold;
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
            if (sender is ComboBox cb)
            {
                dataFieldLabel = ((string)cb.SelectedValue).Trim();
                StartDoneButton.IsEnabled = !string.IsNullOrEmpty(dataFieldLabel);
            }
        }


        // Returns true if the field was populated, false if it wasn't,
        // or null if the user cancelled the warning that existing values would be overwritten
        private async Task<bool?> PopulateAsync()
        {
            bool result = false;
            Dictionary<long, string> counts = null;
            int overwriteCount = 0;
            string dataLabelToUpdate = dataLabelByLabel[dataFieldLabel];
            BusyCancelIndicator.IsBusy = true;
            await Task.Run(() =>
            {
                counts = fileDatabase.RecognitionsComputeDetectionCounts(confidenceValue, Progress);
                overwriteCount = fileDatabase.RecognitionsCountValuesToOverwrite(dataLabelToUpdate, counts);
            }, Token).ConfigureAwait(true);
            if (counts == null)
            {
                BusyCancelIndicator.IsBusy = false;
                return false;
            }

            // Warn the user if existing, different values in the field would be overwritten
            if (overwriteCount > 0)
            {
                BusyCancelIndicator.IsBusy = false;
                if (false == Dialogs.PopulateFieldOverwriteWarningDialog(this, dataFieldLabel, overwriteCount, fileDatabase.FileTable.RowCount))
                {
                    return null;
                }
                BusyCancelIndicator.IsBusy = true;
            }

            await Task.Run(() =>
            {
                result = fileDatabase.RecognitionsWriteFieldData(dataLabelToUpdate, counts, Progress);
            }, Token).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;
            return result;
        }
    }
}
