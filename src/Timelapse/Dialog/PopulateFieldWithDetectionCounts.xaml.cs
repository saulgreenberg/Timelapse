using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
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

            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                switch (control.Type)
                {
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

        private async void Start_Click(object sender, RoutedEventArgs e)
        {

            bool isCompleted = await PopulateAsync().ConfigureAwait(true);
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


        private async Task<bool> PopulateAsync()
        {
            bool result = false;
            BusyCancelIndicator.IsBusy = true;
            await Task.Run(() =>
            {
                string dataLabelToUpdate = dataLabelByLabel[dataFieldLabel];
                result = fileDatabase.RecognitionsPopulateFieldWithDetectionCounts(dataLabelToUpdate, confidenceValue, Progress);
            }, Token).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;
            return result;
        }
    }
}
