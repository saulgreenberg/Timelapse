using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DetectorOptions.xaml
    /// </summary>
    public partial class DetectorOptions : Window
    {
        private TimelapseState timelapseState;
        private FileDatabase fileDatabase;
        private bool useSpeciesDetected;
        public DetectorOptions(TimelapseState timelapseState, FileDatabase fileDataBase, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.timelapseState = timelapseState;
            this.fileDatabase = fileDataBase;
            List<string> dataLabels = this.fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            this.useSpeciesDetected = dataLabels.Contains(Constant.Recognition.SpeciesDetectedDataLabel);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.SetDefaultDialogPosition(this);
            Dialogs.TryFitDialogWindowInWorkingArea(this);
            this.BoundingBoxDisplayThresholdSlider.Value = this.timelapseState.BoundingBoxDisplayThreshold;
            this.RecomputeSpeciesDetectedThresholdSlider.Value = this.timelapseState.SpeciesDetectedThreshold;
            this.RecomputeSpeciesDetectedGroupBox.IsEnabled = this.useSpeciesDetected;
        }

        // Set the bounding box display threshold value to a new value between 0 and 1
        private void BoundingBoxDisplayThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!(sender is Slider slider))
            {
                return;
            }
            this.BoundingBoxThresholdDisplayValue.Text = slider.Value.ToString("0.00");
            if (slider.Value == 0)
            {
                this.BoundingBoxThresholdDisplayValue.Text += " (always display bounding box)";
            }
            else if (slider.Value == 1)
            { 
                this.BoundingBoxThresholdDisplayValue.Text += " (never display bounding box)";
            }
            this.timelapseState.BoundingBoxDisplayThreshold = slider.Value;
        }

        // Reset the bounding box display threshold to its default
        private void ResetBoundingBoxThreshold_Click(object sender, RoutedEventArgs e)
        {
            this.BoundingBoxDisplayThresholdSlider.Value = Constant.Recognition.BoundingBoxDisplayThresholdDefault; 
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void RecomputeSpeciesDetectedThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!(sender is Slider slider))
            {
                return;
            }
            this.RecomputeSpeciesDetectedThresholdDisplayValue.Text = slider.Value.ToString("0.00");
            if (slider.Value == 0)
            {
                this.RecomputeSpeciesDetectedThresholdDisplayValue.Text += " (always checkmarked)";
            }
            else if (slider.Value == 1)
            {
                this.RecomputeSpeciesDetectedThresholdDisplayValue.Text += " (never checkmarked)";
            }
        }

        // Reset the SpeciesDetected display threshold to its default
        private void RecomputeSpeciesDetectedThreshold_Click(object sender, RoutedEventArgs e)
        {
            // Update the SpeciesDetected field in the FileTable and ImageSet to match the current threshold
            UpdateSpeciesDetected(this.RecomputeSpeciesDetectedThresholdSlider.Value);
            this.timelapseState.SpeciesDetectedThreshold = this.RecomputeSpeciesDetectedThresholdSlider.Value;
            this.FeedbackMessage.Text = "Species detected values have been updated";
        }

        // Update the species detected against the threshold in both the database and the file table
        private void UpdateSpeciesDetected(double threshold)
        {
            FileTable fileTable = this.fileDatabase.FileTable;
            // We now have an unselected temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> filesToAdjust = new List<ImageRow>();
            List<ColumnTuplesWithWhere> filesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int row = 0; row < fileTable.RowCount; row++)
            {
                ImageRow imageRow = this.fileDatabase.FileTable[row];
                bool? newSpeciesDetectedValue = null;
                bool forceUpdate = false;
                // Get the current SpeciesDetected value
                if (bool.TryParse(imageRow.GetValueDisplayString(Constant.Recognition.SpeciesDetectedDataLabel), out bool currentSpeciesDetectedValue) == false)
                {
                    forceUpdate = true;
                }
                if (double.TryParse(imageRow.GetValueDisplayString(Constant.Recognition.DataLabelMaxConfidence), out double maxConfidence) == false)
                {
                    // If there is no confidence level, then the detector is likely not working so we always set the new Species detected value to false
                    forceUpdate = true;
                    newSpeciesDetectedValue = false;
                }
                else
                {
                    newSpeciesDetectedValue = (maxConfidence >= threshold) ? true : false;
                }
                if (forceUpdate == true || newSpeciesDetectedValue != currentSpeciesDetectedValue)
                {
                    // System.Diagnostics.Debug.Print(newSpeciesDetectedValue.ToString());
                    string newSpeciesDetectedValueAsString = (newSpeciesDetectedValue == true) 
                        ? Constant.BooleanValue.True 
                        : Constant.BooleanValue.False;
                    imageRow.SetValueFromDatabaseString(Constant.Recognition.SpeciesDetectedDataLabel, newSpeciesDetectedValueAsString);
                    filesToAdjust.Add(imageRow);
                    filesToUpdate.Add(new ColumnTuplesWithWhere(new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Recognition.SpeciesDetectedDataLabel, newSpeciesDetectedValueAsString)
                    }, imageRow.ID));
                }
            }
            // update the database with the new values
            this.fileDatabase.UpdateFiles(filesToUpdate);
        }

        private void ResetSpeciesDetectedThreshold_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.FeedbackMessage.Text = string.Empty;
        }
    }
}
