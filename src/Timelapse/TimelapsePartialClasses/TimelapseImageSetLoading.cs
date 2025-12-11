using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.ImageSetLoadingPipeline;
using Timelapse.QuickPaste;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;
using File = Timelapse.Constant.File;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    /// <summary>
    /// Image Set Loaing - Primary Methods to do it
    /// </summary>
    public partial class TimelapseWindow
    {
        #region TryGetTemplatePath

        // Prompt user to select a template.
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // Default the template selection dialog to the most recently opened database
            State.RecentlyOpenedTemplateFiles.TryGetMostRecent(out string defaultTemplateDatabasePath);
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a TimelapseTemplate.tdb file, which should be located in the root folder containing your images and videos",
                    defaultTemplateDatabasePath,
                    String.Format("Template files (*{0})|*{0}", File.TemplateDatabaseFileExtension),
                    File.TemplateDatabaseFileExtension,
                    out templateDatabasePath) == false)
            {
                return false;
            }

            string templateDatabaseDirectoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (string.IsNullOrEmpty(templateDatabaseDirectoryPath))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region TryOpenTemplateAndBeginLoadFoldersAsync

        // Load the specified database template and then the associated images. 
        // tdbDatabasePath is the Fully qualified path to the tdb database file,
        // fileDatabasePath is the path to the ddb file, although it can be empty indicating that the file must be created
        // Returns the first tuple of true only if both the template and image database file are loaded (regardless of whether any images were loaded) , false otherwise
        // Returns the second tuple of the ddb file if it has to be deleted on failure
        private async Task<Tuple<bool, string>> TryOpenTemplateAndBeginLoadFoldersAsync(string tdbDatabasePath)
        {
            return await TryOpenTemplateAndBeginLoadFoldersAsync(tdbDatabasePath, string.Empty);
        }

        private async Task<Tuple<bool, string>> TryOpenTemplateAndBeginLoadFoldersAsync(string tdbDatabasePath, string fileDatabaseFilePath, bool checkForShortcuts = true)
        {
            State.RecentlyOpenedTemplateFiles.SetMostRecent(tdbDatabasePath);

            // ----------------
            // TDB File loading
            // ----------------

            // Try to create or open the tdb template database
            // As we can't have out parameters in an async method, we return the state and the desired tdb Database as a tuple
            Tuple<bool, CommonDatabase> tupleResult = await CommonDatabase.TryCreateOrOpenAsync(tdbDatabasePath).ConfigureAwait(true);
            templateDatabase = tupleResult.Item2;
            if (!tupleResult.Item1)
            {
                // Notify the user the tdb template couldn't be loaded rather than silently doing nothing
                Mouse.OverrideCursor = null;
                Dialogs.TemplateFileNotLoadedAsCorruptDialog(this, tdbDatabasePath);
                return new(false, string.Empty);
            }

            string unknownTypes = templateDatabase.AreControlsOfKnownTypes();
            if (unknownTypes != string.Empty)
            {
                // The tdb template contains an item of an unknown type. 
                // This could be because we are trying to open a template with an old version of Timelapse
                // that doesn't know about newer types. Warn the user that they should upgrade, and abort.
                Dialogs.TemplateIncludesControlOfUnknownType(this, unknownTypes);
                return new(false, string.Empty);
            }
            // TDB templateDatabase should now be loaded


            // ----------------
            // DDB File loading
            // ----------------

            // Given a root folder, get a valid ddb file path.
            // - If there is only one ddb file, just use that 
            // - If there aren't any ddb files in the folder, then generate a file name that is the same as the template file (albeit with the ddb suffix)
            // - If there are several ddb files in the folder, ask the user to select one of them 
            // Note: importImagesAsNewDDBFile will be true if its a new ddb database file (meaning we should later try to import images)
            bool importImagesAsNewDDBFile = false;
            if (string.IsNullOrEmpty(fileDatabaseFilePath))
            {

                if (TrySelectDatabaseFile(tdbDatabasePath, out string selectedFileDatabaseFilePath, out importImagesAsNewDDBFile) == false)
                {
                    Mouse.OverrideCursor = null;
                    // No ddb database file was selected
                    return new(false, string.Empty);
                }
                fileDatabaseFilePath = selectedFileDatabaseFilePath;
            }

            // Check: if there are any issues with the ddb file.
            // If there are, this will display a dialog informing the user about the specific problem
            //  and the loading will be aborted.
            if (false == Dialogs.DialogIsFileValid(this, fileDatabaseFilePath))
            {
                //  include the pathname of the newly created ddb file if we are trying to load it for the first time
                Mouse.OverrideCursor = null;
                return new(false, importImagesAsNewDDBFile ? fileDatabaseFilePath : string.Empty);
            }

            // Check: if Timelapse is in view-only state but needs to create a ddb file...
            if (State.IsViewOnly && importImagesAsNewDDBFile)
            {
                // There are no .ddb files in this folder, which means Timelapse would normally try to create one.
                // But if Timelapse was started in a ReadOnly state, that is not allowed. Tell the user, and abort.
                Mouse.OverrideCursor = null;
                Dialogs.ViewOnlySoDatabaseCannotBeCreated(this);
                return new(false, string.Empty);
            }

            // Check: the ddb file path length, where we notify the user if the path is too long
            if (IsCondition.IsPathLengthTooLong(fileDatabaseFilePath, FilePathTypeEnum.DDB))
            {
                Mouse.OverrideCursor = null;
                Dialogs.DatabasePathTooLongDialog(this, fileDatabaseFilePath);
                return new(false, importImagesAsNewDDBFile ? fileDatabaseFilePath : string.Empty);
                //return false;
            }

            // Check: expected file path length of tdb and ddb backup files, and warn the user if backups may not be made because their path is too long
            if (IsCondition.IsPathLengthTooLong(tdbDatabasePath, FilePathTypeEnum.Backup) || IsCondition.IsPathLengthTooLong(fileDatabaseFilePath, FilePathTypeEnum.Backup))
            {
                Mouse.OverrideCursor = null;
                Dialogs.BackupPathTooLongDialog(this);
                return new(false, string.Empty);
            }

            // -------------------------------------------------------------------
            // TDB and DDB template upgrading and syncronization with one another
            // -------------------------------------------------------------------

            // Before fully loading an existing image database, 
            // - upgrade the template tables if needed for backwards compatability (done automatically)
            // - compare the controls in the .tdb and .ddb template tables to see if there are any added or missing controls 
            TemplateSyncResults templateSyncResults = new();
            bool backUpJustMade = false;
            using (FileDatabase fileDB = await FileDatabase.UpgradeDatabasesAndCompareTemplates(fileDatabaseFilePath, templateDatabase, templateSyncResults)
                       .ConfigureAwait(true))
            {
                // A file database was available to open
                if (fileDB != null)
                {
                    if (templateSyncResults.ControlSynchronizationErrorsByLevel.Count > 0 || templateSyncResults.SyncRequiredAsDataLabelsDiffer ||
                        templateSyncResults.SyncRequiredAsFolderLevelsDiffer || templateSyncResults.ControlSynchronizationWarningsByLevel.Count > 0)
                    {
                        // A dialog box is raised that illustrates various issues, depending upon what is in templateSyncResults.
                        // Depending on the user response, set the useTemplateDBTemplate to signal whether we should: 
                        // - update the various template and  data columns in the database to match the new template, or
                        // - use the original template
                        Mouse.OverrideCursor = null;
                        TemplateChangedWizard templateChangedWizard = new(this, templateSyncResults,
                            templateDatabase.MetadataInfo, fileDB.MetadataInfo,
                            templateDatabase.MetadataControlsByLevel, fileDB.MetadataControlsByLevel,
                            templateDatabase.Controls, fileDB.Controls);
                        templateChangedWizard.ShowDialog();
                        if (templateChangedWizard.UseTdbTemplate == null)
                        {
                            return new(false, string.Empty);
                        }
                        templateSyncResults.UseTdbTemplate = templateChangedWizard.UseTdbTemplate == true;
                    }
                    else if (templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer)
                    {
                        // There are no critical differences in template, so these don't need reporting.
                        // Just use the Tdb template
                        templateSyncResults.UseTdbTemplate = true;
                    }
                    backUpJustMade = fileDB.mostRecentBackup != DateTime.MinValue;
                }
                else if (System.IO.File.Exists(fileDatabaseFilePath))
                {
                    // The .ddb file (which exists) is for some reason unreadable.
                    // It is likely due to an empty or corrupt or otherwise unreadable database in the file.
                    // Raise an error message
                    bool isEmpty = System.IO.File.Exists(fileDatabaseFilePath) && new FileInfo(fileDatabaseFilePath).Length == 0;
                    Mouse.OverrideCursor = null;
                    Dialogs.DatabaseFileNotLoadedAsCorruptDialog(this, fileDatabaseFilePath, isEmpty);
                    return new(false, string.Empty);
                }
            }


            // ---------------------------------------------------------------------------------------------------------
            // DDB: Create the interface and perform further synchronization between the DDB and TDB templates as needed
            // ---------------------------------------------------------------------------------------------------------

            // At this point:
            // - for backwards compatability, all old databases will have been updated (if needed) to the current version standard
            // - we should have a valid template and image database loaded
            // - we know if the user wants to use the old or the new template (if they differ in a meaningful way)
            // So lets load the database for real. The useTemplateDBTemplate signals whether to use the template stored in the ddb, or to use the tdb template.
            FileDatabase fileDatabase = await FileDatabase
                .CreateOrOpenAsync(fileDatabaseFilePath, templateDatabase, State.CustomSelectionTermCombiningOperator, templateSyncResults, backUpJustMade, checkForShortcuts)
                .ConfigureAwait(true);

            // Check: Unrecognized control
            if (fileDatabase == null)
            {
                // If there is an unrecognized control, inform the user and abort
                Dialogs.TemplateIncludesControlOfUnknownType(this, "unknown control in the Timelapse .ddb data file.");
                return new(false, string.Empty);
            }

            // Check: Are we using a shortcut to an external image folder? (i.e., ShrotcutFoldersFound is not null)
            // If one shortcut, use that. If there is more than one, that can be a problem as we can't determine what folder to use
            if (fileDatabase.ShortcutFoldersFound != null)
            {
                // if Count is 0, we just continue with no shortcuts
                if (fileDatabase.ShortcutFoldersFound.Count == 1)
                {
                    // This is fine. Just generate a message so the user knows what is going on
                    if (State.SuppressShortcutDetectedPrompt == false)
                    {
                        Dialogs.ShortcutDetectedDialog(GlobalReferences.MainWindow, fileDatabase.ShortcutFoldersFound[0]);
                    }
                    else
                    {
                        var toastOptions = new NotificationOptions
                        {
                            ShowCloseButton = true,
                            CloseAfter = 8000 // 8 seconds
                        };
                        string toastMessage = $"Shortcut detected.{Environment.NewLine}- It will be used to search for images.";
                        ToastNotifier.ShowInformation(toastMessage, toastOptions);
                    }
                }
                else if (fileDatabase.ShortcutFoldersFound.Count > 1)
                {
                    // This is an error. Generate a message and abort.
                    Dialogs.ShortcutMultipleShortcutsDetectedDialog(GlobalReferences.MainWindow, fileDatabase.ShortcutFoldersFound);
                    return new(false, string.Empty);
                }
            }

            // TODO: SHOULDN'T WE DO THE DEFAULT SYNCHRONIZATION IN THE PREVIOUS STEPS?
            // The next test is to test and syncronize (if needed) the default values stored in the fileDB table schema to those stored in the template
            // Only invoke this when we know the templateDBs are in sync, and the templateDB matches the FileDB (i.e., same control rows/columns) except for one or more defaults.
            Dictionary<string, string> columndefaultdict = fileDatabase.SchemaGetColumnsAndDefaultValues(DBTables.FileData);
            char[] quote = ['\''];
            foreach (KeyValuePair<string, string> pair in columndefaultdict)
            {
                ControlRow row = templateDatabase.GetControlFromControls(pair.Key);
                if (row != null && pair.Value.Trim(quote) != row.DefaultValue)
                {
                    fileDatabase.UpgradeFileDBSchemaDefaultsFromTemplate();
                    break;
                }
            }

            // Check: if there are any missing folders as specified by the relative paths, ask the user to try to locate those folders.
            int missingFoldersCount = GetMissingFolders(fileDatabase).Count;
            if (missingFoldersCount > 0)
            {
                Dialogs.MissingFoldersInformationDialog(this, missingFoldersCount);
            }

            // Set the window's title to include the file name
            Title = Defaults.MainWindowBaseTitle + " (" + Path.GetFileName(fileDatabase.FilePath) + ")";

            // Generate and render the data entry, regardless of whether there are actually any files in the files database.
            DataHandler = new(fileDatabase);
            DataEntryControls.CreateControls(fileDatabase, DataHandler);
            SetUserInterfaceCallbacks();
            MarkableCanvas.DataEntryControls = DataEntryControls; // so the markable canvas can access the controls
            DataHandler.ThumbnailGrid = MarkableCanvas.ThumbnailGrid;
            DataHandler.MarkableCanvas = MarkableCanvas;

            // Initialize the MetadataDataHandler
            MetadataDataHandler = new(fileDatabase);

            // ---------------------
            // Database Maintenance:
            // ---------------------

            // Try updating the IDs of the File databases if the IDs are really large.
            // IDs can be up to Long.MaxValue, we really don't want to get close to that so we try to restrict it to around Int32.MaxValue
            // This tries to mitigate the issue of multiple merges producing very large IDs that are close to the maximum supported by SQLite.
            long maxID = fileDatabase.GetValueFromLastInsertedRow(DBTables.FileData, DatabaseColumn.ID);
            if (maxID > int.MaxValue)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                BusyCancelIndicator.Reset(true);
                BusyCancelIndicator.EnableForDatabaseMaintenance(true);

                await fileDatabase.ResetIDsAndVacuumAsync();
                fileDatabase.Database.Vacuum();
                BusyCancelIndicator.Reset(false);
                Mouse.OverrideCursor = null;
            }

            // Record the version number of the currently executing version of Timelapse only if its greater than the one already stored in the ImageSet Table.
            // This will indicate the latest timelapse version that is compatable with the database structure. 
            string lastRecordedBackwardsCompatabilityVersion = DataHandler.FileDatabase.ImageSet.BackwardsCompatability;
            string currentVersionNumberAsString = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
            bool syncImageSetRequired = false;
            if (VersionChecks.IsVersion1GreaterThanVersion2(currentVersionNumberAsString, DataHandler.FileDatabase.ImageSet.VersionCompatability))
            {
                DataHandler.FileDatabase.ImageSet.VersionCompatability = currentVersionNumberAsString;
                syncImageSetRequired = true;
            }

            if (VersionChecks.IsVersion1GreaterThanVersion2(Constant.DatabaseValues.VersionNumberBackwardsCompatible, lastRecordedBackwardsCompatabilityVersion))
            {
                DataHandler.FileDatabase.ImageSet.BackwardsCompatability = Constant.DatabaseValues.VersionNumberBackwardsCompatible;
                syncImageSetRequired = true;
            }

            if (syncImageSetRequired)
            {
                DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }

            // Do the same for the template database
            if (VersionChecks.IsVersion1GreaterThanVersion2(currentVersionNumberAsString, templateDatabase.GetTemplateVersionCompatibility()))
            {
                templateDatabase.SetTemplateVersionCompatibility(currentVersionNumberAsString);
                //DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }

            // Check: if the root folder stored in the database is different from the actual root folder update its database value.
            CheckAndUpdateRootFolderIfNeeded(fileDatabase);

            // Create an index on RelativePath, File,and RelativePath/File if it doesn't already exist
            // This is really just a version check in case old databases don't have the index created,
            // Newer databases (from 2.2.4.4 onwards) will have these indexes created and updated whenever images are loaded or added for the first time.
            // If the index exists, this is a very cheap operation so there really is no need to do it by a version number check.
            // TODO This may be redundant as it already exists in TryBeginImageFolderLoad ... perhapse we should move it to OnFolderLoadingComplete and only invoke it once?
            DataHandler.FileDatabase.IndexCreateForFileAndRelativePathIfNotExists();

            // --------------
            // Loading Images
            // --------------
            // If this is a new image database, try to load images (if any) from the folder...  
            if (importImagesAsNewDDBFile)
            {
                if (false == TryBeginImageFolderLoad(RootPathToImages, RootPathToImages, true))
                {
                    return new(false, fileDatabaseFilePath);
                }
            }
            else
            {
                await OnFolderLoadingCompleteAsync(false).ConfigureAwait(true);
            }
            return new(true, string.Empty);
        }

        #endregion

        #region TryBeginImageFolderLoad
        // Note: HandleProcessCorruptedStateExceptions attribute removed as it's obsolete in .NET 8
        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginImageFolderLoad(string imageSetFolderPath, string selectedFolderPath, bool isFirstTimeLoad)
        {
            List<FileInfo> filesToAdd = [];
            List<string> filesSkipped = [];
            bool isCancelled = false;

            // TODO: PUT THIS IN THE SHOW PROGRESS LOOP
            // Generate FileInfo list for every single image / video file in the folder path (including subfolders). These become the files to add to the database
            // PERFORMANCE - takes modest but noticable time to do if there are a huge number of files. 
            List<string> foldersWithImagesOrVideos = [];
            FilesFolders.GetAllImageAndVideoFilesInFolderAndSubfolders(selectedFolderPath, filesToAdd, foldersWithImagesOrVideos);

            // CHECK LEVELS
            // Strip the root folder path off the folders list
            int levels = templateDatabase.GetMetadataInfoTableMaxLevel();
            List<string> relativePathFolderList = [];
            if (levels > 0 && foldersWithImagesOrVideos.Count > 0)
            {
                int rootPathFolderCount = imageSetFolderPath.Split(Path.DirectorySeparatorChar).Length;
                int rootPathLength = imageSetFolderPath.Length;
                List<Tuple<string, int>> divergentFolders = []; // path, number of levels in path
                foreach (string path in foldersWithImagesOrVideos)
                {
                    int relativeImageFolderCount = path.Split(Path.DirectorySeparatorChar).Length - rootPathFolderCount + 1;
                    if (relativeImageFolderCount != levels)
                    {
                        divergentFolders.Add(new(path[rootPathLength..], relativeImageFolderCount));
                    }
                    relativePathFolderList.Add(path[rootPathLength..].TrimStart(Path.DirectorySeparatorChar));
                }

                if (divergentFolders.Count > 0)
                {
                    Mouse.OverrideCursor = null;
                    MetadataFolderComplianceViewer dialog = new(this, DataHandler.FileDatabase, relativePathFolderList, DataHandler.FileDatabase.MetadataInfo, true);
                    if (false == dialog.ShowDialog())
                    {
                        Mouse.OverrideCursor = null;
                        return false;
                    }
                }
            }
            // END CHECK LEVELS

            if (filesToAdd.Count == 0)
            {
                // No images were found in the root folder or subfolders, so there is nothing to do
                Dialogs.ImageSetLoadingNoImagesOrVideosWereFoundDialog(this, selectedFolderPath);
                return false;
            }

            // When set, Timelapse will ask the user which metadata fields to extract whenever there is an attempt to add images
            if (State.ImageMetadataAskOnLoad)
            {
                Cursor cursor = Mouse.OverrideCursor;
                PopulateFieldsWithImageMetadataOnLoad populateField = new(this, DataHandler.FileDatabase, filesToAdd[0].FullName);
                if (ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    State.MetadataOnLoad = populateField.ImageMetadataOnLoad;
                }
                Mouse.OverrideCursor = cursor;
            }

            // Load all the files (matching allowable file types) found in the folder
            //#pragma warning disable CA2000 // Dispose objects before losing scope. Reason: Not required as Dispose on BackgroundWorker doesn't do anything
            BackgroundWorker backgroundWorker = new()
            {
                WorkerReportsProgress = true
            };
            //#pragma warning restore CA2000 // Dispose objects before losing scope

            // folderLoadProgress contains data to be used to provide feedback on the folder loading state
            FolderLoadProgress folderLoadProgress = new(filesToAdd.Count)
            {
                TotalPasses = 2,
                CurrentPass = 1
            };

            //
            // Do work
            //
            backgroundWorker.DoWork += (_, _) =>
            {
                ImageSetLoader loader = new(imageSetFolderPath, filesToAdd, DataHandler);
                backgroundWorker.ReportProgress(0, folderLoadProgress);

                // If the DoWork delegate is async, this is considered finished before the actual image set is loaded.
                // Instead of an async DoWork and an await here, wait for the loading to finish.
                loader.LoadAsync(backgroundWorker.ReportProgress, folderLoadProgress, 500).Wait();
                filesSkipped = loader.ImagesSkippedAsFilePathTooLong;
                backgroundWorker.ReportProgress(0, folderLoadProgress);
            };

            //
            // ProgressChanged
            //
            backgroundWorker.ProgressChanged += (_, ea) =>
            {
                // this gets called on the UI thread
                ImageSetPane.IsActive = true;

                if (filesSkipped.Count > 0)
                {
                    Dialogs.FilePathTooLongDialog(this, filesSkipped);
                }
                string message;
                if (isCancelled)
                {
                    // The user has cancelled the load
                    // I suspect this never gets displayed as progress changed should be surpressed after a cancel, but just in case.
                    message = "Cancelling...";
                }
                else
                {
                    message = (folderLoadProgress.TotalPasses > 1) ? $"Pass {folderLoadProgress.CurrentPass}/{folderLoadProgress.TotalPasses}{Environment.NewLine}"
                        : string.Empty;
                    if (folderLoadProgress.CurrentPass == 1 && folderLoadProgress.CurrentFile == folderLoadProgress.TotalFiles)
                    {
                        // I suspect this never gets displayed, but just in case.
                        message =
                            $"{message}Finalizing analysis of {folderLoadProgress.TotalFiles} files - could take several minutes ";
                    }
                    else
                    {
                        string what = (folderLoadProgress.CurrentPass == 1) ? "Analyzing file" : "Adding files to database";
                        message = (folderLoadProgress.CurrentPass == 2 && folderLoadProgress.CurrentFile == 0)
                            ? $"{message}{what} ..."
                            : $"{message}{what} {folderLoadProgress.CurrentFile} of {folderLoadProgress.TotalFiles}";
                    }
                }

                // Enable the cancel button only on Pass 1.
                // SAULXXX I suppose we could do it on Pass 2 as well,
                //         but since the database is already being updated (and because its relatively fast), we may as well keep on going.
                //         If I do decide to do so, we will have to somehow have to act on the cancellation token in the database update code.
                bool enableCancelButton = folderLoadProgress.CurrentPass <= 1;
                UpdateFolderLoadProgress(BusyCancelIndicator, folderLoadProgress.BitmapSource, ea.ProgressPercentage, message, enableCancelButton, false);
                StatusBar.SetCurrentFile(folderLoadProgress.CurrentFile);
                StatusBar.SetCount(folderLoadProgress.TotalFiles);
            };

            //
            // RunWorkerCompleted
            //
            backgroundWorker.RunWorkerCompleted += async (_, ea) =>
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

                if (GlobalReferences.CancelTokenSource.IsCancellationRequested)
                {
                    // Debug.Print("Cancelled: In BackgroundWorkerCompleted");
                    isCancelled = true;
                    // Stop the ExifToolManager if it was invoked while loading files, which can occurs when populating metadata to a file via the EXIFTool on load.
                    //this.State.ExifToolManager.Stop();

                    if (isFirstTimeLoad)
                    {
                        // If the cancel occured when loading images for the first time, we need to do some minimal cleanup
                        // Reset everything to the Timelapse initial state with no image set loaded. This includes:
                        // - closing the image set
                        // - deleting the .ddb file (as its initial load was never completed)

                        // Get the .ddb file path as we will be deleting it.
                        string filePathToDelete = DataHandler?.FileDatabase != null && System.IO.File.Exists(DataHandler.FileDatabase.FilePath)
                        ? DataHandler.FileDatabase.FilePath
                        : string.Empty;

                        CloseImageSet();
                        State.ExifToolManager.Stop();
                        BusyCancelIndicator.Reset(false);
                        FileNavigatorSlider.Visibility = Visibility.Visible;
                        StatusBar.SetMessage("Cancelled loading of image set");
                        Mouse.OverrideCursor = null;
                        FilesFolders.TryDeleteFileIfExists(filePathToDelete);
                        return;
                    }
                }

                // Create an index on RelativePath, File,and RelativePath/File if it doesn't already exist
                DataHandler.FileDatabase.IndexCreateForFileAndRelativePathIfNotExists();

                // Show the file slider
                FileNavigatorSlider.Visibility = Visibility.Visible;

                await OnFolderLoadingCompleteAsync(true).ConfigureAwait(true);


                // Do some final things
                // Note that if the magnifier is enabled, we temporarily hide so it doesn't appear in the background 
                bool saveMagnifierState = MarkableCanvas.MagnifiersEnabled;
                MarkableCanvas.MagnifiersEnabled = false;
                MarkableCanvas.MagnifiersEnabled = saveMagnifierState;

                // Stop the ExifToolManager if it was invoked while loading files, which can occurs when populating metadata to a file via the EXIFTool on load.
                State.ExifToolManager.Stop();

                BusyCancelIndicator.Reset(false); // Hide the busy indicator and reset the cancel token

                StatusBar.SetMessage(isCancelled
                    ? "Cancelled adding files to image set "
                    : "Loading completed");
                Mouse.OverrideCursor = null;
            };

            // Background worker initialization
            // Set up the user interface to show feedback
            FileNavigatorSlider.Visibility = Visibility.Collapsed;
            BusyCancelIndicator.IsBusy = true; // Display the busy indicator
            StatusBar.SetMessage("Loading image set. Please wait...");

            // Do the work. 
            // Any cleanup is done in the BackgroundWorkerCompleted, including displaying the various final messages.
            backgroundWorker.RunWorkerAsync();
            return true;
        }
        #endregion

        #region UpdateFolderLoadProgress
        private void UpdateFolderLoadProgress(BusyCancelIndicator theBusyCancelIndicator, BitmapSource bitmap, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            if (bitmap != null)
            {
                MarkableCanvas.SetNewImage(bitmap, null);
            }

            // Check the arguments for null 
            ThrowIf.IsNullArgument(theBusyCancelIndicator, nameof(theBusyCancelIndicator));

            // Set it as a progressive or indeterminate bar
            theBusyCancelIndicator.IsIndeterminate = isIndeterminate;

            // Set the progress bar position (only visible if determinate)
            theBusyCancelIndicator.Percent = percent;

            // Update the text message
            theBusyCancelIndicator.Message = message;

            // Update the cancel button to reflect the cancelEnabled argument
            theBusyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
            theBusyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Writing data...";
        }
        #endregion

        #region OnFolderLoadingCompleteAsync
        /// <summary>
        /// When folder loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private async Task OnFolderLoadingCompleteAsync(bool filesJustAdded)
        {
            // Show the image, hide the load button, and make the feedback panels visible
            ImageSetPane.IsActive = true;
            FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            MarkableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // Adjust the visibility of the CopyPreviousValuesButton. Copyable controls will preview/highlight as one enters the CopyPreviousValuesButton
            CopyPreviousValuesButton.Visibility = Visibility.Visible;
            DataEntryControlPanel.IsVisible = true;

            // Show the File Player
            FilePlayer.Visibility = Visibility.Visible;

            // Set whether detections actually exist at this point.
            GlobalReferences.DetectionsExists = DataHandler.FileDatabase.DetectionsExists(true);

            // Sets the default bounding box threshold, either by using a default or reading it from the detection database table (if it exists)
            State.BoundingBoxDisplayThresholdResetToValueInDataBase();

            // Get the QuickPaste JSON from the database and populate the QuickPaste data structure with it
            quickPasteEntries = QuickPasteOperations.QuickPasteEntriesFromJSON(DataHandler.FileDatabase, DataHandler.FileDatabase.ImageSet.QuickPasteAsJSON);

            DataHandler.FileDatabase.FileSelectionEnum = DataHandler.FileDatabase.GetCustomSelectionFromJSON();
            if (DataHandler.FileDatabase.Database.TableExists(DBTables.Detections))
            {
                // Populate the detection categories
                DataHandler.FileDatabase.CreateDetectionCategoriesDictionaryIfNeeded();
                DataHandler.FileDatabase.CreateClassificationCategoriesDictionaryIfNeeded();
            }

            // If recognitions are selected, we set the over-ride of which bounding boxes are displayed by expanding the range to include the selection confidence values.
            CustomSelection.SetDetectionRanges(DataHandler.FileDatabase.CustomSelection.RecognitionSelections);

            // if this is completion of an existing .ddb open, set the current selection and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            long mostRecentFileID = DataHandler.FileDatabase.ImageSet.MostRecentFileID;

            if (filesJustAdded && (DataHandler.ImageCache.CurrentRow != DatabaseValues.InvalidRow && DataHandler.ImageCache.CurrentRow != DatabaseValues.InvalidRow))
            {
                // if this is completion of an add to an existing image set stay on the image, ideally, shown before the import
                if (DataHandler.ImageCache.Current != null)
                {
                    mostRecentFileID = DataHandler.ImageCache.Current.ID;
                }
                // This is heavier weight than desirable, but it's a one off.
                DataHandler.ImageCache.TryInvalidate(mostRecentFileID);
            }

            // PERFORMANCE - Initial but necessary Selection done in OnFolderLoadingComplete invoking this.FilesSelectAndShow to display selected image set 
            // PROGRESSBAR - Display a progress bar on this (and all other) calls to FilesSelectAndShow after a delay of (say) .5 seconds.
            await FilesSelectAndShowAsync(mostRecentFileID, DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);

            // Add the metadata folders
            // Initialize FolderMetadata
            // - if there is any metadata, it will display tabs for those, otherwise only the instructions tab
            DataHandler.FileDatabase.CreateFolderMetadataTablesIfNeeded();
            DataHandler.FileDatabase.MetadataTableLoadRowsFromDatabase();
            MetadataUI.InitalizeFolderMetadataTabs();
            MetadataUI.RelativePathToCurrentImage = null;
            MetadataUI.RelativePathToCurrentImage = DataHandler?.ImageCache?.Current?.RelativePath;

            // match UX availability to file availability
            EnableOrDisableMenusAndControls();

            // Reset the folder list used to construct the Select Folders menu
            this.MenuItemSelectByFolder_ResetFolderList();

            // Trigger updates to the datagrid pane, if its visible to the user.
            if (DataGridPane.IsVisible)
            {
                DataGridPane_IsActiveChanged(null, null);
            }

            // We have to do this again, to ensure that we have switched to the ImageSetPane
            ImageSetPane.IsActive = true;

            // Create  notification toasts UNLESS select or sort settings are the default ones.
            string sortMessage = ShowSortFeedback(true);

            this.ImageDogear = new(this.DataHandler);

            string selectMessage = "Select menu: is now displaying " + Environment.NewLine;
            // ReSharper disable once PossibleNullReferenceException
            switch (DataHandler.FileDatabase.FileSelectionEnum)
            {
                case FileSelectionEnum.All:
                    // As this is the default, don't bother displaying a notification
                    selectMessage = string.Empty;
                    break;
                case FileSelectionEnum.Folders:
                    selectMessage += "- Only files in the folder: " + DataHandler.FileDatabase.GetSelectedFolder;
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

            var toastOptions = new NotificationOptions
            {
                // ShowCloseButton = false,
                CloseAfter = 8000 // 7 seconds
            };

            if (false == string.IsNullOrEmpty(selectMessage) && false == string.IsNullOrEmpty(sortMessage))
            {
                selectMessage += Environment.NewLine + Environment.NewLine;
            }

            if (false == string.IsNullOrEmpty(sortMessage))
            {
                // Combine the sort message with the select message
                selectMessage += "Sort menu:  files sorted by" + Environment.NewLine + "- " + sortMessage;
            }

            if (false == string.IsNullOrEmpty(selectMessage))
            {
                ToastNotifier.ShowInformation(selectMessage, toastOptions);
            }
        }

        #endregion

        #region Helpers
        // Given the location path of the template,  return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - importImages: true when the database file has just been created, which means images still have to be imported.

        // The way this works. Given a root folder path (the templateDatabasePath)
        // - If there is only one ddb file, just use that 
        // - If there aren't any ddb files in the folder, then generate a file name that is the same as the template file (albeit with the ddb suffix)
        // - If there are several ddb files in the folder, ask the user to select one of them 
        // importImages will be true if its a new ddb database file, (meaning we should later ask the user to try to import some images)
        private bool TrySelectDatabaseFile(string templateDatabasePath, out string databaseFilePath, out bool importImages)
        {
            importImages = false;

            string databaseFileName;
            string directoryPath = Path.GetDirectoryName(templateDatabasePath);

            if (directoryPath == null)
            {
                // Null is returned if directory is a root drive (say) C:
                TracePrint.NullException(nameof(directoryPath));
                databaseFilePath = null;
                return false;
            }
            string[] fileDatabasePaths = Directory.GetFiles(directoryPath, "*.ddb");
            if (fileDatabasePaths.Length == 1)
            {
                // There is a single ddb file. Use that.
                databaseFileName = Path.GetFileName(fileDatabasePaths[0]); // Get the file name, excluding the path
            }
            else if (fileDatabasePaths.Length > 1)
            {
                // There is more than one ddb file. Ask the user which one they want to use
                ChooseFromListOfTimelapseFiles chooseDatabaseFile = new(this, fileDatabasePaths, templateDatabasePath);
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
                // There are no existing .ddb files. Generate a ddb file name from the template file
                string templateDatabaseFileName = Path.GetFileName(templateDatabasePath);
                if (String.Equals(templateDatabaseFileName, File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    databaseFileName = File.DefaultFileDatabaseFileName;
                }
                else
                {
                    databaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + File.FileDatabaseFileExtension;
                }
                importImages = true;
            }

            databaseFilePath = Path.Combine(directoryPath, databaseFileName);
            return true;
        }
        #endregion
    }
}
