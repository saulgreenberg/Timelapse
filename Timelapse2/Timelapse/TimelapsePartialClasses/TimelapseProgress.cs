using System;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary> 
    /// Progess handler / Progress bar updates; used by most calls to update the busy indicator
    /// </summary>
    public partial class TimelapseWindow : Window, IDisposable
    {
        /// <summary>
        /// Set up a progress handler that will update the progress bar
        /// </summary>
        readonly Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
        {
            // Update the progress bar
            UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.CancelMessage, value.IsCancelEnabled, value.IsIndeterminate);
        });

        /// <summary>
        /// Update the progress bar in the BusyCancelIndicator.
        /// </summary>
        /// <param name="busyCancelIndicator"></param>
        /// <param name="percent">A number between 0-100 that reflects the percentage done (ignored if isIndeterminate is true)</param>
        /// <param name="message">The message to show in the progress bar</param>
        /// <param name="cancelMessage">the message to show in the cancel button</param>
        /// <param name="isCancelEnabled">whether the Cancel button should be enabled</param>
        /// <param name="isIndeterminate">whether the displayed bar is indeterminatne (just going back and forth) or showing the actual %progress</param>
        static private void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, string cancelMessage, bool isCancelEnabled, bool isIndeterminate)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                // Check the arguments for null 
                ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

                // Set it as a progressive or indeterminate bar
                busyCancelIndicator.IsIndeterminate = isIndeterminate;

                // Set the progress bar position (only visible if indeterminate is false, otherwise it doesn't have any effect)
                busyCancelIndicator.Percent = percent;

                // Update the primary text message in the busyCancelIndicator
                busyCancelIndicator.Message = message;

                // Enable/disable the cancel button text
                // If cancel is enabled, set the button's text to 'Cancel' otherwise set its text to the cancelMessage (e.g., to give more information about this operation)
                busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : cancelMessage;
            });
        }
    }
}
