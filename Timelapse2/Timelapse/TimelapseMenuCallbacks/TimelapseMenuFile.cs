using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

// File Menu Callbacks
// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region File Submenu Opening 
        private void File_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.RecentFileSets_Refresh();

            // Enable / disable various menu items depending on whether we are looking at the single image view or overview
            this.MenuItemExportThisImage.IsEnabled = this.IsDisplayingSingleImage();
            this.MenuItemExportSelectedImages.IsEnabled = this.IsFileDatabaseAvailable();
        }
        #endregion

        #region Menu stub to test some code
        private void MenuItemTestSomeCode_Click(object sender, RoutedEventArgs e)
        {
        }
        #endregion

        #region Loading image sets
        // Load template, images, and video files...
        private async void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryGetTemplatePath(out string templateDatabasePath))
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, templateDatabasePath))
                {
                    // Add it to the list, as its originally invalid, but the user was asked to update it
                    // So its likely ok now.
                    this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
                    this.RecentFileSets_Refresh();
                    return;
                }
                if (false == await this.DoLoadImages(templateDatabasePath))
                {
                    this.StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                }
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<bool> DoLoadImages(string templateDatabasePath)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.StatusBar.SetMessage("Loading images, please wait...");
            Tuple<bool, string> results = await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath).ConfigureAwait(true);
            if (results.Item1 == false)
            {
                this.StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                if (false == string.IsNullOrWhiteSpace(results.Item2))
                {
                    // This is a first time load of a ddb, as indicated by the non-empty returned result of the ddb file path to delete.
                    // Since its failed, try to delete the empty .ddb file as otherwise its existance can be confusing to the user.
                    FilesFolders.TryDeleteFileIfExists(results.Item2);
                }
                return false;
            }
            else
            {
                Mouse.OverrideCursor = null;
                return true;
            }
        }

        // Load a recently used image set
        private async void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;

            // If its not a valid template, display a dialog and abort
            if (false == Dialogs.DialogIsFileValid(this, recentTemplatePath))
            {
                return;
            }

            if (false == await this.DoLoadImages(recentTemplatePath))
            {
                this.StatusBar.SetMessage("Aborted. Images were not added to the image set.");
            }
            Mouse.OverrideCursor = null;
        }

        // Add Images to Image Set 
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(this.FolderPath, out string folderPath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.StatusBar.SetMessage("Adding images, please wait...");
                this.TryBeginImageFolderLoad(this.FolderPath, folderPath, false);
            }
        }
        #endregion

        #region Updating Timelapse files
        // Invoke the Update Timelapse files program
        private void MenuItemUpgradeTimelapseFiles_Click(object sender, RoutedEventArgs e)
        {
            DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFiles.DialogUpgradeFilesAndFolders(this, string.Empty, VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            dialogUpdateFiles.ShowDialog();
        }
        #endregion

        #region Merging: Create an empty database
        // Create an empty Timelapse database based upon the template.
        // Abort if the template does not exist or cannot be opened, generating the various error messages as needed.
        // The empty file is normally used for merging.
        private async void MenuItemCreateEmptyDatabaseForMerging_Click(object sender, RoutedEventArgs e)
        {
            string abortMessage = "Aborted. Empty database was not created.";
            string successMessage = "Empty database created.";

            // Show an optional explanatory dialog
            if (this.State.SuppressCreateAnEmptyDatabaseDialog == false)
            {
                if (Dialogs.MenuFileCreateEmptyDatabaseExplainedDialog(this) == false)
                {
                    return;
                }
            }

            // Get the template path from the user
            if (this.TryGetTemplatePath(out string templateDatabasePath) == false)
            {
                return;
            }

            // Add the template to the recency list
            this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.RecentFileSets_Refresh();

            // If its not a valid template, display a dialog and abort
            if (false == Dialogs.DialogIsFileValid(this, templateDatabasePath))
            {
                return;
            }

            // Generate a pathname for the DDB file we want to create in the template folder
            // It generates a unique name from the name of the template but with the word 'Data' substituted for 'Template' (if possible) and with the .ddb suffix
            // If a file with that name already exists, it tries again by adding (0), then (1) etc.
            string rootFolder = Path.GetDirectoryName(templateDatabasePath);
            if (rootFolder == null)
            {
                // This shouldn't happen
                MessageBox.Show("Something went wrong. Database was not created, likely because its in a root directory (e.g., 'C:'.");
                this.StatusBar.SetMessage(abortMessage);
                return;
            }
            string ddbFileNameBase = Path.GetFileNameWithoutExtension(templateDatabasePath).Replace("Template", "Data");
            string ddbFileName = ddbFileNameBase + Constant.File.FileDatabaseFileExtension;
            string destinationDdbPath = Path.Combine(rootFolder, ddbFileName);
            int index = 0;
            while (File.Exists(destinationDdbPath))
            {
                // A ddb with that name already exists, so generate a new DDD file name
                ddbFileName = $"{ddbFileNameBase}({++index}){Constant.File.FileDatabaseFileExtension}";
                destinationDdbPath = Path.Combine(rootFolder, ddbFileName);
            }

            // We have a unique ddb path. Try to create the empty ddb file
            bool result = await MergeDatabases.TryCreateEmptyDatabaseFromTemplateAsync(
                templateDatabasePath, destinationDdbPath).ConfigureAwait(true);

            if (result == false)
            {
                // This is rare, don't bother trying to figure out what went wrong.
                MessageBox.Show("Could not create an empty database",
                    "Something went wrong. The empty database could not be created.");
                this.StatusBar.SetMessage(abortMessage);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            // Because a non-empty destination Ddb path was provided, it will just load that Ddb even if other Ddb's are available in that folder
            Tuple<bool, string> results = await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath, destinationDdbPath).ConfigureAwait(true);
            if (results.Item1 == false)
            {
                this.StatusBar.SetMessage(abortMessage);
                if (false == string.IsNullOrWhiteSpace(results.Item2))
                {
                    // This is a first time load of a ddb, as indicated by the non-empty returned result of the ddb file path to delete.
                    // Since its failed, try to delete the empty .ddb file as otherwise its existance can be confusing to the user.
                    FilesFolders.TryDeleteFileIfExists(results.Item2);
                }
            }

            if (Directory.GetFiles(rootFolder, "*" + Constant.File.FileDatabaseFileExtension).Length > 1)
            {
                // Since there is more than 1 ddb file, tell the user what the newly created file is called.
                Dialogs.NewFileNameAsOldFileNameExistsDialog(this, ddbFileName);
            }
            this.StatusBar.SetMessage(successMessage);
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Merging: Add or Replace one or more databases into the master
        private async void MenuItemAddDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressMergeDatabasesExplainedDialog == false)
            {
                if (Dialogs.MenuFileMergeDatabasesExplainedDialog(this) == false)
                {
                    return;
                }
            }

            Dialog.MergeSelectedDatabaseFiles mergeSelectedDatabaseFiles =
                new Dialog.MergeSelectedDatabaseFiles(this,
                    this.DataHandler.FileDatabase.FilePath,
                    this.DataHandler.FileDatabase.Database,
                    this.DataHandler.FileDatabase);
            bool? result = mergeSelectedDatabaseFiles.ShowDialog();
            if (result == false)
            {
                return;
            }

            // Reset a bunch of stuff here, as I am not sure if its handled in the OnFolderLoadingComplete method
            // Only reset these if we actually imported some detections, as otherwise nothing has changed.
            if (this.DataHandler?.FileDatabase?.CustomSelection?.DetectionSelections != null)
            {
                this.DataHandler.FileDatabase.CustomSelection.DetectionSelections.CurrentDetectionThreshold = -1; // this forces it to use the default in the new JSON
            }
            if (this.DataHandler?.FileDatabase != null)
            {
                this.DataHandler.FileDatabase.RefreshMarkers();
                if (this.DataHandler.FileDatabase.DetectionsExists(true))
                {
                    this.DataHandler.FileDatabase.RefreshDetectionsDataTable();
                    this.DataHandler.FileDatabase.RefreshClassificationsDataTable();
                }
            }

            // Since we are effectively doing a new image load, invoke this as it resets alot of things
            await this.OnFolderLoadingCompleteAsync(true);
        }
        #endregion

        #region Export/Import CSV file
        // Export data for this image set as a .csv file
        // Export data for this image set as a .csv file and preview in Excel 
        private async void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressSelectedCsvExportPrompt == false &&
                this.DataHandler.FileDatabase.FileSelectionEnum != FileSelectionEnum.All)
            {
                // Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
                if (Dialogs.MenuFileExportCSVOnSelectionDialog(this) == false)
                {
                    return;
                }
            }

            // Generate the candidate file name/path 
            string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + ".csv";

            // Get the selected filepath from the user
            if (false == Dialogs.TryGetFileFromUserUsingSaveFileDialog(
                "Export and save your data as a CSV file",
                csvFileName,
                String.Format("CSV files (*{0})|*{0}", Constant.File.CsvFileExtension),
                Constant.File.CsvFileExtension,
                out string selectedCSVFilePath))
            {
                // Abort, as file selection is cancelled
                this.StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            if (File.Exists(selectedCSVFilePath) && new FileInfo(selectedCSVFilePath).Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                // The file exists but its read only...
                Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                this.StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            // Backup the csv file if it exists, as the export will overwrite it. 
            this.StatusBar.SetMessage(FileBackup.TryCreateBackup(this.FolderPath, selectedCSVFilePath)
                ? "Backup of csv file made."
                : "No csv file backup was made.");

            try
            {
                // Show the Busy indicator
                this.BusyCancelIndicator.IsBusy = true;

                if (false == await CsvReaderWriter.ExportToCsv(this.DataHandler.FileDatabase, selectedCSVFilePath, this.State.CSVDateTimeOptions, this.State.CSVInsertSpaceBeforeDates, this.State.CSVIncludeFolderColumn, this.DataHandler.FileDatabase.ImageSet.RootFolder))
                {
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                    return;
                }
                // Hide the Busy indicator
                this.BusyCancelIndicator.IsBusy = false;
            }
            catch (Exception exception)
            {
                // Can't write the spreadsheet file
                Dialogs.MenuFileCantWriteSpreadsheetFileDialog(this, selectedCSVFilePath, exception.GetType().FullName, exception.Message);
                return;
            }

            MenuItem mi = (MenuItem)sender;
            if (mi == this.MenuItemExportAsCsvAndPreview)
            {
                // Show the file in excel
                // Create a process that will try to show the file
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    RedirectStandardOutput = false,
                    FileName = selectedCSVFilePath
                };
                if (ProcessExecution.TryProcessStart(processStartInfo) == false)
                {
                    // Can't open excel
                    Dialogs.MenuFileCantOpenExcelDialog(this, selectedCSVFilePath);
                    return;
                }
            }
            else if (this.State.SuppressCsvExportDialog == false)
            {
                Dialogs.MenuFileCSVDataExportedDialog(this, selectedCSVFilePath);
            }
            this.StatusBar.SetMessage("Data exported to " + selectedCSVFilePath);
        }

        // Import data from a CSV file. Display instructions and error messages as needed.
        private async void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressCsvImportPrompt == false)
            {
                // Tell the user how importing CSV files work. Give them the opportunity to abort.
                if (Dialogs.MenuFileHowImportingCSVWorksDialog(this) == false)
                {
                    return;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + Constant.File.CsvFileExtension;
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                                 "Select a .csv file to merge into the current image set",
                                 Path.Combine(this.DataHandler.FileDatabase.FolderPath, csvFileName),
                                 String.Format("Comma separated value files (*{0})|*{0}", Constant.File.CsvFileExtension),
                                 Constant.File.CsvFileExtension,
                                 out string csvFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            this.StatusBar.SetMessage(
                FileBackup.TryCreateBackup(this.FolderPath, this.DataHandler.FileDatabase.FileName)
                    ? "Backup of data file made."
                    : "No data file backup was made.");

            try
            {
                // Show the Busy indicator
                this.BusyCancelIndicator.IsBusy = true;

                Tuple<bool, List<string>> resultAndImportErrors = await CsvReaderWriter.TryImportFromCsv(csvFilePath, this.DataHandler.FileDatabase).ConfigureAwait(true);

                this.BusyCancelIndicator.IsBusy = false;

                if (resultAndImportErrors.Item1 == false)
                {
                    // Can't import CSV File
                    Dialogs.MenuFileCantImportCSVFileDialog(this, Path.GetFileName(csvFilePath), resultAndImportErrors.Item2);
                }
                else
                {
                    // Importing done.
                    Dialogs.MenuFileCSVFileImportedDialog(this, Path.GetFileName(csvFilePath), resultAndImportErrors.Item2);

                    // Reload the data
                    this.BusyCancelIndicator.IsBusy = true;
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                    this.BusyCancelIndicator.IsBusy = false;
                    this.StatusBar.SetMessage("CSV file imported.");
                }
            }
            catch (Exception exception)
            {
                // Can't import the .csv file
                Dialogs.MenuFileCantImportCSVFileDialog(this, Path.GetFileName(csvFilePath), exception.Message);
            }
        }
        #endregion

        #region Export the current image or video _file
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataHandler.ImageCache.Current == null)
            {
                TracePrint.NullException(nameof(this.DataHandler.ImageCache.Current));
                Dialogs.MenuFileCantExportCurrentImageDialog(this);
                return;
            }

            if (!this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath))
            {
                // Can't export the currently displayed image as a file
                Dialogs.MenuFileCantExportCurrentImageDialog(this);
                return;
            }
            // Get the file name of the current image 
            string sourceFile = this.DataHandler.ImageCache.Current.File;

            // Set up a Folder Browser with some instructions
            using (SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Export a copy of the currently displayed file",
                Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.DataHandler.ImageCache.Current.File)),
                FileName = sourceFile,
                OverwritePrompt = true
            })
            {
                // Display the Folder Browser dialog
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    // Set the source and destination file names, including the complete path
                    string sourcePath = this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath);
                    string destFileName = dialog.FileName;

                    // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                    // And giving some feedback about its success (or failure) 
                    try
                    {
                        File.Copy(sourcePath, destFileName, true);
                        this.StatusBar.SetMessage(sourceFile + " copied to " + destFileName);
                    }
                    catch (Exception exception)
                    {
                        TracePrint.PrintMessage($"Copy of '{sourceFile}' to '{destFileName}' failed. {exception}");
                        this.StatusBar.SetMessage($"Could not copy '{sourceFile}' for some reason.");
                    }
                }
            }
        }

        private void MenuItemExportAllSelectedImages_Click(object sender, RoutedEventArgs e)
        {
            ExportAllSelectedFiles exportAllSelectedFiles = new ExportAllSelectedFiles(this, this.DataHandler.FileDatabase);
            {
                exportAllSelectedFiles.ShowDialog();
            }
        }
        #endregion

        #region Rename the data file
        private void MenuItemRenameFileDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.DataHandler.FileDatabase.FileName, this)
            {
                Owner = this
            };
            if (true == renameFileDatabase.ShowDialog())
            {
                if (IsCondition.IsPathLengthTooLong(Path.Combine(this.FolderPath, renameFileDatabase.NewFilename), FilePathTypeEnum.DDB))
                {
                    Dialogs.DatabaseRenamedPathTooLongDialog(this, Path.Combine(this.FolderPath, renameFileDatabase.NewFilename));
                    this.StatusBar.SetMessage("Database file not renamed");
                    return;
                }
                if (File.Exists(Path.Combine(this.FolderPath, renameFileDatabase.NewFilename)))
                {
                    Dialogs.FileExistsDialog(this, renameFileDatabase.NewFilename);
                    return;
                }
                this.DataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFilename);
                this.StatusBar.SetMessage("Database file renamed");
                this.Title = $"{Constant.Defaults.MainWindowBaseTitle} ({renameFileDatabase.NewFilename})";
                if (IsCondition.IsPathLengthTooLong(Path.Combine(this.FolderPath, renameFileDatabase.NewFilename), FilePathTypeEnum.Backup))
                {
                    Dialogs.BackupPathTooLongDialog(this);
                }
                return;
            }
            this.StatusBar.SetMessage("Database file could not be renamed");
        }
        #endregion

        #region Close image set / Exit Timelapse
        // Close Image Set
        public void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            this.CloseImageSet();
            this.StatusBar.SetMessage("Image set closed");
        }

        // Exit Timelapse
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.CloseImageSet();
            this.Close();
            Application.Current.Shutdown();
        }
        #endregion
    }
}
