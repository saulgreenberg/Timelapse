using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Util;

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
        private string dataFieldLabel = String.Empty;
        private double confidenceValue;
        #endregion

        public PopulateFieldWithDetectionCounts(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            this.dataLabelByLabel = new Dictionary<string, string>();
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);

            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in this.fileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Counter)
                {
                    this.dataLabelByLabel.Add(control.Label, control.DataLabel);
                    this.ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }
            if (this.ComboBoxSelectNoteField.Items.Count == 0)
            {
                this.PrimaryPanel.Visibility = Visibility.Collapsed;
                this.FeedbackPanel.Visibility = Visibility.Visible;
            }
            else if (this.ComboBoxSelectNoteField.Items.Count == 1)
            {
                this.ComboBoxSelectNoteField.SelectedIndex = 0;
            }
            confidenceValue = GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold; // this.fileDatabase.GetTypicalDetectionThreshold();
            this.SliderConfidence.Value = confidenceValue;
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = true;
        }

        private void SliderConfidence_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.TextBlockSliderValue.Text = this.SliderConfidence.Value.ToString(("0.00"));
            confidenceValue = this.SliderConfidence.Value;
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {

            bool isCompleted = await this.PopulateAsync().ConfigureAwait(true);
            this.DialogResult = isCompleted;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                this.dataFieldLabel = ((string)cb.SelectedValue).Trim();
                this.StartDoneButton.IsEnabled = !string.IsNullOrEmpty(this.dataFieldLabel);
            }
        }


        private async Task<bool> PopulateAsync()
        {
            bool result = false;
            this.BusyCancelIndicator.IsBusy = true;
            await Task.Run(() =>
            {
                string dataLabelToUpdate = this.dataLabelByLabel[this.dataFieldLabel];
                result = this.fileDatabase.DetectionsAddCountToCounter(dataLabelToUpdate, this.confidenceValue, this.Progress);
            }, this.Token).ConfigureAwait(true);
            this.BusyCancelIndicator.IsBusy = true;
            return result;
        }
    }
}
