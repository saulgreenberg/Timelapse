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
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Standards;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
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
            this.MenuItemCopyFiles.IsEnabled = this.IsFileDatabaseAvailable();
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
            this.State.MostRecentImageSets.TryGetMostRecent(out string initialFolder);

            // Get the desired template path of the database
            Dialog.MergeCreateEmptyDatabase mergeCreateEmptyDatabase =
                new Dialog.MergeCreateEmptyDatabase(this, initialFolder);
            if (false == mergeCreateEmptyDatabase.ShowDialog())
            {
                this.StatusBar.SetMessage(abortMessage);
                return;
            }
            string templateDatabasePath = mergeCreateEmptyDatabase.TemplateTdbPath;
            string destinationDdbPath = mergeCreateEmptyDatabase.EmptyDatabaseDdbPath;
            // Add the template to the recency list
            this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.RecentFileSets_Refresh();

            // Because a non-empty destination Ddb path was provided, it will just load that Ddb even if other Ddb's are available in that folder
            Mouse.OverrideCursor = Cursors.Wait;
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
            this.StatusBar.SetMessage(successMessage);
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Merging: Check in one or more databases into the master
        private async void MenuItemCheckInDatabases_Click(object sender, RoutedEventArgs e)
        {
            Dialog.MergeCheckinDatabaseFiles mergeSelectedDatabaseFiles =
                new Dialog.MergeCheckinDatabaseFiles(this,
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

        #region Merging: Check out a database
        private void MenuItemCheckOutDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Get the relative and full path to the desired sub-folder location 
            Dialog.MergeCheckoutChooseSubfolder mergeCheckoutChooseSubfolder =
                new Dialog.MergeCheckoutChooseSubfolder(this, this.DataHandler.FileDatabase.FolderPath, this.templateDatabase.FilePath, this.DataHandler);

            this.StatusBar.SetMessage(false == mergeCheckoutChooseSubfolder.ShowDialog()
                ? "Check out database aborted."
                : "Check out database succeeded.");
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
                
                // TODO SAULXXX CHANGE TRUE TO STATE VARIABLE CONTAINING A PREFERENCE TO INCLUDE THE METADATA FOLDER LOCATIONS AT THE BEGINNING OF THE CSV
                if (false == await CsvReaderWriter.ExportToCsv(this.DataHandler.FileDatabase, this.DataEntryControls, selectedCSVFilePath,
                        this.State.CSVDateTimeOptions, this.State.CSVInsertSpaceBeforeDates, this.State.CSVIncludeFolderColumn, this.DataHandler.FileDatabase.ImageSet.RootFolder))
                {
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                    this.BusyCancelIndicator.IsBusy = false;
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

        private async void MenuItem_ExportFolderDataToCSV_Click(object sender, RoutedEventArgs e)
        {
            // Generate the candidate file name/path 
            if (this.DataHandler?.FileDatabase == null) return;
            //string csvFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + ".csv";

            string folderPath = this.DataHandler.FileDatabase.FolderPath;
            // Get a folder path from the user
            if (false == Dialogs.TryGetFolderFromUserUsingOpenFileDialog("Export and save your folder data as CSV files", folderPath, out string selectedCSVFolderPath))
            {
                this.StatusBar.SetMessage("Csv file export cancelled.");
                // Hide the Busy indicator
                this.BusyCancelIndicator.IsBusy = false;
                return;
            }

            List<string> filesToBeWritten = new List<string>();
            List<string> filesThatExist = new List<string>();
            List<string> filesThatExistReadOnly = new List<string>();
            foreach (MetadataInfoRow infoRow in this.DataHandler.FileDatabase.MetadataInfo)
            {
                string tentativeFileName = Path.Combine(selectedCSVFolderPath, infoRow.Alias + ".csv");
                string tempAlias = ControlsMetadata.MetadataUI.CreateTemporaryAliasIfNeeded(infoRow.Level, infoRow.Alias);
                filesToBeWritten.Add(tempAlias + ".csv");
                if (File.Exists(tentativeFileName))
                {
                    filesThatExist.Add(tempAlias + ".csv");
                    if (new FileInfo(tentativeFileName).Attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        filesThatExistReadOnly.Add(tempAlias + ".csv");
                    }
                }
            }

            if (filesThatExistReadOnly.Count > 0)
            {
                Dialogs.FilesCannotBeModified(this, filesThatExistReadOnly);
                this.StatusBar.SetMessage("Csv file export cancelled.");
                // Hide the Busy indicator
                this.BusyCancelIndicator.IsBusy = false;
                return;
            }
            if (filesThatExist.Count > 0)
            {
                // The file exists ...
                if (false == Dialogs.OverwriteListOfExistingFiles(this, filesThatExist))
                {
                    this.StatusBar.SetMessage("Csv file export cancelled.");
                    // Hide the Busy indicator
                    this.BusyCancelIndicator.IsBusy = false;
                    return;
                }
            }


            // Backup the csv file if it exists, as the export will overwrite it. 
            //this.StatusBar.SetMessage(FileBackup.TryCreateBackup(this.FolderPath, selectedCSVFilePath)
            //    ? "Backup of csv file made."
            //    : "No csv file backup was made.");
            try
            {
                // Show the Busy indicator
                this.BusyCancelIndicator.IsBusy = true;

                if (false == await CsvReaderWriter.ExportMetadataToCsv(this.DataHandler.FileDatabase, selectedCSVFolderPath, this.State.CSVDateTimeOptions, this.State.CSVInsertSpaceBeforeDates))
                {
                    // Hide the Busy indicator
                    this.BusyCancelIndicator.IsBusy = false;
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFolderPath, true);
                    return;
                }
                // Hide the Busy indicator
                this.BusyCancelIndicator.IsBusy = false;
                Dialogs.FolderDataExportedToCSV(this, selectedCSVFolderPath, filesToBeWritten);
            }
            catch
            {
                // Can't write the spreadsheet file
                this.BusyCancelIndicator.IsBusy = false;
                // Hide the Busy indicator
                Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFolderPath, true);
            }

            // If we are following the CamtrapDP Standard, create a CamtrapDP folder (if needed) and write extra files in the 
            // where those files convert the Timelapse data into the exact specs expected by CamtrapDP
            if (this.DataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard())
            {
                string camTrapDPFolder = Path.Combine(selectedCSVFolderPath, Constant.File.CamtrapDPExportFolder);
                // Create a folder holding the exported files
                if (false == Directory.Exists(camTrapDPFolder))
                {
                    Directory.CreateDirectory(camTrapDPFolder);
                }


                // Export the data package
                string dataPackageFilePath = Path.Combine(camTrapDPFolder, Constant.File.CamtrapDPDataPackageJson);
                List<string> datapackageMessages =
                    await CamtrapDPExportFiles.ExportCamtrapDPDataPackageToJsonFile(GlobalReferences.MainWindow.DataHandler.FileDatabase, dataPackageFilePath);
                if (null == datapackageMessages)
                {
                    Debug.Print("Couldn't write CamtrapDP data package");
                }
                else if (datapackageMessages.Count > 0)
                {
     //               Dialogs.CamtrapDPDataPackageMissingRequiredFields(GlobalReferences.MainWindow, datapackageMessages);
                }

                datapackageMessages = await CamtrapDPExportFiles.ExportCamtrapDPDeploymentToCsv(this.DataHandler.FileDatabase, selectedCSVFolderPath, this.State.CSVDateTimeOptions,
                        this.State.CSVInsertSpaceBeforeDates);
                if (null == datapackageMessages)
                {
                    // ERROR DIALOG HERE
                    Debug.Print("Couldn't write CamtrapDP deployment.csv");
                }
                else if (datapackageMessages.Count > 0)
                {
    //                Dialogs.CamtrapDPDataPackageMissingRequiredFields(GlobalReferences.MainWindow, datapackageMessages);
                }

                datapackageMessages = await CamtrapDPExportFiles.ExportCamtrapDPMediaObservationsToCsv(this.DataHandler.FileDatabase, this.DataEntryControls, selectedCSVFolderPath);

                    //datapackageMessages = await CamtrapDPExportFiles.ExportCamtrapDPMediaObservationsToCsv(this.DataHandler.FileDatabase, selectedCSVFolderPath, this.State.CSVDateTimeOptions,
                    //this.State.CSVInsertSpaceBeforeDates);
                if (null == datapackageMessages)
                {
                    // ERROR DIALOG HERE
                    Debug.Print("Couldn't write CamtrapDP deployment.csv");
                }
                else if (datapackageMessages.Count > 0)
                {
     //               Dialogs.CamtrapDPDataPackageMissingRequiredFields(GlobalReferences.MainWindow, datapackageMessages);
                }
            }
        }

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
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export a copy of the currently displayed file";
                dialog.Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.DataHandler.ImageCache.Current.File));
                dialog.FileName = sourceFile;
                dialog.OverwritePrompt = true;
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
                this.DataHandler.FileDatabase.RenameFileDatabase(renameFileDatabase.NewFilename);
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
