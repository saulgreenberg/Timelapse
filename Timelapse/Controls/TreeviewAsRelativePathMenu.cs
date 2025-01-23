using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using Xceed.Wpf.Toolkit.Core.Converters;

namespace Timelapse.Controls
{
    public class TreeViewAsRelativePathMenu : TreeView
    {

        private ItemProvider ItemProvider { get; set; }
        private List<Item> ItemList { get; set; }
        public string SelectedPath { get; set; }
        public bool IgnoreSelection { get; set; } = false;

        #region Constructor
        public TreeViewAsRelativePathMenu()
        {
            this.SelectedItemChanged += TreeView_SelectedItemChanged;
            this.Loaded += TreeView_OnLoaded;
        }
        #endregion

        #region Public methods
        public void SetTreeViewContentsToRelativePathList(List<string> relativePaths)
        {
            this.ItemProvider = new ItemProvider(relativePaths);
            this.ItemList = ItemProvider.GetItems();
            DataContext = this.ItemList;
        }


        #endregion

        #region Callbacks
        // This is triggered whenever the treeview is created, even though it may not yet be displayed
        private void TreeView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.SelectedPath))
            {
                return;
            }

            this.IgnoreSelection = true;
            PathItem item = this.SearchTreeViewForPath(this.SelectedPath);
            CollapseAll(this);
            this.ShowSelectedItem(this, item);
            this.IgnoreSelection = false;
        }

        // Highlight the selected item
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is PathItem pathItem)
            {
                if (this.IgnoreSelection) return;
                this.SelectAndHighlightItem(pathItem);
            }
        }
        #endregion

        #region Private Search for Path, Select and Show Item
        private PathItem SearchTreeViewForPath(string path)
        {
            return this.ItemProvider.Search(this.ItemList, path);
        }

        // Select and highlight the pathItem if its not already selected
        private void SelectAndHighlightItem(PathItem pathItem)
        {
            if (null != pathItem && false == string.IsNullOrWhiteSpace(pathItem.Path))
            {
                if (this.SelectedPath == pathItem.Path)
                {
                    // Its already selected and thus highlit, so abort
                    return;
                }

                this.SelectedPath = pathItem.Path;
                if (this.ItemContainerGenerator.ContainerFromItem(pathItem) is TreeViewItem tvi)
                {
                    tvi.IsSelected = true;
                    tvi.BringIntoView();
                }
            }
        }

        // Expand the tree and highlight the itemToShow
        // Call the TreeView as an ItemsControl to cast it between TreeView and TreeViewItem as we recurse
        private bool ShowSelectedItem(ItemsControl parentContainer, object itemToShow)
        {
            if (itemToShow == null)
            {
                return false;
            }

            // check current level of tree
            foreach (object item in parentContainer.Items)
            {
                TreeViewItem currentContainer = (TreeViewItem)parentContainer.ItemContainerGenerator.ContainerFromItem(item);
                if (currentContainer != null && item == itemToShow)
                {
                    currentContainer.IsExpanded = true;
                    currentContainer.IsSelected = true;
                    currentContainer.Focus(); // Focusing this changes the highlight color to blue rather than shaded grey.
                    currentContainer.BringIntoView();
                    return true;
                }
            }

            // item is not found at current level, check the children
            foreach (object item in parentContainer.Items)
            {
                TreeViewItem currentContainer = (TreeViewItem)parentContainer.ItemContainerGenerator.ContainerFromItem(item);
                if (currentContainer != null && currentContainer.Items.Count > 0)
                {
                    // Have to expand the currentContainer or you can't look at the children
                    currentContainer.IsExpanded = true;
                    currentContainer.UpdateLayout();
                    if (!ShowSelectedItem(currentContainer, itemToShow))
                    {
                        // Haven't found the thing, so collapse it back
                        currentContainer.IsExpanded = false;
                    }
                    else
                    {
                        // We found the item
                        return true;
                    }
                }
            }
            // default
            return false;
        }
        #endregion

        #region Collapse items
        public void CollapseAll(TreeView treeView)
        {
            foreach (var item in treeView.Items)
            {
                if (treeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    CollapseAllItems(treeViewItem);
                }
            }
        }

        private void CollapseAllItems(TreeViewItem treeViewItem)
        {
            treeViewItem.IsExpanded = false;
            foreach (var item in treeViewItem.Items)
            {
                if (treeViewItem.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem childItem)
                {
                    CollapseAllItems(childItem);
                }
            }
        }
        #endregion

        #region UnselectAll
        public void UnselectAllItems(TreeView treeView)
        {
            this.IgnoreSelection = true;
            foreach (var item in treeView.Items)
            {
                UnselectAllItemsRecursive((TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(item));
            }

            this.IgnoreSelection = false;
        }

        private void UnselectAllItemsRecursive(TreeViewItem treeViewItem)
        {
            if (treeViewItem == null) return;

            treeViewItem.IsSelected = false;

            foreach (var subItem in treeViewItem.Items)
            {
                UnselectAllItemsRecursive((TreeViewItem)treeViewItem.ItemContainerGenerator.ContainerFromItem(subItem));
            }
        }
        #endregion
    }

    #region Item, PathItem, ItemProvider and ListItem classes
    internal class Item
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    internal class PathItem : Item
    {
        public List<Item> Items { get; set; } = new List<Item>();
    }

    // The ItemProvider creates a data structure based on a list of relative paths.
    // It assumes that the list is an ordered list of all path components, e.g.:
    //  a, a/b, a/b/c, a/b/d, c, c/d, etc 
    // It does this in a similar manner to recursively searching for folders in a folder system, 
    // i.e., for each top level folder, find the one that matches the first element in the path, 
    //       then recursively do that for that elements' child folders
    internal class ItemProvider
    {
        private List<string> RelativePaths { get; set; }
        internal ItemProvider(List<string> relativePaths)
        {
            this.RelativePaths = relativePaths;
        }

        internal List<Item> GetItems()
        {
            return GetItems(this.RelativePaths, "");
        }

        // Recursively search the list for the node that represents the path
        // null if there is no match
        internal PathItem Search(List<Item> items, string path)
        {
            string head = Util.FilesFolders.GetRelativePathRootFolder(path);
            string tail = Util.FilesFolders.GetRelativePathSubFolder(path);
            foreach (Item item in items)
            {
                PathItem pathItem = (PathItem)item;
                if (pathItem.Name == head)
                {
                    return string.IsNullOrWhiteSpace(tail)
                        ? pathItem
                        : Search(pathItem.Items, tail);
                }
            }
            return null;
        }

        // Given a list of paths, return a hierarchical data structure representing the path elements as a tree
        private List<Item> GetItems(List<string> paths, string pathToHere)
        {
            List<ListItem> listItems = SplitIntoListsByRoot(paths);
            List<Item> items = new List<Item>();
            foreach (ListItem listItem in listItems)
            {
                string newPathToHere = Path.Combine(pathToHere, listItem.PathHead);
                PathItem item = new PathItem
                {
                    Name = listItem.PathHead,
                    Path = newPathToHere,
                    Items = GetItems(listItem.PathTails, newPathToHere)
                };
                items.Add(item);
            }

            return items;
        }

        // Given a list of paths, create multiple lists, each collecting paths with a common root
        // e.g a, a/b, a/b/c, a/b/d, c, c/d, will be compile into two lists:
        //     a, a/b, a/b/c, a/b/d
        //     c, c/d,
        private List<ListItem> SplitIntoListsByRoot(List<string> paths)
        {
            List<ListItem> splitLists = new List<ListItem>();
            ListItem commonRootPathList = new ListItem();
            string currentParent = string.Empty;
            foreach (string path in paths)
            {
                string parent = Util.FilesFolders.GetRelativePathRootFolder(path);
                if (string.IsNullOrEmpty(parent))
                {
                    continue;
                }

                if (currentParent != parent && false == string.IsNullOrEmpty(parent))
                {
                    currentParent = parent;
                    commonRootPathList = new ListItem()
                    {
                        PathHead = parent,
                        PathTails = new List<string>(),
                    };
                    string tail = Util.FilesFolders.GetRelativePathSubFolder(path);
                    if (false == string.IsNullOrWhiteSpace(tail))
                    {
                        commonRootPathList.PathTails.Add(tail);
                    }
                    splitLists.Add(commonRootPathList);
                }
                else
                {
                    string tail = Util.FilesFolders.GetRelativePathSubFolder(path);
                    if (false == string.IsNullOrWhiteSpace(tail))
                    {
                        commonRootPathList.PathTails.Add(tail);
                    }
                }
            }
            return splitLists;
        }

        // A list item has a head, and all the children paths that comprise its tail e.g., 
        // PathHead = a, PathTails = b, b/c, b/d, etc

        public class ListItem
        {
            public string PathHead { get; set; }
            public List<string> PathTails { get; set; }
        }
        #endregion
    }
}
