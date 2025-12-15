using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Select Menu Callbacks - runs database queries that creates a subset of images to display
    public partial class TimelapseWindow
    {
        # region Select sub-menu opening
        private void MenuItemSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going

            MenuItemSelectMissingFiles.IsEnabled = true;

            // Enable menu if there are any files marked for deletion
            bool exists = DataHandler.FileDatabase.ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum.MarkedForDeletion);
            MenuItemSelectFilesMarkedForDeletion.Header = "All files marked for d_eletion";
            if (!exists)
            {
                MenuItemSelectFilesMarkedForDeletion.Header += " (0)";
            }
            MenuItemSelectFilesMarkedForDeletion.IsEnabled = exists;

            // Put a checkmark next to the menu item that matches the stored selection criteria
            FileSelectionEnum selection = DataHandler.FileDatabase.FileSelectionEnum;

            MenuItemSelectAllFiles.IsChecked = selection == FileSelectionEnum.All;

            MenuItemSelectByRelativePath.IsChecked = selection == FileSelectionEnum.Folders;
            MenuItemSelectByRelativePath.IsEnabled = this.tv.HasContent;

            MenuItemSelectMissingFiles.IsChecked = selection == FileSelectionEnum.Missing;
            MenuItemSelectFilesMarkedForDeletion.IsChecked = selection == FileSelectionEnum.MarkedForDeletion;
            MenuItemSelectCustomSelection.IsChecked = selection == FileSelectionEnum.Custom;

            // Random sampling is active only if it isn't the currently active selection. It is also inactive if the EpisodeShowAll selection is true (as it is somewhat nonsensical to do that with a random selection)
            MenuItemSelectRandomSample.IsEnabled = MenuItemSelectRandomSample.IsEnabled &&  false == DataHandler?.FileDatabase?.CustomSelection.EpisodeShowAllIfAnyMatch && DataHandler?.FileDatabase?.CountAllCurrentlySelectedFiles > 2;

            this.MenuItemSetRelativePathSearchTerm();
        }
        #endregion

        #region Select callback: All file, Missing, Marked for Deletion
        private async void MenuItemSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSelectFiles_ClickAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSelectFiles_ClickAsync(object sender)
        {
            MenuItem item = (MenuItem)sender;
            FileSelectionEnum selection;
            FileSelectionEnum oldSelection = DataHandler.FileDatabase.FileSelectionEnum;

            // Any selections below will not trigger a Select with recognitions
            // Consequently, set the bounding box override to 1 i.e., so the over-ride has no effect in the display (if any) of bounding boxes.
            GlobalReferences.TimelapseState.BoundingBoxThresholdOveride = 1;

            // Set the selection enum to match the menu selection
            if (item == MenuItemSelectAllFiles)
            {
                selection = FileSelectionEnum.All;
            }
            else if (item == MenuItemSelectMissingFiles)
            {
                selection = FileSelectionEnum.Missing;
            }
            else if (item == MenuItemSelectFilesMarkedForDeletion)
            {
                selection = FileSelectionEnum.MarkedForDeletion;
            }
            else if (item == MenuItemSelectByRelativePath)
            {
                // MenuItemSelectByRelativePathTreeView and its child folders should not be activated from here,
                // but we add this test just as a reminder that we haven't forgotten it
                return;
            }
            else
            {
                selection = FileSelectionEnum.All;   // Just in case, this is the fallback operation
            }

            // Select and show the files according to the selection made
            if (DataHandler.ImageCache.Current == null)
            {
                // Go to the first result (i.e., index 0) in the given selection set
                await FilesSelectAndShowAsync(selection).ConfigureAwait(true);
            }
            else
            {
                if (false == await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, selection).ConfigureAwait(true))
                {
                    DataHandler.FileDatabase.FileSelectionEnum = oldSelection;
                }
            }
        }
        #endregion

        #region Select By Folder as TreeView
        private void MenuItemSelectByFolder_SubmenuOpening(object sender, RoutedEventArgs e)
        {

            this.MenuItemSetRelativePathSearchTerm();

            // Repopulate the treeview if needed.
            if (this.tv.Items.Count == 0)
            {
                this.MenuItemSelectByFolder_GetRelativePaths();
            }
        }

        private void MenuItemSetRelativePathSearchTerm()
        {
            SearchTerm relativePathSearchTerm = DataHandler?.FileDatabase?.CustomSelection?.SearchTerms.FirstOrDefault(term => term.DataLabel == DatabaseColumn.RelativePath);

            if (string.IsNullOrEmpty(relativePathSearchTerm?.DatabaseValue))
            {
                // Nothing relevant found so just collapse everything
                this.tv.SelectedPath = string.Empty;
                this.tv.FocusSelection = false;
                this.tv.UnselectAll();
                this.tv.CollapseAll();
                return;
            }

            if (false == relativePathSearchTerm.UseForSearching || DataHandler.FileDatabase.FileSelectionEnum != FileSelectionEnum.Folders)
            {
                // Expand the search term, but we don't want it focused
                this.tv.FocusSelection = false;
                this.tv.SelectedPath = relativePathSearchTerm.DatabaseValue;
                return;
            }
            this.tv.FocusSelection = true;
            this.tv.SelectedPath = relativePathSearchTerm.DatabaseValue;
        }

        private void MenuItemSelectByFolder_ResetFolderList()
        {
            // Get the folders from the database
            // PERFORMANCE. This can introduce a delay when there are a large number of files. It is invoked when the user loads images for the first time. 
            List<string> folderList = DataHandler.FileDatabase.GetFoldersFromRelativePaths();//this.DataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);

            if (this.Arguments.ConstrainToRelativePath)
            {
                // Special case.
                // If we are constrained to the relative path, create a new list that removes folders outside that relative path
                List<string> newFolderList = [];
                foreach (string relativePath in folderList)
                {
                    if (false == string.IsNullOrEmpty(relativePath) &&
                        (relativePath == this.Arguments.RelativePath || relativePath.StartsWith(this.Arguments.RelativePath + @"\")))
                    {
                        // An empty header is actually the root folder, which we don't need
                        // We also don't want any relative paths outside the desired one
                        // Add the folder to the menu only if it isn't constrained by the relative path arguments
                        newFolderList.Add(relativePath);
                    }
                }
                folderList = newFolderList;
            }

            this.tv.DontInvoke = true;
            this.tv.SetTreeViewContentsToRelativePathList(folderList);
            this.tv.DontInvoke = false;
        }
        // Populate the menu. Get the folders from the database, and create a menu item representing it
        private void MenuItemSelectByFolder_GetRelativePaths()
        {
            // Add the current folders in the database to the treeview
            this.MenuItemSelectByFolder_ResetFolderList();
        }
        private async void MenuItemSelectByFolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                await MenuItemSelectByFolderTreeView_SelectedItemChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSelectByFolderTreeView_SelectedItemChangedAsync(object sender)
        {
            if (this.tv.DontInvoke)
            {
                return;
            }
            if (!(sender is TreeViewWithRelativePaths TreeViewWithRelativePaths))
            {
                return;
            }

            if (DataHandler.ImageCache.Current == null)
            {
                //Shouldn't happen
                TracePrint.UnexpectedException(nameof(DataHandler.ImageCache.Current));
                return;
            }

            // If its select all folders, then
            if (TreeViewWithRelativePaths.SelectedPath == "All files")
            {
                // its all folders, so just select all folders
                await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.All).ConfigureAwait(true);
                return;
            }

            // Set and only use the relative path as a search term
            DataHandler.FileDatabase.CustomSelection.ClearCustomSearchUses();
            DataHandler.FileDatabase.CustomSelection.SetAndUseRelativePathSearchTerm(TreeViewWithRelativePaths.SelectedPath);

            int count = DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            if (count <= 0)
            {
                Dialogs.NoImageDataAssociatedWithFiles(Application.Current.MainWindow, "No files in this folder", TreeViewWithRelativePaths.SelectedPath);
                //MessageBox messageBox = new MessageBox("No files in this folder", Application.Current.MainWindow)
                //{
                //    Message =
                //    {
                //         Icon = MessageBoxImage.Exclamation,
                //         Reason = $"While the folder {TreeViewWithRelativePaths.SelectedPath} exists, no image data is associated with any files in it.",
                //         Hint = "Perhaps you removed these files and its data during this session?"
                //    }
                //};
                //messageBox.ShowDialog();
            }
            await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
        }
        #endregion

        #region Custom Selection: raises a dialog letting the user specify their selection criteria
        private async void MenuItemSelectCustomSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSelectCustomSelection_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSelectCustomSelection_ClickAsync()
        {
            // the first time the custom selection dialog is launched update the DateTime search terms to the time of the current image
            SearchTerm firstDateTimeSearchTerm = DataHandler.FileDatabase.CustomSelection.SearchTerms.FirstOrDefault(searchTerm => searchTerm.DataLabel == DatabaseColumn.DateTime);
            if (firstDateTimeSearchTerm == null)
            {
                // Shouldn't happen, as there should always be a datetime columne
                TracePrint.NullException(nameof(firstDateTimeSearchTerm));
                return;
            }
            if (firstDateTimeSearchTerm.GetDateTime() == ControlDefault.DateTimeDefaultValue)
            {
                DateTime defaultDate;
                if (DataHandler.ImageCache.Current == null)
                {
                    // Should't happen
                    TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                    defaultDate = DateTime.Now;
                }
                else
                {
                    defaultDate = DataHandler.ImageCache.Current.DateTime;
                }
                DataHandler.FileDatabase.CustomSelection.SetDateTimes(defaultDate);
            }

            // show the dialog and process the results
            // We save the custom selections, so we can restore them if the user cancels the dialog
            CustomSelection savedCustomSelection = Util.ObjectUtillities.DeepClone(DataHandler.FileDatabase.CustomSelection);
            CustomSelectionWithEpisodes customSelection = new(this, DataHandler.FileDatabase, DataEntryControls, DataHandler.ImageCache.Current, DataHandler.FileDatabase.CustomSelection.RecognitionSelections, this.Arguments)
            {
                Owner = this
            };
            bool? changeToCustomSelection = customSelection.ShowDialog();
            // Set the selection to show all images and a valid image
            if (changeToCustomSelection == true)
            {
                if (DataHandler.ImageCache.Current == null)
                {
                    // Shouldn't happen.
                    TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                    return;
                }
                await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, customSelection.FileSelection).ConfigureAwait(true);
            }
            else
            {
                // Since we canceled the custom selection, uncheck the item (but only if another menu item is shown checked)
                bool otherMenuItemIsChecked =
                    MenuItemSelectAllFiles.IsChecked ||
                    MenuItemSelectMissingFiles.IsChecked ||
                    MenuItemSelectByRelativePath.IsChecked ||
                    MenuItemSelectFilesMarkedForDeletion.IsChecked;
                MenuItemSelectCustomSelection.IsChecked = !otherMenuItemIsChecked;
                this.DataHandler.FileDatabase.CustomSelection = savedCustomSelection;
            }
        }
        #endregion

        #region Refresh the Selection
        // Refresh the selection: based on the current select criteria.
        // Useful when, for example, the user has selected a view, but then changed some data values where items no longer match the current selection.
        private async void MenuItemSelectReselect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSelectReselect_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSelectReselect_ClickAsync()
        {
            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }
            // Reselect the images, which re-sorts them to the current sort criteria.
            await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);
        }

        #endregion

        #region Select Random Sample
        private async void MenuItemSelectRandomSample_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSelectRandomSample_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSelectRandomSample_ClickAsync()
        {
            MenuItemSelectAllFiles.IsChecked = false;
            int currentSelectionCount = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;
            RandomSampleSelection customSelection = new(this, currentSelectionCount);
            bool? useRandomSample = customSelection.ShowDialog();
            MenuItemSelectAllFiles.IsChecked = false;
            if (true == useRandomSample)
            {
                if (DataHandler.ImageCache.Current == null)
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                    StatusBar.SetView("Something went wrong with random sample");
                    return;
                }
                DataHandler.FileDatabase.CustomSelection.RandomSample = customSelection.SampleSize;
                await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);
                DataHandler.FileDatabase.CustomSelection.RandomSample = 0;
                MenuItemSelectAllFiles.IsChecked = true;
                StatusBar.SetView("Random Sample of currently selected Files");

                // Disable the random sample selection until the next active selection
                this.MenuItemSelectRandomSample.IsEnabled = false;
            }
        }
        #endregion
    }
}
