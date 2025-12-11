using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Timelapse.DataStructures;
using Timelapse.State;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RecognitionOptionsForBoundingBox.xaml
    /// </summary>
    public partial class RecognitionOptionsForBoundingBox
    {
        private readonly TimelapseState timelapseState;
        public RecognitionOptionsForBoundingBox(Window owner, TimelapseState timelapseState)
        {
            // Check the arguments for null
            // ThrowIf.IsNullArgument(timelapseState, nameof(timelapseState));
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            this.timelapseState = timelapseState;

            // Detections
            CheckBoxBoundingBoxAnnotate.IsChecked = this.timelapseState.BoundingBoxAnnotate;
            CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked = this.timelapseState.BoundingBoxColorBlindFriendlyColors;
            CheckBoxBoundingBoxHideInThisSession.IsChecked = GlobalReferences.HideBoundingBoxes;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            BoundingBoxDisplayThresholdSlider.Value = timelapseState.BoundingBoxDisplayThreshold;
        }

        #region Callbacks - Detection and Bounding Boxsettings
        private void ResetDetections_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxBoundingBoxAnnotate.IsChecked = true;
            CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked = false;
            CheckBoxBoundingBoxHideInThisSession.IsChecked = false;
            BoundingBoxDisplayThresholdSlider.IsEnabled = true;
            timelapseState.BoundingBoxDisplayThresholdResetToDefault();
            BoundingBoxDisplayThresholdSlider.Value = timelapseState.BoundingBoxDisplayThreshold;
            GlobalReferences.HideBoundingBoxes = CheckBoxBoundingBoxHideInThisSession.IsChecked == true;

        }

        private void BoundingBoxDisplayThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!(sender is Slider slider))
            {
                return;
            }
            BoundingBoxThresholdDisplayValue.Text = slider.Value.ToString("0.00", CultureInfo.InvariantCulture);
            BoundingBoxThresholdDisplayText.Text = slider.Value == 0 
                ? "This setting will display all bounding boxes"
                : "Always display bounding boxes at or above this confidence threshold";
            timelapseState.BoundingBoxDisplayThreshold = slider.Value;
        }

        private void CheckBoxBoundingBoxColorBlindRinedlyColors_Click(object sender, RoutedEventArgs e)
        {
            timelapseState.BoundingBoxColorBlindFriendlyColors = CheckBoxBoundingBoxColorBlindFriendlyColors.IsChecked == true;
        }

        private void CheckBoxBoundingBoxAnnotate_Click(object sender, RoutedEventArgs e)
        {
            timelapseState.BoundingBoxAnnotate = CheckBoxBoundingBoxAnnotate.IsChecked == true;
        }
        #endregion

        #region Callback - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion

        private void CheckBoxBoundingBoxHideBoundingBoxes_Click(object sender, RoutedEventArgs e)
        {
            GlobalReferences.HideBoundingBoxes = CheckBoxBoundingBoxHideInThisSession.IsChecked == true;
        }
    }
}
