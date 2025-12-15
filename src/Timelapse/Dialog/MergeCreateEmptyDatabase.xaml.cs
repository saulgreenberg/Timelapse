using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Database;
using Timelapse.DebuggingSupport;
using Timelapse.Util;
using File = Timelapse.Constant.File;
using Path = System.IO.Path;


namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MergeCreateEmptyDatabase.xaml
    /// </summary>
    public partial class MergeCreateEmptyDatabase
    {
        // The path to the selected template
        public string TemplateTdbPath { get; private set; }
        // The path to the created database file 
        public string EmptyDatabaseDdbPath { get; private set; }

        private readonly string InitialFolder;
        #region Constructor/Loading
        public MergeCreateEmptyDatabase(Window owner, string initialFolder)
        {
            InitializeComponent();
            Owner = owner;
            InitialFolder = initialFolder;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            CreateEmptyButton.IsEnabled = false;
        }

        #endregion

        #region Do Create Empty Database
        private async void DoCreateEmptyDatabase()
        {
            try
            {
                await DoCreateEmptyDatabaseAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task DoCreateEmptyDatabaseAsync()
        {
            string message;
            string hint;
            
            // Generate a pathname for the DDB file we want to create in the template folder
            // It generates a unique name from the name of the template but with the word 'Data' substituted for 'Template' (if possible) and with the .ddb suffix
            // If a file with that name already exists, it tries again by adding (0), then (1) etc.
            string rootFolder = Path.GetDirectoryName(TemplateTdbPath);
            if (rootFolder == null)
            {
                // This shouldn't happen
                message ="Something went wrong. Database was not created, likely because its in a root directory(e.g., 'C:'.)";
                hint = "Try moving the template into a folder";
                DisplayResults(message, hint);
                return;
            }
            string ddbFileNameBase = Path.GetFileNameWithoutExtension(TemplateTdbPath)!.Replace("Template", "Data_Master");
            string ddbFileName = ddbFileNameBase + File.FileDatabaseFileExtension;
            if (FilesFolders.GenerateFileNameIfNeeded(rootFolder, ddbFileName, out string newDdbFileName))
            {
                // if needed, generate a unique file name
                ddbFileName = newDdbFileName;
            }
            EmptyDatabaseDdbPath = Path.Combine(rootFolder, ddbFileName);

            // We have a unique ddb path. Try to create the empty ddb file
            bool result = await MergeDatabases.TryCreateEmptyDatabaseFromTemplateAsync(
                TemplateTdbPath, EmptyDatabaseDdbPath).ConfigureAwait(true);

            if (result == false)
            {
                // This is rare, don't bother trying to figure out what went wrong.
                message = $"Could not create an empty database{Environment.NewLine}Something went wrong, but we aren't sure why.";
                DisplayResults(message, string.Empty);
            }

            message = $"Empty database created successfully as:{Environment.NewLine}"
                             + $"\u2022 Name:     {Path.GetFileName(EmptyDatabaseDdbPath)}{Environment.NewLine}"
                             + $"\u2022 Location: {Path.GetDirectoryName(EmptyDatabaseDdbPath)}{Environment.NewLine}{Environment.NewLine}"
                            + "After you click 'Done', Timelapse will load the empty database.";
            hint = "You can rename that file using Windows Explorer if desired.";
            DisplayResults(message, hint);
        }
        #endregion

        #region Display the results
        private void DisplayResults(string result, string hint)
        {
            CreateEmptyButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            DoneButton.Visibility = Visibility.Visible;
            StackPanelCorrect.Visibility = Visibility.Collapsed;
            Message.What = string.Empty;
            Message.Solution = string.Empty;
            Message.Hint = String.IsNullOrWhiteSpace(hint) ? string.Empty : hint;
            Message.Result = result;
            MinHeight = Height - 180;
            Height = MinHeight;
        }
        #endregion

        #region Callbacks
        private void ChooseTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a TimelapseTemplate.tdb file, which should be located in the root folder",
                    InitialFolder,
                    String.Format("Template files (*{0})|*{0}", File.TemplateDatabaseFileExtension),
                    File.TemplateDatabaseFileExtension,
                    out string templateFilePath) == false)
            {
                // User cancelled
                return;
            }

            // If its not a valid template, display a dialog and abort
            if (false == Dialogs.DialogIsFileValid(this, templateFilePath))
            {
                return;
            }

            // Set and display the chosen template file
            TemplateTdbPath = templateFilePath;
            txtboxTemplateFileName.Text = string.IsNullOrWhiteSpace(TemplateTdbPath) 
                ? string.Empty 
                // ReSharper disable once AssignNullToNotNullAttribute
                : Path.Combine(Path.GetDirectoryName(TemplateTdbPath), Path.GetFileNameWithoutExtension(TemplateTdbPath));
            CreateEmptyButton.IsEnabled = !string.IsNullOrWhiteSpace(TemplateTdbPath); // Enable the button only if a folder was specified
        }
        private void CreateEmptyButton_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user if ddb file already exists in that folder
            // ReSharper disable once AssignNullToNotNullAttribute
            if (Directory.GetFiles(Path.GetDirectoryName(TemplateTdbPath), "*" + File.FileDatabaseFileExtension).Length > 0)
            {
                if (false == Dialogs.MergeWarningCreateEmptyDdbFileExists(this))
                {
                    DialogResult = false;
                    return;
                }
            }
            DoCreateEmptyDatabase();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion
    }
}
