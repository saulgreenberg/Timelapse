using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Images;
using Timelapse.Util;
using File = Timelapse.Constant.File;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the images (and possibly the data) of images rows as specified in the deletedImageTable
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DeleteImages
    {
        #region Private Variables
        // these variables will hold the values of the passed in parameters
        private readonly FileDatabase fileDatabase;
        private readonly ImageCache imageCache;
        private readonly List<ImageRow> filesToDelete;
        private readonly bool deleteImage;
        private readonly bool deleteData;
        private readonly bool deleteCurrentImageOnly;
        private readonly bool backupDeletedFiles;

        private bool IsAnyDataUpdated;
        private int maxPathLength = 60;
        #endregion

        #region Constructor, Loaded
        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// -deleteData is true when the data associated with that image should be deleted.
        /// -useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DeleteImages(Window owner, FileDatabase fileDatabase, ImageCache imageCache, List<ImageRow> filesToDelete, bool deleteImage, bool deleteData, bool deleteCurrentImageOnly, bool backupDeletedFiles) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(imageCache, nameof(imageCache));
            ThrowIf.IsNullArgument(filesToDelete, nameof(filesToDelete));

            InitializeComponent();

            this.fileDatabase = fileDatabase;
            this.imageCache = imageCache;
            this.filesToDelete = filesToDelete;
            this.backupDeletedFiles = backupDeletedFiles;

            this.deleteImage = deleteImage;
            this.deleteData = deleteData;
            this.deleteCurrentImageOnly = deleteCurrentImageOnly;

            // Tracks whether any changes to the data or database are made
            IsAnyDataUpdated = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            Mouse.OverrideCursor = Cursors.Wait;

            // Construct the interface for either a single deletion, or for multiple deletions
            if (deleteCurrentImageOnly)
            {
                DeleteCurrentImageOnly();
            }
            else
            {
                DeleteMultipleImages();
            }

            this.Message.BuildContentFromProperties();

            // Depending upon what is being deleted,
            // set the visibility and enablement of various controls
            if (deleteData || backupDeletedFiles == false)
            {
                // Set the UI to require a further confirmation step to delete the data, 
                StartDoneButton.IsEnabled = false;
                chkboxConfirm.Visibility = Visibility.Visible;
            }
            else
            {
                StartDoneButton.IsEnabled = true;
                chkboxConfirm.Visibility = Visibility.Collapsed;
            }

            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Build Initial Dialog Interfaces
        private void DeleteCurrentImageOnly()
        {
            // The single file to delete
            ImageRow imageRow = filesToDelete[0];


            // Show  the deleted file name and image in the interface
            ShowSingleFileView();
            maxPathLength = 70;
            string filePath = Path.Combine(imageRow.RelativePath, imageRow.File);
            if (string.IsNullOrEmpty(filePath) == false)
            {
                filePath = filePath.Length <= maxPathLength ? filePath : "..." + filePath.Substring(filePath.Length - maxPathLength, maxPathLength);
            }

            SingleImageViewer.Source = imageRow.LoadBitmap(fileDatabase.RootPathToImages, ImageValues.PreviewWidth480, out _);
            SingleFilePanel.ToolTip = Path.Combine(imageRow.RelativePath, imageRow.File);
            SingleImageViewer.ToolTip = Path.Combine(imageRow.RelativePath, imageRow.File);
            SingleFileNameRun.Text = filePath;

            // Populate the information pane
            string imageOrVideo = filesToDelete[0].IsVideo ? "video" : "image";

            Message.Hint = $"[li] Select [e]Options|Preferences[/e] to manage deletion behavior, including:" +
                           $"[li 2] whether deleted files are actually backed up to this image set's [i]{File.DeletedFilesFolder}[/i] folder," +
                           $"[li 2] if backed up, when the [i]{File.DeletedFilesFolder}[/i] is emptied." +
                            "[li 2] alternately, whether files are immediately and permanently deleted.";

            if (deleteData && !deleteImage)
            {
                ConfirmCheckBoxText.Text = "Click to confirm permanent deletion of data for the selected file";
                ConfirmCheckBoxTextHint.Text = "(Deleted data is not recoverable)";
            }
            else if (deleteData & deleteImage && !backupDeletedFiles)
            {
                ConfirmCheckBoxText.Text = "Click to confirm permanent deletion of the selected file and its data";
                ConfirmCheckBoxTextHint.Text = "(Deleted data and permanently deleted files are not recoverable)";
            }
            else if (!deleteData & deleteImage && !backupDeletedFiles)
            {
                ConfirmCheckBoxText.Text = "Click to confirm permanent deletion of the selected file";
                ConfirmCheckBoxTextHint.Text = "(Permanently deleted files are not recoverable)";
            }

            if (deleteImage)
            {
                if (this.backupDeletedFiles)
                {
                    Message.DialogTitle = $"Delete the current {imageOrVideo}";
                    Message.What = $"Delete the current {imageOrVideo} if it exists.";
                    Message.Result = $"[li] The deleted {imageOrVideo} will be backed up in a sub-folder named [i]{File.DeletedFilesFolder}[/i].";
                }
                else
                {
                    Message.DialogTitle = $"Permanently delete the current {imageOrVideo}";
                    Message.What = $"Permanently delete the current {imageOrVideo} if it exists.";
                    Message.Result = $"[li] The deleted {imageOrVideo} will be permanently deleted.";
                }


                if (deleteImage && deleteData == false)
                {
                    // Case 1: Delete the current image, but not its data.

                    Message.DialogTitle += " but not its data";
                    Message.What +=
                        $"[br]Don't delete the data associated with this {imageOrVideo}.";
                    Message.Result += $"[li] A placeholder {imageOrVideo} (and its data) will be shown when you try to view it.";
                    if (this.backupDeletedFiles)
                    {
                        Message.Hint += $"[li] You can restore the backed up deleted {imageOrVideo} by moving it from [i]{File.DeletedFilesFolder}[/i] back to its original location.";
                    }
                }
                else if (deleteImage && deleteData)
                {
                    // Case 2: Delete the current image and its data
                    Message.DialogTitle += " and its data";
                    Message.What +=
                        $"[br]Permanently delete the data associated with this {imageOrVideo}.";
                    Message.Result +=
                        $"[li] The [b]data[/b] associated with this {imageOrVideo} is [b]permanently deleted[/b].";
                }
            }
            else
            {
                // Case: Delete the data only, leaving the image intact
                FileLabel.Text = "Affected file:";

                Message.DialogTitle = $"Delete only the current {imageOrVideo}'s data";
                Message.What = $"Delete the data associated with the current {imageOrVideo}.[br]Don't delete the {imageOrVideo}.";
                Message.Result = $"[li] This data record will be removed.";
                Message.Result +=
                    $"[li] The {imageOrVideo} is still in its original location, but Timelapse will not display it unless a duplicate record exists..";
                Message.Hint += $"[li] Deleting only the data is useful for removing an unwanted duplicate record of a file.";
            }
        }

        // Deleting multiple images - set up the UI
        private void DeleteMultipleImages()
        {
            int maxFilesToShow = 50000;
            int numberOfImagesToDelete = filesToDelete.Count;

            // Load the files that are candidates for deletion as listbox items
            ShowMultipleFilesView();
            DeletedFilesListBox.Items.Clear();
            maxPathLength = 100;

            if (filesToDelete.Count < maxFilesToShow)
            {
                foreach (ImageRow imageProperties in filesToDelete)
                {

                    string filePath = Path.Combine(imageProperties.RelativePath, imageProperties.File);
                    if (string.IsNullOrEmpty(filePath) == false)
                    {
                        filePath = filePath.Length <= maxPathLength ? filePath : "..." + filePath.Substring(filePath.Length - maxPathLength, maxPathLength);
                    }

                    ListBoxItem lbi = new()
                    {
                        VerticalAlignment = VerticalAlignment.Top,
                        Height = 28,
                        Content = filePath,
                        Tag = imageProperties
                    };
                    lbi.MouseEnter += Lbi_MouseEnter;
                    lbi.MouseLeave += Lbi_MouseLeave;
                    DeletedFilesListBox.Items.Add(lbi);
                }
            }
            else
            {
                ListBoxItem lbi = new()
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    Height = 28,
                    Content = $"Items marked for deletion are not listed as there are a very large number of them ({numberOfImagesToDelete}).",
                };
                DeletedFilesListBox.FontStyle = FontStyles.Italic;
                DeletedFilesListBox.FontWeight = FontWeights.Bold;
                DeletedFilesListBox.Items.Add(lbi);
            }

            if (deleteData && !deleteImage)
            {
                ConfirmCheckBoxText.Text = "Click to confirm permanent deletion of data for the selected file";
                ConfirmCheckBoxTextHint.Text = "(Deleted data is not recoverable)";
            }
            else if (deleteData & deleteImage && !backupDeletedFiles)
            {
                ConfirmCheckBoxText.Text = "Click to confirm permanent deletion of the selected file and its data";
                ConfirmCheckBoxTextHint.Text = "(Deleted data and permanently deleted files are not recoverable)";
            }
            else if (!deleteData & deleteImage && !backupDeletedFiles)
            {
                ConfirmCheckBoxText.Text = "Click to confirm permanent deletion of the selected file";
                ConfirmCheckBoxTextHint.Text = "(Permanently deleted files are not recoverable)";
            }

            Message.Hint = $"[li] Select [e]Options|Preferences[/e] to manage deletion behavior, including:" +
                           $"[li 2] whether deleted files are actually backed up to this image set's [i]{File.DeletedFilesFolder}[/i] folder," +
                           $"[li 2] if backed up, when the [i]{File.DeletedFilesFolder}[/i] is emptied." +
                            "[li 2] alternately, whether files are immediately and permanently deleted.";

            // Populate the information pane
            if (deleteImage)
            {
                if (this.backupDeletedFiles)
                {
                    Message.DialogTitle = $"Delete {numberOfImagesToDelete} files ";
                    Message.What = $"Delete {numberOfImagesToDelete} image and/or video(s) marked for deletion.";
                    Message.Result = $"[li] The deleted file will be backed up in a sub-folder named [i]{File.DeletedFilesFolder}[/i].";
                }
                else
                {
                    Message.DialogTitle = $"Permanently delete {numberOfImagesToDelete} files(s) ";
                    Message.What = $"Permanently delete {numberOfImagesToDelete} image and/or video(s) marked for deletion.";
                    Message.Result = "[li] The deleted files will be permanently deleted";
                }

                if (deleteData == false)
                {
                    // Case : Delete the images that have the delete flag set, but not their data
                    Message.DialogTitle += "but not their data";
                    Message.What += $"[br]Don't delete the data associated with these files.";
                    Message.Result += $"[li] A placeholder image (and its data) will be shown when you try to view a deleted file.";
                    if (this.backupDeletedFiles)
                    {
                        Message.Hint += $"[li] You can restore these deleted files by moving them from [i]{File.DeletedFilesFolder}[/i] back to its original location.";
                    }
                }
                else // (deleteData == true)
                {
                    // Case : Delete the images that have the delete flag set, and their data
                    Message.DialogTitle += "and their data";
                    Message.What += $"[br]Permanently delete the data associated with these files.";
                    Message.Result += $"[li] [Data is permanently deleted.";


                }
                if (numberOfImagesToDelete > ImageValues.LargeNumberOfDeletedImages)
                {
                    Message.Result +=
                        $"[li] Deleting {numberOfImagesToDelete} files takes time. Please be patient.";
                }
            }
            else
            {
                // Case: Delete the data only, leaving the image intact
                //this.FileLabel.Text = "Affected file:";
                ConfirmCheckBoxText.Text =
                    $"Click to confirm deletion of data for the {numberOfImagesToDelete} selected files";
                Message.DialogTitle = $"Delete only the data for {numberOfImagesToDelete} files(s) ";
                Message.What =
                    $"Delete the data associated with {numberOfImagesToDelete} file(s)[br]Don't delete the files.";
                Message.Result =
                    $"[li]  {numberOfImagesToDelete} data records will be permanently deleted.";
                Message.Result +=
                    $"[li] The {numberOfImagesToDelete} file(s) are still in their original location, but Timelapse will not display them unless a duplicate record exists.";
                Message.Hint += $"[li] Deleting only the data  is useful for removing unwanted duplicate records of a file.";
            }
        }
        #endregion

        #region Do the actual file deletion
        // The (bool, int return value: true if the operation has been cancelled, and if so how many images were deleted before the cancel event
        private async Task<Tuple<bool, int>> DoDeleteFilesAsync(List<ImageRow> imagesToDelete, bool deleteFiles, bool deleteTheData)
        {
            List<ColumnTuplesWithWhere> imagesToUpdate = [];
            List<long> imageIDsToDropFromDatabase = [];
            int fileIndex = 0;
            int filesDeleted = 0;
            bool isCancelled = false;

            await Task.Run(() =>
            {
                int count = imagesToDelete.Count;
                foreach (ImageRow image in imagesToDelete)
                {
                    if (Token.IsCancellationRequested)
                    {
                        isCancelled = true;
                        //  Since some file or its data may have been deleted, we want to set this to true in order to tell the calling method refresh everything.
                        IsAnyDataUpdated = true;
                        return;
                        //return new Tuple<bool,int>(true, filesDeleted);
                    }

                    if (deleteFiles)
                    {
                        // We need to release the file handle to various images as otherwise we won't be able to move them
                        // Release the image cache for this ID, if its actually in the cache  
                        imageCache.TryInvalidate(image.ID);
                        GC.Collect(); // See if this actually gets rid of the pointer to the image, as otherwise we get occassional exceptions
                                      // SAULXXX Note that we should likely pop up a dialog box that displays non-missing files that we can't (for whatever reason) delete
                                      // SAULXXX If we can't delete it, we may want to abort changing the various DeleteFlag 
                                      // SAULXXX A good way is to put an 'image.ImageFileExists' field in, and then do various tests on that.
                        if (image.TryMoveFileToDeletedFilesFolder(fileDatabase.RootPathToImages, backupDeletedFiles))
                        {
                            // keep track of the number of files actually delted
                            filesDeleted++;
                        }
                    }
                    if (deleteTheData)
                    {
                        // mark the image row for dropping
                        imageIDsToDropFromDatabase.Add(image.ID);
                    }
                    else
                    {
                        // as only the file was deleted, clear the delete flag
                        image.DeleteFlag = false;
                        List<ColumnTuple> columnTuples =
                        [
                            new(DatabaseColumn.DeleteFlag, BooleanValue.False)
                        ];
                        imagesToUpdate.Add(new(columnTuples, image.ID));
                    }
                    fileIndex++;
                    if (ReadyToRefresh())
                    {
                        int percentDone = Convert.ToInt32(fileIndex / Convert.ToDouble(count) * 100.0);
                        Progress.Report(new(percentDone,
                            $"Pass 1: Deleting {fileIndex} / {count} files", true, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);
                    }
                }
                Progress.Report(new(100, $"Pass 2: Updating {count} files. Please wait...", false, true));
                Thread.Sleep(ThrottleValues.RenderingBackoffTime);

                if (deleteTheData)
                {
                    // drop images
                    fileDatabase.DeleteFilesAndMarkers(imageIDsToDropFromDatabase);

                    // Vacuum the database to reclaim the space used by the deleted rows
                    fileDatabase.Database.Vacuum();
                }
                else
                {
                    // update image properties
                    fileDatabase.UpdateFiles(imagesToUpdate);
                }
                // A side effect of running this task is that the FileTable will be updated, which means that,
                // at the very least, the calling function will need to run FilesSelectAndShow to either
                // reload the FileTable with the updated data, or to reset the FileTable back to its original form
                // if the operation was cancelled.
                IsAnyDataUpdated = true;
            }).ConfigureAwait(true);

            return new(isCancelled, filesDeleted);
        }
        #endregion

        #region Listbox callbacks to display file image in thumbnails
        // When the user enters a listbox item, show the image
        private void Lbi_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!(sender is ListBoxItem lbi))
            {
                return;
            }
            ImageRow ir = (ImageRow)lbi.Tag;
            Image image = new()
            {
                Source = ir.LoadBitmap(fileDatabase.RootPathToImages, ImageValues.PreviewWidth384, out _),
                Height = 300,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            lbi.ToolTip = image;
        }

        // When the user leaves a listbox item, remove the image
        private void Lbi_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!(sender is ListBoxItem lbi))
            {
                return;
            }
            lbi.ToolTip = null;
        }
        #endregion

        #region Button callbacks
        // Set the confirm checkbox, which enables the ok button if the data deletions are confirmed. 
        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            StartDoneButton.IsEnabled = chkboxConfirm.IsChecked == true;
        }

        // Cancel button selected
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Ok button selected
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await StartButton_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task StartButton_ClickAsync()
        {
            // ConfigureFormatForDateTimeCustom the UI's initial state
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            StartDoneButton.Content = "_Done";
            StartDoneButton.Click -= StartButton_Click;
            StartDoneButton.Click += DoneButton_Click;
            StartDoneButton.IsEnabled = false;
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            Tuple<bool, int> isCancelledAndDeletedImagesCount = await DoDeleteFilesAsync(filesToDelete, deleteImage, deleteData).ConfigureAwait(true);

            // Hide the busy indicator and update the UI, e.g., to show how many files were deleted
            BusyCancelIndicator.IsBusy = false;
            StartDoneButton.IsEnabled = true;
            WindowCloseButtonIsEnabled(true);

            if (deleteData && deleteImage == false)
            {
                DoneMessagePanel.Content =
                    $"Only the data was deleted for {filesToDelete.Count} files.{Environment.NewLine}The actual files are left intact in their folders";
            }
            else if (isCancelledAndDeletedImagesCount.Item1 == false)
            {
                DoneMessagePanel.Content = "Deleted ";
                DoneMessagePanel.Content += filesToDelete.Count == 1 ? filesToDelete[0].File : isCancelledAndDeletedImagesCount.Item2 + " files";
            }
            else
            {
                DoneMessagePanel.Content = "Cancelled, but ";
                DoneMessagePanel.Content += filesToDelete.Count == 1 ? filesToDelete[0].File : isCancelledAndDeletedImagesCount.Item2 + " files were already deleted." + Environment.NewLine;
                DoneMessagePanel.Content += "Deleted files are available in your Deleted folder." + Environment.NewLine;
                DoneMessagePanel.Content += "Data for these deleted images has not been changed." + Environment.NewLine;
            }
            ShowDoneMessageView();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            // We return false if the database was not altered, i.e., if this was all a no-op
            DialogResult = IsAnyDataUpdated;
        }
        #endregion

        #region Helper methods to show/hide various UI panels
        private void ShowSingleFileView()
        {
            SingleFilePanel.Visibility = Visibility.Visible;
            MultipleFilePanel.Visibility = Visibility.Collapsed;
            DoneMessagePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowMultipleFilesView()
        {
            SingleFilePanel.Visibility = Visibility.Collapsed;
            MultipleFilePanel.Visibility = Visibility.Visible;
            DoneMessagePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowDoneMessageView()
        {
            SingleFilePanel.Visibility = Visibility.Collapsed;
            MultipleFilePanel.Visibility = Visibility.Collapsed;
            DoneMessagePanel.Visibility = Visibility.Visible;
            chkboxConfirm.Visibility = Visibility.Collapsed;
        }
        #endregion
    }
}
