using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.QuickPaste;
using Timelapse.Util;

// Edit Menu Callbacks
namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region Edit Submenu Opening 
        private void Edit_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going

            // Enable / disable various edit menu items depending on whether we are looking at the single image view or overview
            bool state = this.IsDisplayingSingleImage();
            this.MenuItemCopyPreviousValues.IsEnabled = state;

            // Enable the FindMissingImage menu only if the current image is missing
            ImageRow currentImage = this.DataHandler?.ImageCache?.Current;
            this.MenuItemFindMissingImage.IsEnabled =
                   this.DataHandler?.ImageCache?.Current != null
                && false == File.Exists(FilesFolders.GetFullPath(this.DataHandler.FileDatabase, currentImage));

            if (this.MarkableCanvas.ThumbnailGrid.IsVisible == false && this.MarkableCanvas.ThumbnailGrid.IsGridActive == false)
            {
                this.MenuItemRestoreDefaults.Header = "Restore default values for this file";
                this.MenuItemRestoreDefaults.ToolTip = "For the currently displayed file, revert all fields to its default values (excepting file paths and dates/times)";
            }
            else
            {
                this.MenuItemRestoreDefaults.Header = "Restore default values for the checkmarked files";
                this.MenuItemRestoreDefaults.ToolTip = "For all checkmarked files, revert their fields to their default values (excepting file paths and dates/times)";
            }

            // Enable duplicates only if we have a single image in the main view to duplicate
            // We don't allow duplications in the overview as the attempts to do so were somewhat buggy. It could be done, but I don't think its critical.
            this.MenuItemDuplicateRecord.IsEnabled = state;
        }
        #endregion

        #region Find image 
        private void MenuItemFindByFileName_Click(object sender, RoutedEventArgs e)
        {
            this.FindBoxSetVisibility(true);
        }
        #endregion

        #region QuickPaste
        /// Show the QuickPaste Window 
        private void MenuItemQuickPasteWindowShow_Click(object sender, RoutedEventArgs e)
        {
            if (this.quickPasteWindow == null)
            {
                // create the quickpaste window if it doesn't already exist.
                this.QuickPasteWindowShow();
            }
            this.QuickPasteRefreshWindowAndJSON();
            this.QuickPasteWindowShow();
        }

        /// Import QuickPaste Items from a .ddb file
        private void MenuItemQuickPasteImportFromDB_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog("Import QuickPaste entries by selecting the Timelapse database (.ddb) file from the image folder where you had used them.",
                                             Path.Combine(this.DataHandler.FileDatabase.FolderPath, Constant.File.DefaultFileDatabaseFileName),
                                             String.Format("Database files (*{0})|*{0}", Constant.File.FileDatabaseFileExtension),
                                             Constant.File.FileDatabaseFileExtension,
                                             out string ddbFile) == true)
            {
                List<QuickPasteEntry> qpe = QuickPasteOperations.QuickPasteImportFromDB(this.DataHandler.FileDatabase, ddbFile);
                if (qpe.Count == 0)
                {
                    Dialogs.MenuEditCouldNotImportQuickPasteEntriesDialog(this);
                    return;
                }
                else
                {
                    this.quickPasteEntries = qpe;
                    this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
                    this.QuickPasteRefreshWindowAndJSON();
                    this.QuickPasteWindowShow();
                }
            }
        }
        #endregion

        #region  Copy Previous Values
        private void MenuItemCopyPreviousValues_Click(object sender, RoutedEventArgs e)
        {
            this.CopyPreviousValues_Click();
        }
        #endregion

        #region Populate metadata
        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private async void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, or if its a video that no longer exists, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath) == false)
            {
                // There are no displayable images, and thus no metadata to choose from, so abort
                Dialogs.MenuEditPopulateDataFieldWithMetadataDialog(this);
                return;
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                           "'Populate data fields with image metadata...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                PopulateFieldsWithMetadata populateField = new PopulateFieldsWithMetadata(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath), false);
                if (this.ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);

                }
                // If the Populate dialog started the ExifToolManager, this will kill the no longer needed ExifTool processes. 
                this.State.ExifToolManager.Stop();
            }
        }
        #endregion

        #region Poplulate Field with Episode Data
        private async void MenuItemEditPopulateFieldWithEpisodeData_Click(object sender, RoutedEventArgs e)
        {
            // Check: needs at least one file in the current selection,
            if (this.DataHandler?.FileDatabase?.CountAllCurrentlySelectedFiles == 0 || this.DataHandler.FileDatabase.ImageSet == null)
            {
                Dialogs.MenuOptionsCantPopulateDataFieldWithEpisodeAsNoFilesDialog(this);
                return;
            }

            // Check: needs at least one Note field that could be populated
            bool noteControlOk = false;
            foreach (ControlRow control in this.DataHandler.FileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Note)
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
            List<SearchTerm> searchTerms = this.DataHandler.FileDatabase.CustomSelection?.SearchTerms;
            if (searchTerms == null)
            {
                searchTermsOk = false;
            }
            else
            {
                foreach (SearchTerm searchTerm in searchTerms)
                {
                    if (searchTerm.UseForSearching && searchTerm.DataLabel != Constant.DatabaseColumn.RelativePath)
                    {
                        searchTermsOk = false;
                    }
                }
            }

            // Check: if the sort terms must be RelativePath x DateTime, or DateTime all ascending
            SortTerm sortTermDB1 = this.DataHandler.FileDatabase.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            SortTerm sortTermDB2 = this.DataHandler.FileDatabase.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            bool sortTermsOk = (sortTermDB1.DataLabel == Constant.DatabaseColumn.RelativePath && Constant.BooleanValue.True == sortTermDB1.IsAscending && sortTermDB2.DataLabel == Constant.DatabaseColumn.DateTime && Constant.BooleanValue.True == sortTermDB1.IsAscending)
                               || (sortTermDB1.DataLabel == Constant.DatabaseColumn.DateTime && Constant.BooleanValue.True == sortTermDB1.IsAscending && String.IsNullOrWhiteSpace(sortTermDB2.DataLabel));

            if (!noteControlOk || !searchTermsOk || !sortTermsOk)
            {
                if (false == Dialogs.MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrong(this, searchTermsOk, sortTermsOk))
                {
                    return;
                }
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                       "'Populate a data field with episodedata...'",
                                                           (bool optOut) =>
                                                           {
                                                               this.State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                           }))
            {
                PopulateFieldWithEpisodeData populateField = new PopulateFieldWithEpisodeData(this, this.DataHandler.FileDatabase);
                {
                    if (this.ShowDialogAndCheckIfChangesWereMade(populateField))
                    {
                        await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                    };
                }
            }

        }
        #endregion

        #region Dupicate the record
        private async void MenuItemEditDuplicateRecord_Click(object sender, RoutedEventArgs e)
        {
            // Check: ideally the sort terms will be RelativePath x DateTime, as otherwise the duplicates may not be in sorted order.
            // The various flags determine whether we show only a problem message, a duplicate info message, or both.
            SortTerm sortTermDB1 = this.DataHandler.FileDatabase.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            SortTerm sortTermDB2 = this.DataHandler.FileDatabase.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            bool sortTermsOKForDuplicateOrdering =
                     (sortTermDB1.DataLabel == Constant.DatabaseColumn.RelativePath && sortTermDB2.DataLabel == Constant.DatabaseColumn.DateTime)
                  || (sortTermDB1.DataLabel == Constant.DatabaseColumn.DateTime && String.IsNullOrWhiteSpace(sortTermDB2.DataLabel));

            if (this.State.SuppressHowDuplicatesWork == false || sortTermsOKForDuplicateOrdering == false)
            {
                if (Dialogs.MenuEditHowDuplicatesWorkDialog(this, sortTermsOKForDuplicateOrdering, this.State.SuppressHowDuplicatesWork) == false)
                {
                    return;
                }
            }
            await this.DuplicateCurrentRecord();
        }
        #endregion

        #region Delete (including sub-menu opening)
        // Delete sub-menu opening
        private void MenuItemDelete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                // Temporarily set the DeleteFlag search term so that it will be used to chec for DeleteFlag = true
                SearchTerm currentSearchTerm = this.DataHandler.FileDatabase.CustomSelection.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.DeleteFlag);
                SearchTerm tempSearchTerm = currentSearchTerm.Clone();
                currentSearchTerm.DatabaseValue = "true";
                currentSearchTerm.UseForSearching = true;
                currentSearchTerm.Operator = "=";

                //bool deletedImages = this.DataHandler.FileDatabase.ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum.MarkedForDeletion);
                //bool deletedImages = this.DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom) > 0;
                bool deletedImages = this.DataHandler.FileDatabase.ExistsFilesMatchingSelectionCondition(FileSelectionEnum.Custom);

                // Reset  the DeleteFlag search term to its previous values
                currentSearchTerm.DatabaseValue = tempSearchTerm.DatabaseValue;
                currentSearchTerm.UseForSearching = tempSearchTerm.UseForSearching;
                currentSearchTerm.Operator = tempSearchTerm.Operator;

                this.MenuItemDeleteFiles.IsEnabled = deletedImages;
                this.MenuItemDeleteFilesAndData.IsEnabled = deletedImages;
                this.MenuItemDeleteFilesData.IsEnabled = deletedImages;

                // Enable the delete current file option only if we are not on the thumbnail grid 
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = this.MarkableCanvas.IsThumbnailGridVisible == false; // Only show this option if the thumbnail grid is visible
                this.MenuItemDeleteCurrentFile.IsEnabled = this.MarkableCanvas.IsThumbnailGridVisible == false && this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath);
                this.MenuItemDeleteCurrentData.IsEnabled = this.MarkableCanvas.IsThumbnailGridVisible == false;

            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(String.Format("Delete submenu failed to open in Delete_SubmenuOpening. {0}", exception.ToString()));

                // This function was blowing up on one user's machine, but not others.
                // I couldn't figure out why, so I just put this fallback in here to catch that unusual case.
                this.MenuItemDeleteFiles.IsEnabled = true;
                this.MenuItemDeleteFilesAndData.IsEnabled = true;
                this.MenuItemDeleteFilesData.IsEnabled = true;
                this.MenuItemDeleteCurrentFile.IsEnabled = true;
                this.MenuItemDeleteCurrentFileAndData.IsEnabled = true;
                this.MenuItemDeleteCurrentData.IsEnabled = true;
            }
        }

        // Delete callback manages all deletion menu choices where: 
        // - the current image or all images marked for deletion are deleted
        // - the data associated with those images may be delted.
        // - deleted images are moved to a backup folder.
        private async void MenuItemDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            // This callback is invoked by DeleteImage (which deletes the current image) and DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            List<ImageRow> filesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFiles;
            bool deleteData;
            if (menuItem.Name.Equals(this.MenuItemDeleteFiles.Name) || menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name) || menuItem.Name.Equals(this.MenuItemDeleteFilesData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFiles = false == menuItem.Name.Equals(this.MenuItemDeleteFilesData.Name); // this is the only condition where we don't delete the file
                deleteData = false == menuItem.Name.Equals(this.MenuItemDeleteFiles.Name); // this is the only condition where we don't delete the data
                // get list of all images marked for deletion in the current seletion
                using (FileTable filetable = this.DataHandler.FileDatabase.SelectFilesMarkedForDeletion())
                {
                    filesToDelete = filetable.ToList();
                }

                for (int index = filesToDelete.Count - 1; index >= 0; index--)
                {
                    if (this.DataHandler.FileDatabase.FileTable.Find(filesToDelete[index].ID) == null)
                    {
                        filesToDelete.Remove(filesToDelete[index]);
                    }
                }
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                deleteCurrentImageOnly = true;
                deleteFiles = false == menuItem.Name.Equals(this.MenuItemDeleteCurrentData.Name); // this is the only condition where we don't delete the current file
                deleteData = false == menuItem.Name.Equals(this.MenuItemDeleteCurrentFile.Name); // this is the only condition where we don't delete the current data

                filesToDelete = new List<ImageRow>();
                if (this.DataHandler.ImageCache.Current != null)
                {
                    filesToDelete.Add(this.DataHandler.ImageCache.Current);
                }
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (filesToDelete == null || filesToDelete.Count < 1)
            {
                Dialogs.MenuEditNoFilesMarkedForDeletionDialog(this);
                return;
            }

            // if we delete data records, we can sometimes get in the situation (particularly if we delete a duplicate) where the next fileID displayed is not the closest to the existing position.
            // To resolve this, we get the closest non-deleted file ID before we do the deletion and then try to display that file.
            long currentFileID = this.DataHandler.ImageCache.Current.ID;
            if (deleteData == true)
            {
                //foreach (ImageRow ir in filesToDelete)
                //{
                //    System.Diagnostics.Debug.Print(ir.ID.ToString());
                //}
                // if we delete the data for the current image only but not the file , we can sometimes get in the situation (particularly if we delete a duplicate) where the next fileID displayed is not the closest to the existing position.
                // To resolve this, we get the closest non-deleted file ID before we do the deletion.
                int fileIndex = this.DataHandler.ImageCache.CurrentRow;
                bool allDone = false;
                for (int nextFileIndex = fileIndex; nextFileIndex < this.DataHandler.FileDatabase.FileTable.Count(); nextFileIndex++)
                {
                    // Check if is a deleted file. 
                    if (this.DataHandler.FileDatabase.IsFileRowInRange(nextFileIndex))
                    {
                        // System.Diagnostics.Debug.Print("-->" + this.DataHandler.FileDatabase.FileTable[nextFileIndex].ID.ToString());
                        if (false == filesToDelete.Any(file => file.ID == this.DataHandler.FileDatabase.FileTable[nextFileIndex].ID))
                        {
                            // Its not a deleted file, so we have a valid next file to display!
                            currentFileID = this.DataHandler.FileDatabase.FileTable[nextFileIndex].ID;
                            allDone = true;
                            break;
                        }
                    }
                }
                if (allDone == false)
                {
                    for (int prevFileIndex = fileIndex; prevFileIndex >= 0; prevFileIndex--)
                    {
                        // Check if is a deleted file. 
                        if (this.DataHandler.FileDatabase.IsFileRowInRange(prevFileIndex))
                        {
                            if (false == filesToDelete.Any(file => file.ID == this.DataHandler.FileDatabase.FileTable[prevFileIndex].ID))
                            {
                                // Its not a deleted file, so we have a valid next file to display!
                                currentFileID = this.DataHandler.FileDatabase.FileTable[prevFileIndex].ID;
                                allDone = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Delete the files
            ImageRow longestFile = null;
            int longestFilePathLength = 0;
            bool backupDeletedFiles = true;
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
            if (longestFile != null && IsCondition.IsPathLengthTooLong(Path.Combine(this.DataHandler.FileDatabase.FolderPath, longestFile.RelativePath, Constant.File.DeletedFilesFolder, longestFile.File), FilePathTypeEnum.Deleted))
            {
                // Path is too long to delete
                if (Dialogs.FilePathDeletedFileTooLongDialog(this) == false)
                {
                    // User cancelled
                    return;
                }
                // User said delete anyways, so don't back up deleted files
                backupDeletedFiles = false;
            }
            // Delete the files

            DeleteImages deleteImagesDialog = new DeleteImages(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache, filesToDelete, deleteFiles, deleteData, deleteCurrentImageOnly, backupDeletedFiles);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {

                Mouse.OverrideCursor = Cursors.Wait;
                // Reload the file datatable. 
                await this.FilesSelectAndShowAsync(currentFileID, this.DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);

                if (deleteData)
                {
                    // Find and show the image closest to the last one shown
                    if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0)
                    {
                        int nextImageRow = this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID);
                        this.FileShow(nextImageRow);
                    }
                    else
                    {
                        // No images left, so disable everything
                        this.EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // display the updated properties on the current image, or the closest one to it.
                    int nextImageRow = this.DataHandler.FileDatabase.FindClosestImageRow(currentFileID);
                    this.FileShow(nextImageRow);
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
                this.DataHandler.FileDatabase,
                this.State.SuppressSelectedRereadDatesFromFilesPrompt,
                "'Reread dates and times from files...'",
                (bool optOut) => { this.State.SuppressSelectedRereadDatesFromFilesPrompt = optOut; }
                ))
            {
                DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this, this.DataHandler.FileDatabase);
                if (this.ShowDialogAndCheckIfChangesWereMade(rereadDates))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                };
            }
        }

        // Correct for daylight savings time
        private async void MenuItemDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this,
                this.DataHandler.FileDatabase,
                this.State.SuppressSelectedDaylightSavingsCorrectionPrompt,
                "'Correct for daylight savings time...'",
                (bool optOut) => { this.State.SuppressSelectedDaylightSavingsCorrectionPrompt = optOut; }
                ))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new DateDaylightSavingsTimeCorrection(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache);
                if (this.ShowDialogAndCheckIfChangesWereMade(dateTimeChange))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                };
            }
        }

        // Correct for cameras not set to the right date and time by specifying an offset
        private async void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedDateTimeFixedCorrectionPrompt,
                                                                           "'Add a fixed correction value to every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedDateTimeFixedCorrectionPrompt = optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current);
                if (this.ShowDialogAndCheckIfChangesWereMade(fixedDateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
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
                this.DataHandler.FileDatabase,
                this.State.SuppressSelectedDateTimeLinearCorrectionPrompt,
                "'Correct for camera clock drift'",
                (bool optOut) => { this.State.SuppressSelectedDateTimeLinearCorrectionPrompt = optOut; }
                ))
            {
                DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this, this.DataHandler.FileDatabase);
                if (this.ShowDialogAndCheckIfChangesWereMade(linearDateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        // Correct ambiguous dates dialog i.e. dates that could be read as either month/day or day/month
        private async void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(
                this, this.DataHandler.FileDatabase, this.State.SuppressSelectedAmbiguousDatesPrompt,
                "'Correct ambiguous dates...'",
                (bool optOut) =>
                 {
                     this.State.SuppressSelectedAmbiguousDatesPrompt = optOut;
                 }))
            {
                DateCorrectAmbiguous dateCorrection = new DateCorrectAmbiguous(this, this.DataHandler.FileDatabase);
                if (this.ShowDialogAndCheckIfChangesWereMade(dateCorrection))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void MenuItemRereadDateTimesfromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the selection All view, or if its a corrupt image or deleted image, or if its a video that no longer exists, tell the person. Selecting ok will shift the selection.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath) == false)
            {
                // There are no displayable images, and thus no metadata to choose from, so abort
                Dialogs.MenuEditPopulateDataFieldWithMetadataDialog(this);
                return;
            }

            // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedPopulateFieldFromMetadataPrompt,
                                                                           "'Re-read dates and times from a metadata field...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                               }))
            {
                PopulateFieldsWithMetadata populateField = new PopulateFieldsWithMetadata(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath), true);
                if (this.ShowDialogAndCheckIfChangesWereMade(populateField))
                {
                    await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                }

                //using (DateTimeRereadFromSelectedMetadataField populateField = new DateTimeRereadFromSelectedMetadataField(this, this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath)))
                //{
                //    if (this.ShowDialogAndCheckIfChangesWereMade(populateField))
                //    {
                //        await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                //    };
                //}
            }
        }
        #endregion

        #region Find Missing Files and Folders
        //Try to find a missing image
        private async void MenuItemEditFindMissingImage_Click(object sender, RoutedEventArgs e)
        {
            // Don't do anything if the image actually exists. This should not fire, as this menu item is only enabled 
            // if there is a current image that doesn't exist. But just in case...
            if (null == this.DataHandler?.ImageCache?.Current || File.Exists(FilesFolders.GetFullPath(this.DataHandler.FileDatabase.FolderPath, this.DataHandler?.ImageCache?.Current)))
            {
                return;
            }

            string folderPath = this.DataHandler.FileDatabase.FolderPath;
            ImageRow currentImage = this.DataHandler?.ImageCache?.Current;

            // Search for - and return as list of relative path / filename tuples -  all folders under the root folder for files with the same name as the fileName.
            List<Tuple<string, string>> matchingRelativePathFileNameList = Util.FilesFolders.SearchForFoldersContainingFileName(folderPath, currentImage.File);

            // Remove any of the tuples that are spoken for i.e., that are already associated with a row in the database
            for (int i = matchingRelativePathFileNameList.Count - 1; i >= 0; i--)
            {
                if (this.DataHandler.FileDatabase.ExistsRelativePathAndFileInDataTable(matchingRelativePathFileNameList[i].Item1, matchingRelativePathFileNameList[i].Item2))
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
            List<string> otherMissingFiles = this.DataHandler.FileDatabase.SelectFileNamesWithRelativePathFromDatabase(currentImage.RelativePath);

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
            Dictionary<string, List<string>> otherMissingFileCandidates = new Dictionary<string, List<string>>();
            foreach (Tuple<string, string> matchingPath in matchingRelativePathFileNameList)
            {
                List<string> orphanMissingFiles = new List<string>();
                foreach (string otherMissingFile in otherMissingFiles)
                {
                    // Its a potential candidate if its not already referenced but it exists in that relative path folder
                    if (false == this.DataHandler.FileDatabase.ExistsRelativePathAndFileInDataTable(matchingPath.Item1, otherMissingFile)
                        && File.Exists(FilesFolders.GetFullPath(FolderPath, matchingPath.Item1, otherMissingFile)))
                    {
                        orphanMissingFiles.Add(otherMissingFile);
                    }
                }
                otherMissingFileCandidates.Add(matchingPath.Item1, orphanMissingFiles);
            }

            Dialog.MissingImageLocateRelativePaths dialog = new Dialog.MissingImageLocateRelativePaths(this, this.DataHandler.FileDatabase, currentImage.RelativePath, currentImage.File, otherMissingFileCandidates);

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                Tuple<string, string> locatedMissingFile = dialog.LocatedMissingFile;
                if (String.IsNullOrEmpty(locatedMissingFile.Item2))
                {
                    return;
                }

                // Update the original missing file
                List<ColumnTuplesWithWhere> columnTuplesWithWhereList = new List<ColumnTuplesWithWhere>();
                ColumnTuplesWithWhere columnTuplesWithWhere = new ColumnTuplesWithWhere();
                columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, locatedMissingFile.Item1)); // The new relative path
                columnTuplesWithWhere.SetWhere(currentImage.RelativePath, currentImage.File); // Where the original relative path/file terms are met
                columnTuplesWithWhereList.Add(columnTuplesWithWhere);

                // Update the other missing files in the database, if any
                foreach (string otherMissingFileName in otherMissingFileCandidates[locatedMissingFile.Item1])
                {
                    columnTuplesWithWhere = new ColumnTuplesWithWhere();
                    columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, locatedMissingFile.Item1)); // The new value
                    columnTuplesWithWhere.SetWhere(currentImage.RelativePath, otherMissingFileName); // Where the original relative path/file terms are met
                    columnTuplesWithWhereList.Add(columnTuplesWithWhere);
                }

                // Now update the database
                this.DataHandler.FileDatabase.UpdateFiles(columnTuplesWithWhereList);
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
            }
        }

        // Find missing folders
        private async void MenuItemEditFindMissingFolder_Click(object sender, RoutedEventArgs e)
        {
            bool? result = TimelapseWindow.GetAndCorrectForMissingFolders(this, this.DataHandler.FileDatabase);
            if (true == result)
            {
                await this.FilesSelectAndShowAsync().ConfigureAwait(true);
                return;
            }
            if (result != null && false == result.Value)
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
            if (Dialogs.MaybePromptToApplyOperationOnSelectionDialog(this, this.DataHandler.FileDatabase, this.State.SuppressSelectedDarkThresholdPrompt,
                                                                           "'Populate a field with Dark classification data...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.State.SuppressSelectedDarkThresholdPrompt = optOut; // SG TODO
                                                               }))
            {
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this, this.DataHandler.FileDatabase, this.State, this.DataHandler.ImageCache.CurrentRow))
                {
                    darkThreshold.Owner = this;
                    if (darkThreshold.ShowDialog() == true)
                    {
                        // Force an update of the current image in case the current values have changed
                        this.FileShow(this.DataHandler.ImageCache.CurrentRow, true);
                    }
                }
            }
        }
        #endregion

        #region Edit notes for this image set
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.DataHandler.FileDatabase.ImageSet.Log, this)
            {
                Owner = this
            };
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                this.DataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase();
            }
        }
        #endregion

        #region Restore default values for this file
        private void MenuRestoreDefaults_Opened(object sender, RoutedEventArgs e)
        {
            // Customize the text to whether full view or overview is being displayed
            if (sender is ContextMenu menu && menu.Items[0] is MenuItem menuItem)
            {
                if (this.MarkableCanvas.ThumbnailGrid.IsVisible == false && this.MarkableCanvas.ThumbnailGrid.IsGridActive == false)
                {
                    menuItem.Header = "Restore default values for this file";
                    menuItem.ToolTip = "For the currently displayed file, revert all fields to its default values (excepting file paths and dates/times)";
                }
                else
                {
                    menuItem.Header = "Restore default values for the checkmarked files";
                    menuItem.ToolTip = "For all checkmarked files, revert their fields to their default values (excepting file paths and dates/times)";
                }
            };
        }

        // Need to 
        //-disable the menu when nothing is in it 
        //-handle overview
        //-put in edit menu
        private void MenuItemRestoreDefaultValues_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the controls
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (control.DataLabel == Constant.DatabaseColumn.File || control.DataLabel == Constant.DatabaseColumn.RelativePath ||
                    control.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    // Ignore stock controls
                    continue;
                }
                ControlRow imageDatabaseControl = this.templateDatabase.GetControlFromTemplateTable(control.DataLabel);
                if (this.MarkableCanvas.ThumbnailGrid.IsVisible == false && this.MarkableCanvas.ThumbnailGrid.IsGridActive == false)
                {
                    // Only a single image is displayed: update the database for the current row with the control's value
                    this.DataHandler.FileDatabase.UpdateFile(this.DataHandler.ImageCache.Current.ID, control.DataLabel, imageDatabaseControl.DefaultValue);
                    // System.Diagnostics.Debug.Print(control.DataLabel + ":" + control.Content + ":" + imageDatabaseControl.DefaultValue);
                }
                else
                {
                    // Multiple images are displayed: update the database for all selected rows with the control's value
                    this.DataHandler.FileDatabase.UpdateFiles(this.MarkableCanvas.ThumbnailGrid.GetSelected(), control.DataLabel, imageDatabaseControl.DefaultValue);
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
