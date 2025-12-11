using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.Controls
{
    // TreeViewWithClassificationDescriptions is an extended treeview control designed to display classification category descriptions (if any
    // as collected from the recognition files.
    // It is used to display classification descriptions in the Select dialog 
    public class TreeViewWithClassificationDescriptions : TreeView
    {
        #region Public properties
        // The currently selected path (e.g., a;b;c)
        public string SelectedPath { get; set; }
        // Whether or not to invoke some code
        // Used to stop unecessary invocation of the SelectedTaxaItemChanged callback, which can otherwise lead to performance issues
        public bool DontInvoke { get; set; }

        // Whether or not to apply the focus on the selected TaxaItem
        public bool FocusSelection { get; set; } = true;

        public bool HasContent => this.TaxaItemList is { Count: > 0 };
        #endregion

        #region Private properties
        private TaxaItemProvider TaxaItemProvider { get; set; }
        private List<TaxaItem> TaxaItemList { get; set; }
        #endregion

        #region Constructor
        public TreeViewWithClassificationDescriptions()
        {
            this.SelectedItemChanged += TreeView_SelectedTaxaItemChanged;
            this.Loaded += TreeView_OnLoaded;

            SetTreeViewContentsToRelativePathList(GlobalReferences.MainWindow.DataHandler.FileDatabase.classificationDescriptionsDictionary);
        }
        #endregion

        #region Public methods
        internal List<TaxaItem> SetTreeViewContentsToRelativePathList(Dictionary<string,string> classificationDescriptionsDictionary)
        {
            List<string> taxaStrings1 = [.. classificationDescriptionsDictionary.Values];
            List<string> taxaStrings2 = [];
            foreach (string taxaString in taxaStrings1)
            {
                taxaStrings2.Add(taxaString[(taxaString.IndexOf(';') + 1)..]);
            }
            taxaStrings2.Sort();
            this.TaxaItemProvider = new(taxaStrings2);
            this.TaxaItemList = TaxaItemProvider.GetTaxaItems();
            DataContext = this.TaxaItemList;
            this.ItemsSource = this.TaxaItemList;
            return this.TaxaItemList;
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

            PathTaxaItem TaxaItem = this.GetPathTaxaItemFromPath(this.SelectedPath);
            this.UnselectAll();
            this.CollapseAll();
            this.DontInvoke = true;
            this.ShowSelectedPathTaxaItem(this, TaxaItem);
            this.DontInvoke = false;

        }

        // Given a relative path return the first folder in the path e.g. e.g. a\b\c, returns a
        // If its a root folder, then just return that e.g., a returns a
        public static string GetTaxaRoot(string path)
        {
            int indx = path.NthIndexOf(';', 1);
            return indx == -1
                ? path // the path is just the root folder
                : path[..indx]; // trim off everything from the first path separator onwards
        }

        // Given a relative path return the subfolder path after the first folder e.g., e.g. a\b\c returns b\c
        // If its a root folder, return "" as there are not folders after that e.g., a returns ""
        public static string GetTaxaSubPath(string path)
        {
            int indx = path.NthIndexOf(';', 1);
            if (indx == -1 || indx == path.Length)
            {
                return string.Empty;
            }

            return path[(indx + 1)..];
        }

        // Highlight the selected TaxaItem
        private void TreeView_SelectedTaxaItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (false == this.DontInvoke && e.NewValue is PathTaxaItem pathTaxaItem)
            {
                this.SelectAndHighlightPathTaxaItem(pathTaxaItem);
            }
        }
        #endregion

        #region Private Search for Path, Select and Show TaxaItem
        private PathTaxaItem GetPathTaxaItemFromPath(string path)
        {
            this.DontInvoke = true;
            PathTaxaItem pathTaxaItem = this.TaxaItemProvider.Search(this.TaxaItemList, path);
            this.DontInvoke = false;
            return pathTaxaItem;
        }

        // Select and highlight the pathTaxaItem if its not already selected
        private void SelectAndHighlightPathTaxaItem(PathTaxaItem pathTaxaItem)
        {
            if (null != pathTaxaItem && false == string.IsNullOrWhiteSpace(pathTaxaItem.Path))
            {
                if (this.SelectedPath == pathTaxaItem.Path)
                {
                    // Its already selected and thus highlit, so abort
                    return;
                }

                this.SelectedPath = pathTaxaItem.Path;
                if (this.ItemContainerGenerator.ContainerFromItem(pathTaxaItem) is TreeViewItem tvi)
                {
                    tvi.IsSelected = true;
                    tvi.BringIntoView();
                }
            }
        }

        // Expand the tree, select and optionally focus the TaxaItemToShow
        // We call the TreeView as an TaxaItemsControl to cast it between TreeView and TreeViewTaxaItem as we recurse
        private bool ShowSelectedPathTaxaItem(ItemsControl parentContainer, object TaxaItemToShow)
        {
            if (TaxaItemToShow == null)
            {
                return false;
            }

            // check current level of tree
            foreach (object TaxaItem in parentContainer.Items)
            {
                TreeViewItem currentContainer = (TreeViewItem)parentContainer.ItemContainerGenerator.ContainerFromItem(TaxaItem);
                if (currentContainer != null && TaxaItem == TaxaItemToShow)
                {
                    currentContainer.IsExpanded = true;

                    if (this.FocusSelection)
                    {
                        // FocusSelection is set externally, and is set only if the selection is an active one 
                        // (rather than a setting that is currently inactive)
                        // Focus will also make the TaxaItem appear with a  blue (rather than grey) background to show that it is active
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

            // TaxaItem is not found at current level, check the children
            foreach (object TaxaItem in parentContainer.Items)
            {
                TreeViewItem currentContainer = (TreeViewItem)parentContainer.ItemContainerGenerator.ContainerFromItem(TaxaItem);
                if (currentContainer is { Items.Count: > 0 })
                {
                    // Have to expand the currentContainer or you can't look at the children
                    currentContainer.IsExpanded = true;
                    currentContainer.UpdateLayout();
                    if (!ShowSelectedPathTaxaItem(currentContainer, TaxaItemToShow))
                    {
                        // Haven't found the thing, so collapse it back
                        currentContainer.IsExpanded = false;
                    }
                    else
                    {
                        // We found the TaxaItem
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
            foreach (var TaxaItem in this.Items)
            {
                if (this.ItemContainerGenerator.ContainerFromItem(TaxaItem) is TreeViewItem treeViewTaxaItem)
                {
                    CollapseAllRecursive(treeViewTaxaItem);
                }
            }
            this.DontInvoke = false;
        }

        private static void CollapseAllRecursive(TreeViewItem treeViewTaxaItem)
        {
            treeViewTaxaItem.IsExpanded = false;
            treeViewTaxaItem.IsSelected = false;
            foreach (var TaxaItem in treeViewTaxaItem.Items)
            {
                if (treeViewTaxaItem.ItemContainerGenerator.ContainerFromItem(TaxaItem) is TreeViewItem childTaxaItem)
                {
                    CollapseAllRecursive(childTaxaItem);
                }
            }
        }
        #endregion

        #region UnselectAll
        public void UnselectAll()
        {
            this.DontInvoke = true;
            foreach (var TaxaItem in this.Items)
            {
                UnselectAllRecursive((TreeViewItem)this.ItemContainerGenerator.ContainerFromItem(TaxaItem));
            }
            this.DontInvoke = false;
        }

        // Unselect all TaxaItems in the tree
        private static void UnselectAllRecursive(TreeViewItem treeViewTaxaItem)
        {
            if (treeViewTaxaItem == null) return;

            treeViewTaxaItem.IsSelected = false;

            foreach (var subTaxaItem in treeViewTaxaItem.Items)
            {
                UnselectAllRecursive((TreeViewItem)treeViewTaxaItem.ItemContainerGenerator.ContainerFromItem(subTaxaItem));
            }
        }
        #endregion
    }

    #region TaxaItem, PathTaxaItem, TaxaItemProvider and ListTaxaItem classes
    internal class TaxaItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string CommonName { get; set; }
    }

    internal class PathTaxaItem : TaxaItem
    {
        public List<TaxaItem> TaxaItems { get; set; } = [];
    }

    // The TaxaItemProvider creates a data structure based on a list of relative paths.
    // It assumes that the list is an ordered list of all path components, e.g.:
    //  a, a/b, a/b/c, a/b/d, c, c/d, etc 
    // It does this in a similar manner to recursively searching for folders in a folder system, 
    // i.e., for each top level folder, find the one that matches the first element in the path, 
    //       then recursively do that for that elements' child folders
    internal class TaxaItemProvider
    {
        private List<string> RelativePaths { get; }
        internal TaxaItemProvider(List<string> relativePaths)
        {
            this.RelativePaths = relativePaths;
        }

        internal List<TaxaItem> GetTaxaItems()
        {
            return GetTaxaItems(this.RelativePaths, "");
        }

        // Recursively search the list for the node that represents the path
        // null if there is no match
        internal PathTaxaItem Search(List<TaxaItem> TaxaItems, string path)
        {
            string head = TreeViewWithClassificationDescriptions.GetTaxaRoot(path);
            string tail = TreeViewWithClassificationDescriptions.GetTaxaSubPath(path);
            foreach (TaxaItem TaxaItem in TaxaItems)
            {
                PathTaxaItem pathTaxaItem = (PathTaxaItem)TaxaItem;
                if (pathTaxaItem.Name == head)
                {
                    return string.IsNullOrWhiteSpace(tail)
                        ? pathTaxaItem
                        : Search(pathTaxaItem.TaxaItems, tail);
                }
            }
            return null;
        }

        // Given a list of paths, return a hierarchical data structure representing the path elements as a tree
        // Each description has the form "429257d4-3ef2-47fb-b849-66ee6c107346;mammalia;cetartiodactyla;cervidae;alces;alces;moose", 
        // 1st field: GUID
        // 2nd-6th fields: Taxa as: Class;Order;Family;Genus;Species
        // 7th field: common name
        private List<TaxaItem> GetTaxaItems(List<string> paths, string pathToHere)
        {
            List<ListTaxaItem> listTaxaItems = SplitIntoListsByRoot(paths);
            List<TaxaItem> TaxaItems = [];
            foreach (ListTaxaItem listTaxaItem in listTaxaItems)
            {
                Debug.Print(listTaxaItem.PathHead + " | " + listTaxaItem.PathTails[0]);
                string newPathToHere = Path.Combine(pathToHere, listTaxaItem.PathHead);
                PathTaxaItem TaxaItem = new()
                {
                    Name = listTaxaItem.PathHead,
                    Path = newPathToHere,
                    //CommonName = listTaxaItem.PathTails.Count == 0
                    //    ? listTaxaItem.PathHead // Use the tail as the common name if there is only one tail
                    //    : string.Empty, // No common name if there are multiple tails
                    //TaxaItems = listTaxaItem.PathTails.Count > 0
                    //    ? GetTaxaItems(listTaxaItem.PathTails, newPathToHere)
                    //    : null
                };
                if (-1 != listTaxaItem.PathTails[0].IndexOf(";", StringComparison.CurrentCultureIgnoreCase))
                {
                    TaxaItem.CommonName = string.Empty;
                    TaxaItems = GetTaxaItems(listTaxaItem.PathTails, newPathToHere);
                }
                else
                {
                    TaxaItem.CommonName = listTaxaItem.PathTails[0];
                }
                TaxaItems.Add(TaxaItem);
            }
            return TaxaItems;
        }

        // Given a list of paths, create multiple lists, each collecting paths with a common root
        // e.g a, a/b, a/b/c, a/b/d, c, c/d, will be compile into two lists:
        //     a, a/b, a/b/c, a/b/d
        //     c, c/d,
        private List<ListTaxaItem> SplitIntoListsByRoot(List<string> paths)
        {
            List<ListTaxaItem> splitLists = [];
            ListTaxaItem commonRootPathList = new();
            string currentParent = string.Empty;
            foreach (string path in paths)
            {
                string parent = TreeViewWithClassificationDescriptions.GetTaxaRoot(path);
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
                    string tail = TreeViewWithClassificationDescriptions.GetTaxaSubPath(path);
                    if (false == string.IsNullOrWhiteSpace(tail))
                    {
                        commonRootPathList.PathTails.Add(tail);
                    }
                    splitLists.Add(commonRootPathList);
                }
                else
                {
                    string tail = TreeViewWithClassificationDescriptions.GetTaxaSubPath(path);
                    if (false == string.IsNullOrWhiteSpace(tail))
                    {
                        commonRootPathList.PathTails.Add(tail);
                    }
                }
            }
            return splitLists;
        }

        // A list TaxaItem has a head, and all the children paths that comprise its tail e.g., 
        // PathHead = a, PathTails = b, b/c, b/d, etc

        public class ListTaxaItem
        {
            public string PathHead { get; set; }
            public List<string> PathTails { get; set; }
        }
        #endregion
    }
}

