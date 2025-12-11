using System;
using System.Globalization;
using System.Windows;
using Timelapse.Constant;
using Timelapse.State;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for FilePlayerOptions.xaml
    /// </summary>
    public partial class FilePlayerOptions
    {
        #region Private Variables
        private readonly double playSlowMinimum = FilePlayerValues.PlaySlowMinimum.TotalSeconds;
        private readonly double playFastMaximum = FilePlayerValues.PlayFastMaximum.TotalSeconds;
        private readonly TimelapseState state;
        #endregion

        #region Constructor, Loaded
        public FilePlayerOptions(TimelapseState state, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            this.state = state;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            SlowSpeedSlider.Minimum = playSlowMinimum;
            SlowSpeedSlider.Maximum = FilePlayerValues.PlaySlowMaximum.TotalSeconds;
            SlowSpeedSlider.Value = state.FilePlayerSlowValue;
            SlowSpeedSlider.ValueChanged += SlowSpeedSlider_ValueChanged;

            FastSpeedSlider.Minimum = FilePlayerValues.PlayFastMinimum.TotalSeconds;
            FastSpeedSlider.Maximum = playFastMaximum;
            FastSpeedSlider.Value = state.FilePlayerFastValue;
            FastSpeedSlider.ValueChanged += FastSpeedSlider_ValueChanged;

            DisplayFeedback();
        }
        #endregion

        #region Private Methods: Display Feedback
        private void DisplayFeedback()
        {
            if (state.FilePlayerSlowValue <= 1)
            {
                int framerate = (int)Math.Round(1.0 / state.FilePlayerSlowValue);
                string plural = (framerate == 1) ? string.Empty : "s";
                SlowSpeedText.Text = $"{framerate} image{plural} every second";
            }
            else
            {
                SlowSpeedText.Text = $"1 image every {state.FilePlayerSlowValue.ToString("N2", CultureInfo.InvariantCulture)} seconds";
            }

            if (state.FilePlayerFastValue <= 1)
            {
                int framerate = (int)Math.Round(1.0 / state.FilePlayerFastValue);
                string plural = (framerate == 1) ? string.Empty : "s";
                FastSpeedText.Text = $"{framerate} image{plural} every second";
            }
            else
            {
                FastSpeedText.Text = $"1 image every {state.FilePlayerFastValue.ToString("N2", CultureInfo.InvariantCulture)} seconds";
            }
        }
        #endregion

        #region Callbacks
        private void SlowSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            state.FilePlayerSlowValue = SlowSpeedSlider.Value;
            DisplayFeedback();
        }

        private void ResetSlowSpeedSlider_Click(object sender, RoutedEventArgs e)
        {
            SlowSpeedSlider.Value = FilePlayerValues.PlaySlowDefault.TotalSeconds;
        }

        private void FastSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            state.FilePlayerFastValue = FastSpeedSlider.Value;
            DisplayFeedback();
        }

        private void ResetFastSpeedSlider_Click(object sender, RoutedEventArgs e)
        {
            FastSpeedSlider.Value = FilePlayerValues.PlayFastDefault.TotalSeconds;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion
    }
}
