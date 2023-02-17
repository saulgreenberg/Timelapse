using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for FilePlayerOptions.xaml
    /// </summary>
    public partial class FilePlayerOptions
    {
        #region Private Variables
        private readonly double playSlowMinimum = Constant.FilePlayerValues.PlaySlowMinimum.TotalSeconds;
        private readonly double playFastMaximum = Constant.FilePlayerValues.PlayFastMaximum.TotalSeconds;
        private readonly TimelapseState state;
        #endregion

        #region Constructor, Loaded
        public FilePlayerOptions(TimelapseState state, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.state = state;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            this.SlowSpeedSlider.Minimum = this.playSlowMinimum;
            this.SlowSpeedSlider.Maximum = Constant.FilePlayerValues.PlaySlowMaximum.TotalSeconds;
            this.SlowSpeedSlider.Value = this.state.FilePlayerSlowValue;
            this.SlowSpeedSlider.ValueChanged += this.SlowSpeedSlider_ValueChanged;

            this.FastSpeedSlider.Minimum = Constant.FilePlayerValues.PlayFastMinimum.TotalSeconds;
            this.FastSpeedSlider.Maximum = this.playFastMaximum;
            this.FastSpeedSlider.Value = this.state.FilePlayerFastValue;
            this.FastSpeedSlider.ValueChanged += this.FastSpeedSlider_ValueChanged;

            this.DisplayFeedback();
        }
        #endregion

        #region Private Methods: Display Feedback
        private void DisplayFeedback()
        {
            if (this.state.FilePlayerSlowValue <= 1)
            {
                int framerate = (int)System.Math.Round(1.0 / this.state.FilePlayerSlowValue);
                string plural = (framerate == 1) ? string.Empty : "s";
                this.SlowSpeedText.Text = $"{framerate} image{plural} every second";
            }
            else
            {
                this.SlowSpeedText.Text = $"1 image every {this.state.FilePlayerSlowValue:N2} seconds";
            }

            if (this.state.FilePlayerFastValue <= 1)
            {
                int framerate = (int)System.Math.Round(1.0 / this.state.FilePlayerFastValue);
                string plural = (framerate == 1) ? string.Empty : "s";
                this.FastSpeedText.Text = $"{framerate} image{plural} every second";
            }
            else
            {
                this.FastSpeedText.Text = $"1 image every {this.state.FilePlayerFastValue:N2} seconds";
            }
        }
        #endregion

        #region Callbacks
        private void SlowSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.state.FilePlayerSlowValue = this.SlowSpeedSlider.Value;
            this.DisplayFeedback();
        }

        private void ResetSlowSpeedSlider_Click(object sender, RoutedEventArgs e)
        {
            this.SlowSpeedSlider.Value = Constant.FilePlayerValues.PlaySlowDefault.TotalSeconds;
        }

        private void FastSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.state.FilePlayerFastValue = this.FastSpeedSlider.Value;
            this.DisplayFeedback();
        }

        private void ResetFastSpeedSlider_Click(object sender, RoutedEventArgs e)
        {
            this.FastSpeedSlider.Value = Constant.FilePlayerValues.PlayFastDefault.TotalSeconds;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion
    }
}
