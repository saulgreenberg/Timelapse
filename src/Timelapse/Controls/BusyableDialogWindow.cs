using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Timelapse.Constant;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Controls
{
    // A specialized window to be used as an inherited dialog, where:
    // - it is 'busyable' i.e., 
    // -- it can display a passed in busy indicator, where the dialog window would then be greyed out if the busyindicator is enabled
    // -- it can disable the window's close button (e.g., if its in the middle of something that should not be cancelled)
    // - it fits the window into the calling window

    // - Cancellation tokens (and disposes of them afterwards) are include
    // - CloseButtonIsEnabled(bool enable): the window's close button can be enabled or disabled
    //#pragma warning disable CA1001 // Types that own disposable fields should be disposable. Reason: Handled in Closed event
    public class BusyableDialogWindow : Window
    //#pragma warning restore CA1001 // Types that own disposable fields should be disposableReason: Handled in Closed event
    {
        #region Cancellation Token
        // Token to let us cancel the task
        private CancellationTokenSource tokenSource;
        public CancellationToken Token { get; set; }
        protected CancellationTokenSource TokenSource => tokenSource;
        #endregion

        #region Variables for Close Button
        // Allows us to access the close button on the window
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]

        // These are used by the close button when it is enabled / disablwed
        static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_ENABLED = 0x00000000;
        private const uint SC_CLOSE = 0xF060;
        #endregion

        #region Constructor / Loaded 
        public BusyableDialogWindow(Window owner)
        {
            Owner = owner;
            // Initialize the cancellation CancelToken
            tokenSource = new();
            Token = tokenSource.Token;
            Loaded += BusyableDialogWindow_Loaded;
            Closed += BusyableDialogWindow_Closed;
        }

        public void TokenReset()
        {
            this.tokenSource.Dispose();
            this.tokenSource = new();
            this.Token = tokenSource.Token;
        }
        // Fit the dialog into the calling window
        private void BusyableDialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region Protected - Progress handler Initialization plus refresh
        protected Progress<ProgressBarArguments> ProgressHandler { get; set; }
        public IProgress<ProgressBarArguments> Progress { get; set; }

        /// <summary>
        /// Hook up the busy indicator to the progress handler.
        /// This must be invoked by parent e.g., in Loaded as this.InitalizeProgressHandler(this.BusyCancelIndicator);
        /// </summary>
        /// <param name="busyCancelIndicator"></param>
        protected void InitalizeProgressHandler(BusyCancelIndicator busyCancelIndicator)
        {
            ProgressHandler = new(value =>
            {
                // Update the progress bar
                UpdateProgressBar(busyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            Progress = ProgressHandler;
        }


        #endregion

        #region Cancellation callbacks
        // The user has indicates that he/she wishes to cancel the operation
        public void CancelAsyncOperationButton_Click(object sender, RoutedEventArgs e)
        {
            // Set this so that it will be caught in the above await task
            TokenSource.Cancel();
        }
        #endregion

        #region Protected methods
        // Show progress information in the passed in progress bar as indicated
        protected static void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

            // Set it as a progressive or indeterminate bar
            busyCancelIndicator.IsIndeterminate = isIndeterminate;

            // Set the progress bar position (only visible if determinate)
            busyCancelIndicator.Percent = percent;

            // Update the text message
            busyCancelIndicator.Message = message;

            // Update the cancel button to reflect the cancelEnabled argument
            busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
            busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Writing data...";
        }

        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;
        protected bool ReadyToRefresh()
        {
            TimeSpan intervalFromLastRefresh = DateTime.Now - lastRefreshDateTime;
            if (intervalFromLastRefresh > ThrottleValues.ProgressBarRefreshInterval)
            {
                lastRefreshDateTime = DateTime.Now;
                return true;
            }
            return false;
        }

        // Set the Window's Close Button Enable state
        protected void WindowCloseButtonIsEnabled(bool enableCloseButton)
        {
            Window window = GetWindow(this);
            if (window == null)
            {
                return;
            }
            var wih = new WindowInteropHelper(window);
            IntPtr hwnd = wih.Handle;

            IntPtr hMenu = GetSystemMenu(hwnd, false);
            uint enableAction = enableCloseButton ? MF_ENABLED : MF_GRAYED;
            if (hMenu != IntPtr.Zero)
            {
                EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | enableAction);
            }
        }
        #endregion

        #region Internal methods
        private void BusyableDialogWindow_Closed(object sender, EventArgs e)
        {
            // TokenSources need to be disposed, so here it is.
            TokenSource?.Dispose();
        }
        #endregion
    }
}
