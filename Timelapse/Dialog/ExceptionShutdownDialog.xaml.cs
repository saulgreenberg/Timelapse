using System;
using System.Windows;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for ExceptionShutdownDialog.xaml
    /// </summary>
    public partial class ExceptionShutdownDialog
    {
        #region Private Variables
        private readonly string ProgramName;
        private readonly UnhandledExceptionEventArgs UnhandledExceptionArgs;
        private const string to = "saul@ucalgary.ca";
        private const string subject = "Timelapse bug report";
        private string body;
        #endregion

        #region Constructor, Loaded
        public ExceptionShutdownDialog(Window owner, string programName, UnhandledExceptionEventArgs unhandledExceptionArgs)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(unhandledExceptionArgs, nameof(unhandledExceptionArgs));

            Owner = owner;
            ProgramName = programName;
            UnhandledExceptionArgs = unhandledExceptionArgs;
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Create the text body of the report
            body =
                $"Add details here to explain what happened, if you can.{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}Timelapse has crashed. The bug report is below.{Environment.NewLine}";
            body +=
                $"{typeof(TimelapseWindow).Assembly.GetName()}, {Environment.OSVersion}, .NET runtime {Environment.Version}{Environment.NewLine}";
            if (UnhandledExceptionArgs.ExceptionObject != null)
            {
                body += UnhandledExceptionArgs.ExceptionObject.ToString();
            }

            Message.Title = ProgramName + " needs to close. Please report this error.";
            Message.Problem = ProgramName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
            Message.Result =
                $"Timelapse will shut down.{Environment.NewLine}The data file is likely OK.  If it's not you can restore from the {File.BackupFolder} folder.";
            ExceptionReport.Text = body;

            // Add text to the body explaining the specific exception
            Exception custom_excepton = (Exception)UnhandledExceptionArgs.ExceptionObject;
            if (custom_excepton != null)
            {
                switch (custom_excepton.Message)
                {
                    case ExceptionTypes.TemplateReadWriteException:
                        Message.Problem =
                            ProgramName + "  could not read data from the template (.tdb) file. This could be because: " + Environment.NewLine +
                            "\u2022 the .tdb file is corrupt, or" + Environment.NewLine +
                            "\u2022 your system is somehow blocking Timelapse from manipulating that file (e.g., Citrix security will do that)" + Environment.NewLine +
                            "If you let us know, we will try and fix it. ";
                        break;
                    default:
                        Message.Problem = ProgramName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
                        break;
                }
            }
            else
            {
                Message.Problem = ProgramName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
            }
            CopyButton_Click(null, null);
        }
        #endregion

        #region Callbacks
        // Start an email
        private void MailButton_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri($"mailto:{to}?subject={subject}&body={Uri.EscapeUriString(body)}");
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
