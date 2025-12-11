using System;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for ExceptionShutdownDialog.xaml
    /// </summary>
    public partial class ExceptionShutdownDialog
    {
        #region Private Variables
        private readonly UnhandledExceptionEventArgs UnhandledExceptionArgs;
        private const string to = "saul@ucalgary.ca";
        private const string subject = "Timelapse bug report";
        private string body;
        #endregion

        #region Constructor, Loaded
        public ExceptionShutdownDialog(Window owner, UnhandledExceptionEventArgs unhandledExceptionArgs)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(unhandledExceptionArgs, nameof(unhandledExceptionArgs));

            Owner = owner;
            UnhandledExceptionArgs = unhandledExceptionArgs;
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Create the text body of the report
            body =
                $"Add details here to explain what happened, if you can.{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}Timelapse has crashed. The bug report is below.{Environment.NewLine}";
            body +=
                $"{typeof(TimelapseWindow).Assembly.GetName()}, {Environment.OSVersion}, .NET runtime {Environment.Version}{Environment.NewLine}";
            body += UnhandledExceptionArgs.ExceptionObject.ToString();

            ExceptionReport.Text = body;
            CopyButton_Click(null, null);
        }
        #endregion

        #region Callbacks
        // Start an email
        private void MailButton_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new($"mailto:{to}?subject={subject}&body={Uri.EscapeDataString(body)}");
            if (ProcessExecution.TryProcessStart(uri) == false)
            {
                MailButton.Content = "Mailing failed - See 'alternate' instructions above, or press Cancel.";
                MailButton.IsEnabled = false;
            }
        }

        // Copy the bug report into the clipboard
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.Clear();
            Clipboard.SetText($"Email this to {to} {Environment.NewLine}{body}");
        }
        #endregion

        #region Callbacks - Dialog Buttons
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
