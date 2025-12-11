using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.QuickPaste;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;
using File = System.IO.File;

// Edit Menu Callbacks
// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region Edit Submenu Opening 
        private void Edit_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going

            // Enable / disable various edit menu items depending on whether we are looking at the single image view or overview
            bool state = IsDisplayingSingleImage();
            MenuItemCopyPreviousValues.IsEnabled = state;

            // Enable the FindMissingImage menu only if the current image is missing
            ImageRow currentImage = DataHandler?.ImageCache?.Current;
            MenuItemFindMissingImage.IsEnabled =
                   DataHandler?.ImageCache?.Current != null
                && false == File.Exists(FilesFolders.GetFullPath(DataHandler.FileDatabase, currentImage));

            if (MarkableCanvas.ThumbnailGrid.IsVisible == false && MarkableCanvas.ThumbnailGrid.IsGridActive == false)
            {
                MenuItemRestoreDefaults.Header = "Restore default values for this file";
                MenuItemRestoreDefaults.ToolTip = "For the currently displayed file, revert all fields to its default values (excepting file paths and dates/times)";
            }
            else
            {
                MenuItemRestoreDefaults.Header = "Restore default values for the checkmarked files";
                MenuItemRestoreDefaults.ToolTip = "For all checkmarked files, revert their fields to their default values (excepting file paths and dates/times)";
            }

            // Enable duplicates only if we have a single image in the main view to duplicate
            // We don't allow duplications in the overview as the attempts to do so were somewhat buggy. It could be done, but I don't think its critical.
            MenuItemDuplicateRecordUsingDefaultValues.IsEnabled = state;
            MenuItemDuplicateRecordUsingCurrentValues.IsEnabled = state;

            // Enable Frame extraction only if we  have a displayable paused single video in the main view within a media player
            MenuItemExtractVideoFrameUsingCurrentValues.IsEnabled =
                state &&
                DataHandler?.ImageCache?.Current is VideoRow &&
                DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages) &&
                DataHandler.ImageCache.Current.IsVideo &&
                null != this.MarkableCanvas?.VideoPlayer?.MediaElement &&
                this.MarkableCanvas.VideoPlayer.PlayOrPause.IsChecked == false;
        }
        #endregion

        #region Find image 
        private void MenuItemFindByFileName_Click(object sender, RoutedEventArgs e)
        {
            FindBoxSetVisibility(true);
        }
        #endregion

        #region QuickPaste
        /// Show the QuickPaste Window 
        private void MenuItemQuickPasteWindowShow_Click(object sender, RoutedEventArgs e)
        {
            if (quickPasteWindow == null)
            {
                // create the quickpaste window if it doesn't already exist.
                QuickPasteWindowShow();
            }
            QuickPasteRefreshWindowAndJSON();
            QuickPasteWindowShow();
        }

        /// Import QuickPaste Items from a .ddb file
        private void MenuItemQuickPasteImportFromDB_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog("Import QuickPaste entries by selecting the Timelapse database (.ddb) file from the image folder where you had used them.",
                                             Path.Combine(DataHandler.FileDatabase.RootPathToDatabase, Constant.File.DefaultFileDatabaseFileName),
                                             String.Format("Database files (*{0})|*{0}", Constant.File.FileDatabaseFileExtension),
                                             Constant.File.FileDatabaseFileExtension,
                                             out string ddbFile))
            {
                List<QuickPasteEntry> qpe = QuickPasteOperations.QuickPasteImportFromDB(DataHandler.FileDatabase, ddbFile);
                if (qpe.Count == 0)
                {
                    Dialogs.MenuEditCouldNotImportQuickPasteEntriesDialog(this);
                }
                else
                {
                    quickPasteEntries = qpe;
                    DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
                    QuickPasteRefreshWindowAndJSON();
                    QuickPasteWindowShow();
                }
            }
        }
        #endregion

        #region  Copy Previous Values
        private void MenuItemCopyPreviousValues_Click(object sender, RoutedEventArgs e)
        {
            CopyPreviousValues_Click();
        }
        #endregion

        #region Populate metadata text field
        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private async void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, or if its a video that no longer exists, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (DataHandler.ImageCache.Current != null && DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages) == false)
            {
                // There are no displayable images, and thus no metadata to choose from, so abort
                Dialogs.MenuEditPopulateDataFieldWithMetadataDialog(this);
                return;
            }

            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                           "Populate data fields with image metadata",
                                                               optOut =>
                                                               {
                                                                   State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                PopulateFieldsWithMetadata populateField = new(this, DataHandler.FileDatabase, DataHandler.ImageCache.Current.GetFilePath(RootPathToImages), false);
                if (ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);

                }
                // If the Populate dialog started the ExifToolManager, this will kill the no longer needed ExifTool processes. 
                State.ExifToolManager.Stop();
            }
        }
        #endregion

        #region Populate metadata date time field
        private async void MenuItemPopulateDateTimesfromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, or if its a video that no longer exists, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (DataHandler.ImageCache.Current != null && DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages) == false)
            {
                // There are no displayable images, and thus no metadata to choose from, so abort
                Dialogs.MenuEditRereadDateTimesFromMetadataDialog(this);
                return;
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                           "Populate one or more Date/Time fields...",
                                                               optOut =>
                                                               {
                                                                   State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                if (DataHandler.ImageCache.Current == null)
                {
                    //Shouldn't happen. And should likely produce an error message.
                    TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                    return;
                }
                PopulateFieldsWithMetadata populateField = new(this, DataHandler.FileDatabase, DataHandler.ImageCache.Current.GetFilePath(RootPathToImages), true);
                if (ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }
        #endregion

        #region Poplulate Field with Episode Data
        private async void MenuItemEditPopulateFieldWithEpisodeData_Click(object sender, RoutedEventArgs e)
        {
            // Check: needs at least one file in the current selection,
            if (DataHandler?.FileDatabase?.CountAllCurrentlySelectedFiles == 0 || DataHandler?.FileDatabase?.ImageSet == null)
            {
                Dialogs.MenuOptionsCantPopulateDataFieldWithEpisodeAsNoFilesDialog(this);
                return;
            }

            // Check: needs at least one Note field that could be populated
            bool noteControlOk = false;
            foreach (ControlRow control in DataHandler.FileDatabase.Controls)
            {
                if (IsCondition.IsControlType_Note_MultiLine(control.Type))
                {
                    noteControlOk = true;
                }
            }
            if (!noteControlOk)
            {
                Dialogs.MenuOptionsCantPopulateDataFieldWithEpisodeAsNoNoteFields(this);
                return;
            }

            // Check: search term, can only be none or relativePath
            bool searchTermsOk = true;
            List<SearchTerm> searchTerms = DataHandler.FileDatabase.CustomSelection?.SearchTerms;
            if (searchTerms == null)
            {
                searchTermsOk = false;
            }
            else
            {
                foreach (SearchTerm searchTerm in searchTerms)
                {
                    if (searchTerm.UseForSearching && searchTerm.DataLabel != DatabaseColumn.RelativePath)
                    {
                        searchTermsOk = false;
                    }
                }
            }

            // Check: if the sort terms must be RelativePath x DateTime, or DateTime all ascending
            SortTerm sortTermDB1 = DataHandler.FileDatabase.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            SortTerm sortTermDB2 = DataHandler.FileDatabase.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            bool sortTermsOk = (sortTermDB1.DataLabel == DatabaseColumn.RelativePath && BooleanValue.True == sortTermDB1.IsAscending && sortTermDB2.DataLabel == DatabaseColumn.DateTime && BooleanValue.True == sortTermDB1.IsAscending)
                               || (sortTermDB1.DataLabel == DatabaseColumn.DateTime && BooleanValue.True == sortTermDB1.IsAscending && string.IsNullOrWhiteSpace(sortTermDB2.DataLabel));

            if (!searchTermsOk || !sortTermsOk)
            {
                if (false == Dialogs.MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrong(this, searchTermsOk, sortTermsOk))
                {
                    return;
                }
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                       "Populate a data field with episode data",
                                                           optOut =>
                                                           {
                                                               State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                           }))
            {
                PopulateFieldWithEpisodeData populateField = new(this, DataHandler.FileDatabase);
                {
                    if (ShowDialogAndCheckIfChangesWereMade(populateField))
                    {
                        await FilesSelectAndShowAsync().ConfigureAwait(true);
                    }
                }
            }

        }
        #endregion

        #region Populate Field with GUID
        private async void MenuItemEditPopulateFieldWithGUID_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                           "Populate data fields with globally unique identifiers (GUIDs)",
                                                               optOut =>
                                                               {
                                                                   State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                PopulateFieldWithGUID populateField = new(this, DataHandler.FileDatabase);
                if (ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
                // If the Populate dialog started the ExifToolManager, this will kill the no longer needed ExifTool processes. 
                State.ExifToolManager.Stop();
            }
        }
        #endregion

        #region Dupicate the record

        private static bool IsDuplicating;
        private async void MenuItemEditDuplicateRecord_Click(object sender, RoutedEventArgs e)
        {
            if (IsDisplayingSingleImage() == false)
            {
                // We only allow duplication if we are displaying a single image in the main view
                return;
            }

            if (IsDuplicating)
            {
                Dialogs.MenuEditDuplicatesPleaseWait(this);
                return;
            }

            // Check: ideally the sort terms will be RelativePath x DateTime, as otherwise the duplicates may not be in sorted order.
            // The various flags determine whether we show only a problem message, a duplicate info message, or both.
            SortTerm sortTermDB1 = DataHandler.FileDatabase.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            SortTerm sortTermDB2 = DataHandler.FileDatabase.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            bool sortTermsOKForDuplicateOrdering =
                     (sortTermDB1.DataLabel == DatabaseColumn.RelativePath && sortTermDB2.DataLabel == DatabaseColumn.DateTime)
                  || (sortTermDB1.DataLabel == DatabaseColumn.DateTime && string.IsNullOrWhiteSpace(sortTermDB2.DataLabel));

            if (State.SuppressHowDuplicatesWork == false || sortTermsOKForDuplicateOrdering == false)
            {
                if (Dialogs.MenuEditHowDuplicatesWorkDialog(this, sortTermsOKForDuplicateOrdering, State.SuppressHowDuplicatesWork) == false)
                {
                    return;
                }
            }

            bool useCurrentValues = false;
            if (sender is MenuItem { Tag: not null } mi)
            {
                useCurrentValues = bool.TryParse((string)mi.Tag, out bool tmpCurrentValues) && tmpCurrentValues;
            }

            IsDuplicating = true;
            Mouse.OverrideCursor = Cursors.Wait;
            await DuplicateCurrentRecord(useCurrentValues);

            // Need to refresh how duplicate episodes are displayed, as otherwise the total in a sequence could be wrong
            Episodes.Episodes.EpisodeGetEpisodesInRange(DataHandler.FileDatabase.FileTable, DataHandler.ImageCache.CurrentRow);

            Mouse.OverrideCursor = null;
            IsDuplicating = false;
        }
        #endregion

        #region Extract video frame as a record
        // Extracts the current video frame, creates a jpeg image file from it, and creates a record to it using the current data in the video frame
        // The file location is in the same folder as the video frame.
        // The file name is the same as the video frame but with the video position's timestamp appended to it e.g. bear.avi => bear_1.5.jpg 
        // The invoking menu is enabled only if Timelapse is displaying a video and the video player is paused, so those tests are not repeated here.
        private async void MenuItemExtractVideoFrameUsingCurrentValues_Click(object sender, RoutedEventArgs e)
        {
            if (false == this.DataHandler.ImageCache.Current is VideoRow videoRow)
            {
                // Shouldn't happen, as the menu is only enabled if we are displaying a video
                return;
            }

            try
            {
                // Get the time of the current video frame, then correct it by subtracting half the time of a frame
                // This seems that the best way to get the correct frame. Not sure why, but it seems to work
                TimeSpan timeSpanVideoPosition = this.MarkableCanvas.VideoPlayer.MediaElement.Position;
                float videoPositionInSeconds = (float)timeSpanVideoPosition.TotalSeconds;
                float? frameRate = this.MarkableCanvas.VideoPlayer.FrameRate;
                if (frameRate == null || frameRate <= 0)
                {
                    Dialogs.MenuEditExtractVideoFrameProblem(this, "The recognition for this video does not include a valid frame rate");
                    return;
                }

                // Check: ideally the sort terms will be RelativePath x DateTime, as otherwise the duplicates may not be in sorted order.
                // The various flags determine whether we show only a problem message, a duplicate info message, or both.
                SortTerm sortTermDB1 = DataHandler.FileDatabase.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
                SortTerm sortTermDB2 = DataHandler.FileDatabase.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
                bool sortTermsOKForDuplicateOrdering =
                    (sortTermDB1.DataLabel == DatabaseColumn.RelativePath && sortTermDB2.DataLabel == DatabaseColumn.DateTime)
                    || (sortTermDB1.DataLabel == DatabaseColumn.DateTime && string.IsNullOrWhiteSpace(sortTermDB2.DataLabel));

                if (sortTermsOKForDuplicateOrdering == false)
                {
                    if (Dialogs.MenuEditExtractVideoFrameSortOrderWarning(this) == false)
                    {
                        return;
                    }
                }

                videoPositionInSeconds -= (float) (1.0 / frameRate * .5f);
                if (videoPositionInSeconds < 0) videoPositionInSeconds = 0; // In case its at the beginning of the video

                // Do the frame grab, where its source will be the frame as a BitmapImage
                // Should only be invoked with a valid imageRow that is a video, and a valid frametime
                Image frame = new()
                {
                    Source = videoRow.LoadVideoBitmap(this.RootPathToImages, null, ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseHeight, videoPositionInSeconds, out bool isCorruptOrMissing)
                };
                if (isCorruptOrMissing || !(frame.Source is BitmapImage bitmapImage))
                {
                    Dialogs.MenuEditExtractVideoFrameProblem(this, "Timelapse was unable to get it from the video file.");
                    return;
                }

                // Create a new file name from the video file name comprising the fileName_<frameTimeInSeconds>.jpg 
                // e.g. Video.avi becomes Video_1.34.jpg where the 1.34 indicates the frame's time position in the video in seconds
                // Also compose the full file path to the new image file, which will be in the same folder as the video file
                string timestamp = videoPositionInSeconds.ToString("0.000", CultureInfo.InvariantCulture);
                string newFileName = $"{Path.GetFileNameWithoutExtension(videoRow.File)}_{timestamp}.jpg";
                string newFilePath = string.IsNullOrWhiteSpace(videoRow.RelativePath)
                    ? Path.Combine(this.RootPathToImages, newFileName)
                    : Path.Combine(this.RootPathToImages, videoRow.RelativePath, newFileName);

                if (File.Exists(newFilePath))
                {
                    Dialogs.MenuEditExtractVideoFrameProblem(this, $"A file and data record already exists for the current frame:{Environment.NewLine} • {newFilePath}");
                    return;
                }

                if (false == bitmapImage.SaveToFile(newFilePath))
                {
                    Dialogs.MenuEditExtractVideoFrameProblem(this, "While Timelapse could extract the frame, it couldn't write it to a file");
                }
                
                if (false == await DuplicateCurrentRecord(true, newFileName))
                {
                    Dialogs.MenuEditExtractVideoFrameProblem(this, "While Timelapse created the frame as a file, it couldn't create the data record for it");
                }
            }
            catch
            {
                Dialogs.MenuEditExtractVideoFrameProblem(this, "Its not clear what went wrong. However, your data should be unchanged.");
            }
        }
        #endregion

        #region Delete (including sub-menu opening)
        // Delete sub-menu opening
        private void MenuItemDelete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                // Temporarily set the DeleteFlag search term so that it will be used to chec for DeleteFlag = true
                SearchTerm currentSearchTerm = DataHandler.FileDatabase.CustomSelection.SearchTerms.First(term => term.DataLabel == DatabaseColumn.DeleteFlag);
                SearchTerm tempSearchTerm = currentSearchTerm.Clone();
                currentSearchTerm.DatabaseValue = "true";
                currentSearchTerm.UseForSearching = true;
                currentSearchTerm.Operator = "=";

                bool deletedImages = DataHandler.FileDatabase.ExistsFilesMatchingSelectionCondition(FileSelectionEnum.Custom);

                // Reset  the DeleteFlag search term to its previous values
                currentSearchTerm.DatabaseValue = tempSearchTerm.DatabaseValue;
                currentSearchTerm.UseForSearching = tempSearchTerm.UseForSearching;
                currentSearchTerm.Operator = tempSearchTerm.Operator;

                MenuItemDeleteFiles.IsEnabled = deletedImages;
                MenuItemDeleteFilesAndData.IsEnabled = deletedImages;
                MenuItemDeleteFilesData.IsEnabled = deletedImages;

                // Enable the delete current file option only if we are not on the thumbnail grid 
                MenuItemDeleteCurrentFileAndData.IsEnabled = MarkableCanvas.IsThumbnailGridVisible == false; // Only show this option if the thumbnail grid is visible
                MenuItemDeleteCurrentFile.IsEnabled = MarkableCanvas.IsThumbnailGridVisible == false && DataHandler?.ImageCache?.Current != null && DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages);
                MenuItemDeleteCurrentData.IsEnabled = MarkableCanvas.IsThumbnailGridVisible == false;

            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Delete submenu failed to open in Delete_SubmenuOpening. {exception}");

                // This function was blowing up on one user's machine, but not others.
                // I couldn't figure out why, so I just put this fallback in here to catch that unusual case.
                MenuItemDeleteFiles.IsEnabled = true;
                MenuItemDeleteFilesAndData.IsEnabled = true;
                MenuItemDeleteFilesData.IsEnabled = true;
                MenuItemDeleteCurrentFile.IsEnabled = true;
                MenuItemDeleteCurrentFileAndData.IsEnabled = true;
                MenuItemDeleteCurrentData.IsEnabled = true;
            }
        }

        // Delete callback manages all deletion menu choices where: 
        // - the current image or all images marked for deletion are deleted
        // - the data associated with those images may be delted.
        // - deleted images are moved to a backup folder.
        private async void MenuItemDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }

            // This callback is invoked by DeleteImage (which deletes the current image) and DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            List<ImageRow> filesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFiles;
            bool deleteData;

            // Set backup initially to true unless the setting is ImmediatelyDelete
            bool backupDeletedFiles = this.State.DeleteFolderManagement != DeleteFolderManagementEnum.ImmediatelyDelete;

            if (menuItem.Name.Equals(MenuItemDeleteFiles.Name) || menuItem.Name.Equals(MenuItemDeleteFilesAndData.Name) || menuItem.Name.Equals(MenuItemDeleteFilesData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFiles = false == menuItem.Name.Equals(MenuItemDeleteFilesData.Name); // this is the only condition where we don't delete the file
                deleteData = false == menuItem.Name.Equals(MenuItemDeleteFiles.Name); // this is the only condition where we don't delete the data
                // get list of all images marked for deletion in the current seletion
                using (FileTable filetable = DataHandler.FileDatabase.SelectFilesMarkedForDeletion())
                {
                    filesToDelete = filetable.ToList();
                }

                for (int index = filesToDelete.Count - 1; index >= 0; index--)
                {
                    if (DataHandler.FileDatabase.FileTable.Find(filesToDelete[index].ID) == null)
                    {
                        filesToDelete.Remove(filesToDelete[index]);
                    }
                }
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                deleteCurrentImageOnly = true;
                deleteFiles = false == menuItem.Name.Equals(MenuItemDeleteCurrentData.Name); // this is the only condition where we don't delete the current file
                deleteData = false == menuItem.Name.Equals(MenuItemDeleteCurrentFile.Name); // this is the only condition where we don't delete the current data

                filesToDelete = [];
                if (DataHandler.ImageCache.Current != null)
                {
                    filesToDelete.Add(DataHandler.ImageCache.Current);
                }
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (filesToDelete.Count < 1)
            {
                Dialogs.MenuEditNoFilesMarkedForDeletionDialog(this);
                return;
            }

            // if we delete data records, we can sometimes get in the situation (particularly if we delete a duplicate) where the next fileID displayed is not the closest to the existing position.
            // To resolve this, we get the closest non-deleted file ID before we do the deletion and then try to display that file.
            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }

            long currentFileID = DataHandler.ImageCache.Current.ID;
            if (deleteData)
            {
                // if we delete the data for the current image only but not the file , we can sometimes get in the situation (particularly if we delete a duplicate) where the next fileID displayed is not the closest to the existing position.
                // To resolve this, we get the closest non-deleted file ID before we do the deletion.
                int fileIndex = DataHandler.ImageCache.CurrentRow;
                bool allDone = false;
                Mouse.OverrideCursor = Cursors.Wait;
                int count = DataHandler.FileDatabase.FileTable.Count();
                for (int nextFileIndex = fileIndex; nextFileIndex < count; nextFileIndex++)
                {
                    // Check if is a deleted file. 
                    if (DataHandler.FileDatabase.IsFileRowInRange(nextFileIndex))
                    {
                        if (DataHandler.FileDatabase.FileTable[nextFileIndex].DeleteFlag == false)
                        {
                            // Its not a deleted file, so we have a valid next file to display!
                            currentFileID = DataHandler.FileDatabase.FileTable[nextFileIndex].ID;
                            allDone = true;
                            break;
                        }
                    }
                }
                Mouse.OverrideCursor = null;
                if (allDone == false)
                {
                    for (int prevFileIndex = fileIndex; prevFileIndex >= 0; prevFileIndex--)
                    {
                        // Check if is a deleted file. 
                        if (DataHandler.FileDatabase.IsFileRowInRange(prevFileIndex))
                        {
                            if (DataHandler.FileDatabase.FileTable[prevFileIndex].DeleteFlag == false)
                            {
                                // Its not a deleted file, so we have a valid next file to display!
                                currentFileID = DataHandler.FileDatabase.FileTable[prevFileIndex].ID;
                                //allDone = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Delete the files
            ImageRow longestFile = null;
            int longestFilePathLength = 0;
            if (deleteFiles)
            {
                foreach (ImageRow fileToDelete in filesToDelete)
                {
                    int thisFileLength = fileToDelete.File.Length + fileToDelete.RelativePath.Length;
                    if (thisFileLength > longestFilePathLength)
                    {
                        longestFile = fileToDelete;
                        longestFilePathLength = thisFileLength;
                    }
                }
            }
            if (longestFile != null && IsCondition.IsPathLengthTooLong(Path.Combine(DataHandler.FileDatabase.RootPathToImages, longestFile.RelativePath, Constant.File.DeletedFilesFolder, longestFile.File), FilePathTypeEnum.Deleted))
            {
                // Path is too long to back up
                if (Dialogs.FilePathDeletedFileTooLongDialog(this) == false)
                {
                    // User cancelled
                    return;
                }
                // User said delete anyways, so don't back up deleted files
                backupDeletedFiles = false;
            }
            // Delete the files

            DeleteImages deleteImagesDialog = new(this, DataHandler.FileDatabase, DataHandler.ImageCache, filesToDelete, deleteFiles, deleteData, deleteCurrentImageOnly, backupDeletedFiles);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {

                Mouse.OverrideCursor = Cursors.Wait;
                // Reload the file datatable. 
                await FilesSelectAndShowAsync(currentFileID, DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);

                if (deleteData)
                {
                    // Find and show the image closest to the last one shown
                    if (DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
                    {
                        int nextImageRow = DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID);
                        FileShow(nextImageRow);
                    }
                    else
                    {
                        // No images left, so disable everything
                        EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // display the updated properties on the current image, or the closest one to it.
                    int nextImageRow = DataHandler.FileDatabase.FindClosestImageRow(currentFileID);
                    FileShow(nextImageRow);
                }
                Mouse.OverrideCursor = null;
            }
        }
        #endregion

        #region Date Correction (including sub-menu opening)
        private void MenuItemDateCorrection_SubmenuOpened(object sender, RoutedEventArgs e)
        {
        }

        // Re-read dates and times from files
        private async void MenuItemRereadDateTimesfromFiles_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                DataHandler.FileDatabase,
                State.SuppressSelectedRereadDatesFromFilesPrompt,
                "Reread dates and times from files",
                optOut => { State.SuppressSelectedRereadDatesFromFilesPrompt = optOut; }
                ))
            {
                DateTimeRereadFromFiles rereadDates = new(this, DataHandler.FileDatabase);
                if (ShowDialogAndCheckIfChangesWereMade(rereadDates))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct for daylight savings time
        private async void MenuItemDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                DataHandler.FileDatabase,
                State.SuppressSelectedDaylightSavingsCorrectionPrompt,
                "Correct for daylight savings time",
                optOut => { State.SuppressSelectedDaylightSavingsCorrectionPrompt = optOut; }
                ))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new(this, DataHandler.FileDatabase, DataHandler.ImageCache);
                if (ShowDialogAndCheckIfChangesWereMade(dateTimeChange))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct for cameras not set to the right date and time by specifying an offset
        private async void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedDateTimeFixedCorrectionPrompt,
                                                                           "Add a fixed correction value to every date/time",
                                                               optOut =>
                                                               {
                                                                   State.SuppressSelectedDateTimeFixedCorrectionPrompt = optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new(this, DataHandler.FileDatabase, DataHandler.ImageCache.Current);
                if (ShowDialogAndCheckIfChangesWereMade(fixedDateCorrection))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct for cameras whose clock runs fast or slow (clock drift). 
        // Note that the correction is applied only to images in the selected view.
        private async void MenuItemDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                DataHandler.FileDatabase,
                State.SuppressSelectedDateTimeLinearCorrectionPrompt,
                "Correct for camera clock drift",
                optOut => { State.SuppressSelectedDateTimeLinearCorrectionPrompt = optOut; }
                ))
            {
                DateTimeLinearCorrection linearDateCorrection = new(this, DataHandler.FileDatabase);
                if (ShowDialogAndCheckIfChangesWereMade(linearDateCorrection))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct ambiguous dates dialog i.e. dates that could be read as either month/day or day/month
        private async void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this, DataHandler.FileDatabase, State.SuppressSelectedAmbiguousDatesPrompt,
                "Correct ambiguous dates",
                optOut =>
                 {
                     State.SuppressSelectedAmbiguousDatesPrompt = optOut;
                 }))
            {
                DateCorrectAmbiguous dateCorrection = new(this, DataHandler.FileDatabase);
                if (ShowDialogAndCheckIfChangesWereMade(dateCorrection))
                {
                    await FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        #endregion

        #region Folder editor

        // Raise the folder editor
        private async void MenuItemFolderEditor_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user about using the folder editor if metadata is in use
            if (DataHandler?.FileDatabase?.MetadataInfo is { RowCount: > 0 })
            {
                if (false == Dialogs.FolderEditorMetadataWarning(this))
                {
                    return;
                }
            }

            RelativePathEditor relativePathEditor = new(this, DataHandler?.FileDatabase);
            if (true == relativePathEditor.ShowDialog())
            {
                // true is returned if any edits were actually made by the user that led to changes
                // So reselect/display the files to show those changes
                // We also clear the items in the select menu folder tree view, which means it will be rebuilt with the new folder structure
                await FilesSelectAndShowAsync().ConfigureAwait(true);
                this.MenuItemSelectByFolder_ResetFolderList();
            }
        }
        #endregion

        #region CompareFoldersToExpectedStructure
        private void MenuItemAnalyzeFolderStructure_Click(object sender, RoutedEventArgs e)
        {
            if (null == DataHandler?.FileDatabase?.MetadataInfo) return;
            MetadataFolderComplianceViewer dialog = new(this, DataHandler.FileDatabase, [], DataHandler.FileDatabase.MetadataInfo, false);
            dialog.ShowDialog();

        }
        #endregion

        #region Find Missing Files and Folders
        //Try to find a missing image
        private async void MenuItemEditFindMissingImage_Click(object sender, RoutedEventArgs e)
        {
            // Don't do anything if the image actually exists. This should not fire, as this menu item is only enabled 
            // if there is a current image that doesn't exist. But just in case...

            if (null == DataHandler?.ImageCache?.Current || File.Exists(FilesFolders.GetFullPath(DataHandler.FileDatabase.RootPathToImages, DataHandler?.ImageCache?.Current)))
            {
                return;
            }
            // Note:  there are redundant null checks due to Resharper indicating possible nullreference exceptions
            if (null == DataHandler)
            {
                // Shouldn't happen. 
                TracePrint.NullException(nameof(DataHandler));
                return;
            }
            string folderPath = DataHandler.FileDatabase.RootPathToImages;
            ImageRow currentImage = DataHandler?.ImageCache?.Current;

            if (null == currentImage)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(currentImage));
                return;
            }
            // Search for - and return as list of relative path / filename tuples -  all folders under the root folder for files with the same name as the fileName.
            List<Tuple<string, string>> matchingRelativePathFileNameList = FilesFolders.SearchForFoldersContainingFileName(folderPath, currentImage.File);

            // Remove any of the tuples that are spoken for i.e., that are already associated with a row in the database
            for (int i = matchingRelativePathFileNameList.Count - 1; i >= 0; i--)
            {
                if (null == DataHandler)
                {
                    // Shouldn't happen. 
                    TracePrint.NullException(nameof(DataHandler));
                    return;
                }
                if (DataHandler.FileDatabase.ExistsRelativePathAndFileInDataTable(matchingRelativePathFileNameList[i].Item1, matchingRelativePathFileNameList[i].Item2))
                {
                    // We only want matching files that are not already assigned to another datafield in the database
                    matchingRelativePathFileNameList.RemoveAt(i);
                }
            }

            // If there are no remaining tuples, it means no potential matches were found. 
            // Display a message saying so and abort.
            if (matchingRelativePathFileNameList.Count == 0)
            {
                Dialogs.MissingFileSearchNoMatchesFoundDialog(this, currentImage.File);
                return;
            }

            // Now retrieve a list of all filenames located in the same folder (i.e., that have the same relative path) as the missing file.
            if (null == DataHandler)
            {
                // Shouldn't happen. 
                TracePrint.NullException(nameof(DataHandler));
                return;
            }
            List<string> otherMissingFiles = DataHandler.FileDatabase.SelectFileNamesWithRelativePathFromDatabase(currentImage.RelativePath);

            // Remove the current missing file from the list, as well as any file that exists i.e., that is not missing.
            for (int i = otherMissingFiles.Count - 1; i >= 0; i--)
            {
                if (String.Equals(otherMissingFiles[i], currentImage.File) || File.Exists(Path.Combine(folderPath, currentImage.RelativePath, otherMissingFiles[i])))
                {
                    otherMissingFiles.RemoveAt(i);
                }
            }

            // For those that are left (if any), see if other files in the returned locations are in each path. Get their count, save them, and pass the count as a parameter e.g., a Dict with matching files, etc. 
            // Or perhapse even better, a list of file names for each path Dict<string, List<string>>
            // As we did above, go through the other missing files and remove those that are spoken for i.e., that are already associated with a row in the database.
            // What remains will be a list of  root paths, each with a list of  missing (unassociated) files that could be candidates for locating
            Dictionary<string, List<string>> otherMissingFileCandidates = [];
            foreach (Tuple<string, string> matchingPath in matchingRelativePathFileNameList)
            {
                List<string> orphanMissingFiles = [];
                foreach (string otherMissingFile in otherMissingFiles)
                {
                    // Its a potential candidate if its not already referenced but it exists in that relative path folder
                    if (false == DataHandler.FileDatabase.ExistsRelativePathAndFileInDataTable(matchingPath.Item1, otherMissingFile)
                        && File.Exists(FilesFolders.GetFullPath(RootPathToImages, matchingPath.Item1, otherMissingFile)))
                    {
                        orphanMissingFiles.Add(otherMissingFile);
                    }
                }
                otherMissingFileCandidates.Add(matchingPath.Item1, orphanMissingFiles);
            }

            MissingImageLocateRelativePaths dialog = new(this, DataHandler.FileDatabase, currentImage.RelativePath, currentImage.File, otherMissingFileCandidates);

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                Tuple<string, string> locatedMissingFile = dialog.LocatedMissingFile;
                if (string.IsNullOrEmpty(locatedMissingFile.Item2))
                {
                    return;
                }

                // Update the original missing file
                List<ColumnTuplesWithWhere> columnTuplesWithWhereList = [];
                ColumnTuplesWithWhere columnTuplesWithWhere = new();
                columnTuplesWithWhere.Columns.Add(new(DatabaseColumn.RelativePath, locatedMissingFile.Item1)); // The new relative path
                columnTuplesWithWhere.SetWhere(currentImage.RelativePath, currentImage.File); // Where the original relative path/file terms are met
                columnTuplesWithWhereList.Add(columnTuplesWithWhere);

                // Update the other missing files in the database, if any
                foreach (string otherMissingFileName in otherMissingFileCandidates[locatedMissingFile.Item1])
                {
                    columnTuplesWithWhere = new();
                    columnTuplesWithWhere.Columns.Add(new(DatabaseColumn.RelativePath, locatedMissingFile.Item1)); // The new value
                    columnTuplesWithWhere.SetWhere(currentImage.RelativePath, otherMissingFileName); // Where the original relative path/file terms are met
                    columnTuplesWithWhereList.Add(columnTuplesWithWhere);
                }

                // Now update the database
                DataHandler.FileDatabase.UpdateFiles(columnTuplesWithWhereList);
                await FilesSelectAndShowAsync().ConfigureAwait(true);
            }
        }

        // Find missing folders
        private async void MenuItemEditFindMissingFolder_Click(object sender, RoutedEventArgs e)
        {
            bool? result = GetAndCorrectForMissingFolders(this, DataHandler.FileDatabase);
            if (true == result)
            {
                await FilesSelectAndShowAsync().ConfigureAwait(true);
                return;
            }
            if (result != null) // && false == result.Value -> this is always true
            {
                Dialogs.MenuEditNoFoldersAreMissing(this);
            }
            // if result is null, it means that the operation was aborted for some reason, or the folders were missing but not updated.
        }
        #endregion

        #region Identify or reclassify dark files.
        private void MenuItemEditClassifyDarkImages_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, DataHandler.FileDatabase, State.SuppressSelectedDarkThresholdPrompt,
                                                                           "Populate a field with Dark classification data",
                                                               optOut =>
                                                               {
                                                                   State.SuppressSelectedDarkThresholdPrompt = optOut; // SG TODO
                                                               }))
            {
                using DarkImagesThreshold darkThreshold = new(this, DataHandler.FileDatabase, State, DataHandler.ImageCache.CurrentRow);
                darkThreshold.Owner = this;
                if (darkThreshold.ShowDialog() == true)
                {
                    // Force an update of the current image in case the current values have changed
                    FileShow(DataHandler.ImageCache.CurrentRow, true);
                }
            }
        }
        #endregion

        #region Edit notes for this image set
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new(DataHandler.FileDatabase.ImageSet.Log, this)
            {
                Owner = this
            };
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                DataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }
        }
        #endregion

        #region Restore default values for this file
        private void MenuRestoreDefaults_Opened(object sender, RoutedEventArgs e)
        {
            // Customize the text to whether full view or overview is being displayed
            if (sender is ContextMenu menu && menu.Items[0] is MenuItem menuItem)
            {
                if (MarkableCanvas.ThumbnailGrid.IsVisible == false && MarkableCanvas.ThumbnailGrid.IsGridActive == false)
                {
                    menuItem.Header = "Restore default values for this file";
                    menuItem.ToolTip = "For the currently displayed file, revert all fields to its default values (excepting file paths and dates/times)";
                }
                else
                {
                    menuItem.Header = "Restore default values for the checkmarked files";
                    menuItem.ToolTip = "For all checkmarked files, revert their fields to their default values (excepting file paths and dates/times)";
                }
            }
        }

        // Need to 
        //-disable the menu when nothing is in it 
        //-handle overview
        //-put in edit menu
        private void MenuItemRestoreDefaultValues_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the controls
            foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
            {
                DataEntryControl control = pair.Value;
                if (control.DataLabel == DatabaseColumn.File || control.DataLabel == DatabaseColumn.RelativePath ||
                    control.DataLabel == DatabaseColumn.DateTime)
                {
                    // Ignore stock controls
                    continue;
                }
                ControlRow imageDatabaseControl = templateDatabase.GetControlFromControls(control.DataLabel);
                if (MarkableCanvas.ThumbnailGrid.IsVisible == false && MarkableCanvas.ThumbnailGrid.IsGridActive == false)
                {
                    // Only a single image is displayed: update the database for the current row with the control's value
                    if (DataHandler.ImageCache.Current == null)
                    {
                        // Shouldn't happen
                        TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                        continue;
                    }
                    DataHandler.FileDatabase.UpdateFile(DataHandler.ImageCache.Current.ID, control.DataLabel, imageDatabaseControl.DefaultValue);
                }
                else
                {
                    // Multiple images are displayed: update the database for all selected rows with the control's value
                    DataHandler.FileDatabase.UpdateFiles(MarkableCanvas.ThumbnailGrid.GetSelected(), control.DataLabel, imageDatabaseControl.DefaultValue);
                }
                control.SetContentAndTooltip(imageDatabaseControl.DefaultValue);
            }
        }
        #endregion

        #region Helper function, only referenced by the above menu callbacks.
        // Various dialogs perform a bulk edit, after which various states have to be refreshed
        // This method shows the dialog and (if a bulk edit is done) refreshes those states.
        private bool ShowDialogAndCheckIfChangesWereMade(Window dialog)
        {
            dialog.Owner = this;
            return (dialog.ShowDialog() == true);
        }
        #endregion
    }
}
