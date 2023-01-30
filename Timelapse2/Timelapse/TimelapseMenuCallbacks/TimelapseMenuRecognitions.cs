using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;

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
                    this.BusyCancelIndicator.Reset();
                    return;
                }

                if (jsonRecognitions.info == null)
                {
                    // A null info signals that the operation was cancelled. 
                    this.BusyCancelIndicator.Reset();
                    return;
                }

                // The json file is now successfuly read into the jsonRecognitions structure

                //
                // Set up a progress handler that will update the progress bar for the remaining operations
                //
                Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
                {
                    // Update the progress bar
                    FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
                });
                IProgress<ProgressBarArguments> progress = progressHandler;


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
                        this.BusyCancelIndicator.Reset();
                        return;
                    }

                    if (resultRecognizerPathTest == RecognizerPathTestResults.PathsRelativeToSubFolder)
                    {
                        RecognitionsAddSubfolderToFilePaths messageBox = new RecognitionsAddSubfolderToFilePaths(this, subFolderPrefix);
                        if (false == messageBox.ShowDialog())
                        {
                            this.BusyCancelIndicator.Reset();
                            return;
                        }
                        if (messageBox.AddSubFolderPrefix)
                        {
                            // The user indicated we should add the prefix, so do so.
                            if (CancelStatusEnum.Cancelled == await RecognitionUtilities.RecognitionsAddPrefixToFilePaths(jsonRecognitions, subFolderPrefix, progress, GlobalReferences.CancelTokenSource))
                            {
                                this.BusyCancelIndicator.Reset();
                                return;
                            }
                        }
                    }
                    else if (resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles)
                    {
                        string sampleFile = jsonRecognitions.images.Count == 0 ? String.Empty : jsonRecognitions.images[0].file;
                        if (false == Dialogs.RecognizerNoMatchToExistingFiles(this, sampleFile))
                        {
                            // The user decided to abort the operation 
                            this.BusyCancelIndicator.Reset();
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
                        this.BusyCancelIndicator.Reset();
                        return;
                    }

                    if (resultRecognizerPathTest == RecognizerPathTestResults.NoMatchToExistingFiles)
                    {
                        string sampleFile = jsonRecognitions.images.Count == 0 ? String.Empty : jsonRecognitions.images[0].file;
                        if (false == Dialogs.RecognizerNoMatchToExistingFiles(this, sampleFile))
                        {
                            // The user decided to abort the operation 
                            this.BusyCancelIndicator.Reset();
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
                RecognizerImportResultEnum result = await this.DataHandler.FileDatabase.PopulateRecognitionTablesFromRecognizerAsync(jsonRecognitions, jsonFilePath, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, true, progress, GlobalReferences.CancelTokenSource);
                if (result == RecognizerImportResultEnum.Cancelled)
                {
                    this.BusyCancelIndicator.Reset();
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
                this.BusyCancelIndicator.Reset();

                //
                // 6. Check for incompatable detections (and delete old recognition data if needed) and/or Report the status.
                //
                if (result == RecognizerImportResultEnum.IncompatableDetectionCategories
                    || result == RecognizerImportResultEnum.IncompatableClassificationCategories)
                {
                    RecognitionsDeleteOldData messageBox = new RecognitionsDeleteOldData(this, result);
                    if (true == messageBox.ShowDialog())
                    {
                        // Try again by deleting the old recognition data 
                        result = await this.DataHandler.FileDatabase.PopulateRecognitionTablesFromRecognizerAsync(jsonRecognitions, jsonFilePath, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth, false, progress, GlobalReferences.CancelTokenSource);
                        if (result == RecognizerImportResultEnum.Cancelled)
                        {
                            this.BusyCancelIndicator.Reset();
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
                    Dialogs.MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(this, details);
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
            string folderDetails = String.Empty;
            if (foldersInDBListButNotInJSon.Count == 0 && foldersInJsonButNotInDB.Count == 0)
            {
                // All folders match, so don't show any details.
                return folderDetails;
            }

            folderDetails += "For the folders listed in the Timelapse database, this is what happened." + Environment.NewLine;

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
    }
}
