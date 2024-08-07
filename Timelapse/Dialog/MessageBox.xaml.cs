using System;
using System.Windows;
using Timelapse.DebuggingSupport;

namespace Timelapse.Dialog
{
    public partial class MessageBox
    {
        #region Constructor, Loaded
        public bool IsNoSelected { get; set; }
        public MessageBox(string title, Window owner)
            : this(title, owner, MessageBoxButton.OK)
        {
        }

        public MessageBox(string title, Window owner, MessageBoxButton buttonType)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A title must be specified for the message box.", nameof(title));
            }

            InitializeComponent();
            Message.Title = title;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Title = title;

            switch (buttonType)
            {
                case MessageBoxButton.OK:
                    OkButton.IsCancel = true;
                    CancelButton.IsCancel = false;
                    CancelButton.IsEnabled = false;
                    NoButton.IsEnabled = false;
                    break;
                case MessageBoxButton.OKCancel:
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    OkButton.Content = "_Yes";
                    CancelButton.Content = "_No";
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    OkButton.Content = "_Yes";
                    NoButton.Content = "_No";
                    NoButton.Visibility = Visibility.Visible;
                    NoButton.IsEnabled = true;
                    CancelButton.Visibility = Visibility.Visible;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonType), $"Unhandled button type {buttonType}.");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

        }
        #endregion

        #region Callbacks - Dialog buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // The RelativePathControl seems to be invoking this twice, which generates and error
                // Not sure why, so that is why there is a try catch here.
                DialogResult = true;
            }
            catch
            {
                TracePrint.PrintMessage("Caught this.DialogResult issue in MessageBox");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Only if this is a Yes/No/Cancel dialog, then a 
        // - cancel returns false, with IsNoSelected false
        // - no returns false, with IsNoSelected true
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            IsNoSelected = true;
        }
        #endregion
    }
}
