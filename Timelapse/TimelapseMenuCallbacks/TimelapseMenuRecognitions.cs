using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;
using ToastNotifications.Core;
using ToastNotifications.Messages;
using Path = System.IO.Path;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region Recognitions Submenu Opening 
        private void MenuItemRecognitions_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            bool detectionsExist = false;
            if (this.DataHandler?.FileDatabase != null)
            {
                detectionsExist = this.DataHandler.FileDatabase.DetectionsExists();
            }
            // Enable / disable various menu items depending on whether we are looking at the single image view or overview
            this.MenuItemPopulateWithDetectionCounts.IsEnabled = detectionsExist;
            this.MenuBoundingBoxSetOptions.IsEnabled = detectionsExist;
        }
        #endregion

        #region Populate a data field with detection counts
        private void MenuItemPopulateDataFieldWithDetectionCounts_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataHandler?.FileDatabase?.Database != null && this.DataHandler.FileDatabase.DetectionsExists())
            {
                PopulateFieldWithDetectionCounts dialog = new PopulateFieldWithDetectionCounts(this, this.DataHandler.FileDatabase);
                if (true == dialog.ShowDialog())
                {
                    this.FileShow(this.DataHandler.ImageCache.CurrentRow, true);
                }
            }
        }
        #endregion

        private void MenuBoundingBoxSetOptions_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataHandler?.FileDatabase?.Database != null && this.DataHandler.FileDatabase.DetectionsExists())
            {
                RecognitionOptionsForBoundingBox dialog = new RecognitionOptionsForBoundingBox(this, this.State);
                if (true == dialog.ShowDialog())
                {
                    this.FileShow(this.DataHandler.ImageCache.CurrentRow, true);
                }
            }
        }

        #region Import recognition data
        private async void MenuItemImportRecognitionData_Click(object sender, RoutedEventArgs e)
        {
            //
            // 1. Get the Json file from the user
            //
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

            //
            // 2. Read recognitions from the Json file.
            //    Note that this has its own progress handler
            //
            this.BusyCancelIndicator.IsBusy = true;
            using (Recognizer jsonRecognitions = await this.DataHandler.FileDatabase.JsonDeserializeRecognizerFileAsync(jsonFilePath).ConfigureAwait(true))
            {
                if (jsonRecognitions == null)
                {
                    // Abort. The json file could not be read.
                    Dialogs.MenuFileRecognizersDataCouldNotBeReadDialog(this);
                    this.BusyCancelIndicator.Reset(false);
                    return;
                }

                if (jsonRecognitions.info == null)
                {
                    // A null info signals that the operation was cancelled. 
                    this.BusyCancelIndicator.Reset(false);
                    return;
                }

                // The json file is now successfuly read into the jsonRecognitions structure

                //
                // Set up a progress handler that will update the progress bar for the remaining operations
                //
                Progress<ProgressBarArguments> progressHandlerArgs = new Progress<ProgressBarArguments>(value =>
                {
                    // Update the progress bar
                    FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
                });
                IProgress<ProgressBarArguments> progress = progressHandlerArgs;


                //
                // 3. See if we need to adjust the folder path.
                //    Conditions:
                //    - json recognizer file was found in a sub-folder somewhere under the root folder
                //    - json recognizer's image paths do not have the sub-folder prefix
                //    - at least one file is found that matches a path comprising and added sub-folder prefix
                string subFolderPrefix = RecognitionUtilities.GetRecognizersFileSubfolderPathIfAny(this.DataHandler.FileDatabase.FolderPath, jsonFilePath);
                if (false == string.IsNullOrEmpty(subFolderPrefix))
                {
                    RecognizerPathTestResults resultRecognizerPathTest = await RecognitionUtilities.IsRecognizersFilePathsLikelyRelativeToTheSubfolder(jsonRecognitions, this.DataHandler.FileDatabase.FolderPath, subFolderPrefix, progress, GlobalReferences.CancelTokenSource);

                    // If the operation was cancelled, abort.
                    if (resultRecognizerPathTest == RecognizerPathTestResults.Cancelled)
                    {
                        this.BusyCancelIndicator.Reset(false);
                        return;
                    }

                    if (resultRecognizerPathTest == RecognizerPathTestResults.PathsRelativeToSubFolder)
                    {


                        // Automatically add the prefix, so do so.
                        if (CancelStatusEnum.Cancelled == await RecognitionUtilities.RecognitionsAddPrefixToFilePaths(jsonRecognitions, subFolderPrefix, progress, GlobalReferences.CancelTokenSource))
                        {
                            this.BusyCancelIndicator.Reset(false);
                            return;
                        }

                        // This is old code from before EcoAssist was added to Timelapse
                        // It asks the user to choose between options where the recognition file should be added as is,
                        // or wether the path should be repaired to add a prefix path that will make it consistent with a path from the root folder
                        //RecognitionsAddSubfolderToFilePaths messageBox = new RecognitionsAddSubfolderToFilePaths(this, subFolderPrefix);
                        //if (false == messageBox.ShowDialog())
                        //{
                        //    this.BusyCancelIndicator.Reset(false);
                        //    return;
                        //}
                        //if (messageBox.AddSubFolderPrefix)
                        //{
                        //    // The user indicated we should add the prefix, so do so.
                        //    if (CancelStatusEnum.Cancelled == await RecognitionUtilities.RecognitionsAddPrefixToFilePaths(jsonRecognitions, subFolderPrefix, progress, GlobalReferences.CancelTokenSource))
                        //    {
                        //        this.BusyCancelIndicator.Reset(false);
                        //        return;
                        //    }
                        //}
                    }
                    else if (resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles)
                    {
                        string sampleFile = jsonRecognitions.images.Count == 0 ? string.Empty : jsonRecognitions.images[0].file;
                        if (false == Dialogs.RecognizerNoMatchToExistingFiles(this, sampleFile))
                        {
                            // The user decided to abort the operation 
                            this.BusyCancelIndicator.Reset(false);
                            return;
                        }
                    }
                    // This is the only other thing it could be, so implicit. We do nothing as the paths are correct
                    //else resultRecognizerPathTest == RecognizerPathTestResults.PathsRelativeToRootFolder
                }
                else
                {
                    // Likely outside the root folder. Check the paths again, and generate an error message if needed
                    RecognizerPathTestResults resultRecognizerPathTest = await RecognitionUtilities.IsRecognizersFilePathsLikelyRelativeToTheSubfolder(jsonRecognitions, this.DataHandler.FileDatabase.FolderPath, subFolderPrefix, progress, GlobalReferences.CancelTokenSource);

                    // If the operation was cancelled, abort.
                    if (resultRecognizerPathTest == RecognizerPathTestResults.Cancelled)
                    {
                        this.BusyCancelIndicator.Reset(false);
                        return;
                    }

                    if (resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles)
                    {
                        string sampleFile = jsonRecognitions.images.Count == 0 ? string.Empty : jsonRecognitions.images[0].file;
                        if (false == Dialogs.RecognizerNoMatchToExistingFiles(this, sampleFile))
                        {
                            // The user decided to abort the operation 
                            this.BusyCancelIndicator.Reset(false);
                            return;
                        }
                    }
                    // else resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles
                }
                // The  recognition file is ready to go (and paths repaired if required)

                //
                // 4. Populate the database with the new recognition data found in the jsonRecognitions
                //
                List<string> foldersInDBListButNotInJSon = new List<string>();
                List<string> foldersInJsonButNotInDB = new List<string>();
                List<string> foldersInBoth = new List<string>();
                RecognizerImportResultEnum result = await this.DataHandler.FileDatabase.PopulateRecognitionTablesFromRecognizerAsync(jsonRecognitions, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, true, progress, GlobalReferences.CancelTokenSource);
                if (result == RecognizerImportResultEnum.Cancelled)
                {
                    this.BusyCancelIndicator.Reset(false);
                    return;
                }

                // 
                // 5. Reset various recognition settings as needed and refresh the display
                //
                if (result == RecognizerImportResultEnum.Success)
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
                this.BusyCancelIndicator.Reset(false);

                //
                // 6. Check for incompatible detections (and delete old recognition data if needed) and/or Report the status.
                //
                if (result == RecognizerImportResultEnum.IncompatibleDetectionCategories
                    || result == RecognizerImportResultEnum.IncompatibleClassificationCategories)
                {
                    RecognitionsDeleteOldData messageBox = new RecognitionsDeleteOldData(this, result);
                    if (true == messageBox.ShowDialog())
                    {
                        if (this.DataHandler?.FileDatabase == null)
                        {
                            //Shouldn't happen
                            TracePrint.NullException(nameof(this.DataHandler.FileDatabase));
                            return;
                        }
                        // Try again by deleting the old recognition data 
                        result = await this.DataHandler.FileDatabase.PopulateRecognitionTablesFromRecognizerAsync(jsonRecognitions, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, false, progress, GlobalReferences.CancelTokenSource);
                        if (result == RecognizerImportResultEnum.Cancelled)
                        {
                            this.BusyCancelIndicator.Reset(false);
                        }
                    }
                    else
                    {
                        // Cancelled
                        return;
                    }
                }
                else if (result == RecognizerImportResultEnum.JsonFileCouldNotBeRead)
                {
                    Dialogs.MenuFileRecognitionsFailedImportedDialog(this, result);
                    return;
                }

                string details = ComposeFolderDetails(foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth);
                if (result != RecognizerImportResultEnum.Success)
                {
                    // No matching folders in the DB and the recognizer file
                    Dialogs.MenuFileRecognitionDataNotImportedDialog(this, details);
                }
                else if (foldersInDBListButNotInJSon.Count > 0)
                {
                    // Some folders missing - show which folder paths in the DB are not in the recognizer file
                    // Trim the uneeded path from the jsonFilePath
                    string trimmedJsonPath = null == this.DataHandler?.FileDatabase?.FolderPath
                        ? jsonFilePath
                        : Path.GetDirectoryName(jsonFilePath.Substring(this.DataHandler.FileDatabase.FolderPath.Length + 1));
                    Dialogs.MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(this, trimmedJsonPath, details);
                }
                else
                {
                    // Detections successfully imported message
                    Dialogs.MenuFileRecognitionsSuccessfulyImportedDialog(this, details);
                }
            }
        }


        // Return a string that will be included in the message box invoked above that details the match (or mismatch) between the image set folder and recognition data folders
        private static string ComposeFolderDetails(List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
        {
            string folderDetails = string.Empty;
            if (foldersInDBListButNotInJSon.Count == 0 && foldersInJsonButNotInDB.Count == 0)
            {
                // All folders match, so don't show any details.
                return folderDetails;
            }

            folderDetails += "For the folders included in the Timelapse data (.ddb) file, this is what happened." + Environment.NewLine;

            // At this point, there is a mismatch, so we should show something.
            if (foldersInBoth.Count > 0)
            {
                folderDetails += "- Recognitions were updated for some/all images in these folders:";
                foreach (string folder in foldersInBoth)
                {
                    folderDetails += Environment.NewLine + "    \u2022 " + folder.TrimEnd('\\');
                }
                folderDetails += Environment.NewLine;
            }

            if (foldersInDBListButNotInJSon.Count > 0)
            {
                folderDetails += "- No recognitions were updated for images in these folders, as none are mentioned in the recognition file:";
                foreach (string folder in foldersInDBListButNotInJSon)
                {
                    folderDetails += Environment.NewLine + "    \u2022 " + folder.TrimEnd('\\');
                }
                folderDetails += Environment.NewLine;
            }

            if (foldersInJsonButNotInDB.Count > 0)
            {
                folderDetails += "- While the recognition file included images in these folders, they were skipped over " + Environment.NewLine;
                folderDetails += "   as their folder paths did not match any Relative Paths in the Timelapse database.";
                foreach (string folder in foldersInJsonButNotInDB)
                {
                    folderDetails += Environment.NewLine + "    \u2022 " + folder.TrimEnd('\\');
                }
            }
            return folderDetails;
        }
        #endregion

        #region EcoAssist menu items
       
        // Download and install ecoassist. 
        private void MenuItemEcoAssistDownload_Click(object sender, RoutedEventArgs e)
        {
            string ecoAssistExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),  Constant.EcoAssist.EcoAssistSubfolderExecutable);
            string ecoAssistExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Constant.EcoAssist.EcoAssistSubfolderExecutable);

            // If an installation already exists, check with the user to see if he/she wants to continue...
            if (File.Exists(ecoAssistExecutable1) || File.Exists(ecoAssistExecutable2))
            {
                if (false == Dialogs.EcoAssistAlreadyDownloaded(this))
                {
                    return;
                }
            }

            // Give the user information about the installation...
            if (true == Dialogs.EcoAssistInstallationInformaton(this))
            {
                ProcessExecution.TryProcessStart(new Uri(Constant.EcoAssist.EcoAssistDownload));
            }
        }

        private void MenuItemEcoAssistUninstall_Click(object sender, RoutedEventArgs e)
        {
            string ecoAssistExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Constant.EcoAssist.EcoAssistSubfolderExecutable);
            string ecoAssistExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Constant.EcoAssist.EcoAssistSubfolderExecutable);

            // If an installation already exists, check with the user to see if he/she wants to continue...
            if (false == File.Exists(ecoAssistExecutable1) && false == File.Exists(ecoAssistExecutable2))
            {
                if (false == Dialogs.EcoAssistNotInstalled(this))
                {
                    return;
                }
            }
            ProcessExecution.TryProcessStart(new Uri(Constant.EcoAssist.EcoAssistUninstallDownload));
        }

        private void MenuItemEcoAssistRun_Click(object sender, RoutedEventArgs e)
        {
            string homepath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string initialFolderPath = this.DataHandler?.FileDatabase?.FolderPath ?? myDocuments;
            // Since we don't have an image set, ask the user to select the folder.
            // Or maybe we should always do that, where we use the initial folder as the root folder if the image set is open.
            if (false == Dialogs.TryGetFolderFromUserUsingOpenFileDialog("Run EcoAssist on the selected folder", initialFolderPath, out string selectedFolderPath))
            {
                return;
            }

            string cmd = $@"/k (cd /d {programFiles} && ""{Path.Combine(programFiles, Constant.EcoAssist.EcoAssistSubfolderExecutable)}"" timelapse ""{selectedFolderPath}"" ) || (cd /d {homepath} && ""{homepath}\EcoAssist_files\EcoAssist\open.bat"" timelapse ""{selectedFolderPath}"" ) ";
            if (false == ProcessExecution.TryProcessRunCommand(cmd))
            {
                Dialogs.EcoAssistCouldNotBeStarted(this);
            }
            else
            {
                Dialogs.EcoAssistApplicationInstructions(this);
                MessageOptions toastOptions = new MessageOptions
                {
                    FontSize = 14, // set notification font size
                    FreezeOnMouseEnter = true, // set the option to prevent notification disappearing automatically if user move cursor on it
                    UnfreezeOnMouseLeave = true
                };
                this.ToastNotifier.ShowInformation("The EcoAssist application should appear shortly in a separate window (about 2-20 seconds)", toastOptions);
            }
        }
        #endregion

        private void MenuItem_OnEcoAssistSubmenuOpened(object sender, RoutedEventArgs e)
        {
            string ecoAssistExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Constant.EcoAssist.EcoAssistSubfolderExecutable);
            string ecoAssistExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Constant.EcoAssist.EcoAssistSubfolderExecutable);

            // Enable runing ecoassist only if the Ecoassist executable seems to be installed.
            this.MenuItemEcoAssistRun.IsEnabled = File.Exists(ecoAssistExecutable1) || File.Exists(ecoAssistExecutable2);
        }
    }
}
