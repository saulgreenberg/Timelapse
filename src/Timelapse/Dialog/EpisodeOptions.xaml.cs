using System;
using System.Windows;
using Timelapse.Constant;
using Timelapse.Util;

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
            InitializeComponent();
            Owner = owner;
            EpisodeTimeThreshold = timeDifferenceThreshold;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            TimeThresholdSlider.Minimum = EpisodeDefaults.TimeThresholdMinimum * 60;
            TimeThresholdSlider.Maximum = EpisodeDefaults.TimeThresholdMaximum * 60; TimeThresholdSlider.SmallChange = 1;
            TimeThresholdSlider.LargeChange = 1;
            TimeThresholdSlider.ValueChanged += TimeThresholdSlider_ValueChanged;
            TimeThresholdSlider.Value = EpisodeTimeThreshold.TotalSeconds;
            DisplayFeedback();
        }
        #endregion

        #region Private Methods - Display Feedback
        private void DisplayFeedback()
        {
            TimeSpan duration = TimeSpan.FromSeconds(TimeThresholdSlider.Value);
            string label = (duration >= TimeSpan.FromMinutes(1)) ? "minutes" : "seconds";
            TimeThresholdText.Text = $"{duration:m\\:ss} {label}";
        }
        #endregion

        #region Callbacks
        private void TimeThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // this.state.FilePlayerSlowValue = this.SlowSpeedSlider.Value;
            DisplayFeedback();
            EpisodeTimeThreshold = TimeSpan.FromSeconds(TimeThresholdSlider.Value);
        }

        private void ResetTimeThresholdSlider_Click(object sender, RoutedEventArgs e)
        {
            TimeThresholdSlider.Value = EpisodeDefaults.TimeThresholdDefault * 60;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion
    }
}
