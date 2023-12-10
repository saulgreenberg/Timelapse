using System;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for EpisodeOptions.xaml
    /// </summary>
    public partial class EpisodeOptions
    {
        #region Public Properties
        public TimeSpan EpisodeTimeThreshold { get; set; }
        #endregion

        #region Constructore, Loaded
        public EpisodeOptions(TimeSpan timeDifferenceThreshold, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.EpisodeTimeThreshold = timeDifferenceThreshold;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            this.TimeThresholdSlider.Minimum = Constant.EpisodeDefaults.TimeThresholdMinimum * 60;
            this.TimeThresholdSlider.Maximum = Constant.EpisodeDefaults.TimeThresholdMaximum * 60; this.TimeThresholdSlider.SmallChange = 1;
            this.TimeThresholdSlider.LargeChange = 1;
            this.TimeThresholdSlider.ValueChanged += this.TimeThresholdSlider_ValueChanged;
            this.TimeThresholdSlider.Value = this.EpisodeTimeThreshold.TotalSeconds;
            this.DisplayFeedback();
        }
        #endregion

        #region Private Methods - Display Feedback
        private void DisplayFeedback()
        {
            TimeSpan duration = TimeSpan.FromSeconds(this.TimeThresholdSlider.Value);
            string label = (duration >= TimeSpan.FromMinutes(1)) ? "minutes" : "seconds";
            this.TimeThresholdText.Text = $"{duration:m\\:ss} {label}";
        }
        #endregion

        #region Callbacks
        private void TimeThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // this.state.FilePlayerSlowValue = this.SlowSpeedSlider.Value;
            this.DisplayFeedback();
            this.EpisodeTimeThreshold = TimeSpan.FromSeconds(this.TimeThresholdSlider.Value);
        }

        private void ResetTimeThresholdSlider_Click(object sender, RoutedEventArgs e)
        {
            this.TimeThresholdSlider.Value = Constant.EpisodeDefaults.TimeThresholdDefault * 60;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion
    }
}
