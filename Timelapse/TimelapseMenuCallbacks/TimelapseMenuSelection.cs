using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;
using MessageBox = Timelapse.Dialog.MessageBox;

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

            MenuItemSelectMissingFiles.IsChecked = selection == FileSelectionEnum.Missing;
            MenuItemSelectFilesMarkedForDeletion.IsChecked = selection == FileSelectionEnum.MarkedForDeletion;
            MenuItemSelectCustomSelection.IsChecked = selection == FileSelectionEnum.Custom;
            MenuItemSelectRandomSample.IsEnabled = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 2;
        }
        #endregion

        #region Select callback: All file, Missing, Marked for Deletion
        private async void MenuItemSelectFiles_Click(object sender, RoutedEventArgs e)
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
                // MenuItemSelectByFolder and its child folders should not be activated from here, 
                // but we add this test just as a reminder that we haven't forgotten it
                return;
            }
            else
            {
                selection = FileSelectionEnum.All;   // Just in case, this is the fallback operation
            }


            // Clear all the checkmarks from the Folder menu
            // But, not sure where we Treat the other menu checked status as a radio button i.e., we would want to toggle their states so only the clicked menu item is checked. 
            MenuItemSelectByRelativePath_ClearAllCheckmarks();

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

        #region Select by Folder Submenu(including submenu opening)
        private void MenuItemSelectByFolder_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything if the folder menu item list, except set its checkmark, if it has previously been populated
            if (!(sender is MenuItem menu))
            {
                // shouldn't happen
                return;
            }

            // Repopulate the menu if needed. 
            if (menu.Items.Count != 1)
            {
                // Gets the folders from the database, and created a menu item representing it
                MenuItemSelectByFolder_ResetFolderList();
            }
            // Set the checkmark to reflect the current search term for the relative path
            MenuItemFolderListSetCheckmark();
        }

        private void MenuItemFolderListSetCheckmark()
        {
            SearchTerm relativePathSearchTerm = DataHandler?.FileDatabase?.CustomSelection?.SearchTerms.First(term => term.DataLabel == DatabaseColumn.RelativePath);
            if (relativePathSearchTerm == null)
            {
                return;
            }

            foreach (MenuItem menuItem in MenuItemSelectByRelativePath.Items)
            {
                menuItem.IsChecked = relativePathSearchTerm.UseForSearching && String.Equals((string)menuItem.Header, relativePathSearchTerm.DatabaseValue);
            }
        }

        // Populate the menu. Get the folders from the database, and create a menu item representing it
        private void MenuItemSelectByFolder_ResetFolderList()
        {

            // Clear the list, excepting the first menu item all folders, which should be kept.
            MenuItem item = (MenuItem)MenuItemSelectByRelativePath.Items[0];
            MenuItemSelectByRelativePath.Items.Clear();
            MenuItemSelectByRelativePath.Items.Add(item);

            // Populate the menu . Get the folders from the database, and create a menu item representing it
            int i = 1;
            // PERFORMANCE. THIS introduces a delay when there are a large number of files. It is invoked when the user loads images for the first time. 
            // PROGRESSBAR - at the very least, show a progress bar if needed.

            List<string> folderList = DataHandler.FileDatabase.GetFoldersFromRelativePaths();//this.DataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            foreach (string header in folderList)
            {
                if (string.IsNullOrEmpty(header))
                {
                    // An empty header is actually the root folder. Since we already have an entry representng all files, we don't need it.
                    continue;
                }

                // Add the folder to the menu only if it isn't constrained by the relative path arguments
                if (Arguments.ConstrainToRelativePath && !(header == Arguments.RelativePath || header.StartsWith(Arguments.RelativePath + @"\")))
                {
                    continue;
                }
                // Create a menu item for each folder
                MenuItem menuitemFolder = new MenuItem
                {
                    Header = header,
                    IsCheckable = true,
                    ToolTip = "Show only files in the folder (including its own sub-folders): " + header
                };
                menuitemFolder.Click += MenuItemSelectFolder_Click;
                MenuItemSelectByRelativePath.Items.Insert(i++, menuitemFolder);
            }
        }


        // A specific folder was selected.
        private async void MenuItemSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mi))
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
            if (mi == MenuItemSelectAllFolders)
            {
                // its all folders, so just select all folders
                await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.All).ConfigureAwait(true);
                return;
            }

            // Set and only use the relative path as a search term
            DataHandler.FileDatabase.CustomSelection.ClearCustomSearchUses();
            DataHandler.FileDatabase.CustomSelection.SetAndUseRelativePathSearchTerm((string)mi.Header);

            int count = DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            if (count <= 0)
            {
                MessageBox messageBox = new MessageBox("No files in this folder", Application.Current.MainWindow)
                {
                    Message =
                    {
                         Icon = MessageBoxImage.Exclamation,
                         Reason = $"While the folder {mi.Header} exists, no image data is associated with any files in it.",
                         Hint = "Perhaps you removed these files and its data during this session?"
                    }
                };
                messageBox.ShowDialog();
            }
            MenuItemSelectByRelativePath_ClearAllCheckmarks();
            MenuItemSelectByRelativePath.IsChecked = true;
            mi.IsChecked = true;
            await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
            //await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.Custom).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
        }

        private void MenuItemSelectByRelativePath_ClearAllCheckmarks()
        {
            MenuItemSelectByRelativePath.IsChecked = false;
            foreach (MenuItem mi in MenuItemSelectByRelativePath.Items)
            {
                mi.IsChecked = false;
            }
        }
        #endregion

        #region Custom Selection: raises a dialog letting the user specify their selection criteria
        private async void MenuItemSelectCustomSelection_Click(object sender, RoutedEventArgs e)
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

            // show the dialog and process the resuls
            CustomSelectionWithEpisodes customSelection = new CustomSelectionWithEpisodes(DataHandler.FileDatabase, DataEntryControls, this, DataHandler.FileDatabase.CustomSelection.DetectionSelections, DataHandler.ImageCache.Current)
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
                await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.Custom).ConfigureAwait(true);
                if (MenuItemSelectCustomSelection.IsChecked || MenuItemSelectCustomSelection.IsChecked)
                {
                    MenuItemSelectByRelativePath_ClearAllCheckmarks();
                }
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
            }
        }
        #endregion

        #region Refresh the Selection
        // Refresh the selection: based on the current select criteria. 
        // Useful when, for example, the user has selected a view, but then changed some data values where items no longer match the current selection.
        private async void MenuItemSelectReselect_Click(object sender, RoutedEventArgs e)
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
            MenuItemSelectAllFiles.IsChecked = false;
            int currentSelectionCount = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;
            RandomSampleSelection customSelection = new RandomSampleSelection(this, currentSelectionCount);
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
            }

        }
        #endregion
    }
}
