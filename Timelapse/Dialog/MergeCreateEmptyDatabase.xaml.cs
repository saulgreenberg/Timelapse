using System;
using System.IO;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;
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
            this.Owner = owner;
            this.InitialFolder = initialFolder;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.CreateEmptyButton.IsEnabled = false;
        }

        #endregion

        #region Do Create Empty Database
        private async void DoCreateEmptyDatabase()
        {
            string message;
            string hint;
            
            // Generate a pathname for the DDB file we want to create in the template folder
            // It generates a unique name from the name of the template but with the word 'Data' substituted for 'Template' (if possible) and with the .ddb suffix
            // If a file with that name already exists, it tries again by adding (0), then (1) etc.
            string rootFolder = Path.GetDirectoryName(this.TemplateTdbPath);
            if (rootFolder == null)
            {
                // This shouldn't happen
                message ="Something went wrong. Database was not created, likely because its in a root directory(e.g., 'C:'.)";
                hint = "Try moving the template into a folder";
                this.DisplayResults(message, hint);
                return;
            }
            string ddbFileNameBase = Path.GetFileNameWithoutExtension(this.TemplateTdbPath).Replace("Template", "Data_Master");
            string ddbFileName = ddbFileNameBase + Constant.File.FileDatabaseFileExtension;
            if (FilesFolders.GenerateFileNameIfNeeded(rootFolder, ddbFileName, out string newDdbFileName))
            {
                // if needed, generate a unique file name
                ddbFileName = newDdbFileName;
            }
            this.EmptyDatabaseDdbPath = Path.Combine(rootFolder, ddbFileName);

            // We have a unique ddb path. Try to create the empty ddb file
            bool result = await MergeDatabases.TryCreateEmptyDatabaseFromTemplateAsync(
                this.TemplateTdbPath, this.EmptyDatabaseDdbPath).ConfigureAwait(true);

            if (result == false)
            {
                // This is rare, don't bother trying to figure out what went wrong.
                message = $"Could not create an empty database{Environment.NewLine}Something went wrong, but we aren't sure why.";
                this.DisplayResults(message, string.Empty);
            }

            message = $"Empty database created successfully as:{Environment.NewLine}"
                             + $"\u2022 Name:     {Path.GetFileName(this.EmptyDatabaseDdbPath)}{Environment.NewLine}"
                             + $"\u2022 Location: {Path.GetDirectoryName(this.EmptyDatabaseDdbPath)}{Environment.NewLine}{Environment.NewLine}"
                            + "After you click 'Done', Timelapse will load the empty database.";
            hint = "You can rename that file using Windows Explorer if desired.";
            this.DisplayResults(message, hint);
        }
        #endregion

        #region Display the results
        private void DisplayResults(string result, string hint)
        {
            this.CreateEmptyButton.Visibility = Visibility.Collapsed;
            this.CancelButton.Visibility = Visibility.Collapsed;
            this.DoneButton.Visibility = Visibility.Visible;
            this.StackPanelCorrect.Visibility = Visibility.Collapsed;
            this.Message.What = string.Empty;
            this.Message.Solution = string.Empty;
            this.Message.Hint = String.IsNullOrWhiteSpace(hint) ? string.Empty : hint;
            this.Message.Result = result;
            this.MinHeight = Height - 180;
            this.Height = MinHeight;
        }
        #endregion

        #region Callbacks
        private void ChooseTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a TimelapseTemplate.tdb file, which should be located in the root folder",
                    this.InitialFolder,
                    String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension),
                    Constant.File.TemplateDatabaseFileExtension,
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
            this.TemplateTdbPath = templateFilePath;
            this.txtboxTemplateFileName.Text = string.IsNullOrWhiteSpace(this.TemplateTdbPath) 
                ? string.Empty 
                // ReSharper disable once AssignNullToNotNullAttribute
                : Path.Combine(Path.GetDirectoryName(this.TemplateTdbPath), Path.GetFileNameWithoutExtension(this.TemplateTdbPath));
            this.CreateEmptyButton.IsEnabled = !string.IsNullOrWhiteSpace(this.TemplateTdbPath); // Enable the button only if a folder was specified
        }
        private void CreateEmptyButton_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user if ddb file already exists in that folder
            // ReSharper disable once AssignNullToNotNullAttribute
            if (Directory.GetFiles(Path.GetDirectoryName(this.TemplateTdbPath), "*" + Constant.File.FileDatabaseFileExtension).Length > 0)
            {
                if (false == Dialogs.MergeWarningCreateEmptyDdbFileExists(this))
                {
                    this.DialogResult = false;
                    return;
                }
            }
            this.DoCreateEmptyDatabase();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion
    }
}
