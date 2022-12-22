using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Detection;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

// File Menu Callbacks
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
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
                    // Add it to the list,as its originally invalid, but the user was asked to update it
                    // So its likely ok now.
                    this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
                    this.RecentFileSets_Refresh();
                    return;
                }
                await this.DoLoadImages(templateDatabasePath);
            }
        }

        private async Task DoLoadImages(string templateDatabasePath)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.StatusBar.SetMessage("Loading images, please wait...");
            await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath).ConfigureAwait(true);
            this.StatusBar.SetMessage("Image set is now loaded.");
            Mouse.OverrideCursor = null;
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

            Mouse.OverrideCursor = Cursors.Wait;
            this.StatusBar.SetMessage("Loading images, please wait...");
            bool result = await this.TryOpenTemplateAndBeginLoadFoldersAsync(recentTemplatePath).ConfigureAwait(true);
            if (result == false)
            {
                this.State.MostRecentImageSets.TryRemove(recentTemplatePath);
                this.RecentFileSets_Refresh();
            }
            this.StatusBar.SetMessage("Image set is now loaded.");
            Mouse.OverrideCursor = null;
        }

        // Add Images to Image Set 
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(this.FolderPath, out string folderPath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.StatusBar.SetMessage("Adding images, please wait...");
                if (false == this.TryBeginImageFolderLoad(this.FolderPath, folderPath))
                {
                    this.StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                }
                this.StatusBar.SetMessage("Images added to the image set.");
                Mouse.OverrideCursor = null;
            }
        }

        #endregion

        #region Updating Timelapse files
        // Invoke the Update Timelapse files program
        private void MenuItemUpgradeTimelapseFiles_Click(object sender, RoutedEventArgs e)
        {
            DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFiles.DialogUpgradeFilesAndFolders(this, String.Empty, VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            dialogUpdateFiles.ShowDialog();
        }
        #endregion

        #region Merging databases
        private async void MenuItemMergeDatabases_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressMergeDatabasesPrompt == false)
            {
                if (Dialogs.MenuFileMergeDatabasesExplainedDialog(this) == false)
                {
                    return;
                }
            }
            // Get the location of the template, which also determines the root folder
            if (this.TryGetTemplatePath(out string templateDatabasePath))
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, templateDatabasePath))
                {
                    return;
                }

                // If an invalid file is found when trying to merge, it will raise a dialog box as well.
                Dialog.MergeChooseDatabaseFiles mergeChooseDatabaseFiles = new Dialog.MergeChooseDatabaseFiles(this, templateDatabasePath);
                if (mergeChooseDatabaseFiles.FoundInvalidFiles == true)
                {
                    // some of the found ddb files are invalid, so abort.
                    return;
                }

                bool? result = mergeChooseDatabaseFiles.ShowDialog();
                this.StatusBar.SetMessage(result == true ? "Merged database created" : "Aborted creation of merged database");
                this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
                if (result == true && mergeChooseDatabaseFiles.DatabaseToLoad != String.Empty)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    this.StatusBar.SetMessage("Loading images, please wait...");
                    await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath).ConfigureAwait(true);
                    this.StatusBar.SetMessage("Image set is now loaded.");

                    Mouse.OverrideCursor = null;
                }
            }
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

            if (File.Exists(selectedCSVFilePath) && new System.IO.FileInfo(selectedCSVFilePath).Attributes.HasFlag(System.IO.FileAttributes.ReadOnly))
            {
                // The file exists but its read only...
                Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                this.StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            // Backup the csv file if it exists, as the export will overwrite it. 
            if (FileBackup.TryCreateBackup(this.FolderPath, selectedCSVFilePath))
            {
                this.StatusBar.SetMessage("Backup of csv file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No csv file backup was made.");
            }

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
            if (FileBackup.TryCreateBackup(this.FolderPath, this.DataHandler.FileDatabase.FileName))
            {
                this.StatusBar.SetMessage("Backup of data file made.");
            }
            else
            {
                this.StatusBar.SetMessage("No data file backup was made.");
            }

            try
            {
                // Show the Busy indicator
                this.BusyCancelIndicator.IsBusy = true;

                Tuple<bool, List<string>> resultAndImportErrors;
                resultAndImportErrors = await CsvReaderWriter.TryImportFromCsv(csvFilePath, this.DataHandler.FileDatabase).ConfigureAwait(true);

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

        #region Import recognition data
        private async void MenuItemImportDetectionData_Click(object sender, RoutedEventArgs e)
        {
            // Get the Json file from the user
            string jsonFileName = Constant.File.RecognitionJsonDataFileName;
            if (false == Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                      "Select a .json file that contains the recognition data. It will be merged into the current image set",
                      Path.Combine(this.DataHandler.FileDatabase.FolderPath, jsonFileName),
                      String.Format("JSon files (*{0})|*{0}", Constant.File.JsonFileExtension),
                      Constant.File.JsonFileExtension,
                      out string jsonFilePath))
            {
                return;
            }

            List<string> foldersInDBListButNotInJSon = new List<string>();
            List<string> foldersInJsonButNotInDB = new List<string>();
            List<string> foldersInBoth = new List<string>();
            string addPrefixToPath = String.Empty;

            // mergeDetections, which indicates we should merge json data with existing data, is true if detections already exist
            bool mergeDetections = true == this.DataHandler?.FileDatabase?.DetectionsExists(); // If there are no detections, then merge will be false
            bool addSubfolderPrefix = false;

            // This could is commented out until we decide not to go this route. If so, strip out the decision code notonly here but n PopulateDetectionTablesAsync
            // (just look for how mergeDetections is used)
            // If detections exist, its asks the user if s/he wants to merge detections with the existing ones, or clear the old detections first
            // mergeDetections is set according to the response.
            //if (mergeDetections == true)
            //{
            //    // Since detections exist, ask the user if s/he wants to merge detections with the existing ones, or clear the old detections first
            //    Dialog.DetectionsMergeOrRemoveOldData messageBox = new Dialog.DetectionsMergeOrRemoveOldData(this);
            //    if (false == messageBox.ShowDialog())
            //    {
            //        return; // Cancelled
            //    }
            //    mergeDetections = messageBox.IsMergeSelected;
            //}

            // Determine whether the json is in a subfolder, the root folder, or outside of the root folder
            // If in a subfolder, ask the user if s/he wants to add the subfolder prefix
            Tuple<string, string, string> splitPath = Util.FilesFolders.SplitFullPath(this.DataHandler.FileDatabase.FolderPath, jsonFilePath);
            if (splitPath == null)
            {
                // Don't add prefix: file is outside of root folder and its subfolders
                System.Diagnostics.Debug.Print("file is outside of root folder and its subfolders");
            }
            else if (String.IsNullOrEmpty(splitPath.Item2))
            {
                // Don't add prefix: file is in the root folder
                System.Diagnostics.Debug.Print("file is in root folder");
            }
            else
            {
                // file is in a sub-folder, check whether there is evidence to add a prefix
                string sampleFilePath = DetectorUtilities.JsonGetFirstFilePath(jsonFilePath);
                System.Diagnostics.Debug.Print("In sub folder" + splitPath.Item2);
                System.Diagnostics.Debug.Print("Sample json file path is: " + sampleFilePath);
                if (sampleFilePath.StartsWith(splitPath.Item2))
                {
                    // Don't add prefix: Highly like that json was started in the root folder, as the path begins with this subfolder's name
                    // System.Diagnostics.Debug.Print("Highly like that json was started in the root folder, as the path begins with this subfolder's name");

                }
                else if (File.Exists(Path.Combine(this.DataHandler.FileDatabase.FolderPath, sampleFilePath)))
                {
                    // Don't add prefix: Highly likely that json was started in the root folder, as the file in the indicated path
                    System.Diagnostics.Debug.Print("Highly likely that json was started in the root folder, as the file in the indicated path");
                }
                else if (File.Exists(Path.Combine(this.DataHandler.FileDatabase.FolderPath, splitPath.Item2, sampleFilePath)))
                {
                    // Add prefix: Highly likely that json was started in this subfolder. Sample path does not have the subfolder prefix. If prefix added, the file exists
                    // Ask!!!
                    System.Diagnostics.Debug.Print("Highly likely that json was started in this subfolder. Sample path does not have the subfolder prefix. If prefix added, the file exists");
                    addPrefixToPath = splitPath.Item2;
                }
                else
                {
                    // Unclear if we should add the prefix. There is some evidence that json was started in this subfolder as sample path does not have the subfolder prefix
                    // but the file in the json could not be found.
                    // Ask?
                    System.Diagnostics.Debug.Print("Some evidence that json was started in this subfolder as sample path does not have the subfolder prefix");
                }

                //Dialog.DetectionsAddSubfolderToJsonFilePaths message = new Dialog.DetectionsAddSubfolderToJsonFilePaths(this, splitPath.Item2, sampleFilePath);
                //if (false == message.ShowDialog())
                //{
                //    return;
                //}
                //if (message.AddSubFolderPrefix)
                //{
                //    addSubfolderPrefix = message.AddSubFolderPrefix;
                //}
            }
            System.Diagnostics.Debug.Print("Add the prefix is " + addSubfolderPrefix.ToString());

            // Show the Busy indicator
            this.BusyCancelIndicator.IsBusy = true;

            // Load the detections
            RecognitionImportResultEnum result = await this.DataHandler.FileDatabase.PopulateDetectionTablesAsync(jsonFilePath, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, mergeDetections, addPrefixToPath).ConfigureAwait(true);
            if (result == RecognitionImportResultEnum.Success)
            {
                // Only reset these if we actually imported some detections, as otherwise nothing has changed.
                GlobalReferences.DetectionsExists = this.DataHandler.FileDatabase.DetectionsExists();
                if (this.DataHandler?.FileDatabase?.CustomSelection?.DetectionSelections != null)
                {
                    this.DataHandler.FileDatabase.CustomSelection.DetectionSelections.CurrentDetectionThreshold = -1; // this forces it to use the default in the new JSON
                }
                // Reset the BoundingBox threshold to its new values.
                this.State.BoundingBoxDisplayThresholdResetToDefault();
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
            }

            // Hide the Busy indicator
            this.BusyCancelIndicator.IsBusy = false;


            if (result == RecognitionImportResultEnum.IncompatableDetectionCategories
                || result == RecognitionImportResultEnum.IncompatableClassificationCategories
                || result == RecognitionImportResultEnum.JsonFileCouldNotBeRead)
            {
                Dialogs.MenuFileDetectionsFailedImportedDialog(this, result);
            }
            else
            {
                string details = ComposeFolderDetails(foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth);
                if (result != RecognitionImportResultEnum.Success)
                {
                    // No matching folders in the DB and the detector
                    Dialogs.MenuFileRecognitionDataNotImportedDialog(this, details);
                }
                else if (foldersInDBListButNotInJSon.Count > 0)
                {
                    // Some folders missing - show which folder paths in the DB are not in the detector
                    Dialogs.MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(this, details);
                }
                else
                {
                    // Detections successfully imported message
                    Dialogs.MenuFileDetectionsSuccessfulyImportedDialog(this, details);
                }
            }
        }

        // Return a string that will be included in the message box invoked above that details the match (or mismatch) between the image set folder and recognition data folders
        private static string ComposeFolderDetails(List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
        {
            string folderDetails = String.Empty;
            if (foldersInDBListButNotInJSon.Count == 0 && foldersInJsonButNotInDB.Count == 0)
            {
                // All folders match, so don't show any details.
                return folderDetails;
            }

            // At this point, there is a mismatch, so we should show something.
            if (foldersInBoth.Count > 0)
            {
                folderDetails += foldersInBoth.Count.ToString() + " of your folders had matching recognition data:" + Environment.NewLine;
                foreach (string folder in foldersInBoth)
                {
                    folderDetails += "\u2022 " + folder + Environment.NewLine;
                }
                folderDetails += Environment.NewLine;
            }

            if (foldersInDBListButNotInJSon.Count > 0)
            {
                folderDetails += foldersInDBListButNotInJSon.Count.ToString() + " of your folders had no matching recognition data:" + Environment.NewLine;
                foreach (string folder in foldersInDBListButNotInJSon)
                {
                    folderDetails += "\u2022 " + folder + Environment.NewLine;
                }
                folderDetails += Environment.NewLine;
            }
            if (foldersInJsonButNotInDB.Count > 0)
            {
                folderDetails += "The recognition file also included " + foldersInJsonButNotInDB.Count.ToString() + " other ";
                folderDetails += (foldersInJsonButNotInDB.Count == 1) ? "folder" : "folders";
                folderDetails += " not found in your folders:" + Environment.NewLine;
                foreach (string folder in foldersInJsonButNotInDB)
                {
                    folderDetails += "\u2022 " + folder + Environment.NewLine;
                }
            }
            return folderDetails;
        }
        #endregion

        #region Export the current image or video _file
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
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
                        TracePrint.PrintMessage(String.Format("Copy of '{0}' to '{1}' failed. {2}", sourceFile, destFileName, exception.ToString()));
                        this.StatusBar.SetMessage(String.Format("Could not copy '{0}' for some reason.", sourceFile));
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
                this.Title = String.Format("{0} ({1})", Constant.Defaults.MainWindowBaseTitle, renameFileDatabase.NewFilename);
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
