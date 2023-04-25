using System;
using System.IO;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Get a folder that is a subfolder of the initial folder,
    /// This will be used (as the text messsage indicates) for a checked out database location
    /// </summary>
    public partial class MergeCheckoutChooseSubfolder
    {
        // The full folder path to the selected folder
        public string FullSubFolderPath { get; private set; }

        // The relative sub folder path under the initial folder to the selected folder
        public string RelativeSubFolderPath { get; private set; }

        private readonly string InitialFolder;
        private readonly string TemplateDatabasePath;
        private readonly DataEntryHandler DataHandler;
        private bool success = true;

        #region Constructor/Loading
        public MergeCheckoutChooseSubfolder(Window owner, string initialFolder, string templateDatabasePath, DataEntryHandler dataHandler)
        {
            InitializeComponent();
            this.Owner = owner;
            this.InitialFolder = initialFolder;
            this.TemplateDatabasePath = templateDatabasePath;
            this.DataHandler = dataHandler;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.ButtonCheckOut.IsEnabled = false;
        }
        #endregion

        #region Do Checkout
        private async void DoCheckout()
        {
            // Copy the template to that folder, generating a unique name if needed
            string tdbFileName = Constant.File.DefaultTemplateDatabaseFileName;
            bool tdbFileNameChanged = FilesFolders.GenerateFileNameIfNeeded(this.FullSubFolderPath, tdbFileName, out string newTdbFileName);
            if (tdbFileNameChanged)
            {
                tdbFileName = newTdbFileName;
            }

            // Copy the main template into that folder, perhaps renaming it
            string destinationTdbPath = Path.Combine(this.FullSubFolderPath, tdbFileName);
            try
            {
                File.Copy(this.TemplateDatabasePath, destinationTdbPath);
            }
            catch
            {
                this.SetFeedbackAndDoneVisibility();
                Message.What = string.Empty;
                Message.Details = string.Empty;
                Message.Solution = string.Empty;
                Message.Hint = string.Empty;
                Message.Result =
                    $"Could not check out the database.{Environment.NewLine}The template could not be copied into the desired folder";
                this.success = false;
                return;
            }

            // Create an empty database in that folder
            string ddbFileName = Constant.File.DefaultFileDatabaseFileName;
            bool ddbFileNameChanged =
                FilesFolders.GenerateFileNameIfNeeded(this.FullSubFolderPath, ddbFileName, out string newDdbFileName);
            if (ddbFileNameChanged)
            {
                // if needed, generate a unique file name
                ddbFileName = newDdbFileName;
            }
            string destinationDdbPath = Path.Combine(this.FullSubFolderPath, ddbFileName);

            // We have a unique ddb path. Try to create the empty ddb file
            bool result = await MergeDatabases.TryCreateEmptyDatabaseFromTemplateAsync(
                destinationTdbPath, destinationDdbPath).ConfigureAwait(true);

            if (result == false)
            {
                // This is rare, don't bother trying to figure out what went wrong.
                this.SetFeedbackAndDoneVisibility();
                Message.What = string.Empty;
                Message.Details = string.Empty;
                Message.Solution = string.Empty;
                Message.Hint = string.Empty;
                Message.Result = $"Could not check out the database.{Environment.NewLine}{Environment.NewLine}"
                    + "Something went wrong when trying to create an empty database";
                this.success = false;
                return;
            }

            // Tell the user the name(s) of the created file
            string shortDestinationTdbPath = Path.Combine(this.RelativeSubFolderPath, newTdbFileName);
            string shortDestinationDdbPath = Path.Combine(this.RelativeSubFolderPath, newDdbFileName);

            // We now have a template and an empty database in the destination folder.
            // Populate it with the data from the source database.
            MergeDatabases.CheckoutDatabaseWithRelativePath(this.DataHandler.FileDatabase, this.DataHandler.FileDatabase.FilePath, destinationDdbPath,
                this.RelativeSubFolderPath);
            this.SetFeedbackAndDoneVisibility();
            this.DataHandler.FileDatabase.ImageSet.Log +=
                $"{Environment.NewLine}{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}: Checked out:  {shortDestinationDdbPath}";
            this.RedoMessageBoxWithResults(shortDestinationTdbPath, shortDestinationDdbPath);
        }
        #endregion

        private void RedoMessageBoxWithResults(string tdbFileName, string ddbFileName)
        {
            string fileList;
            string pluralityText = "s are";

            if (false == string.IsNullOrWhiteSpace(tdbFileName) && false == string.IsNullOrWhiteSpace(ddbFileName))
            {
                // Both files were renamed
                fileList = $"\u2022 {tdbFileName}{Environment.NewLine}"
                           + $"\u2022 {ddbFileName}";
            }
            else if (false == string.IsNullOrWhiteSpace(tdbFileName))
            {
                // The template file only was renamed
                pluralityText = " is ";
                fileList = $"\u2022 {tdbFileName}";
            }
            else
            {
                // The datafile only was renamed
                pluralityText = " is";
                fileList = $"\u2022 {ddbFileName}";
            }

            Message.What = string.Empty;
            Message.Details = string.Empty;
            Message.Solution = string.Empty;
            Message.Result = $"Check out completed successfully.{Environment.NewLine}{Environment.NewLine}The checked-out Timelapse file{pluralityText} named in the sub-folder as:{Environment.NewLine}"
                   + $"{fileList}{Environment.NewLine}{Environment.NewLine}"
                   + "You can rename those files using Windows Explorer if desired.";
        }

        private void SetFeedbackAndDoneVisibility()
        {
            PanelGetFolder.Visibility = Visibility.Collapsed;
            ButtonCheckOut.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            DoneButton.Visibility = Visibility.Visible;
        }
        #region Callbacks
        private void ButtonChooseFolder_OnClick(object sender, RoutedEventArgs e)
        {
            this.RelativeSubFolderPath = Dialogs.LocateRelativePathUsingOpenFileDialog(this.InitialFolder, String.Empty);
            if (this.RelativeSubFolderPath == null)
            {
                // User cancelled
                return;
            }

            if (string.IsNullOrWhiteSpace(this.RelativeSubFolderPath))
            {
                this.txtboxNewFolderName.Text = string.Empty;
                this.FullSubFolderPath = string.Empty;
            }
            else
            {
                this.txtboxNewFolderName.Text = this.RelativeSubFolderPath;
                this.FullSubFolderPath = Path.Combine(this.InitialFolder, this.RelativeSubFolderPath);
            }
            this.ButtonCheckOut.IsEnabled = !string.IsNullOrWhiteSpace(this.RelativeSubFolderPath); // Enable the button only if a folder was specified
        }
        private void ButtonCheckOut_Click(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            if (Directory.GetFiles(this.FullSubFolderPath, "*" + Constant.File.FileDatabaseFileExtension).Length > 0)
            {
                if (false == Dialogs.MergeWarningCheckOutDdbFileExists(this))
                {
                    return;
                }
            }
            this.DoCheckout();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = success;
        }
    }
}
