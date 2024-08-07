using System;
using System.IO;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.Util;
using File = Timelapse.Constant.File;

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
            Owner = owner;
            InitialFolder = initialFolder;
            TemplateDatabasePath = templateDatabasePath;
            DataHandler = dataHandler;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            ButtonCheckOut.IsEnabled = false;
        }
        #endregion

        #region Do Checkout
        private async void DoCheckout()
        {
            // Copy the template to that folder, generating a unique name if needed
            string tdbFileName = File.DefaultTemplateDatabaseFileName;
            bool tdbFileNameChanged = FilesFolders.GenerateFileNameIfNeeded(FullSubFolderPath, tdbFileName, out string newTdbFileName);
            if (tdbFileNameChanged)
            {
                tdbFileName = newTdbFileName;
            }

            // Copy the main template into that folder, perhaps renaming it
            string destinationTdbPath = Path.Combine(FullSubFolderPath, tdbFileName);
            try
            {
                System.IO.File.Copy(TemplateDatabasePath, destinationTdbPath);
            }
            catch
            {
                SetFeedbackAndDoneVisibility();
                Message.What = string.Empty;
                Message.Details = string.Empty;
                Message.Solution = string.Empty;
                Message.Hint = string.Empty;
                Message.Result =
                    $"Could not check out the database.{Environment.NewLine}The template could not be copied into the desired folder";
                success = false;
                return;
            }

            // Alter the just-copied template database so that the ignore flags for the intervening levels are set
            // The idea is not to change anything in the template except for those flags.
            // TODO not sure if ignore flags are the best way to do this yet. May be over-complicated
            int levelsToIgnore = string.IsNullOrWhiteSpace(RelativeSubFolderPath) ? 0 : RelativeSubFolderPath.Split(Path.DirectorySeparatorChar).Length;
            using (CommonDatabase childTdbTemplate = new CommonDatabase(destinationTdbPath))
            {
                childTdbTemplate.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
                for (int i = 1; i <= levelsToIgnore; i++)
                {
                    // Delete the levels from the table
                    // Note that because we are always deleting the first level, it resets everything back to 1
                    // which means we are always deleting the (new) level 1
                    childTdbTemplate.MetadataDeleteLevelFromDatabase(1);
                }
            }

            // Create an empty database in that folder
            string ddbFileName = File.DefaultFileDatabaseFileName;
            bool ddbFileNameChanged =
                FilesFolders.GenerateFileNameIfNeeded(FullSubFolderPath, ddbFileName, out string newDdbFileName);
            if (ddbFileNameChanged)
            {
                // if needed, generate a unique file name
                ddbFileName = newDdbFileName;
            }
            string destinationDdbPath = Path.Combine(FullSubFolderPath, ddbFileName);

            // We have a unique ddb path. Try to create the empty ddb file
            bool result = await MergeDatabases.TryCreateEmptyDatabaseFromTemplateAsync(
                destinationTdbPath, destinationDdbPath).ConfigureAwait(true);

            if (result == false)
            {
                // This is rare, don't bother trying to figure out what went wrong.
                SetFeedbackAndDoneVisibility();
                Message.What = string.Empty;
                Message.Details = string.Empty;
                Message.Solution = string.Empty;
                Message.Hint = string.Empty;
                Message.Result = $"Could not check out the database.{Environment.NewLine}{Environment.NewLine}"
                    + "Something went wrong when trying to create an empty database";
                success = false;
                return;
            }

            // Tell the user the name(s) of the created file
            string shortDestinationTdbPath = Path.Combine(RelativeSubFolderPath, tdbFileName);
            string shortDestinationDdbPath = Path.Combine(RelativeSubFolderPath, ddbFileName);

            // We now have a template and an empty database in the destination folder.
            // Populate it with the data from the source database.
            MergeDatabases.CheckoutDatabaseWithRelativePath(DataHandler.FileDatabase, DataHandler.FileDatabase.FilePath, destinationDdbPath,
                RelativeSubFolderPath);
            SetFeedbackAndDoneVisibility();
            DataHandler.FileDatabase.ImageSet.Log +=
                $"{Environment.NewLine}{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}: Checked out:  {shortDestinationDdbPath}";
            RedoMessageBoxWithResults(shortDestinationTdbPath, shortDestinationDdbPath);
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
            RelativeSubFolderPath = Dialogs.LocateRelativePathUsingOpenFileDialog(InitialFolder, String.Empty);
            if (RelativeSubFolderPath == null)
            {
                // User cancelled
                return;
            }

            if (string.IsNullOrWhiteSpace(RelativeSubFolderPath))
            {
                txtboxNewFolderName.Text = string.Empty;
                FullSubFolderPath = string.Empty;
            }
            else
            {
                txtboxNewFolderName.Text = RelativeSubFolderPath;
                FullSubFolderPath = Path.Combine(InitialFolder, RelativeSubFolderPath);
            }
            ButtonCheckOut.IsEnabled = !string.IsNullOrWhiteSpace(RelativeSubFolderPath); // Enable the button only if a folder was specified
        }
        private void ButtonCheckOut_Click(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            if (Directory.GetFiles(FullSubFolderPath, "*" + File.FileDatabaseFileExtension).Length > 0)
            {
                if (false == Dialogs.MergeWarningCheckOutDdbFileExists(this))
                {
                    return;
                }
            }
            DoCheckout();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = success;
        }
    }
}
