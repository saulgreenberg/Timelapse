using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;
using Path = System.IO.Path;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region Recognitions Submenu Opening 
        private void MenuItemRecognitions_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            bool detectionsExist = false;
            if (DataHandler?.FileDatabase != null)
            {
                detectionsExist = DataHandler.FileDatabase.DetectionsExists();
            }
            // Enable / disable various menu items depending on whether we are looking at the single image view or overview
            MenuItemPopulateWithDetectionCounts.IsEnabled = detectionsExist;
            MenuItemPopulateWithRecognitinoData.IsEnabled = detectionsExist;
            MenuBoundingBoxSetOptions.IsEnabled = detectionsExist;
        }
        #endregion

        #region Populate a data field with detection counts
        private void MenuItemPopulateDataFieldWithDetectionCounts_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler?.FileDatabase?.Database != null && DataHandler.FileDatabase.DetectionsExists())
            {
                // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
                if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                        "Populate a data field with Detection counts...",
                        optOut => { State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut; }))
                {

                    PopulateFieldWithDetectionCounts dialog = new(this, DataHandler.FileDatabase);
                    if (true == dialog.ShowDialog())
                    {
                        FileShow(DataHandler.ImageCache.CurrentRow, true);
                    }
                }
            }
        }
        #endregion

        #region Populate a data field with recognition data

        private void MenuItemPopulateDataFieldWithRecognitionData_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler?.FileDatabase?.Database != null && DataHandler.FileDatabase.DetectionsExists())
            {
                // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
                if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase,
                        State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                        "Populate a data field with Recognition data...",
                        optOut => { State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut; }))
                {

                    PopulateFieldWithRecognitionData dialog = new(this, DataHandler.FileDatabase);
                    if (true == dialog.ShowDialog())
                    {
                        FileShow(DataHandler.ImageCache.CurrentRow, true);
                    }
                }
            }
        }
        #endregion

        #region Set BoundingBox options
        private void MenuBoundingBoxSetOptions_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler?.FileDatabase?.Database != null && DataHandler.FileDatabase.DetectionsExists())
            {
                RecognitionOptionsForBoundingBox dialog = new(this, State);
                if (true == dialog.ShowDialog())
                {
                    FileShow(DataHandler.ImageCache.CurrentRow, true);
                }
            }
        }
        #endregion

        #region Import recognition data
        private async void MenuItemImportRecognitionData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemImportRecognitionData_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemImportRecognitionData_ClickAsync()
        {
            //
            // 1. Get the Json file from the user
            //
            string jsonFileName = File.RecognitionJsonDataFileName;
            if (false == Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                      "Select a .json file that contains the recognition data. It will be merged into the current image set",
                      Path.Combine(DataHandler.FileDatabase.RootPathToImages, jsonFileName),
                      String.Format("JSon files (*{0})|*{0}", File.JsonFileExtension),
                      File.JsonFileExtension,
                      out string jsonFilePath))
            {
                return;
            }

            // Check if the file name is image_recognitions.json; if so, warn the user that this file was likely created outside of Timelapse
            if (Path.GetFileName(jsonFilePath) == "image_recognition_file.json")
            {
                if (false == Dialogs.MenuFileRecognitionsLikelyFromExternallyRunAddaxAIDialog(this))
                {
                    return;
                }
            }
            //
            // 2. Read recognitions from the Json file.
            //    Note that this has its own progress handler
            BusyCancelIndicator.IsBusy = true;
            using Recognizer jsonRecognitions = await DataHandler.FileDatabase.JsonDeserializeRecognizerFileAsync(jsonFilePath).ConfigureAwait(true);
            if (jsonRecognitions == null)
            {
                // Abort. The json file could not be read.
                Dialogs.MenuFileRecognizersDataCouldNotBeReadDialog(this);
                BusyCancelIndicator.Reset(false);
                return;
            }

            if (jsonRecognitions.info == null)
            {
                // A null info signals that the operation was cancelled. 
                BusyCancelIndicator.Reset(false);
                return;
            }

            // The json file is now successfuly read into the jsonRecognitions structure

            //
            // Set up a progress handler that will update the progress bar for the remaining operations
            //
            Progress<ProgressBarArguments> progressHandlerArgs = new(value =>
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
            string subFolderPrefix = RecognitionUtilities.GetRecognizersFileSubfolderPathIfAny(DataHandler.FileDatabase.RootPathToImages, jsonFilePath);
            if (false == string.IsNullOrEmpty(subFolderPrefix))
            {
                RecognizerPathTestResults resultRecognizerPathTest = await RecognitionUtilities.IsRecognizersFilePathsLikelyRelativeToTheSubfolder(jsonRecognitions, DataHandler.FileDatabase.RootPathToImages, subFolderPrefix, progress, GlobalReferences.CancelTokenSource);

                // If the operation was cancelled, abort.
                if (resultRecognizerPathTest == RecognizerPathTestResults.Cancelled)
                {
                    BusyCancelIndicator.Reset(false);
                    return;
                }

                if (resultRecognizerPathTest == RecognizerPathTestResults.PathsRelativeToSubFolder)
                {


                    // Automatically add the prefix, so do so.
                    if (CancelStatusEnum.Cancelled == await RecognitionUtilities.RecognitionsAddPrefixToFilePaths(jsonRecognitions, subFolderPrefix, progress, GlobalReferences.CancelTokenSource))
                    {
                        BusyCancelIndicator.Reset(false);
                        return;
                    }
                }
                else if (resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles)
                {
                    string sampleFile = jsonRecognitions.images.Count == 0 ? string.Empty : jsonRecognitions.images[0].file;
                    if (false == Dialogs.RecognizerNoMatchToExistingFiles(this, sampleFile))
                    {
                        // The user decided to abort the operation 
                        BusyCancelIndicator.Reset(false);
                        return;
                    }
                }
            }
            else
            {
                // Likely outside the root folder. Check the paths again, and generate an error message if needed
                RecognizerPathTestResults resultRecognizerPathTest = await RecognitionUtilities.IsRecognizersFilePathsLikelyRelativeToTheSubfolder(jsonRecognitions, DataHandler.FileDatabase.RootPathToImages, subFolderPrefix, progress, GlobalReferences.CancelTokenSource);

                // If the operation was cancelled, abort.
                if (resultRecognizerPathTest == RecognizerPathTestResults.Cancelled)
                {
                    BusyCancelIndicator.Reset(false);
                    return;
                }

                if (resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles)
                {
                    string sampleFile = jsonRecognitions.images.Count == 0 ? string.Empty : jsonRecognitions.images[0].file;
                    if (false == Dialogs.RecognizerNoMatchToExistingFiles(this, sampleFile))
                    {
                        // The user decided to abort the operation 
                        BusyCancelIndicator.Reset(false);
                        return;
                    }
                }
                // else resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles
            }
            // The  recognition file is ready to go (and paths repaired if required)

            //
            // 4. Populate the database with the new recognition data found in the jsonRecognitions
            //
            List<string> foldersInDBListButNotInJSon = [];
            List<string> foldersInJsonButNotInDB = [];
            List<string> foldersInBoth = [];

            RecognizerImportResultEnum result = await DataHandler.FileDatabase.PopulateRecognitionTablesFromRecognizerAsync(jsonRecognitions, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, true, progress, GlobalReferences.CancelTokenSource);
            if (result == RecognizerImportResultEnum.Cancelled)
            {
                BusyCancelIndicator.Reset(false);
                return;
            }

            // 
            // 5. Reset various recognition settings as needed and refresh the display
            //
            if (result == RecognizerImportResultEnum.Success)
            {
                // Only reset these if we actually imported some detections, as otherwise nothing has changed.
                GlobalReferences.DetectionsExists = DataHandler.FileDatabase.DetectionsExists();
                if (DataHandler?.FileDatabase?.CustomSelection?.RecognitionSelections != null)
                {
                    DataHandler.FileDatabase.CustomSelection.RecognitionSelections.DetectionConfidenceLowerForUI = -1; // this forces it to use the default in the new JSON
                }
                // Reset the BoundingBox threshold to its new values.
                State.BoundingBoxDisplayThresholdResetToDefault();
                await FilesSelectAndShowAsync().ConfigureAwait(true);
            }

            // Hide the Busy indicator
            BusyCancelIndicator.Reset(false);

            //
            // 6. Check for incompatible detections (and delete old recognition data if needed) and/or Report the status.
            //
            if (result == RecognizerImportResultEnum.IncompatibleDetectionCategories
                || result == RecognizerImportResultEnum.IncompatibleClassificationCategories)
            {
                RecognitionsDeleteOldData messageBox = new(this, result);
                if (true == messageBox.ShowDialog())
                {
                    if (DataHandler?.FileDatabase == null)
                    {
                        //Shouldn't happen
                        TracePrint.NullException(nameof(DataHandler.FileDatabase));
                        return;
                    }
                    // Try again by deleting the old recognition data 
                    result = await DataHandler.FileDatabase.PopulateRecognitionTablesFromRecognizerAsync(jsonRecognitions, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, false, progress, GlobalReferences.CancelTokenSource);
                    if (result == RecognizerImportResultEnum.Cancelled)
                    {
                        BusyCancelIndicator.Reset(false);
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
                string trimmedJsonPath = null == DataHandler?.FileDatabase?.RootPathToImages
                    ? jsonFilePath
                    : Path.GetDirectoryName(jsonFilePath[(DataHandler.FileDatabase.RootPathToImages.Length + 1)..]);
                Dialogs.MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(this, trimmedJsonPath, details);
            }
            else
            {
                // Detections successfully imported message
                Dialogs.MenuFileRecognitionsSuccessfulyImportedDialog(this, details, jsonRecognitions.info.summary_report);
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

        #region AddaxAI menu items
        // Adjust menu based on whether AddaxAI is installed
        // NOTE: EcoAssist is now called AddaxAI. To smooth out the transition of names, we check both ecoassist paths and addax paths.
        private void MenuItem_OnAddaxAISubmenuOpened(object sender, RoutedEventArgs e)
        {
            string ecoAssistExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), EcoAssist.EcoAssistSubfolderExecutable);
            string ecoAssistExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), EcoAssist.EcoAssistSubfolderExecutable);
            string addaxAIExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AddaxAI.AddaxAISubfolderExecutable);
            string addaxAIExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AddaxAI.AddaxAISubfolderExecutable);
            // Enable running ecoassist only if the Ecoassist executable seems to be installed.
            MenuItemAddaxAIRun.IsEnabled = System.IO.File.Exists(ecoAssistExecutable1) || System.IO.File.Exists(ecoAssistExecutable2) ||
                                             System.IO.File.Exists(addaxAIExecutable1) || System.IO.File.Exists(addaxAIExecutable2);
        }

        // Download and install AddaxAI. 
        private void MenuItemAddaxAIDownload_Click(object sender, RoutedEventArgs e)
        {
            string ecoAssistExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),  EcoAssist.EcoAssistSubfolderExecutable);
            string ecoAssistExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), EcoAssist.EcoAssistSubfolderExecutable);
            string addaxAIExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AddaxAI.AddaxAISubfolderExecutable);
            string addaxAIExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AddaxAI.AddaxAISubfolderExecutable);

            // If an installation already exists, check with the user to see if he/she wants to continue...
            if (System.IO.File.Exists(ecoAssistExecutable1) || System.IO.File.Exists(ecoAssistExecutable2) ||
                System.IO.File.Exists(addaxAIExecutable1) || System.IO.File.Exists(addaxAIExecutable2))
            {
                if (false == Dialogs.AddaxAIAlreadyDownloaded(this))
                {
                    return;
                }
            }

            // Give the user information about the installation...
            if (true == Dialogs.AddaxAIInstallationInformaton(this))
            {
                ProcessExecution.TryProcessStart(new Uri(AddaxAI.AddaxAIDownload));
            }
        }

        private void MenuItemAddaxAIUninstall_Click(object sender, RoutedEventArgs e)
        {
            string ecoAssistExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), EcoAssist.EcoAssistSubfolderExecutable);
            string ecoAssistExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), EcoAssist.EcoAssistSubfolderExecutable);
            string addaxAIExecutable1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AddaxAI.AddaxAISubfolderExecutable);
            string addaxAIExecutable2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AddaxAI.AddaxAISubfolderExecutable);

            // If an installation already exists, check with the user to see if he/she wants to continue...
            if (false == System.IO.File.Exists(ecoAssistExecutable1) && false == System.IO.File.Exists(ecoAssistExecutable2) &&
                false == System.IO.File.Exists(addaxAIExecutable1) && false == System.IO.File.Exists(addaxAIExecutable2))
            {
                if (false == Dialogs.AddaxAINotInstalled(this))
                {
                    return;
                }
            }
            ProcessExecution.TryProcessStart(new Uri(AddaxAI.AddaxAIUninstallDownload));
        }

        private void MenuItemAddaxAIRun_Click(object sender, RoutedEventArgs e)
        {
            string homepath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string myDocuments = State.RecentlyOpenedTemplateFiles.TryGetMostRecent(out string mostRecent)
                ? Path.GetDirectoryName(mostRecent)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string initialFolderPath = DataHandler?.FileDatabase?.RootPathToImages ?? myDocuments;
            // Since we don't have an image set, ask the user to select the folder.
            // Or maybe we should always do that, where we use the initial folder as the root folder if the image set is open.
            if (false == Dialogs.TryGetFolderFromUserUsingOpenFileDialog("Run AddaxAI on the selected folder", initialFolderPath, out string selectedFolderPath))
            {
                return;
            }

            string cmd = @"/k ";
                   cmd += $@"(cd /d {programFiles} && ""{Path.Combine(programFiles, AddaxAI.AddaxAISubfolderExecutable)}"" timelapse ""{selectedFolderPath}"" ) || ";
                   cmd += $@"(cd /d {homepath} && ""{homepath}\{AddaxAI.AddaxAISubfolderExecutable}"" timelapse ""{selectedFolderPath}"" ) || ";
                   cmd += $@"(cd /d {programFiles} && ""{Path.Combine(programFiles, EcoAssist.EcoAssistSubfolderExecutable)}"" timelapse ""{selectedFolderPath}"" ) || ";
                   cmd += $@"(cd /d {homepath} && ""{homepath}\{EcoAssist.EcoAssistSubfolderExecutable}"" timelapse ""{selectedFolderPath}"" ) ";
            if (false == ProcessExecution.TryProcessRunCommand(cmd))
            {
                Dialogs.AddaxAICouldNotBeStarted(this);
            }
            else
            {
                Dialogs.AddaxAIApplicationInstructions(this);
            }
        }
        #endregion

    }
}
