namespace Timelapse.Controls
{
    public class ProgressBarArguments(int percentDone, string message, string cancelMessage, bool cancelEnabled, bool IsIndeterminate)
    {
        // Between 0 - 100
        public int PercentDone { get; set; } = percentDone;

        // Any text message, preferably not too long
        public string Message { get; set; } = message;
        // ReSharper disable once UnusedMember.Global
        public string CancelMessage { get; set; } = cancelMessage;

        // Whether the Cancel button should be enabled or disabled
        public bool IsCancelEnabled { get; set; } = cancelEnabled;

        // Whether the Random progress bar should be enabled or disabled
        public bool IsIndeterminate { get; set; } = IsIndeterminate;

        public ProgressBarArguments(int percentDone, string message, bool cancelEnabled, bool IsIndeterminate) : this(percentDone, message, "Please wait...", cancelEnabled, IsIndeterminate)
        {
        }
    }
}
