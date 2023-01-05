using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RecognitionOptionsForBoundingBox.xaml
    /// </summary>
    public partial class RecognitionOptionsForBoundingBox : Window
    {
        private readonly TimelapseState timelapseState;
        public RecognitionOptionsForBoundingBox(Window owner, TimelapseState timelapseState)
        {
            // Check the arguments for null
            // ThrowIf.IsNullArgument(timelapseState, nameof(timelapseState));
            InitializeComponent();
            this.Owner = owner;
            this.timelapseState = timelapseState;

            // Detections
            this.CheckBoxBoundingBoxAnnotate.IsChecked = this.timelapseState.BoundingBoxAnnotate;
            this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked = this.timelapseState.BoundingBoxColorBlindFriendlyColors;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.BoundingBoxDisplayThresholdSlider.Value = this.timelapseState.BoundingBoxDisplayThreshold;
        }

        #region Callbacks - Detection and Bounding Boxsettings
        private void ResetDetections_Click(object sender, RoutedEventArgs e)
        {
            this.CheckBoxBoundingBoxAnnotate.IsChecked = true;
            this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked = false;
            this.BoundingBoxDisplayThresholdSlider.IsEnabled = true;
            this.timelapseState.BoundingBoxDisplayThresholdResetToDefault();
            this.BoundingBoxDisplayThresholdSlider.Value = this.timelapseState.BoundingBoxDisplayThreshold;
        }

        private void BoundingBoxDisplayThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!(sender is Slider slider))
            {
                return;
            }
            this.BoundingBoxThresholdDisplayValue.Text = slider.Value.ToString("0.00");
            if (slider.Value == 0)
            {
                this.BoundingBoxThresholdDisplayText.Text = "This setting will display all bounding boxes";
            }
            //else if (slider.Value == 1)
            //{
            //    this.BoundingBoxThresholdDisplayText.Text = "This setting will never display bounding boxes";
            //}
            else
            {
                this.BoundingBoxThresholdDisplayText.Text = "Always display bounding boxes at or above this confidence threshold";
            }
            this.timelapseState.BoundingBoxDisplayThreshold = slider.Value;
        }

        private void CheckBoxBounidngBoxColorBlindRinedlyColors_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.BoundingBoxColorBlindFriendlyColors = this.CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked == true;
        }

        private void CheckBoxBounidngBoxAnnotate_Click(object sender, RoutedEventArgs e)
        {
            this.timelapseState.BoundingBoxAnnotate = this.CheckBoxBoundingBoxAnnotate.IsChecked == true;
        }
        #endregion

        #region Callback - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion
    }
}
