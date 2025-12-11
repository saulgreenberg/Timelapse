using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Constant;
using Timelapse.DataStructures;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for BusyCancelIndicator.xaml
    /// </summary>
    public partial class BusyCancelIndicator
    {
        #region CancelButtonIsEnabled
        /// <summary>
        /// Enable or disable state of the Cancel button
        /// </summary>
        public bool CancelButtonIsEnabled
        {
            get => (bool)GetValue(CancelButtonIsEnabledProperty);
            set => SetValue(CancelButtonIsEnabledProperty, value);
        }
        public static readonly DependencyProperty CancelButtonIsEnabledProperty =
            DependencyProperty.Register(nameof(CancelButtonIsEnabled), typeof(bool), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(true));
        #endregion

        #region CancelButtonIsVisible
        /// <summary>
        /// Enable or disable state of the Cancel button
        /// </summary>
        public Visibility CancelButtonIsVisible
        {
            get => (Visibility)GetValue(CancelButtonIsVisibleProperty);
            set => SetValue(CancelButtonIsVisibleProperty, value);
        }
        public static readonly DependencyProperty CancelButtonIsVisibleProperty =
            DependencyProperty.Register(nameof(CancelButtonIsVisible), typeof(Visibility), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(Visibility.Visible));
        #endregion

        #region CancelButtonText
        /// <summary>
        /// The text that appears in the Cancel button
        /// </summary>
        public string CancelButtonText
        {
            get => (string)GetValue(CancelButtonTextProperty);
            set => SetValue(CancelButtonTextProperty, value);
        }
        public static readonly DependencyProperty CancelButtonTextProperty =
            DependencyProperty.Register(nameof(CancelButtonText), typeof(string), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata("Cancel"));
        #endregion

        #region IsBusy
        /// <summary>
        /// Enable or disable state of the Cancel button
        /// </summary>
        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(true));

        // ReSharper disable once UnusedMember.Global
        public bool DisplayImmediately
        {
            set => Busy.DisplayAfter = value ? TimeSpan.FromSeconds(0) : TimeSpan.FromMilliseconds(100);
        }
        #endregion

        #region IsIndeterminate
        /// 
        /// Whether the bar is indeterminate (randomly going from left to right) or a progress bar
        /// 
        /// <summary>
        /// Text message that appears in the progress bar
        /// </summary>
        public bool IsIndeterminate
        {
            get => (bool)GetValue(IsIndeterminateProperty);
            set => SetValue(IsIndeterminateProperty, value);
        }
        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(true));
        #endregion

        #region Message
        /// <summary>
        /// Text message that appears in the progress bar
        /// </summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata("Please wait..."));
        #endregion

        #region Percent
        /// <summary>
        /// The percentage progress shown in the bar (if the Percent bar is enabled)
        /// </summary>
        public double Percent
        {
            get => (double)GetValue(PercentProperty);
            set => SetValue(PercentProperty, value);
        }
        public static readonly DependencyProperty PercentProperty =
            DependencyProperty.Register(nameof(Percent), typeof(double), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(0.0));
        #endregion

        #region CancelClick
        public event RoutedEventHandler CancelClick;

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalReferences.CancelTokenSource.Cancel();
            CancelClick?.Invoke(this, new());
        }
        #endregion

        #region static ProgressWrapper for long-running methods with no progress indicators
        // The progress wrapper is a convenient way to get progress on a long-running atomic action that in of itself does not report progress.
        // An example is a database query that may take a long time to return. Essentially, the wrapper
        // - invokes the passed in method as a task
        // - generates a progress report every so often if the task is still in progress,
        // - return the method's result when completed.
        // - if cancelled, returns the default value for the methods return type.
        // The example below shows how to invoke the wrapper around the method 'AsyncDoSomething', which in turn invokes the long-running procedure that (in this imaginary case) returns a list of strings.
        // Note that
        // - the 'progress' argument is normally the inherited BusyCancelIndicator progress handler 
        //- the CancellationTokenSource is normally the GlobalReferences.CancelTokenSource 
        //    List<string> myList = await BusyCancelIndicator.ProgressWrapper(() => AsyncSearchFor("foo"), this.ProgressHandler, "Please wait 1", true);

        //    public async Task<List<string>> AsyncSearchFor(string searchString)
        //    {
        //        return await Task.Run(() => this.LongRunningSearch(searchString));
        //    }
        public static async Task<T> ProgressWrapper<T>(Func<Task<T>> method, IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource, string message, bool cancelEnabled)
        {
            Task<T> task = method();
            while (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted && !cancelTokenSource.IsCancellationRequested)
            {
                await Task.WhenAny(task, Task.Delay(ThrottleValues.ProgressBarRefreshInterval)); // Checks and thus updates every 250 ms
                progress.Report(new(0, message, cancelEnabled, true));
            }

            if (cancelTokenSource.IsCancellationRequested)
            {
                return default;
            }
            return await task;
        }
        #endregion
        public BusyCancelIndicator()
        {
            InitializeComponent();
        }

        public void Reset(bool isBusy)
        {
            IsBusy = isBusy;
            GlobalReferences.CancelTokenSource = new();
        }
    }
}
