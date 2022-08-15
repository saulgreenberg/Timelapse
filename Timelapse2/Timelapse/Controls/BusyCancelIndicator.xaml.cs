using System.Windows;
using System.Windows.Controls;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for BusyCancelIndicator.xaml
    /// </summary>
    public partial class BusyCancelIndicator : UserControl
    {
        #region CancelButtonIsEnabled
        /// <summary>
        /// Enable or disable state of the Cancel button
        /// </summary>
        public bool CancelButtonIsEnabled
        {
            get { return (bool)GetValue(CancelButtonIsEnabledProperty); }
            set { SetValue(CancelButtonIsEnabledProperty, value); }
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
            get { return (Visibility)GetValue(CancelButtonIsVisibleProperty); }
            set { SetValue(CancelButtonIsVisibleProperty, value); }
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
            get { return (string)GetValue(CancelButtonTextProperty); }
            set { SetValue(CancelButtonTextProperty, value); }
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
            get { return (bool)GetValue(IsBusyProperty); }
            set { SetValue(IsBusyProperty, value); }
        }
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(true));

        public bool DisplayImmediately
        {
            set
            {
                this.Busy.DisplayAfter = value ? System.TimeSpan.FromSeconds(0) : System.TimeSpan.FromMilliseconds(100);
            }
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
            get { return (bool)GetValue(IsIndeterminateProperty); }
            set { SetValue(IsIndeterminateProperty, value); }
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
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata("Please wait..."));
        #endregion

        #region Percent
        /// <summary>
        /// <summary>
        /// The percentage progress shown in the bar (if the Percent bar is enabled)
        /// </summary>
        public double Percent
        {
            get { return (double)GetValue(PercentProperty); }
            set { SetValue(PercentProperty, value); }
        }
        public static readonly DependencyProperty PercentProperty =
            DependencyProperty.Register(nameof(Percent), typeof(double), typeof(BusyCancelIndicator), new FrameworkPropertyMetadata(0.0));
        #endregion

        #region CancelClick
        public event RoutedEventHandler CancelClick;

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelClick?.Invoke(this, new RoutedEventArgs());
        }
        #endregion

        public BusyCancelIndicator()
        {
            InitializeComponent();
        }
    }
}
