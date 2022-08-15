using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.ImageSetLoadingPipeline;
using Timelapse.QuickPaste;
using Timelapse.Util;
using ToastNotifications.Core;
using ToastNotifications.Messages;

namespace Timelapse
{
    /// <summary>
    /// Image Set Loaing - Primary Methods to do it
    /// </summary>
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region TryGetTemplatePath
        // Prompt user to select a template.
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // Default the template selection dialog to the most recently opened database
            this.State.MostRecentImageSets.TryGetMostRecent(out string defaultTemplateDatabasePath);
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                "Select a TimelapseTemplate.tdb file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension),
                                             Constant.File.TemplateDatabaseFileExtension,
                                             out templateDatabasePath) == false)
            {
                return false;
            }

            string templateDatabaseDirectoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (String.IsNullOrEmpty(templateDatabaseDirectoryPath))
            {
                return false;
            }
            return true;
        }
        #endregion

        #region TryOpenTemplateAndBeginLoadFoldersAsync
        // Load the specified database template and then the associated images. 
        // templateDatabasePath is the Fully qualified path to the template database file.
        // Returns true only if both the template and image database file are loaded (regardless of whether any images were loaded) , false otherwise
        private async Task<bool> TryOpenTemplateAndBeginLoadFoldersAsync(string templateDatabasePath)
        {
            this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.RecentFileSets_Refresh();

            // Try to create or open the template database
            // As we can't have out parameters in an async method, we return the state and the desired templateDatabase as a tuple
            Tuple<bool, TemplateDatabase> tupleResult = await TemplateDatabase.TryCreateOrOpenAsync(templateDatabasePath).ConfigureAwait(true);
            this.templateDatabase = tupleResult.Item2;
            if (!tupleResult.Item1)
            {
                // Notify the user the template couldn't be loaded rather than silently doing nothing
                Mouse.OverrideCursor = null;
                Dialogs.TemplateFileNotLoadedAsCorruptDialog(this, templateDatabasePath);
                return false;
            }

            string unknownTypes = this.templateDatabase.AreControlsOfKnownTypes();
            if (unknownTypes != String.Empty)
            {
                // The template contains an item of an unknown type. 
                // This could be because we are trying to open a template with an old version of Timelapse
                // that doesn't know about newer types.
                Dialogs.TemplateIncludesControlOfUnknownType(this, unknownTypes);
                return false;
            }

            // The .tdb templateDatabase should now be loaded
            // Try to get the image database file path 
            // importImages will be true if its a new image database file, (meaning we should later ask the user to try to import some images)
            if (this.TrySelectDatabaseFile(templateDatabasePath, out string fileDatabaseFilePath, out bool importImages) == false)
            {
                // No image database file was selected
                return false;
            }
            else
            {   // XXXX THIS IS WHERE WE SHOULD CHECK THE DATAFILE. CAN WE DO IT BEFORE OPENING THE TEMPLATE?
                // THE IMPORTIMAGES FLAG DOESN"T SEEM TO DO WHAT THE ABOVE SUGGESTS... LOOK INTO IT
                if (false == Dialogs.DialogIsFileValid(this, fileDatabaseFilePath))
                {
                    // System.Diagnostics.Debug.Print(Util.FilesFolders.QuickCheckDatabaseFile("Oops: " + fileDatabaseFilePath).ToString());
                    return false;
                }
            }

            if (this.State.IsViewOnly && importImages == true)
            {
                // There are no .ddb files in this folder, which means Timelapse would normally try to create one.
                // But if Timelapse was started in a ReadOnly state, that is not allowed. Tell the user, and abort.
                Dialogs.ViewOnlySoDatabaseCannotBeCreated(this);
                return false;
            }

            // Check the file path length of the .ddb file and notify the user the ddb couldn't be loaded because its path is too long
            if (IsCondition.IsPathLengthTooLong(fileDatabaseFilePath))
            {
                Mouse.OverrideCursor = null;
                Dialogs.DatabasePathTooLongDialog(this, fileDatabaseFilePath);
                return false;
            }

            // Check the expected file path length of the backup files, and warn the user if backups may not be made because thier path is too long
            if (IsCondition.IsBackupPathLengthTooLong(templateDatabasePath) || IsCondition.IsBackupPathLengthTooLong(fileDatabaseFilePath))
            {
                Mouse.OverrideCursor = null;
                Dialogs.BackupPathTooLongDialog(this);
            }

            // Before fully loading an existing image database, 
            // - upgrade the template tables if needed for backwards compatability (done automatically)
            // - compare the controls in the .tdb and .ddb template tables to see if there are any added or missing controls 
            TemplateSyncResults templateSyncResults = new Database.TemplateSyncResults();
            bool backUpJustMade = false;
            using (FileDatabase fileDB = await FileDatabase.UpgradeDatabasesAndCompareTemplates(fileDatabaseFilePath, this.templateDatabase, templateSyncResults).ConfigureAwait(true))
            {
                // A file database was available to open
                if (fileDB != null)
                {
                    if (templateSyncResults.ControlSynchronizationErrors.Count > 0 || (templateSyncResults.ControlSynchronizationWarnings.Count > 0 && templateSyncResults.SyncRequiredAsDataLabelsDiffer == false))
                    {
                        // There are unresolvable syncronization issues. Report them now as we cannot use this template.
                        // Depending on the user response, we either abort Timelapse or use the template found in the ddb file
                        Mouse.OverrideCursor = null;
                        Dialog.TemplateSynchronization templatesNotCompatibleDialog;

                        templatesNotCompatibleDialog = new Dialog.TemplateSynchronization(templateSyncResults.ControlSynchronizationErrors, templateSyncResults.ControlSynchronizationWarnings, this);
                        bool? result = templatesNotCompatibleDialog.ShowDialog();
                        if (result == false)
                        {
                            // user indicates exiting rather than continuing.
                            Application.Current.Shutdown();
                            return false;
                        }
                        else
                        {
                            templateSyncResults.UseTemplateDBTemplate = templatesNotCompatibleDialog.UseNewTemplate;
                            templateSyncResults.SyncRequiredAsChoiceMenusDiffer = templateSyncResults.ControlSynchronizationWarnings.Count > 0;
                        }
                    }
                    else if (templateSyncResults.SyncRequiredAsDataLabelsDiffer)
                    {
                        // If there are any new or missing columns, report them now
                        // Depending on the user response, set the useTemplateDBTemplate to signal whether we should: 
                        // - update the template and image data columns in the image database 
                        // - use the old template
                        Mouse.OverrideCursor = null;
                        TemplateChangedAndUpdate templateChangedAndUpdate = new TemplateChangedAndUpdate(templateSyncResults, this);
                        bool? result1 = templateChangedAndUpdate.ShowDialog();
                        templateSyncResults.UseTemplateDBTemplate = result1 == true;
                    }
                    else if (templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer)
                    {
                        // Non critical differences in template, so these don't need reporting
                        templateSyncResults.UseTemplateDBTemplate = true;
                    }
                    backUpJustMade = fileDB.mostRecentBackup != DateTime.MinValue;
                }
                else if (File.Exists(fileDatabaseFilePath) == true)
                {
                    // The .ddb file (which exists) is for some reason unreadable.
                    // It is likely due to an empty or corrupt or otherwise unreadable database in the file.
                    // Raise an error message
                    bool isEmpty = File.Exists(fileDatabaseFilePath) && new FileInfo(fileDatabaseFilePath).Length == 0;
                    Mouse.OverrideCursor = null;
                    Dialogs.DatabaseFileNotLoadedAsCorruptDialog(this, fileDatabaseFilePath, isEmpty);
                    return false;
                };
            }

            // At this point:
            // - for backwards compatability, all old databases will have been updated (if needed) to the current version standard
            // - we should have a valid template and image database loaded
            // - we know if the user wants to use the old or the new template
            // So lets load the database for real. The useTemplateDBTemplate signals whether to use the template stored in the ddb, or to use the tdb template.
            FileDatabase fileDatabase = await FileDatabase.CreateOrOpenAsync(fileDatabaseFilePath, this.templateDatabase, this.State.CustomSelectionTermCombiningOperator, templateSyncResults, backUpJustMade).ConfigureAwait(true);
            if (fileDatabase == null)
            {
                // This happens if there is an unrecognized control
                Dialogs.TemplateIncludesControlOfUnknownType(this, "unknown control in the Timelapse .ddb data file.");
                return false;
            }

            // The next test is to test and syncronize (if needed) the default values stored in the fileDB table schema to those stored in the template
            Dictionary<string, string> columndefaultdict = fileDatabase.SchemaGetColumnsAndDefaultValues(Constant.DBTables.FileData);
            char[] quote = { '\'' };
            foreach (KeyValuePair<string, string> pair in columndefaultdict)
            {
                ControlRow row = this.templateDatabase.GetControlFromTemplateTable(pair.Key);
                if (row != null && pair.Value.Trim(quote) != row.DefaultValue)
                {
                    // If even one default is different between the schema default and the template default, update the entire file table.
                    fileDatabase.UpgradeFileDBSchemaDefaultsFromTemplate();
                    break;
                }
            }
            // Check to see if the root folder stored in the database is the same as the actual root folder. If not, ask the user if it should be changed.
            this.CheckAndCorrectRootFolder(fileDatabase);

            // Check to see if there are any missing folders as specified by the relative paths. For those missing, ask the user to try to locate those folders.
            int missingFoldersCount = TimelapseWindow.GetMissingFolders(fileDatabase).Count;
            if (missingFoldersCount > 0)
            {
                Dialogs.MissingFoldersInformationDialog(this, missingFoldersCount);
            }

            // Generate and render the data entry controls, regardless of whether there are actually any files in the files database.
            this.DataHandler = new DataEntryHandler(fileDatabase);
            this.DataEntryControls.CreateControls(fileDatabase, this.DataHandler);
            this.SetUserInterfaceCallbacks();
            this.MarkableCanvas.DataEntryControls = this.DataEntryControls; // so the markable canvas can access the controls
            this.DataHandler.ThumbnailGrid = this.MarkableCanvas.ThumbnailGrid;
            this.DataHandler.MarkableCanvas = this.MarkableCanvas;

            this.Title = Constant.Defaults.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) + ")";

            // Record the version number of the currently executing version of Timelapse only if its greater than the one already stored in the ImageSet Table.
            // This will indicate the latest timelapse version that is compatable with the database structure. 
            string currentVersionNumberAsString = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
            if (VersionChecks.IsVersion1GreaterThanVersion2(currentVersionNumberAsString, this.DataHandler.FileDatabase.ImageSet.VersionCompatability))
            {
                this.DataHandler.FileDatabase.ImageSet.VersionCompatability = currentVersionNumberAsString;
                this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }

            // Create an index on RelativePath, File,and RelativePath/File if it doesn't already exist
            // This is really just a version check in case old databases don't have the index created,
            // Newer databases (from 2.2.4.4 onwards) will have these indexes created and updated whenever images are loaded or added for the first time.
            // If the index exists, this is a very cheap operation so there really is no need to do it by a version number check.
            this.DataHandler.FileDatabase.IndexCreateForFileAndRelativePathIfNotExists();

            // If this is a new image database, try to load images (if any) from the folder...  
            if (importImages)
            {
                this.TryBeginImageFolderLoad(this.FolderPath, this.FolderPath);
            }
            else
            {
                await this.OnFolderLoadingCompleteAsync(false).ConfigureAwait(true);
            }
            return true;
        }
        #endregion

        #region TryBeginImageFolderLoad
        [HandleProcessCorruptedStateExceptions]
        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginImageFolderLoad(string imageSetFolderPath, string selectedFolderPath)
        {
            List<FileInfo> filesToAdd = new List<FileInfo>();
            List<string> filesSkipped = new List<string>();

            // Generate FileInfo list for every single image / video file in the folder path (including subfolders). These become the files to add to the database
            // PERFORMANCE - takes modest but noticable time to do if there are a huge number of files. 
            // TO DO: PUT THIS IN THE SHOW PROGRESS LOOP
            Util.FilesFolders.GetAllImageAndVideoFilesInFolderAndSubfolders(selectedFolderPath, filesToAdd);

            if (filesToAdd.Count == 0)
            {
                // No images were found in the root folder or subfolders, so there is nothing to do
                Dialogs.ImageSetLoadingNoImagesOrVideosWereFoundDialog(this, selectedFolderPath);
                return false;
            }
            if (this.State.MetadataAskOnLoad)
            {
                Cursor cursor = Mouse.OverrideCursor;
                PopulateFieldsWithMetadataOnLoad populateField = new PopulateFieldsWithMetadataOnLoad(this, this.DataHandler.FileDatabase, filesToAdd[0].FullName);
                if (this.ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    this.State.MetadataOnLoad = populateField.MetadataOnLoad;

                }
                Mouse.OverrideCursor = cursor;
            }

            // Load all the files (matching allowable file types) found in the folder
            // Show image previews of the files to the user as they are individually loaded
            // Generally, Background worker examines each image, and extracts data from it which it stores in a data structure, which in turn is used to compose bulk database inserts. 
            // PERFORMANCE This is likely the place that the best performance increases can be gained by transforming its foreach loop into a Parallel.ForEach. 
            // Indeed, you will see commented out remnants of a Parallel.ForEach in the code where this was done, but using it introduced errors. 
            //#pragma warning disable CA2000 // Dispose objects before losing scope. Reason: Not required as Dispose on BackgroundWorker doesn't do anything
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };
            //#pragma warning restore CA2000 // Dispose objects before losing scope

            // folderLoadProgress contains data to be used to provide feedback on the folder loading state
            FolderLoadProgress folderLoadProgress = new FolderLoadProgress(filesToAdd.Count)
            {
                TotalPasses = 2,
                CurrentPass = 1
            };

            backgroundWorker.DoWork += (ow, ea) =>
            {
                ImageSetLoader loader = new ImageSetLoader(imageSetFolderPath, filesToAdd, this.DataHandler);
                backgroundWorker.ReportProgress(0, folderLoadProgress);

                // If the DoWork delegate is async, this is considered finished before the actual image set is loaded.
                // Instead of an async DoWork and an await here, wait for the loading to finish.
                loader.LoadAsync(backgroundWorker.ReportProgress, folderLoadProgress, 500).Wait();
                filesSkipped = loader.ImagesSkippedAsFilePathTooLong;
                backgroundWorker.ReportProgress(0, folderLoadProgress);
            };

            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                this.ImageSetPane.IsActive = true;

                if (filesSkipped.Count > 0)
                {
                    Dialogs.FilePathTooLongDialog(this, filesSkipped);
                }
                if (folderLoadProgress.CurrentPass == 1 && folderLoadProgress.CurrentFile == 0)
                {
                    // skip the 0th file of the 1st pass, as there is not really much of interest to show
                    return;
                }
                string message = (folderLoadProgress.TotalPasses > 1) ? String.Format("Pass {0}/{1}{2}", folderLoadProgress.CurrentPass, folderLoadProgress.TotalPasses, Environment.NewLine) : String.Empty;
                if (folderLoadProgress.CurrentPass == 1 && folderLoadProgress.CurrentFile == folderLoadProgress.TotalFiles)
                {
                    message = String.Format("{0}Finalizing analysis of {1} files - could take several minutes ", message, folderLoadProgress.TotalFiles);
                }
                else
                {
                    string what = (folderLoadProgress.CurrentPass == 1) ? "Analyzing file" : "Adding files to database";
                    message = (folderLoadProgress.CurrentPass == 2 && folderLoadProgress.CurrentFile == 0)
                        ? String.Format("{0}{1} ...", message, what)
                        : String.Format("{0}{1} {2} of {3} ({4})", message, what, folderLoadProgress.CurrentFile, folderLoadProgress.TotalFiles, folderLoadProgress.CurrentFileName);
                }

                this.UpdateFolderLoadProgress(this.BusyCancelIndicator, folderLoadProgress.BitmapSource, ea.ProgressPercentage, message, false, false);
                this.StatusBar.SetCurrentFile(folderLoadProgress.CurrentFile);
                this.StatusBar.SetCount(folderLoadProgress.TotalFiles);
            };

            backgroundWorker.RunWorkerCompleted += async (o, ea) =>
            {
                // BackgroundWorker aborts execution on an exception and transfers it to completion for handling
                // If something went wrong rethrow the error so the user knows there's a problem.  Otherwise what would happen is either 
                //  1) some or all of the folder load file scan progress displays but no files get added to the database as the insert is skipped
                //  2) only some of the files get inserted and the rest are silently dropped
                // Both of these outcomes result in quite poor user experience and are best avoided.
                if (ea.Error != null)
                {
                    throw new FileLoadException("Folder loading failed unexpectedly.  See inner exception for details.", ea.Error);
                }

                // Create an index on RelativePath, File,and RelativePath/File if it doesn't already exist
                this.DataHandler.FileDatabase.IndexCreateForFileAndRelativePathIfNotExists();

                // Show the file slider
                this.FileNavigatorSlider.Visibility = Visibility.Visible;

                await this.OnFolderLoadingCompleteAsync(true).ConfigureAwait(true);

                // Do some final things
                // Note that if the magnifier is enabled, we temporarily hide so it doesn't appear in the background 
                bool saveMagnifierState = this.MarkableCanvas.MagnifiersEnabled;
                this.MarkableCanvas.MagnifiersEnabled = false;
                this.StatusBar.SetMessage(folderLoadProgress.TotalFiles + " files are now loaded");
                this.MarkableCanvas.MagnifiersEnabled = saveMagnifierState;

                // Stop the ExifToolManager if it was invoked while loading files, which can occurs when populating metadata to a file via the EXIFTool on load.
                this.State.ExifToolManager.Stop();

                this.BusyCancelIndicator.IsBusy = false; // Hide the busy indicator
            };

            // Set up the user interface to show feedback
            this.BusyCancelIndicator.IsBusy = true; // Display the busy indicator

            this.FileNavigatorSlider.Visibility = Visibility.Collapsed;
            // First feedback message
            this.UpdateFolderLoadProgress(GlobalReferences.BusyCancelIndicator, null, 0, String.Format("Initializing...{0}Analyzing and loading {1} files ", Environment.NewLine, filesToAdd.Count), false, false);
            this.StatusBar.SetMessage("Loading folders...");
            backgroundWorker.RunWorkerAsync();
            return true;
        }
        #endregion

        #region UpdateFolderLoadProgress
        private void UpdateFolderLoadProgress(BusyCancelIndicator BusyCancelIndicator, BitmapSource bitmap, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            if (bitmap != null)
            {
                this.MarkableCanvas.SetNewImage(bitmap, null);
            }

            // Check the arguments for null 
            ThrowIf.IsNullArgument(BusyCancelIndicator, nameof(BusyCancelIndicator));

            // Set it as a progressive or indeterminate bar
            BusyCancelIndicator.IsIndeterminate = isIndeterminate;

            // Set the progress bar position (only visible if determinate)
            BusyCancelIndicator.Percent = percent;

            // Update the text message
            BusyCancelIndicator.Message = message;

            // Update the cancel button to reflect the cancelEnabled argument
            BusyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
            BusyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Writing data...";
        }
        #endregion

        #region OnFolderLoadingCompleteAsync
        /// <summary>
        /// When folder loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private async Task OnFolderLoadingCompleteAsync(bool filesJustAdded)
        {

            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageSetPane.IsActive = true;
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.MarkableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // Adjust the visibility of the CopyPreviousValuesButton. Copyable controls will preview/highlight as one enters the CopyPreviousValuesButton
            this.CopyPreviousValuesButton.Visibility = Visibility.Visible;
            this.DataEntryControlPanel.IsVisible = true;

            // Show the File Player
            this.FilePlayer.Visibility = Visibility.Visible;

            // Set whether detections actually exist at this point.
            GlobalReferences.DetectionsExists = this.DataHandler.FileDatabase.DetectionsExists();

            // Sets the default bounding box threshold, either by using a default or reading it from the detection database table (if it exists)
            this.State.BoundingBoxDisplayThresholdResetToValueInDataBase();

            // Get the QuickPaste JSON from the database and populate the QuickPaste data structure with it
            this.quickPasteEntries = QuickPasteOperations.QuickPasteEntriesFromJSON(this.DataHandler.FileDatabase, this.DataHandler.FileDatabase.ImageSet.QuickPasteAsJSON);

            this.DataHandler.FileDatabase.FileSelectionEnum = this.DataHandler.FileDatabase.GetCustomSelectionFromJSON();

            // if this is completion of an existing .ddb open, set the current selection and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            long mostRecentFileID = this.DataHandler.FileDatabase.ImageSet.MostRecentFileID;

            if (filesJustAdded && (this.DataHandler.ImageCache.CurrentRow != Constant.DatabaseValues.InvalidRow && this.DataHandler.ImageCache.CurrentRow != Constant.DatabaseValues.InvalidRow))
            {
                // if this is completion of an add to an existing image set stay on the image, ideally, shown before the import
                if (this.DataHandler.ImageCache.Current != null)
                {
                    mostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                }
                // This is heavier weight than desirable, but it's a one off.
                this.DataHandler.ImageCache.TryInvalidate(mostRecentFileID);
            }

            // PERFORMANCE - Initial but necessary Selection done in OnFolderLoadingComplete invoking this.FilesSelectAndShow to display selected image set 
            // PROGRESSBAR - Display a progress bar on this (and all other) calls to FilesSelectAndShow after a delay of (say) .5 seconds.
            await this.FilesSelectAndShowAsync(mostRecentFileID, this.DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);

            // match UX availability to file availability
            this.EnableOrDisableMenusAndControls();

            // Reset the folder list used to construct the Select Folders menu
            this.MenuItemSelectByFolder_ResetFolderList();

            // Trigger updates to the datagrid pane, if its visible to the user.
            if (this.DataGridPane.IsVisible)
            {
                this.DataGridPane_IsActiveChanged(null, null);
            }

            // We have to do this again, to ensure that we have switched to the ImageSetPane
            this.ImageSetPane.IsActive = true;
            string sortMessage = this.ShowSortFeedback(true);

            string selectMessage = "Select menu: is now displaying " + Environment.NewLine;
            switch (this.DataHandler.FileDatabase.FileSelectionEnum)
            {
                case FileSelectionEnum.All:
                    selectMessage += "- All files. ";
                    break;
                case FileSelectionEnum.Folders:
                    selectMessage += "- Only files in the folder: " + this.DataHandler.FileDatabase.GetSelectedFolder;
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    selectMessage += "- Only files marked for deletion.";
                    break;
                case FileSelectionEnum.Custom:
                    selectMessage += "- Only a custom selection of your files.";
                    break;
                case FileSelectionEnum.Missing:
                    selectMessage += "- Only missing files in the current selection.";
                    break;
                default:
                    selectMessage += " Unknown, so make a selection.";
                    break;
            }

            MessageOptions toastOptions = new MessageOptions
            {
                FontSize = 14, // set notification font size
                FreezeOnMouseEnter = true, // set the option to prevent notification dissapear automatically if user move cursor on it
            };

            if (false == String.IsNullOrEmpty(sortMessage))
            {
                sortMessage = "Sort menu:  files sorted by" + Environment.NewLine + "- " + sortMessage;
                this.ToastNotifier.ShowInformation(sortMessage, toastOptions);
            }
            this.ToastNotifier.ShowInformation(selectMessage, toastOptions);
        }
        #endregion

        #region Helpers
        // Given the location path of the template,  return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - importImages: true when the database file has just been created, which means images still have to be imported.
        private bool TrySelectDatabaseFile(string templateDatabasePath, out string databaseFilePath, out bool importImages)
        {
            importImages = false;

            string databaseFileName;
            string directoryPath = Path.GetDirectoryName(templateDatabasePath);
            string[] fileDatabasePaths = Directory.GetFiles(directoryPath, "*.ddb");
            if (fileDatabasePaths.Length == 1)
            {
                databaseFileName = Path.GetFileName(fileDatabasePaths[0]); // Get the file name, excluding the path
            }
            else if (fileDatabasePaths.Length > 1)
            {
                ChooseFileDatabaseFile chooseDatabaseFile = new ChooseFileDatabaseFile(fileDatabasePaths, templateDatabasePath, this);
                Cursor cursor = Mouse.OverrideCursor;
                Mouse.OverrideCursor = null;
                bool? result = chooseDatabaseFile.ShowDialog();
                Mouse.OverrideCursor = cursor;
                if (result == true)
                {
                    databaseFileName = chooseDatabaseFile.SelectedFile;
                }
                else
                {
                    // User cancelled .ddb selection
                    databaseFilePath = null;
                    return false;
                }
            }
            else
            {
                // There are no existing .ddb files
                string templateDatabaseFileName = Path.GetFileName(templateDatabasePath);
                if (String.Equals(templateDatabaseFileName, Constant.File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    databaseFileName = Constant.File.DefaultFileDatabaseFileName;
                }
                else
                {
                    databaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + Constant.File.FileDatabaseFileExtension;
                }
                importImages = true;
            }

            databaseFilePath = Path.Combine(directoryPath, databaseFileName);
            return true;
        }
        #endregion
    }
}
