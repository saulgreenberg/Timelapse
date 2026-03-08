using System;
using System.Data.SQLite;
using System.Windows;
using Timelapse.Database;
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
        private readonly bool IsSqlLiteError;
        private readonly SqlOperationResult SqlOperatonResult;
        #endregion

        #region Constructor, Loaded
        public ExceptionShutdownDialog(Window owner, UnhandledExceptionEventArgs unhandledExceptionArgs, SqlOperationResult sqlOperatonResult = null)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(unhandledExceptionArgs, nameof(unhandledExceptionArgs));

            Owner = owner;
            UnhandledExceptionArgs = unhandledExceptionArgs;
            IsSqlLiteError = null != sqlOperatonResult;
            SqlOperatonResult = sqlOperatonResult;
            InitializeComponent();
        }

        // Overload for use when only an Exception is available (e.g. from DispatcherUnhandledException,
        // where UnhandledExceptionEventArgs cannot be constructed).
        public ExceptionShutdownDialog(Window owner, Exception exception, SqlOperationResult sqlOperatonResult = null)
        {
            ThrowIf.IsNullArgument(exception, nameof(exception));

            Owner = owner;
            // Wrap the Exception in an UnhandledExceptionEventArgs so the rest of the dialog can use it uniformly.
            UnhandledExceptionArgs = new UnhandledExceptionEventArgs(exception, false);
            IsSqlLiteError = null != sqlOperatonResult;
            SqlOperatonResult = sqlOperatonResult;
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string emailText = "[ni] Email the bug report (optional) by either selecting:"
                               + "[li 2][b]Mail report[/b] to automatically create an email to the Timelapse developer, or"
                               + $"[li 2][b]Copy report text[/b] and paste the text into an email you create to [link:{Constant.ExternalLinks.EmailAddressAsMailTo}|{Constant.ExternalLinks.EmailAddress}].";
            string selectText = "[ni] Then select:";
            string exitText = "[li 2][b]Exit Timelapse[/b] to shutdown. When you restart, Timelapse usually picks up where you left off.";
            string hintText = $"[ni] If problems recur or are serious, zip up your [i].tdb[/i] and [i].ddb[/i] files and email them to "
                              + $"[link:{Constant.ExternalLinks.GmailAddressAsMailTo}|{Constant.ExternalLinks.GmailAddress}] to help him figure out what happened (his other [e]ucalgary.ca[/e] address does not accept zip files)." +
                              $"[ni] If your data file was damaged(rare), you may be able to restore as saved version from the [i]Backup[/i] folder.";
            string separator = $"{Environment.NewLine}--------------------------{ Environment.NewLine}"
            ;
            if (IsSqlLiteError)
            {
                ContinueButton.Visibility = Visibility.Visible;
                Message.DialogTitle = "Timelapse had problems with the last database request.";

                Message.Problem = "Timelapse had problems executing the last database request. Please help us fix it! ";
                Message.Solution = emailText
                                   + selectText
                                   + "[li 2][b]Keep going anyways[/b] to ignore the error if you think its non-critical, or"
                                   + exitText;
            }
            else
            {
                ContinueButton.Visibility = Visibility.Collapsed;
                Message.DialogTitle = "Timelapse needs to close. Please report this error";
                Message.Problem = "Timelapse encountered a problem, likely due to a bug. Please help us fix it!";
                Message.Solution = emailText
                                   + selectText
                                   + exitText;
            }


            Message.Hint = hintText;
            //FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Create the text body of the report
            body =
                $"Add details here to explain what you were doing when this happened, if you can.{Environment.NewLine}{separator}Timelapse has crashed. The bug report is below.{Environment.NewLine}";
            body +=
                $"{typeof(TimelapseWindow).Assembly.GetName()}, {Environment.OSVersion}, .NET runtime {Environment.Version}{separator}";
            if (IsSqlLiteError)
            {
                SQLiteException sException = (SQLiteException)UnhandledExceptionArgs.ExceptionObject;
                string details = SqlOperatonResult == null 
                    ? string.Empty 
                    : $"Context: {SqlOperatonResult.Context}{separator}Failing query: {SqlOperatonResult.FailingStatement}" 
                        + separator
                        + sException.StackTrace;
                body += ((SQLiteException)UnhandledExceptionArgs.ExceptionObject).Message + separator + details;
            }
            else
            {
                body += UnhandledExceptionArgs.ExceptionObject.ToString();
            }

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
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

    }
}
