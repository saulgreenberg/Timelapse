namespace Timelapse.Controls
{
    public class ProgressBarArguments
    {
        // Between 0 - 100
        public int PercentDone { get; set; }

        // Any text message, preferably not too long
        public string Message { get; set; }

        public string CancelMessage { get; set; }

        // Whether the Cancel button should be enabled or disabled
        public bool IsCancelEnabled { get; set; }

        // Whether the Random progress bar should be enabled or disabled
        public bool IsIndeterminate { get; set; }

        public ProgressBarArguments(int percentDone, string message, string cancelMessage, bool cancelEnabled, bool IsIndeterminate)
        {
            this.PercentDone = percentDone;
            this.Message = message;
            this.CancelMessage = cancelMessage;
            this.IsCancelEnabled = cancelEnabled;
            this.IsIndeterminate = IsIndeterminate;
        }

        public ProgressBarArguments(int percentDone, string message, bool cancelEnabled, bool IsIndeterminate)
        {
            this.PercentDone = percentDone;
            this.Message = message;
            this.CancelMessage = "Please wait...";
            this.IsCancelEnabled = cancelEnabled;
            this.IsIndeterminate = IsIndeterminate;
        }
    }
}
