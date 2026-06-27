using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.Dialog;
using Timelapse.Extensions;
using Timelapse.Util;

// ReSharper disable EmptyGeneralCatchClause
namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for MetadataFolderComplianceControl.xaml
    /// </summary>
    public partial class MetadataFolderComplianceControl
    {
        #region Private Variables
        private readonly string RootFolderName = "Root folder";
        private FileDatabase FileDatabase { get; set; }
        private MetadataFolderComplianceViewer ParentDialogWindow { get; set; }

        private int metadataInfoRowCount;
        private string RootPathToImages => FileDatabase.RootPathToImages;
        private DataTableBackedList<MetadataInfoRow> MetadataInfo => FileDatabase.MetadataInfo;
        
        // A list of the relative paths contained by the Timelapse database, passed into the control.
        // As its in the database, each of these complete relative paths must be associated with one or more images. 
        private List<string> RelativePaths = [];

        // PathList will eventually contain an entry for each subfolder (and entries for each of its subfolders etc) under the root folder
        // As only the complete paths (not path portions) in the RelativePaths list are associated with images, we can distinguish  
        // them from other paths which are not associated with Timelapse images.
        private readonly PathList MyPathList = new();

        // We collect a list of grids used for each treeview row, which is then used
        // to adjust the grid's column size
        private readonly List<Grid> TreeViewGridRows = [];
        private double maxFolderNamePixelWidth;
        private double maxLevelAliasPixelWidth;
        #endregion

        #region Constructors
        public MetadataFolderComplianceControl()
        {
            InitializeComponent();
        }
        #endregion

        #region Initialization
        // Initialize everything.  Retrieve a list of relative paths from the database and the actual file subfolders under the root folder,
        // then build the tree and corresponding node data structure from it and display it.
        // Needs to be invoked externally, usually immediately after the control is created. However, it can be re-invoked 
        // any time, which is usually used for testing purposes (e.g., to see if the changes to the treeview mirror the changes in the database)
        public async Task<bool> AsyncInitialize(MetadataFolderComplianceViewer owner, FileDatabase fileDatabase, List<string> addedRelativePathList,
            IProgress<ProgressBarArguments> progressHandler, CancellationTokenSource cancelTokenSource)
        {
            if (addedRelativePathList.Count == 0 && null == fileDatabase)
            {
                // Can't really do anything, so display that.
                TreeViewItem tvi = new() { Header = "No files appear to be loaded" };
                TreeView.Items.Add(tvi);
                return false;
            }

            FileDatabase = fileDatabase;
            ParentDialogWindow = owner;

            metadataInfoRowCount = MetadataInfo.RowCount;

            // Start: Wrap long operations to show progress: 
            // Get the relative paths from the database
            if (addedRelativePathList.Count == 0)
            {
                RelativePaths = await BusyCancelIndicator.ProgressWrapper(FileDatabase.AsyncGetRelativePaths, progressHandler, cancelTokenSource, "Retrieving folders. Please wait...", true);
            }
            else
            {
                RelativePaths = addedRelativePathList;
            }
            if (RelativePaths == null)
            {
                return false;
            }

            List<string> physicalFolders = await BusyCancelIndicator.ProgressWrapper(() => FilesFolders.AsyncGetAllFoldersExceptBackupAndDeletedFolders(FileDatabase.RootPathToImages, FileDatabase.RootPathToImages), progressHandler, cancelTokenSource, "Retrieving folders. Please wait...", true);
            if (physicalFolders == null)
            {
                return false;
            }
            // End: show progress (the remaining steps should be relatively fast)

            // Build the initial relative path list from the relative paths
            // Because they are in the database, these RelativePaths indicate that Timelapse associates that path with images, so containsImages will be true.
            foreach (string path in RelativePaths)
            {
                MyPathList.Items.Add(new(path, true));
            }

            // Now build addition paths based on the parent subfolders found in each relativePath.
            // If they are not present in the path list, then we know that these are folders that Timelapse does not associate with photos.
            // For example, if a/b/c is in the database, we know that a/b/c has images. However, unless they are also represented
            // by a RelativePath, we also need to  add a and a/b in the path list. 
            PathList pathListKnownToTimelapse = new();
            foreach (PathItem item in MyPathList.Items)
            {
                pathListKnownToTimelapse.Items.Add(item);
                string parent = GetParent(item.Path);
                while (parent != string.Empty)
                {
                    if (false == pathListKnownToTimelapse.Items.Any(s =>
                            s.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
                    {
                        pathListKnownToTimelapse.Items.Add(new(parent, false));
                    }
                    parent = GetParent(parent);
                }
            }
            MyPathList.Items.AddRange(pathListKnownToTimelapse.Items);

            // The above could create duplicate entries. So remove duplicate entries by path
            MyPathList.Items = [.. MyPathList.Items.GroupBy(x => x).Select(d => d.First())];

            // At this point, we should have a pathlist that distinguishes :
            // - all relative paths and their component paths that are somehow covered in the Timelapse Database 
            // - whether a particular path contains or does not contain images known to Timelapse

            // Now, we need to match these against the actual existing sub-folders in the file system, and include those missing from the PathList structure.
            // Add sub-folders (if any) that are under the root folder but not currently represented in our Path List (which currently is generated only
            // from the relativePaths in the database). We do this by getting all sub-folders under the root folder, and adding those that are not in the my PathList.
            // Note that we set containsImages to false. While the physical folders may actually contain images, they are not known to Timelapse.
            foreach (string folder in physicalFolders)
            {
                if (MyPathList.Items.Any(s => s.Path.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                MyPathList.Items.Add(new(folder, false));
            }

            // Finally, sort and rebuild the tree and node structure from these paths. 
            RebuildTreeAndNodes(true);
            return true;
        }
        #endregion

        #region Build the Node and TreeView structures
        // Rebuild everything from the PathList, which populates / displays the treeView and the node structure with the items in the MyPathList
        public void RebuildTreeAndNodes(bool sortTree = false)
        {
            if (null == FileDatabase)
            {
                return;
            }

            if (sortTree)
            {
                // Sort the tree if directed to do so
                MyPathList.OrderInPlace();
            }

            // Create the initial Root Folder node.
            // This node is a special case, where we independently have to check and set
            // - if the root folder exists(it should always exist)
            // - if it containsImages (indicated by an "" in the list) 
            PathItem rootPathItem = MyPathList.Items.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Path));
            bool rootNodeContainsImages = rootPathItem is { ContainsImages: true };
            Node rootNode = new()
            {
                FolderExists = Directory.Exists(this.RootPathToImages),
                ContainsImages = rootNodeContainsImages,
            };

            // Add the nodes to it to mirror the PathList correctly as children as needed
            // - AddPath breaks down a path into its components (i.e., strings separated by '\') to children sub-nodes as well
            //   - For example, if a path is a/b/c, nodes would be created for a, a/b, a/b/c
            //   - AddPath also checks to see if that path or path component was previously created, and if so it does not duplicate it.
            foreach (PathItem nodePathItem in MyPathList.Items)
            {
                // Add the nodes to the root node to mirror the PathList hierarchy
                rootNode.AddPath(nodePathItem);
            }

            // Record whether or not the old treeviewitem (using its complete path as its key) is or is not expanded in a dictionary
            Dictionary<string, bool> dictIsExpandedState = [];
            if (TreeView.Items.Count > 0)
            {
                RecordIsExpandedStateInDict((TreeViewItem)TreeView.Items[0], "", dictIsExpandedState);
            }

            // Create the initial TreeViewItem representing the root folder.
            Grid sp = CreateTreeViewItemHeaderAsStackPanel(rootNode, RootFolderName, rootNodeContainsImages, rootNode.FolderExists);
            TreeViewItem tvi = new()
            {
                Header = sp,
                IsExpanded = true,
                Tag = rootNode,
            };

            // Create a special context menu for the Root folder that doesn't include the Rename item,
            // as we don't allow the root folder to be renamed
            tvi.ContextMenu = TreeViewItemCreateContextMenu(tvi, rootNode.FolderExists);

            // Add children to the root TreeViewItem to match the node structure hierarchy
            TraverseNodeAndAddToTreeViewItem(tvi, rootNode, dictIsExpandedState);

            // Adjust the  size of each column in each TreeView row to provide an aligned multicolumn effect
            maxLevelAliasPixelWidth = Math.Max(70, maxLevelAliasPixelWidth);
            foreach (Grid grid in TreeViewGridRows)
            {
                // Icon
                ColumnDefinition c0 = new()
                {
                    Width = new(16, GridUnitType.Pixel)
                };
                // Foldername
                ColumnDefinition c1 = new()
                {
                    Width = new(maxFolderNamePixelWidth, GridUnitType.Pixel),
                };
                // Alias
                ColumnDefinition c2 = new()
                {
                    Width = new(maxLevelAliasPixelWidth, GridUnitType.Pixel),
                };
                ColumnDefinition c3 = new()
                {
                    Width = GridLength.Auto, //new GridLength(1, GridUnitType.Auto),
                };
                grid.ColumnDefinitions.Clear();
                grid.ColumnDefinitions.Add(c0);
                grid.ColumnDefinitions.Add(c1);
                grid.ColumnDefinitions.Add(c2);
                grid.ColumnDefinitions.Add(c3);
            }

            // Clear the treeView, in case it has been previously populated;
            // and add the (now populated) TreeViewItem to the treeView
            TreeView.Items.Clear();
            TreeView.Items.Add(tvi);

        }

        // Record whether or not a treeviewitem (as denoted by its complete path) in a dictionary
        // We will use this to set the expanded state when the tree view item is rebuilt
        public void RecordIsExpandedStateInDict(TreeViewItem parentItem, string path, Dictionary<string, bool> dictIsExpandedState)
        {
            // first time through, so use "" instead of "Root Folder"
            string newpath = dictIsExpandedState.Count == 0
                ? path
                : Path.Combine(path, GetTextBlockFromTreeViewItem(parentItem).Text);
            dictIsExpandedState.Add(newpath, parentItem.IsExpanded);

            // Start recursion on all subnodes.
            foreach (TreeViewItem childItem in parentItem.Items)
            {
                RecordIsExpandedStateInDict(childItem, newpath, dictIsExpandedState);
            }
        }

        // Given a node and a treeViewItem, recursively traverse this node's hierarchy
        // and populate a treeview item to reflect each node and its position in the hierarchy.
        private void TraverseNodeAndAddToTreeViewItem(TreeViewItem currentTvi, Node node, Dictionary<string, bool> dictIsExpandedState)
        {
            TreeViewItem tvi;
            if (node.Name != string.Empty)
            {
                // This node is a child item as the path isn't empty
                // So we need to create a new TreeViewItem representing that path, which will be added as a child the existing TreeViewItem
                // The tag is used to associate a node with its respective TreeViewItem
                node.FolderExists = Directory.Exists(Path.Combine(RootPathToImages, node.Path));
                Grid FolderGrid = CreateTreeViewItemHeaderAsStackPanel(node, node.Name, node.ContainsImages, node.FolderExists);
                tvi = new()
                {
                    Header = FolderGrid,
                    Tag = node
                };
                tvi.ContextMenu = TreeViewItemCreateContextMenu(tvi, node.FolderExists);

                // Restore whether or not a node is expanded, as recorded in the isExpandedDict dictionary.
                // If its not there, just set it to isExpanded.
                tvi.IsExpanded = !dictIsExpandedState.ContainsKey(node.Path) || dictIsExpandedState[node.Path];
                currentTvi.Items.Add(tvi);

            }
            else
            {
                // This node is a leaf item as the path is empty
                // So we will just use it as is
                tvi = currentTvi;
            }

            // Recursively traverse the children nodes, if any
            foreach (Node child in node.Children.Values)
            {
                TraverseNodeAndAddToTreeViewItem(tvi, child, dictIsExpandedState);
            }
        }

        // Expand or collapse all tree view items
        public void ExpandTreeView(bool expandTheTreeViewItem)
        {
            if (TreeView.Items.Count > 0)
            {
                TreeViewItem tvi = (TreeViewItem)TreeView.Items[0];
                ExpandTreeView(tvi, expandTheTreeViewItem);
                tvi!.IsExpanded = true; // always expand the root node
            }
        }
        private void ExpandTreeView(TreeViewItem item, bool expandTheTreeViewItem)
        {
            // item.IsExpanded = expandTheTreeViewItem;
            // Start recursion on all subnodes.
            foreach (TreeViewItem childItem in item.Items)
            {
                childItem.IsExpanded = expandTheTreeViewItem;
                ExpandTreeView(childItem, expandTheTreeViewItem);
            }
        }
        #endregion

        #region TreeViewItem Context Menu Creation and Callbacks
        // Create a context menu to be used by the treeview, as indicated by the parameters
        private ContextMenu TreeViewItemCreateContextMenu(TreeViewItem tvi, bool folderExists)
        {
            ContextMenu contextMenu = new();

            // Show in Explorer menu item
            MenuItem menuOpenInExplorer = new()
            {
                Header = "Show in Explorer",
                Tag = tvi,
                IsEnabled = folderExists
            };
            menuOpenInExplorer.Click += MenuOpenInExplorer_Click;
            contextMenu.Items.Add(menuOpenInExplorer);
            contextMenu.Tag = tvi;


            tvi.ContextMenuOpening += ContextMenu_ContextMenuOpening;
            return contextMenu;
        }

        // When the context menu when it is opened,
        // - enable or disable the Delete menu item as needed.  
        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is TreeViewItem tvi)
            {
                tvi.IsSelected = true;
                e.Handled = true; // So it doesn't propagate up the hierarchy
                ContextMenu cm = tvi.ContextMenu;
                if (null == cm)
                {
                    return;
                }

                cm.IsOpen = true;
            }
        }
        private void MenuOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                TreeViewItem tvi = (TreeViewItem)mi.Tag;
                Node node = (Node)tvi.Tag;

                // If the folder doesn't exists, flash the node and don't start the editing operation
                if (node.FolderExists)
                {
                    ProcessExecution.TryProcessStartUsingFileExplorerOnFolder(Path.Combine(this.RootPathToImages, node.Path));
                }
                //FLASH FLASH FLASH
            }
        }
        #endregion

        #region Treeview and TreeViewItem helpers
        // A TreeViewItem's header is a stackpanel comprising an icon and a textbox
        private Grid CreateTreeViewItemHeaderAsStackPanel(Node node, string text, bool containsPhotos, bool folderExists)
        {
            int level = string.IsNullOrWhiteSpace(node.Path) ? 0 : node.Path.Split(Path.DirectorySeparatorChar).Length;
            string warning = string.Empty;

            Grid grid = new();
            TreeViewGridRows.Add(grid);

            // 1. Generate the appropriate icon for the treeview
            BitmapImage folderIcon;
            if (containsPhotos)
            {
                folderIcon = folderExists
                    ? new(new(@"/Icons/FolderPhotos.png", UriKind.Relative))
                    : new BitmapImage(new(@"/Icons/FolderPhotos_Missing.png", UriKind.Relative));
            }
            else
            {
                folderIcon = folderExists
                    ? new(new(@"/Icons/FolderEmpty.png", UriKind.Relative))
                    : new BitmapImage(new(@"/Icons/FolderEmpty_Missing.png", UriKind.Relative));
            }
            Image image = new()
            {
                Source = folderIcon
            };
            Grid.SetColumn(image, 0);
            grid.Children.Add(image);


            // 2. Generate the folder name
            TextBlock tbFolderName = new();
            tbFolderName.Inlines.Add(new Run
            {
                Text = text,
            });
            Grid.SetColumn(tbFolderName, 1);
            grid.Children.Add(tbFolderName);
            // Accumulate the maximum  width of all folder names, used to aligned columns in the treeview
            maxFolderNamePixelWidth = Math.Max(tbFolderName.MeasureStringWidth(text) + (19*level), maxFolderNamePixelWidth);


            // 3. Generate thise level's Alias name
            string alias;

            if (level < metadataInfoRowCount)
            {
                alias = MetadataUI.CreateTemporaryAliasIfNeeded(level+1, MetadataInfo[level].Alias);
            }
            else
            {
                alias = "????????";
                warning += "Extra undefined level. ";
            }

            TextBlock tbLevelAliasName = new() { Margin = new(-19 * level + 30, 0, 0, 0) };
            tbLevelAliasName.Inlines.Add(new Run
            {
                Text = alias,
                FontStyle = FontStyles.Italic,
                Foreground = !string.IsNullOrWhiteSpace(warning) ? Brushes.Crimson : (SolidColorBrush)new BrushConverter().ConvertFromString("#FF3A3A3A") //Brushes.DarkSlateGray

            });
            Grid.SetColumn(tbLevelAliasName, 2);
            grid.Children.Add(tbLevelAliasName);
            // Accumulate the maximum  width of all aliases, used to aligned columns in the treeview
            maxLevelAliasPixelWidth = Math.Max(tbLevelAliasName.MeasureStringWidth(alias), maxLevelAliasPixelWidth);

            //// 4. Generate the warning message
            //if (node.Children.Count == 0 && level < this.metadataInfoRowCount - 1 && containsPhotos)
            //{
            //    warning += "Missing lower levels. ";
            //}

            string lastLevelAlias = "lowest defined";
            if (containsPhotos && metadataInfoRowCount - 1 != level)
            {
                lastLevelAlias = MetadataUI.CreateTemporaryAliasIfNeeded(metadataInfoRowCount - 1, MetadataInfo[metadataInfoRowCount - 1].Alias);
                warning += $"Only {lastLevelAlias} folders should contain images.";
            }

            if (!string.IsNullOrWhiteSpace(warning))
            {
                TextBlock tbWarning = new() { Margin = new(-19 * level + 40, 0, 0, 0) };
                tbWarning.Inlines.Add(new Run
                {
                    Text = warning,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Crimson

                });
                tbFolderName.Foreground = Brushes.Crimson;
                tbLevelAliasName.Foreground = Brushes.Crimson;
                Grid.SetColumn(tbWarning, 3);
                grid.Children.Add(tbWarning);

                // Change which message is displayed in the parent window.
                ParentDialogWindow.MessageDivergence.Visibility = Visibility.Visible;
                ParentDialogWindow.MessageDivergence.Reason += $"{lastLevelAlias} folder level.";
                ParentDialogWindow.MessageNoDivergence.Visibility = Visibility.Collapsed;
                ParentDialogWindow.CancelButton.Visibility = Visibility.Visible;
            }
            return grid;
        }

        private TextBlock GetTextBlockFromTreeViewItem(TreeViewItem tvi)
        {
            // If the folder doesn't exists, flash the node and don't start the editing operation
            StackPanel sp = (StackPanel)tvi.Header;
            foreach (UIElement child in sp.Children)
            {
                if (child is TextBlock headerTextBlock)
                {
                    return headerTextBlock;
                }
            }
            return null;
        }
        #endregion

        #region Path manipulations
        private string GetParent(string path)
        {
            string parentPath = string.Empty;
            int index = path.LastIndexOf(Path.DirectorySeparatorChar);
            if (index >= 0)
            {
                parentPath = path[..index];
                if (false == string.IsNullOrEmpty(parentPath))
                {
                    return parentPath;
                }
            }
            return parentPath;
        }
        #endregion

        #region Class: Node

        // The Node structure represents a hierarchical tree.
        // Each node contains
        // - Name (except for the root node which is empty,
        // - Name: the path so far to it.
        private class Node
        {
            // The dictionary represents a node in a hierarchical tree, where:
            // Name is the name of the node
            // Children are the child nodes, if any, contained by the node
            // For example, the hierarchical structure
            //  @"a\b"'
            //  @"a\c",
            //  @"d"
            // would be represented as:
            // Name = "", Path = "", Children = 
            //      Name="a", Path="a", Children =
            //          Name="b", Path="a\b", Children = <empty>
            //          Name="c", Path="a\c", Chldren=<empty>
            //      Name="d", Path="d", Children =<empty>
            public readonly IDictionary<string, Node> Children = new Dictionary<string, Node>();
            public string Name { get; private init; } = string.Empty;
            public string Path { get; private init; } = string.Empty;
            public bool ContainsImages { get; set; }
            public bool FolderExists { get; set; }

            private readonly char[] charSeparators = ['\\'];

            public void AddPath(PathItem pathItem)
            {
                string path = pathItem.Path;
                bool containsPhotos = pathItem.ContainsImages;
                // Split the path into its constituent names e.g., a\b would be a, b
                string[] names = pathItem.Path.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);

                // For each constutuent part
                Node currentNode = this;
                string pathSoFar = string.Empty; // represents the path to that constient part 
                foreach (string name in names)
                {
                    pathSoFar = System.IO.Path.Combine(pathSoFar, name);

                    // Check if the constituent name already exists as a child, which happens if it was already created from a previous path.
                    if (!currentNode.Children.TryGetValue(name, out Node childNode))
                    {
                        // There is no child with that part, so create a new childNode representing it
                        childNode = new()
                        {
                            Name = name,
                            Path = pathSoFar,
                            ContainsImages = containsPhotos && pathSoFar == path
                        };
                        currentNode.Children[name] = childNode;
                    }

                    // The name already exists, so lets go to that node
                    currentNode = childNode;

                    // If the path is the same, update the currentPhotos attribute to match the original PathItem
                    if (pathItem.Path.Equals(pathSoFar, StringComparison.OrdinalIgnoreCase))
                    {
                        currentNode.ContainsImages = containsPhotos;
                    }
                }
            }
        }

        #endregion

        #region Class: PathList and PathItem
        private class PathItem(string path, bool containsImages)
        {
            public string Path { get; } = path;
            public bool ContainsImages { get; } = containsImages;
        }

        private class PathList
        {
            public List<PathItem> Items { get; set; } = [];

            public void OrderInPlace()
            {
                Items = [.. Items.OrderBy(s => s.Path)];
            }
        }
        #endregion
    }
}
