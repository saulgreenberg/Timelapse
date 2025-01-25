using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {
        private ControlsDataEntry.DataEntryHandler DataHandler;
        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            DataHandler = GlobalReferences.MainWindow.DataHandler;
            RepopulateIfNeeded();
            //this.MenuItemSetSearchTerm();
        }

        private void MenuItemSetSearchTerm()
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
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
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

            this.DropDownTreeViewButton.Content = TreeViewWithRelativePaths.SelectedPath;
            //// If its select all folders, then 
            //if (TreeViewWithRelativePaths.SelectedPath == "All files")
            //{
            //    // its all folders, so just select all folders
            //    await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.All).ConfigureAwait(true);
            //    return;
            //}

            //// Set and only use the relative path as a search term
            //DataHandler.FileDatabase.CustomSelection.ClearCustomSearchUses();
            //DataHandler.FileDatabase.CustomSelection.SetAndUseRelativePathSearchTerm(TreeViewWithRelativePaths.SelectedPath);

            //int count = DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            //if (count <= 0)
            //{
            //    MessageBox messageBox = new MessageBox("No files in this folder", Application.Current.MainWindow)
            //    {
            //        Message =
            //        {
            //             Icon = MessageBoxImage.Exclamation,
            //             Reason = $"While the folder {TreeViewWithRelativePaths.SelectedPath} exists, no image data is associated with any files in it.",
            //             Hint = "Perhaps you removed these files and its data during this session?"
            //        }
            //    };
            //    messageBox.ShowDialog();
            //}
            //await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set

        }

        private void Header_OnClick(object sender, RoutedEventArgs e)
        {
            RepopulateIfNeeded();
        }

        private void RepopulateIfNeeded()
        {
            this.MenuItemSetSearchTerm();

            // Repopulate the treeview if needed.
            if (this.tv.Items.Count == 0)
            {
                this.MenuItemSelectByFolder_GetRelativePathsTreeView();
            }
        }

        // Populate the menu. Get the folders from the database, and create a menu item representing it
        private void MenuItemSelectByFolder_GetRelativePathsTreeView()
        {
            // Add the current folders in the database to the treeview
            this.MenuItemSelectByFolderTreeView_ResetFolderList();
        }
        private void MenuItemSelectByFolderTreeView_ResetFolderList()
        {
            // Get the folders from the database
            // PERFORMANCE. This can introduce a delay when there are a large number of files. It is invoked when the user loads images for the first time. 
            List<string> folderList = DataHandler.FileDatabase.GetFoldersFromRelativePaths();//this.DataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            foreach (string header in folderList)
            {
                if (string.IsNullOrEmpty(header))
                {
                    // An empty header is actually the root folder. Since we already have an entry representng all files, we don't need it.
                    continue;
                }

                // Add the folder to the menu only if it isn't constrained by the relative path arguments
                //TODO TODO TODO
                //if (Arguments.ConstrainToRelativePath && !(header == Arguments.RelativePath || header.StartsWith(Arguments.RelativePath + @"\")))
                //{
                //    continue;
                //}
            }
            this.tv.DontInvoke = true;
            this.tv.SetTreeViewContentsToRelativePathList(folderList);
            this.tv.DontInvoke = false;
        }
    }
}
