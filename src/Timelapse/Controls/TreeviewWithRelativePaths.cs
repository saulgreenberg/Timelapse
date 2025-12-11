using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows;

namespace Timelapse.Controls
{
    // TreeViewWithRelativePaths is an extended treeview control designed to display relative paths as collected from the current image set.
    // It is used twice in Timelapse:
    // - Select | All files in a folder and its subfolders... , where the pull-right menu is displayed as a treeViewWithRelativePaths
    // - Select | Custom selection...  RelativePath folder, where the combobox (actually a dropdown button) displays the treeViewWithRelativePaths
    public class TreeViewWithRelativePaths : TreeView
    {
        #region Public properties
        // The currently selected path (e.g., a/b/c)
        public string SelectedPath { get; set; }
        // Whether or not to invoke some code
        // Used to stop unecessary invocation of the SelectedItemChanged callback, which can otherwise lead to performance issues
        public bool DontInvoke { get; set; }

        // Whether or not to apply the focus on the selected item
        public bool FocusSelection { get; set; } = true;

        public bool HasContent => this.ItemList is { Count: > 0 };
        #endregion

        #region Private properties
        private ItemProvider ItemProvider { get; set; }
        private List<Item> ItemList { get; set; }
        #endregion

        #region Constructor
        public TreeViewWithRelativePaths()
        {
            this.SelectedItemChanged += TreeView_SelectedItemChanged;
            this.Loaded += TreeView_OnLoaded;
        }
        #endregion

        #region Public methods
        internal List<Item> SetTreeViewContentsToRelativePathList(List<string> relativePaths)
        {
            this.ItemProvider = new(relativePaths);
            this.ItemList = ItemProvider.GetItems();
            DataContext = this.ItemList;
            return this.ItemList;
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

            PathItem item = this.GetPathItemFromPath(this.SelectedPath);
            this.UnselectAll();
            this.CollapseAll();
            this.DontInvoke = true;
            this.ShowSelectedPathItem(this, item);
            this.DontInvoke = false;

        }

        // Highlight the selected item
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (false == this.DontInvoke && e.NewValue is PathItem pathItem)
            {
                this.SelectAndHighlightPathItem(pathItem);
            }
        }
        #endregion

        #region Private Search for Path, Select and Show Item
        private PathItem GetPathItemFromPath(string path)
        {
            this.DontInvoke = true;
            PathItem pathItem = this.ItemProvider.Search(this.ItemList, path);
            this.DontInvoke = false;
            return pathItem;
        }

        // Select and highlight the pathItem if its not already selected
        private void SelectAndHighlightPathItem(PathItem pathItem)
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

        // Expand the tree, select and optionally focus the itemToShow
        // We call the TreeView as an ItemsControl to cast it between TreeView and TreeViewItem as we recurse
        private bool ShowSelectedPathItem(ItemsControl parentContainer, object itemToShow)
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
                    
                    if (this.FocusSelection)
                    {
                        // FocusSelection is set externally, and is set only if the selection is an active one 
                        // (rather than a setting that is currently inactive)
                        // Focus will also make the item appear with a  blue (rather than grey) background to show that it is active
                        currentContainer.IsSelected = true; 
                        currentContainer.Focus(); // Focusing this changes the highlight color to blue rather than shaded grey.
                    }
                    else
                    {
                        currentContainer.IsSelected = false;
                    }
                    currentContainer.BringIntoView();
                    return true;
                }
            }

            // Item is not found at current level, check the children
            foreach (object item in parentContainer.Items)
            {
                TreeViewItem currentContainer = (TreeViewItem)parentContainer.ItemContainerGenerator.ContainerFromItem(item);
                if (currentContainer is { Items.Count: > 0 })
                {
                    // Have to expand the currentContainer or you can't look at the children
                    currentContainer.IsExpanded = true;
                    currentContainer.UpdateLayout();
                    if (!ShowSelectedPathItem(currentContainer, itemToShow))
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

        #region Collapse
        public void CollapseAll()
        {
            this.DontInvoke = true;
            foreach (var item in this.Items)
            {
                if (this.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
                {
                    CollapseAllRecursive(treeViewItem);
                }
            }
            this.DontInvoke = false;
        }

        private void CollapseAllRecursive(TreeViewItem treeViewItem)
        {
            treeViewItem.IsExpanded = false;
            treeViewItem.IsSelected = false;
            foreach (var item in treeViewItem.Items)
            {
                if (treeViewItem.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem childItem)
                {
                    CollapseAllRecursive(childItem);
                }
            }
        }
        #endregion

        #region UnselectAll
        public void UnselectAll()
        {
            this.DontInvoke = true;
            foreach (var item in this.Items)
            {
                UnselectAllRecursive((TreeViewItem)this.ItemContainerGenerator.ContainerFromItem(item));
            }
            this.DontInvoke = false;
        }

        // Unselect all items in the tree
        private void UnselectAllRecursive(TreeViewItem treeViewItem)
        {
            if (treeViewItem == null) return;

            treeViewItem.IsSelected = false;

            foreach (var subItem in treeViewItem.Items)
            {
                UnselectAllRecursive((TreeViewItem)treeViewItem.ItemContainerGenerator.ContainerFromItem(subItem));
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
        public List<Item> Items { get; set; } = [];
    }

    // The ItemProvider creates a data structure based on a list of relative paths.
    // It assumes that the list is an ordered list of all path components, e.g.:
    //  a, a/b, a/b/c, a/b/d, c, c/d, etc 
    // It does this in a similar manner to recursively searching for folders in a folder system, 
    // i.e., for each top level folder, find the one that matches the first element in the path, 
    //       then recursively do that for that elements' child folders
    internal class ItemProvider
    {
        private List<string> RelativePaths { get; }
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
            List<Item> items = [];
            foreach (ListItem listItem in listItems)
            {
                string newPathToHere = Path.Combine(pathToHere, listItem.PathHead);
                PathItem item = new()
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
            List<ListItem> splitLists = [];
            ListItem commonRootPathList = new();
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
                    commonRootPathList = new()
                    {
                        PathHead = parent,
                        PathTails = [],
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
