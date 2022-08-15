using System;
using System.Windows;

namespace Timelapse.Dialog
{
    public partial class MessageBox : Window
    {
        #region Constructor, Loaded
        public bool IsNoSelected { get; set; }
        public MessageBox(string title, Window owner)
            : this(title, owner, MessageBoxButton.OK)
        {
        }

        public MessageBox(string title, Window owner, MessageBoxButton buttonType)
        {
            if (String.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A title must be specified for the message box.", nameof(title));
            }

            this.InitializeComponent();
            this.Message.Title = title;
            this.Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.Title = title;

            switch (buttonType)
            {
                case MessageBoxButton.OK:
                    this.OkButton.IsCancel = true;
                    this.CancelButton.IsCancel = false;
                    this.CancelButton.IsEnabled = false;
                    this.NoButton.IsEnabled = false;
                    break;
                case MessageBoxButton.OKCancel:
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    this.OkButton.Content = "_Yes";
                    this.CancelButton.Content = "_No";
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    this.OkButton.Content = "_Yes";
                    this.NoButton.Content = "_No";
                    this.NoButton.Visibility = Visibility.Visible;
                    this.NoButton.IsEnabled = true;
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonType), String.Format("Unhandled button type {0}.", buttonType));
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
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Only if this is a Yes/No/Cancel dialog, then a 
        // - cancel returns false, with IsNoSelected false
        // - no returns false, with IsNoSelected true
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            IsNoSelected = true;
        }
        #endregion
    }
}
