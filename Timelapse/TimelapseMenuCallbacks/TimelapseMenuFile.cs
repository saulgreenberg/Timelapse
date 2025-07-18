﻿using DialogUpgradeFiles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Standards;
using Timelapse.Util;
using DialogResult = System.Windows.Forms.DialogResult;
using File = Timelapse.Constant.File;
using FilePathTypeEnum = Timelapse.Enums.FilePathTypeEnum;
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
            FilePlayer_Stop(); // In case the FilePlayer is going
            RecentFileSets_Refresh();

            // Enable / disable various menu items depending on whether we are looking at the single image view or overview
            MenuItemExportThisImage.IsEnabled = IsDisplayingSingleImage();
            MenuItemExportSelectedImages.IsEnabled = IsFileDatabaseAvailable();
            MenuItemCopyFiles.IsEnabled = IsFileDatabaseAvailable();
        }
        #endregion

        #region Menu stub to test some code

        private void MenuItemTestSomeCode_Click(object sender, RoutedEventArgs e)
        {
            //TestSomeCodeDialog dialog = new TestSomeCodeDialog(this);
            //if (dialog.ShowDialog() == true)
            //{
            //}

        }
        #endregion

        #region Loading and adding image sets
        // Load template, images, and video files...
        private async void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetTemplatePath(out string templateDatabasePath))
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, templateDatabasePath))
                {
                    // Add it to the list, as its originally invalid, but the user was asked to update it
                    // So its likely ok now.
                    State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
                    RecentFileSets_Refresh();
                    return;
                }
                if (false == await DoLoadImages(templateDatabasePath))
                {
                    StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                }
                Mouse.OverrideCursor = null;
            }
        }

        private async Task<bool> DoLoadImages(string templateDatabasePath)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            StatusBar.SetMessage("Loading images, please wait...");
            Tuple<bool, string> results = await TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath).ConfigureAwait(true);
            if (results.Item1 == false)
            {
                StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                if (false == string.IsNullOrWhiteSpace(results.Item2))
                {
                    // This is a first time load of a ddb, as indicated by the non-empty returned result of the ddb file path to delete.
                    // Since its failed, try to delete the empty .ddb file as otherwise its existance can be confusing to the user.
                    FilesFolders.TryDeleteFileIfExists(results.Item2);
                }
                return false;
            }

            Mouse.OverrideCursor = null;
            return true;
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

            if (false == await DoLoadImages(recentTemplatePath))
            {
                StatusBar.SetMessage("Aborted. Images were not added to the image set.");
            }
            Mouse.OverrideCursor = null;
        }

        // Add Images to Image Set 
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (ShowFolderSelectionDialog(RootPathToImages, out string folderPath))
            {
                Mouse.OverrideCursor = Cursors.Wait;
                StatusBar.SetMessage("Adding images, please wait...");
                TryBeginImageFolderLoad(RootPathToImages, folderPath, false);
            }
        }
        #endregion

        #region Updating Timelapse files
        // Invoke the Update Timelapse files program
        private void MenuItemUpgradeTimelapseFiles_Click(object sender, RoutedEventArgs e)
        {
            DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFilesAndFolders(this, string.Empty, VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
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
            State.MostRecentImageSets.TryGetMostRecent(out string initialFolder);

            // Get the desired template path of the database
            MergeCreateEmptyDatabase mergeCreateEmptyDatabase =
                new MergeCreateEmptyDatabase(this, initialFolder);
            if (false == mergeCreateEmptyDatabase.ShowDialog())
            {
                StatusBar.SetMessage(abortMessage);
                return;
            }
            string templateDatabasePath = mergeCreateEmptyDatabase.TemplateTdbPath;
            string destinationDdbPath = mergeCreateEmptyDatabase.EmptyDatabaseDdbPath;
            // Add the template to the recency list
            State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            RecentFileSets_Refresh();

            // Because a non-empty destination Ddb path was provided, it will just load that Ddb even if other Ddb's are available in that folder
            Mouse.OverrideCursor = Cursors.Wait;
            Tuple<bool, string> results = await TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath, destinationDdbPath, false).ConfigureAwait(true);
            if (results.Item1 == false)
            {
                StatusBar.SetMessage(abortMessage);
                if (false == string.IsNullOrWhiteSpace(results.Item2))
                {
                    // This is a first time load of a ddb, as indicated by the non-empty returned result of the ddb file path to delete.
                    // Since its failed, try to delete the empty .ddb file as otherwise its existance can be confusing to the user.
                    FilesFolders.TryDeleteFileIfExists(results.Item2);
                }
            }
            StatusBar.SetMessage(successMessage);
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Merging: Check in one or more databases into the master
        private async void MenuItemCheckInDatabases_Click(object sender, RoutedEventArgs e)
        {
            MergeCheckinDatabaseFiles mergeSelectedDatabaseFiles =
                new MergeCheckinDatabaseFiles(this,
                    DataHandler.FileDatabase.FilePath,
                    DataHandler.FileDatabase.Database,
                    DataHandler.FileDatabase);
            bool? result = mergeSelectedDatabaseFiles.ShowDialog();
            if (result == false)
            {
                return;
            }

            // Reset a bunch of stuff here, as I am not sure if its handled in the OnFolderLoadingComplete method
            // Only reset these if we actually imported some detections, as otherwise nothing has changed.
            if (DataHandler?.FileDatabase?.CustomSelection?.RecognitionSelections != null)
            {
                DataHandler.FileDatabase.CustomSelection.RecognitionSelections.DetectionConfidenceLowerForUI = -1; // this forces it to use the default in the new JSON
            }
            if (DataHandler?.FileDatabase != null)
            {
                DataHandler.FileDatabase.RefreshMarkers();
                if (DataHandler.FileDatabase.DetectionsExists(true))
                {
                    await DataHandler.FileDatabase.RefreshDetectionsDataTableAsync();
                }
            }

            // Since we are effectively doing a new image load, invoke this as it resets alot of things
            await OnFolderLoadingCompleteAsync(true);
        }
        #endregion

        #region Merging: Check out a database
        private void MenuItemCheckOutDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Get the relative and full path to the desired sub-folder location 
            MergeCheckoutChooseSubfolder mergeCheckoutChooseSubfolder =
                new MergeCheckoutChooseSubfolder(this, DataHandler.FileDatabase.RootPathToDatabase, templateDatabase.FilePath, DataHandler, DataHandler.FileDatabase.IsShortcutToImageFolder ? DataHandler.FileDatabase.RootPathToImages : null);

            StatusBar.SetMessage(false == mergeCheckoutChooseSubfolder.ShowDialog()
                ? "Check out database aborted."
                : "Check out database succeeded.");
        }
        #endregion

        #region Export/Import image CSV file
        // Export data for this image set as a .csv file
        // Export data for this image set as a .csv file and preview in Excel 
        private async void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (State.SuppressSelectedCsvExportPrompt == false &&
                DataHandler.FileDatabase.FileSelectionEnum != FileSelectionEnum.All)
            {
                // Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
                if (Dialogs.MenuFileExportCSVOnSelectionDialog(this) == false)
                {
                    return;
                }
            }

            // Generate the candidate file name/path 
            string csvFileName = Path.Combine(DataHandler.FileDatabase.RootPathToDatabase, File.CSVImageDataFileName);

            // Get the selected filepath from the user
            if (false == Dialogs.TryGetFileFromUserUsingSaveFileDialog(
                "Export and save your data as a CSV file",
                csvFileName,
                String.Format("CSV files (*{0})|*{0}", File.CsvFileExtension),
                File.CsvFileExtension,
                out string selectedCSVFilePath))
            {
                // Abort, as file selection is cancelled
                StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            if (System.IO.File.Exists(selectedCSVFilePath) && new FileInfo(selectedCSVFilePath).Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                // The file exists but its read only...
                Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            // Backup the csv file if it exists, as the export will overwrite it. 
            StatusBar.SetMessage(FileBackup.TryCreateBackup(RootPathToDatabase, selectedCSVFilePath)
                ? "Backup of csv file made."
                : "No csv file backup was made.");

            try
            {
                // Show the Busy indicator
                BusyCancelIndicator.IsBusy = true;
                if (false == await CsvReaderWriter.ExportToCsv(DataHandler.FileDatabase, DataEntryControls, selectedCSVFilePath,
                        State.CSVDateTimeOptions, State.CSVInsertSpaceBeforeDates, State.CSVIncludeFolderColumn, DataHandler.FileDatabase.ImageSet.RootFolderName))
                {
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, selectedCSVFilePath, true);
                    BusyCancelIndicator.IsBusy = false;
                    return;
                }
                // Hide the Busy indicator
                BusyCancelIndicator.IsBusy = false;
            }
            catch (Exception exception)
            {
                // Can't write the spreadsheet file
                Dialogs.MenuFileCantWriteSpreadsheetFileDialog(this, selectedCSVFilePath, exception.GetType().FullName, exception.Message);
                return;
            }

            MenuItem mi = (MenuItem)sender;
            if (mi == MenuItemExportAsCsvAndPreview)
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
            else if (State.SuppressCsvExportDialog == false)
            {
                Dialogs.MenuFileCSVDataExportedDialog(this, selectedCSVFilePath);
            }
            StatusBar.SetMessage("Data exported to " + selectedCSVFilePath);
        }

        // Import data from a CSV file. Display instructions and error messages as needed.
        private async void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            if (State.SuppressCsvImportPrompt == false)
            {
                // Tell the user how importing CSV files work. Give them the opportunity to abort.
                if (Dialogs.MenuFileHowImportingCSVWorksDialog(this) == false)
                {
                    return;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(DataHandler.FileDatabase.FileName) + File.CsvFileExtension;
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                                 "Select a .csv file to merge into the current image set",
                                 Path.Combine(DataHandler.FileDatabase.RootPathToDatabase, csvFileName),
                                 String.Format("Comma separated value files (*{0})|*{0}", File.CsvFileExtension),
                                 File.CsvFileExtension,
                                 out string csvFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            StatusBar.SetMessage(
                FileBackup.TryCreateBackup(RootPathToDatabase, DataHandler.FileDatabase.FileName)
                    ? "Backup of data file made."
                    : "No data file backup was made.");

            try
            {
                // Show the Busy indicator
                BusyCancelIndicator.IsBusy = true;

                Tuple<bool, List<string>> resultAndImportErrors = await CsvReaderWriter.TryImportFromCsv(csvFilePath, DataHandler.FileDatabase).ConfigureAwait(true);

                BusyCancelIndicator.IsBusy = false;

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
                    BusyCancelIndicator.IsBusy = true;
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                    BusyCancelIndicator.IsBusy = false;
                    StatusBar.SetMessage("CSV file imported.");
                }
            }
            catch (Exception exception)
            {
                // Can't import the .csv file
                Dialogs.MenuFileCantImportCSVFileDialog(this, Path.GetFileName(csvFilePath), exception.Message);
            }
        }
        #endregion

        #region Export All data to CSV
        private async void MenuItem_ExportAllDataToCSV_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler?.FileDatabase == null) return;

            // If we are not viewing all files, generate a warning and exit.
            int filesTotalCount = DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.All);
            int filesSelectedCount = DataHandler.FileDatabase.FileTable.RowCount;
            if (filesTotalCount != filesSelectedCount)
            {
                Dialogs.MenuFileExportRequiresAllFilesSelected(this, "all your data ");
                StatusBar.SetMessage("Export cancelled.");
                return;
            }

            // Get the folder path
            string initialFolderPath = DataHandler.FileDatabase.RootPathToDatabase;

            if (false == Dialogs.TryGetFolderFromUserUsingOpenFileDialog(
                    $"Select a folder to contain the {File.CsvExportFolder} folder and its csv files",
                    initialFolderPath, out string csvExportFolder))
            {
                StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            // Create the export folder and ensure it exists
            csvExportFolder = Path.Combine(csvExportFolder, File.CsvExportFolder);
            if (false == Directory.Exists(csvExportFolder))
            {
                Directory.CreateDirectory(csvExportFolder);
            }

            // Lists used For error checking
            List<string> filesThatExist = new List<string>();
            List<string> filesThatExistReadOnly = new List<string>();

            // Compose the image file path
            string imageFilePath = Path.Combine(csvExportFolder, File.CSVImageDataFileName);
            if (System.IO.File.Exists(imageFilePath))
            {
                filesThatExist.Add(File.CSVImageDataFileName);
                if (new FileInfo(imageFilePath).Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    filesThatExistReadOnly.Add(File.CSVImageDataFileName);
                }
            }

            //Compose the folder level data files
            List<string> filesToBeWritten = new List<string> { File.CSVImageDataFileName };
            foreach (MetadataInfoRow infoRow in DataHandler.FileDatabase.MetadataInfo)
            {
                string tentativeFileName = Path.Combine(csvExportFolder, infoRow.Alias + ".csv");
                string tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(infoRow.Level, infoRow.Alias);
                filesToBeWritten.Add(tempAlias + ".csv");
                if (System.IO.File.Exists(tentativeFileName))
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
                StatusBar.SetMessage("Csv file export cancelled.");
                // Hide the Busy indicator
                BusyCancelIndicator.IsBusy = false;
                return;
            }

            if (filesThatExist.Count > 0)
            {
                // The file exists ...
                if (false == Dialogs.OverwriteListOfExistingFiles(this, filesThatExist))
                {
                    StatusBar.SetMessage("Csv file export cancelled.");
                    return;
                }
            }

            // TODO We don't currently backup existing csv files if the export will overwrite it. But not sure we really need to

            // Export the Image data
            try
            {
                // Show the Busy indicator
                BusyCancelIndicator.IsBusy = true;
                if (false == await CsvReaderWriter.ExportToCsv(DataHandler.FileDatabase, DataEntryControls, imageFilePath,
                        State.CSVDateTimeOptions, State.CSVInsertSpaceBeforeDates, State.CSVIncludeFolderColumn, DataHandler.FileDatabase.ImageSet.RootFolderName))
                {
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, imageFilePath, true);
                    BusyCancelIndicator.IsBusy = false;
                    StatusBar.SetMessage("Csv file export cancelled.");
                    return;
                }
                // Hide the Busy indicator
                BusyCancelIndicator.IsBusy = false;
            }
            catch (Exception exception)
            {
                // Can't write the spreadsheet file
                BusyCancelIndicator.IsBusy = false;
                Dialogs.MenuFileCantWriteSpreadsheetFileDialog(this, imageFilePath, exception.GetType().FullName, exception.Message);
                StatusBar.SetMessage("Csv file export cancelled.");
                return;
            }

            try
            {
                // Show the Busy indicator
                BusyCancelIndicator.IsBusy = true;
                if (false == await CsvReaderWriter.ExportMetadataToCsv(DataHandler.FileDatabase, csvExportFolder, State.CSVDateTimeOptions, State.CSVInsertSpaceBeforeDates))
                {
                    // Hide the Busy indicator
                    BusyCancelIndicator.IsBusy = false;
                    Dialogs.FileCantOpen(GlobalReferences.MainWindow, csvExportFolder, true);
                    return;
                }
                // Data files successfully exported
                BusyCancelIndicator.IsBusy = false;
                Dialogs.AllDataExportedToCSV(this, csvExportFolder, filesToBeWritten, true);
            }
            catch
            {
                // Can't write the spreadsheet file
                BusyCancelIndicator.IsBusy = false;
                Dialogs.FileCantOpen(GlobalReferences.MainWindow, csvExportFolder, true);
            }
        }
        #endregion

        #region Export Camtrap files
        private async void MenuItem_ExportCamtrapDP_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler?.FileDatabase == null || false == DataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard()) return;

            // We want to show the prompt only if the promptState is true, and we are  viewing all images
            int filesTotalCount = DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.All);
            int filesSelectedCount = DataHandler.FileDatabase.FileTable.RowCount;
            if (filesTotalCount != filesSelectedCount)
            {
                Dialogs.MenuFileExportRequiresAllFilesSelected(this, $"as {Constant.Standards.CamtrapDPStandard}");
                StatusBar.SetMessage("Export cancelled.");
                return;
            }

            if (false == Dialogs.ExportToCamtrapDPExplanation(this))
            {
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            // Get a folder path from the user
            if (false == Dialogs.TryGetFolderFromUserUsingOpenFileDialog(
                    $"Select a folder to contain the {File.CamtrapDPExportFolder} folder and CamtrapDP files",
                    DataHandler.FileDatabase.RootPathToDatabase, out string camTrapDPFolder))
            {
                StatusBar.SetMessage("Export cancelled.");
                return;
            }

            // Create a CamtrapDP export folder (if needed)
            camTrapDPFolder = Path.Combine(camTrapDPFolder, File.CamtrapDPExportFolder);
            if (false == Directory.Exists(camTrapDPFolder))
            {
                Directory.CreateDirectory(camTrapDPFolder);
            }

            // Write all the data into the expected camtrapDP files
            // where those files convert the Timelapse data into the exact CamtrapDP specs
            // Export the data package
            string dataPackageFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPDataPackageJsonFilename);
            List<string> datapackageMessages = await CamtrapDPExportFiles.ExportCamtrapDPDataPackageToJsonFile(GlobalReferences.MainWindow.DataHandler.FileDatabase, dataPackageFilePath);
            if (null == datapackageMessages)
            {
                // Something went wrong.
                Dialogs.MenuFileExportFailedForUnknownReasonDialog(GlobalReferences.MainWindow, dataPackageFilePath);
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            if (datapackageMessages.Count > 0)
            {
                datapackageMessages.Insert(0, $"{Environment.NewLine}In {File.CamtrapDPDataPackageJsonFilename}:");
            }

            // Export the deployment csv file
            string deploymentFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPDeploymentCSVFilename);
            List<string> deploymentMessages = await CamtrapDPExportFiles.ExportCamtrapDPDeploymentToCsv(DataHandler.FileDatabase, deploymentFilePath);
            if (null == deploymentMessages)
            {
                Dialogs.MenuFileExportFailedForUnknownReasonDialog(GlobalReferences.MainWindow, deploymentFilePath);
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            if (deploymentMessages.Count > 0)
            {
                deploymentMessages.Insert(0, $"{Environment.NewLine}In {File.CamtrapDPDeploymentCSVFilename}:");
            }

            // Export the media and observations csv file
            // This has a progress indicator as it could be a big file
            string mediaFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPMediaCSVFilename);
            string observationsFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPObservationsCSVFilename);

            BusyCancelIndicator.IsBusy = true;
            List<string> mediaObservationsMessages = await CamtrapDPExportFiles.ExportCamtrapDPMediaObservationsToCsv(DataHandler.FileDatabase, DataEntryControls, mediaFilePath, observationsFilePath);
            BusyCancelIndicator.IsBusy = false;
            if (null == mediaObservationsMessages)
            {
                Dialogs.MenuFileExportFailedForUnknownReasonDialog(GlobalReferences.MainWindow, $"{Environment.NewLine}{mediaFilePath} and {observationsFilePath}:");
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            if (mediaObservationsMessages.Count > 0)
            {
                mediaObservationsMessages.Insert(0, $"{Environment.NewLine}In {File.CamtrapDPMediaCSVFilename} and {File.CamtrapDPObservationsCSVFilename}:");
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            List<string> allMessages = datapackageMessages.Concat(deploymentMessages).Concat(mediaObservationsMessages).ToList();
            if (allMessages.Count > 0)
            {
                Dialogs.CamtrapDPDataPackageMissingRequiredFields(GlobalReferences.MainWindow, allMessages);
                StatusBar.SetMessage("Export completed, with warnings.");
            }
            else
            {
                Dialogs.AllDataExportedToCSV(this, camTrapDPFolder,
                    new List<string>
                    {
                        File.CamtrapDPDataPackageJsonFilename, File.CamtrapDPDeploymentCSVFilename, File.CamtrapDPMediaCSVFilename,
                        File.CamtrapDPObservationsCSVFilename
                    }, false);
                StatusBar.SetMessage("Export completed.");
            }
        }
        #endregion

        #region Export the current image or video _file
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler.ImageCache.Current == null)
            {
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                Dialogs.MenuFileCantExportCurrentImageDialog(this);
                return;
            }

            if (!DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages))
            {
                // Can't export the currently displayed image as a file
                Dialogs.MenuFileCantExportCurrentImageDialog(this);
                return;
            }
            // Get the file name of the current image 
            string sourceFile = DataHandler.ImageCache.Current.File;

            // Set up a Folder Browser with some instructions
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export a copy of the currently displayed file";
                dialog.Filter = String.Format("*{0}|*{0}", Path.GetExtension(DataHandler.ImageCache.Current.File));
                dialog.FileName = sourceFile;
                dialog.OverwritePrompt = true;
                // Display the Folder Browser dialog
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    // Set the source and destination file names, including the complete path
                    string sourcePath = DataHandler.ImageCache.Current.GetFilePath(RootPathToImages);
                    string destFileName = dialog.FileName;

                    // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                    // And giving some feedback about its success (or failure) 
                    try
                    {
                        System.IO.File.Copy(sourcePath, destFileName, true);
                        StatusBar.SetMessage(sourceFile + " copied to " + destFileName);
                    }
                    catch (Exception exception)
                    {
                        TracePrint.PrintMessage($"Copy of '{sourceFile}' to '{destFileName}' failed. {exception}");
                        StatusBar.SetMessage($"Could not copy '{sourceFile}' for some reason.");
                    }
                }
            }
        }

        private void MenuItemExportAllSelectedImages_Click(object sender, RoutedEventArgs e)
        {
            ExportAllSelectedFiles exportAllSelectedFiles = new ExportAllSelectedFiles(this, DataHandler.FileDatabase);
            {
                exportAllSelectedFiles.ShowDialog();
            }
        }
        #endregion

        #region Rename the data file
        private void MenuItemRenameFileDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(DataHandler.FileDatabase.FileName, this)
            {
                Owner = this
            };
            if (true == renameFileDatabase.ShowDialog())
            {
                if (IsCondition.IsPathLengthTooLong(Path.Combine(RootPathToDatabase, renameFileDatabase.NewFilename), FilePathTypeEnum.DDB))
                {
                    Dialogs.DatabaseRenamedPathTooLongDialog(this, Path.Combine(RootPathToDatabase, renameFileDatabase.NewFilename));
                    StatusBar.SetMessage("Database file not renamed");
                    return;
                }
                if (System.IO.File.Exists(Path.Combine(RootPathToDatabase, renameFileDatabase.NewFilename)))
                {
                    Dialogs.FileExistsDialog(this, renameFileDatabase.NewFilename);
                    return;
                }
                DataHandler.FileDatabase.RenameFileDatabase(renameFileDatabase.NewFilename);
                StatusBar.SetMessage("Database file renamed");
                Title = $"{Defaults.MainWindowBaseTitle} ({renameFileDatabase.NewFilename})";
                if (IsCondition.IsPathLengthTooLong(Path.Combine(RootPathToDatabase, renameFileDatabase.NewFilename), FilePathTypeEnum.Backup))
                {
                    Dialogs.BackupPathTooLongDialog(this);
                }
                return;
            }
            StatusBar.SetMessage("Database file could not be renamed");
        }
        #endregion

        #region Close image set / Exit Timelapse
        // Close Image Set
        public void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            CloseImageSet();
            StatusBar.SetMessage("Image set closed");
        }

        // Exit Timelapse
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            CloseImageSet();
            Close();
            Application.Current.Shutdown();
        }
        #endregion
    }
}
