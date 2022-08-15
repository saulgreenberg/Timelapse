using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for DialogMessageBox.xaml
    /// </summary>
    public partial class DialogMessageBox : Window
    {
        private MessageBoxButton buttonType = MessageBoxButton.OK;
        private MessageBoxImage iconType = MessageBoxImage.Exclamation;

        #region Properties
        public MessageBoxImage IconType
        {
            get
            {
                return this.iconType;
            }
            set
            {
                this.iconType = value;
                this.SetIconType();
            }
        }

        public MessageBoxButton ButtonType
        {
            get
            {
                return this.buttonType;
            }
            set
            {
                this.buttonType = value;
                this.SetButtonType();
            }
        }

        // Property: the Text of the Title Message
        public string MessageTitle
        {
            get
            {
                return this.Title;
            }
            set
            {
                this.txtBlockTitle.Text = value;
                this.Title = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageProblem
        {
            get
            {
                return this.tbProblemText.Text;
            }
            set
            {
                this.tbProblemText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageReason
        {
            get
            {
                return this.tbReasonText.Text;
            }
            set
            {
                this.tbReasonText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageSolution
        {
            get
            {
                return this.tbSolutionText.Text;
            }
            set
            {
                this.tbSolutionText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageResult
        {
            get
            {
                return this.tbResultText.Text;
            }
            set
            {
                this.tbResultText.Text = value;
                this.SetFieldVisibility();
            }
        }

        public string MessageHint
        {
            get
            {
                return this.tbHintText.Text;
            }
            set
            {
                this.tbHintText.Text = value;
                this.SetFieldVisibility();
            }
        }
        #endregion 

        public DialogMessageBox()
        {
            this.InitializeComponent();
            this.Title = "Message";
            this.SetFieldVisibility();
        }

        private void SetFieldVisibility()
        {
            this.myGrid.RowDefinitions[1].Height = (this.MessageProblem == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[2].Height = (this.MessageReason == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[3].Height = (this.MessageSolution == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[4].Height = (this.MessageResult == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
            this.myGrid.RowDefinitions[5].Height = (this.MessageHint == String.Empty) ? new GridLength(0) : new GridLength(1, GridUnitType.Auto);
        }

        private void SetIconType()
        {
            switch (this.IconType)
            {
                case MessageBoxImage.Question:
                    this.lblIconType.Content = "?";
                    break;
                case MessageBoxImage.Exclamation:
                    this.lblIconType.Content = "!";
                    break;
                case MessageBoxImage.None:
                case MessageBoxImage.Information:
                    this.lblIconType.Content = "i";
                    break;
                case MessageBoxImage.Error:
                    Run run = new Run(); // Create a symbol of a stopped hand
                    run.FontFamily = new FontFamily("Wingdings 2");
                    run.Text = "\u004e";
                    this.lblIconType.Content = run;
                    break;
                default:
                    return;
            }
        }

        private void SetButtonType()
        {
            switch (this.ButtonType)
            {
                case MessageBoxButton.OK:
                    this.OkButton.Content = "Okay";
                    this.OkButton.IsDefault = true;
                    this.OkButton.IsCancel = true;
                    this.CancelButton.IsCancel = false;
                    this.CancelButton.IsEnabled = false;
                    this.CancelButton.Visibility = Visibility.Collapsed;

                    break;
                case MessageBoxButton.OKCancel:
                    this.OkButton.Content = "Okay";
                    this.OkButton.IsCancel = false;
                    this.CancelButton.IsCancel = true;
                    this.CancelButton.Content = "Cancel";
                    this.CancelButton.IsEnabled = true;
                    this.CancelButton.IsDefault = true;
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    this.OkButton.Content = "Yes";
                    this.CancelButton.Content = "No";
                    this.OkButton.IsCancel = false;
                    this.CancelButton.IsCancel = true;
                    this.CancelButton.IsEnabled = true;
                    this.CancelButton.IsDefault = true;
                    this.CancelButton.Visibility = Visibility.Visible;
                    break;
                default:
                    return;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}