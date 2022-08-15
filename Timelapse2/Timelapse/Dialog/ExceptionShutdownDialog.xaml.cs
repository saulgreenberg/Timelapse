using System;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for ExceptionShutdownDialog.xaml
    /// </summary>
    public partial class ExceptionShutdownDialog : Window
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

            this.Owner = owner;
            this.ProgramName = programName;
            this.UnhandledExceptionArgs = unhandledExceptionArgs;
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Create the text body of the report
            this.body = String.Format("Add details here to explain what happened, if you can.{0}{0}{0}Timelapse has crashed. The bug report is below.{0}", Environment.NewLine);
            this.body += String.Format("{0}, {1}, .NET runtime {2}{3}", typeof(TimelapseWindow).Assembly.GetName(), Environment.OSVersion, Environment.Version, Environment.NewLine);
            if (this.UnhandledExceptionArgs.ExceptionObject != null)
            {
                this.body += UnhandledExceptionArgs.ExceptionObject.ToString();
            }

            this.Message.Title = this.ProgramName + " needs to close. Please report this error.";
            this.Message.Problem = this.ProgramName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
            this.Message.Result = String.Format("Timelapse will shut down.{0}The data file is likely OK.  If it's not you can restore from the {1} folder.", Environment.NewLine, Constant.File.BackupFolder);
            this.ExceptionReport.Text = body;

            // Add text to the body explaining the specific exception
            Exception custom_excepton = (Exception)this.UnhandledExceptionArgs.ExceptionObject;
            switch (custom_excepton.Message)
            {
                case Constant.ExceptionTypes.TemplateReadWriteException:
                    this.Message.Problem =
                        this.ProgramName + "  could not read data from the template (.tdb) file. This could be because: " + Environment.NewLine +
                        "\u2022 the .tdb file is corrupt, or" + Environment.NewLine +
                        "\u2022 your system is somehow blocking Timelapse from manipulating that file (e.g., Citrix security will do that)" + Environment.NewLine +
                        "If you let us know, we will try and fix it. ";
                    break;
                default:
                    this.Message.Problem = this.ProgramName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
                    break;
            }
            CopyButton_Click(null, null);
        }
        #endregion

        #region Callbacks
        // Start an email
        private void MailButton_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri(String.Format("mailto:{0}?subject={1}&body={2}", to, subject, Uri.EscapeUriString(body)));
            if (ProcessExecution.TryProcessStart(uri) == false)
            {
                this.MailButton.Content = "Mailing failed - See 'alternate' instructions above, or press Cancel.";
                this.MailButton.IsEnabled = false;
            }
        }

        // Copy the bug report into the clipboard
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.Clear();
            Clipboard.SetText(String.Format("Email this to {0} {1}{2}", to, Environment.NewLine, this.body));
        }
        #endregion

        #region Callbacks - Dialog Buttons
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
