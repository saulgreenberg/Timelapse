using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;


// ReSharper disable EmptyGeneralCatchClause

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for RelativePathControl.xaml
    /// </summary>
    public partial class RelativePathControl
    {
        // TODO: Add Argument / variable to hold FileDatabase.Database
        #region Constants


        private readonly char charSeparator = '\\';
        private readonly string RootFolder = "Root folder";
        private readonly string NewFolder = "New folder";

        // Invalid folder name characters and folder names. Used for checking renamed relative paths
        private readonly char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();

        private readonly string[] invalidFileNames =
        {
            "CON", "PRN", "AUX", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
            "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
            "LPT8", "LPT9"
        };
        #endregion

        #region Properties
        public FileDatabase FileDatabase { get; set; }
        public Window ParentDialogWindow { get; set; }

        public bool WereEditsMade { get; set; } = false;
        #endregion

        #region Variables

        private readonly DispatcherTimer RenameItemUITimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        // This contains the list of relative paths passed into the control
        private List<string> RelativePaths = new List<string>();
        // This list is constructed from the above, where it indicates if each relative path is associated with at least one photo or video.
        // We can do this as only the complete paths (not path portions) in the RelativePath list are associated with photos
        // The path list will also separate and add each sub-path in a RelativePath, where the subpaths would indicate that they do not have a photo.
        private PathList MyPathList = new PathList();

        // For recording dragging state during 
        private Point _lastMouseDown;
        private TreeViewItem draggedItem, _target;
        #endregion

        #region Constructors and Loading
        public RelativePathControl()
        {
            InitializeComponent();
            RenameItemUITimer.Tick += RenameItemUiTimerTick;
        }

        private void RelativePathControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (null != FileDatabase)
            {
                // The above test is so that something can be displayed at design time
                this.RebuildTree();
            }
        }
        #endregion

        #region Public Methods

        // Given a list of relative paths, build the tree from it and display it.
        public void Initialize(List<string> relativePaths)
        {
            if (null == FileDatabase)
            {
                TreeViewItem tvi = new TreeViewItem { Header = "No files appear to be loaded" };
                TreeView.Items.Add(tvi);
                return;
            }
            this.RelativePaths = relativePaths;

            foreach (string path in this.RelativePaths)
            {
                this.MyPathList.Items.Add(new PathItem(path, true));
            }

            PathList newPathList = new PathList();
            foreach (PathItem item in this.MyPathList.Items)
            {
                newPathList.Items.Add(item);
                string parent = this.GetParent(item.Path);
                while (parent != string.Empty)
                {
                    if (false == newPathList.Items.Any(s => s.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
                    {
                        newPathList.Items.Add(new PathItem(parent, false));
                    }
                    parent = this.GetParent(parent);
                }
            }

            // Initially, ensure that we sort the paths, as otherwise edited items will appear in the wrong place
            this.RefreshAsSortedRelativePaths();
        }
        public void RefreshAsSortedRelativePaths()
        {
            this.RelativePaths = this.RelativePaths.OrderBy(s => s).ToList();
            this.MyPathList.OrderInPlace();
            this.RebuildTree();
        }
        #endregion

        #region Building (and optionally sorting) the tree from the RelativePaths
        // Populate the data structure and the treeView with the items in the relativePath
        private void RebuildTree()
        {
            if (null == FileDatabase)
            {
                return;
            }
            Node rootNode = new Node();

            foreach (PathItem nodePathItem in this.MyPathList.Items)
            {
                // Add each path to the node. 
                // Note that AddPath breaks down a path into its components (i.e., strings separated by '\') to children sub-nodes as well
                rootNode.AddPath(nodePathItem);
            }

            // Check if the root folder exits (it should always exist)
            bool folderExists = Directory.Exists(this.FileDatabase.FolderPath);
            StackPanel sp = CreateStackPanel(this.RootFolder, false, folderExists);
            // Create an expanded treeview item representing the Root folder
            // and add the nodes to it, where it places them correctly as children as needed
            TreeViewItem tvi = new TreeViewItem
            {
                Header = sp,
                IsExpanded = true,
                Tag = rootNode,
            };

            // Create a special context menu for the Root folder
            ContextMenu contextMenu = new ContextMenu();
            MenuItem menuCreateNewFolderAfterItem = new MenuItem
            {
                Header = "New folder...",
                Tag = tvi,
                IsEnabled = folderExists
            };
            menuCreateNewFolderAfterItem.Click += MenuItemNewFolder_Click;
            contextMenu.Items.Add(menuCreateNewFolderAfterItem);
            tvi.ContextMenu = contextMenu;

            this.TraverseNodeAndAddToTreeViewItem(tvi, rootNode);

            // Clear the treeView, in case it has been previously populated;
            // and add the populated TreeViewItem to the treeView
            this.TreeView.Items.Clear();
            this.TreeView.Items.Add(tvi);
        }

        // Given a node, traverse its structure, where we populate a treeview item to 
        // reflect the contents of the nodes and sub-nodes as its being traversed
        // Note that this is a recursive routine
        private void TraverseNodeAndAddToTreeViewItem(TreeViewItem currentTvi, Node node)
        {
            TreeViewItem tvi;
            if (node.Name != string.Empty)
            {
                // This node is a child item as the path isn't empty
                // So we need to create a new TreeViewItem representing that path, which will be added as a child the existing TreeViewItem
                // The tag is used to associate a node with its respective TreeViewItem
                bool folderExists = Directory.Exists(Path.Combine(this.FileDatabase.FolderPath, node.Path));
                node.FolderExists = folderExists;
                StackPanel sp = this.CreateStackPanel(node.Name, node.ContainsPhotos, folderExists);
                
                tvi = new TreeViewItem
                {
                    Header = sp,
                    Tag = node
                };

                // Add a context menu to the treeview
                ContextMenu contextMenu = new ContextMenu();
                MenuItem menuItemRename = new MenuItem
                {
                    Header = "Rename...",
                    Tag = tvi,
                    IsEnabled = folderExists
                };
                menuItemRename.Click += MenuItemRename_Click;
                contextMenu.Items.Add(menuItemRename);

                MenuItem menuCreateNewFolderAfterItem = new MenuItem
                {
                    Header = "New folder...",
                    Tag = tvi,
                    IsEnabled = folderExists
                };
                menuCreateNewFolderAfterItem.Click += MenuItemNewFolder_Click;
                contextMenu.Items.Add(menuCreateNewFolderAfterItem);

                tvi.ContextMenu = contextMenu;
                currentTvi.Items.Add(tvi);
            }
            else
            {
                // This node is not a child item as the path is empty
                // So we will just use it as is
                tvi = currentTvi;
            }

            foreach (Node child in node.Children.Values)
            {
                TraverseNodeAndAddToTreeViewItem(tvi, child);
            }
            tvi.IsExpanded = true;
        }

        private StackPanel CreateStackPanel(string text, bool containsPhotos, bool folderExists)
        {
            StackPanel sp = new StackPanel
            {
                Name = "StackPanel",
                Orientation = Orientation.Horizontal
            };
            BitmapImage bi;
            if (containsPhotos)
            {
                bi = folderExists
                    ? new BitmapImage(new Uri(@"/Icons/FolderPhotos.png", UriKind.Relative))
                    : new BitmapImage(new Uri(@"/Icons/FolderPhotos_Missing.png", UriKind.Relative));
            }
            else
            {
                bi = folderExists
                    ? new BitmapImage(new Uri(@"/Icons/FolderEmpty.png", UriKind.Relative))
                    : new BitmapImage(new Uri(@"/Icons/FolderEmpty_Missing.png", UriKind.Relative));
            }
            Image image = new Image
            {
                Source = bi
            };

            TextBlock tb = new TextBlock
            {
                Name = "TextBlock",
                Text = text,
                Foreground = containsPhotos ? Brushes.Black : (SolidColorBrush)new BrushConverter().ConvertFromString("#FF3A3A3A") //Brushes.DarkSlateGray
            };
            sp.Children.Add(image);
            sp.Children.Add(tb);
            return sp;
        }
        #endregion

        #region UI: Create new folder
        private void MenuItemNewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                TreeViewItem tvi = (TreeViewItem)mi.Tag;
                this.CreateNewFolder(tvi);
            }
        }
        #endregion

        #region UI: Renaming on a 1 second mouse left hold-down
        // Initiate renaming when an item is selected.
        // Rename is invoked when the mouse is held down for more than the RenameItemUITimer's duration
        // Rename initiation is cancelled if the mouse is moved more than a certain amount (see TreeViewItem_MouseMove, which stops the timer)
        private void TreeViewItem_Selected(object Sender, RoutedEventArgs e)
        {
            try
            {
                RenameItemUITimer.Start();
            }
            catch (Exception)
            {
            }
        }

        private void TreeViewItem_MouseUp(object sender, MouseEventArgs e)
        {
            // Cancel on a mouse up
            RenameItemUITimer.Stop();
            if (this.TreeView.SelectedItem is TreeViewItem tvi)
            {
                tvi.IsSelected = false;
            }
        }

        private void RenameItemUiTimerTick(object sender, EventArgs e)
        {
            // We'ved held the mouse down on a selected item for more than the RenameItemUITimer's duration
            // So enable renaming on it.
            TreeViewItem selectedItem = (TreeViewItem)this.TreeView.SelectedItem;
            RenameItemUITimer.Stop();
            this.InitiateRenameUI(selectedItem);
        }
        #endregion

        #region UI: Renaming interaction
        private double textBoxEditNodeMinWidth = -1;
        private string originalTextInHeader;
        private string originalPathInHeader;
        private void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                this.InitiateRenameUI((TreeViewItem)mi.Tag);
            }
        }

        private void InitiateRenameUI(TreeViewItem tvi)
        {
            if (tvi == null)
            {
                return;
            }
            Node node = (Node)tvi.Tag;

            if (false == node.FolderExists)
            {
                // If the folder doesn't exists, flash the node and don't start the editing operation
                StackPanel sp = (StackPanel)tvi.Header;
                foreach (UIElement child in sp.Children)
                {
                    if (child is TextBlock headerTextBlock)
                    {
                        this.Flash(headerTextBlock);
                        break;
                    }
                }
                return;
            }

            GeneralTransform myTransform = tvi.TransformToAncestor(this.Canvas);
            Point myOffset = myTransform.Transform(new Point(0, 0));
            this.TextBoxEditNode.Visibility = Visibility.Visible;
            this.TextBoxEditNode.Text = node.Name;
            this.TextBoxEditNode.SetValue(Canvas.TopProperty, myOffset.Y - 1);
            this.TextBoxEditNode.SetValue(Canvas.LeftProperty, myOffset.X + 33);
            this.TextBoxEditNode.Tag = tvi;
            this.textBoxEditNodeMinWidth = -1;
            this.originalTextInHeader = node.Name;
            this.originalPathInHeader = node.Path;
        }

        private void TextBoxEditNode_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (this.textBoxEditNodeMinWidth < 0)
                {
                    textBoxEditNodeMinWidth = tb.ActualWidth;
                    tb.MinWidth = textBoxEditNodeMinWidth;
                }
            }
        }

        // Inhibit disallowed characters from a potential file name
        private void TextBoxEditNode_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox tb)
            {
                char[] characters = e.Text.ToCharArray();
                foreach (char c in characters)
                {
                    if (invalidChars.Contains(c))
                    {
                        this.Flash(tb);
                        e.Handled = true;
                    }
                }
            }
        }

        private void TextBoxEditNode_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (e.Key == Key.Escape)
                {
                    tb.Visibility = Visibility.Collapsed;
                }
                else if (e.Key == Key.Return || e.Key == Key.Enter)
                {
                    tb.Visibility = Visibility.Collapsed;
                    tb.Text = tb.Text.Trim();
                    if (tb.Text == this.originalTextInHeader)
                    {
                        // If the text is the same, do nothing
                        return;
                    }

                    if (tb.Text.EndsWith("."))
                    {
                        this.DisplayFeedback("Rename cancelled", "Windows does not allow folder and file names to end with a '.'");
                    }
                    else if (this.invalidFileNames.Contains(tb.Text, StringComparer.OrdinalIgnoreCase))
                    {
                        this.DisplayFeedback("Rename cancelled", $"Windows does not allow '{tb.Text}' as a folder name as it is a reserved name.");
                    }
                    else if (string.IsNullOrWhiteSpace(tb.Text))
                    {
                        TreeViewItem tvi = (TreeViewItem)tb.Tag;
                        StackPanel sp = (StackPanel)tvi.Header;
                        foreach (UIElement child in sp.Children)
                        {
                            if (child is TextBlock headerTextBlock)
                            {
                                this.Flash(headerTextBlock);
                                break;
                            }
                        }
                    }
                    else
                    {
                        this.ChangeName(tb.Text, (TreeViewItem)tb.Tag);
                    }
                }
            }
        }

        // If the user does certain actions (e.g., clicking)
        // cancel the textBoxEditNode by turning its visibility off (in case its open for editing)
        private void TreeViewItem_Cancel(object sender, object e)
        {
            this.TextBoxEditNode.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region UI: Moving a folder (DragDrop interaction)
        // Disallow drag and drop of a non-node source onto the Canvas
        private void Canvas_DragOver(object sender, DragEventArgs e)
        {
            if (draggedItem == null || false == draggedItem.Tag is Node)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        // Emphasise the dragged over node if its a drop target
        private void TreeViewItem_DragEnter(object sender, DragEventArgs e)
        {
            TreeViewItem item = GetNearestContainer(e.OriginalSource as UIElement);
            if (IsValidDropTarget(draggedItem, item))
            {
                item.Background = Brushes.PaleTurquoise;
            }
        }

        // Unemphasise the dragged over node as needed 
        private void TreeViewItem_DragLeave(object sender, DragEventArgs e)
        {
            TreeViewItem item = GetNearestContainer(e.OriginalSource as UIElement);
            if (IsValidDropTarget(draggedItem, item))
            {
                item.Background = Brushes.White;
            }
        }

        private void TreeViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point currentPosition = e.GetPosition(this.TreeView);

                    if ((Math.Abs(currentPosition.X - _lastMouseDown.X) > 10.0) ||
                        (Math.Abs(currentPosition.Y - _lastMouseDown.Y) > 10.0))
                    {
                        // As we are likely dragging rather than initiating a rename sequence, stop the RenameItemUITimer.
                        RenameItemUITimer.Stop();

                        draggedItem = (TreeViewItem)this.TreeView.SelectedItem;
                        if (draggedItem != null)
                        {
                            DragDropEffects finalDropEffect = DragDrop.DoDragDrop(this.TreeView, this.TreeView.SelectedValue, DragDropEffects.Move);
                            // Checking target is not null and item is dragging(moving)
                            if (finalDropEffect == DragDropEffects.Move && _target != null)
                            {
                                // A Move drop was accepted
                                string draggedItemText = ((Node)draggedItem.Tag).Path;
                                string targetItemText = ((Node)_target.Tag).Path;
                                if (!draggedItemText.Equals(targetItemText))
                                {
                                    this.MoveDraggedItem(draggedItem, _target);
                                    _target = null;
                                    draggedItem = null;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Show dragover feedback only if the target is a valid drop target
        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                Point currentPosition = e.GetPosition(this.TreeView);
                if ((Math.Abs(currentPosition.X - _lastMouseDown.X) > 10.0) ||
                    (Math.Abs(currentPosition.Y - _lastMouseDown.Y) > 10.0))
                {
                    // Verify that this is a valid drop and then store the drop target
                    TreeViewItem item = GetNearestContainer(e.OriginalSource as UIElement);

                    TreeViewAutoScrollDuringDrag(currentPosition.Y);
                    if (IsValidDropTarget(draggedItem, item))
                    {
                        e.Effects = DragDropEffects.Move;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                        item.Background = Brushes.White;
                    }
                }
                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }

        private void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;

                // Verify that this is a valid drop and then store the drop target
                TreeViewItem TargetItem = GetNearestContainer(e.OriginalSource as UIElement);
                if (TargetItem != null && draggedItem != null)
                {
                    _target = TargetItem;
                    e.Effects = DragDropEffects.Move;
                }
            }
            catch (Exception)
            {
            }
        }

        // Return false if we are trying to drag and drop a non-node source onto a destination.
        private bool IsValidDropTarget(TreeViewItem _sourceItem, TreeViewItem _targetItem)
        {
            if (_sourceItem == null || false == _sourceItem.Tag is Node || _targetItem == null)
            {
                return false;
            }

            //Check whether the destination is a subfolder of the source,
            // as we shouldn't be able to move a folder into its subfolder
            string destPath = ((Node)_targetItem.Tag).Path;
            string sourcePath = ((Node)_sourceItem.Tag).Path;
            string sourceName = ((Node)_sourceItem.Tag).Name;

            if (destPath.Equals(this.GetParent(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check and disallow if dragging node to a node that has a child node of the same name
            foreach (KeyValuePair<string, Node> kvp in ((Node)_targetItem.Tag).Children)
            {
                if (kvp.Key.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (false == destPath.StartsWith(sourcePath))
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Internal: NewFolder
        // If the new folder already exists, we seem to be moving it... Or is it being added?
        private void CreateNewFolder(TreeViewItem tvi)
        {
            // Get the node corresponding to that TreeViewItem
            Node node = (Node)tvi.Tag;
            // Ignore this if the node is null
            if (node == null)
            {
                return;
            }
            PathList newPathList = new PathList();
            string newFolderPath = Path.Combine(node.Path, this.NewFolder);
            if (this.MyPathList.Items.Any(p => p.Path.Equals(newFolderPath, StringComparison.OrdinalIgnoreCase)))
            {
                this.DisplayFeedback("The new folder was not created", $"A folder called {newFolderPath} already exists.");
                return;
            }

            // Create a deep copy of the current path list in case there is an error, as this will let us restore 
            // things to their original state
            PathList originalPathList = this.MyPathList.DeepClone();

            if (string.IsNullOrWhiteSpace(node.Path))
            {
                // We are adding it to the Root Folder, so insert it at the beginning
                // The new folder does not contain any photos
                newPathList.Items.Add(new PathItem(this.NewFolder, false));
                newPathList.Items.AddRange(this.MyPathList.Items);
            }
            else
            {
                // Insert the new path in the correct place in the list i.e., just before the current item
                bool inserted = false;
                int index = 0;
                foreach (PathItem item in MyPathList.Items)
                {
                    string path = item.Path;
                    bool containsPhotos = item.ContainsPhotos;
                    newPathList.Items.Add(new PathItem(path, containsPhotos));
                    if (inserted == false && path.StartsWith(node.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        // The new folder does not contain any photos
                        newPathList.Items.Insert(index, new PathItem(Path.Combine(node.Path, this.NewFolder), false));
                        inserted = true;
                    }
                    index++;
                }
            }
            this.MyPathList = newPathList;

            CreateSubfolderResultEnum result = ExternalCreateNewFolder(node.Path, this.NewFolder);
            if (result != CreateSubfolderResultEnum.Success && result != CreateSubfolderResultEnum.FailAsDestinationFolderExists)
            {
                Dialog.Dialogs.RenameRelativePathError(this.ParentDialogWindow, result, node.Path, this.NewFolder);
                // Restore the tree etc to its pre-renamed form
                this.MyPathList = originalPathList;
            }
            this.RebuildTree();
        }
        #endregion

        #region Internal: Rename items
        private void ChangeName(string newName, TreeViewItem tvi)
        {
            bool isInteriorNode = false;


            // Get the node corresponding to that TreeViewItem
            Node node = (Node)tvi.Tag;

            // Ignore this if the node is null
            if (node == null)
            {
                return;
            }

            // Create a deep copy of the current path list in case there is an error, as this will let us restore 
            // things to their original state
            PathList originalPathList = this.MyPathList.DeepClone();

            string newString = ReplaceLastOccurrence(node.Path, node.Name, newName);

            // We don't allow renaming if:
            // - the new path portion (as delimited with a \) matches an existing path portion, as that would be a duplicate
            // - the complete path matches an existing complete path portion, as that would also be a duplicate

            // Create a list where its paths don't begin with the exactly matching original path item
            List<PathItem> tmpPathItemListWithoutMatchingPath = new List<PathItem>();
            foreach (PathItem pathItem in this.MyPathList.Items)
            {
                if (pathItem.Path.StartsWith(originalPathInHeader + this.charSeparator)
                    || pathItem.Path.Equals(originalPathInHeader))
                {
                    continue;
                }
                tmpPathItemListWithoutMatchingPath.Add(pathItem);
            }

            // Check to see if there are other paths/nodes partialy or wholey matching the original path item. If so, we shouldn't copy it.
            bool isMatchToOtherPath = tmpPathItemListWithoutMatchingPath.Any(s => s.Path.StartsWith(newString + this.charSeparator, StringComparison.OrdinalIgnoreCase))
                                      || tmpPathItemListWithoutMatchingPath.Any(s => s.Path.Equals(newString, StringComparison.OrdinalIgnoreCase));
            if (isMatchToOtherPath)
            {
                this.DisplayFeedback("Rename cancelled", $"A path with that name already exists: {newString}");
                return;
            }

            // If its under the Root folder, we have to special case it as there is no leading path to make it different from other instances
            // of the root folder
            if (newString.IndexOf(this.charSeparator) < 0)
            {
                PathList newPathList = new PathList();
                foreach (PathItem item in MyPathList.Items)
                {
                    if (item.Path.StartsWith(node.Path + this.charSeparator, StringComparison.OrdinalIgnoreCase) || item.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        // this is a renamed item
                        newPathList.Items.Add(new PathItem(this.ReplaceFirstOccurrence(item.Path, node.Path, newString), item.ContainsPhotos));
                    }
                    else
                    {
                        // this is an unchanged item
                        newPathList.Items.Add(new PathItem(item.Path, item.ContainsPhotos));
                    }


                }
                this.MyPathList = newPathList;
            }
            else
            {
                // Change the elements as needed in the path list to reflect the new path 
                PathList newPathList = new PathList();
                foreach (PathItem item in MyPathList.Items)
                {
                    string path = item.Path;
                    bool containsPhotos = item.ContainsPhotos;
                    newPathList.Items.Add(new PathItem(path.Replace(node.Path, newString), containsPhotos));
                }
                this.MyPathList = newPathList;
            }


            // Check to see if the original node was an interior node (i.e., path prefix). If its a '/' terminating prefix for even one of the paths, then it must be
            // This could be done in one of the other existing loops for efficiencys, but its easier this way
            foreach (PathItem item in originalPathList.Items)
            {
                if (item.Path.StartsWith(node.Path + this.charSeparator))
                {
                    isInteriorNode = true;
                    break;
                }
            }

            // Rebuild the tree by populating it.
            //this.RebuildTree();

            // Now try to actually rename the folder, both in the folder structure and the database
            MoveFolderResultEnum result = ExternalRenameFolder(node.Path, newString, isInteriorNode);
            if (result != MoveFolderResultEnum.Success)
            {
                Dialog.Dialogs.RenameRelativePathError(this.ParentDialogWindow, result, node.Path, newString);
                // Restore the tree etc to its pre-renamed form
                this.MyPathList = originalPathList;
                // this.RebuildTree();
            }
            this.RebuildTree();
        }
        #endregion

        #region Internal: Moving items
        private void MoveDraggedItem(TreeViewItem _sourceItem, TreeViewItem _targetItem)
        {
            try
            {
                Node sourceNode = (Node)_sourceItem.Tag;
                Node targetNode = (Node)_targetItem.Tag;
                this.MoveRelativePath(sourceNode.Path, sourceNode.Name, targetNode.Path);
                this.RebuildTree();
            }
            catch
            {
            }
        }

        private void MoveRelativePath(string sourcePath, string sourceName, string destinationPath)
        {
            string newPath = Path.Combine(destinationPath, sourceName);
            List<PathItem> newRelativePathItemList = new List<PathItem>();
            List<PathItem> alteredPathItemList = new List<PathItem>();
            List<PathItem> unalteredPathItemList = new List<PathItem>();

            bool isSourceInteriorNode = false;
            // Create a deep copy of the current path list in case there is an error, as this will let us restore 
            // things to their original state
            PathList originalPathList = this.MyPathList.DeepClone();

            // Check to see if the original node was an interior node (i.e., path prefix). If its a '/' terminating prefix for even one of the paths, then it must be
            // This could be done in one of the other existing loops for efficiencys, but its easier this way
            foreach (PathItem item in originalPathList.Items)
            {
                if (item.Path.StartsWith(sourcePath + this.charSeparator))
                {
                    isSourceInteriorNode = true;
                    break;
                }
            }

            // Create two lists:
            // - alteredPathItemList is a list containing only those paths that were altered
            // - unalteredPathItemList is a list containing only those paths that were not altered
            // We do this so we can insert the altered paths into a location that makes sense when displaying the tree
            foreach (PathItem item in MyPathList.Items)
            {
                if (item.Path.StartsWith(sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    string newEntry = this.ReplaceFirstOccurrence(item.Path, sourcePath, newPath);
                    alteredPathItemList.Add(new PathItem(newEntry, item.ContainsPhotos));
                }
                else
                {
                    unalteredPathItemList.Add(item);
                }
            }

            bool alteredPathItemListAdded = false;
            foreach (PathItem item in unalteredPathItemList)
            {
                if (alteredPathItemListAdded == false && item.Path.StartsWith(destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    newRelativePathItemList.AddRange(alteredPathItemList);
                    alteredPathItemListAdded = true;
                }
                newRelativePathItemList.Add(item);
            }

            // If we have an 'orphanded' subtree (e.g., a/b where we move b but a has no photos so there is no 'a' in the list)
            // put it in the list as a new list it with no photos in it.
            string parent = this.GetParent(sourcePath);
            if (false == string.IsNullOrEmpty(parent) && false == newRelativePathItemList.Any(s => s.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
            {
                newRelativePathItemList.Add(new PathItem(parent, false));
            }

            this.MyPathList.Items = newRelativePathItemList;
            // To move a folder into a destination folder: create a new destination folder path comprising the destination path and the source folder name
            string destinationFolderPathAndFolderName = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
            MoveFolderResultEnum result = this.ExternalMoveFolderIntoFolder(sourcePath, destinationFolderPathAndFolderName, isSourceInteriorNode);
            if (result != MoveFolderResultEnum.Success)
            {
                Dialog.Dialogs.RenameRelativePathError(this.ParentDialogWindow, result, sourcePath, destinationPath);
                // Restore the tree etc to its pre-renamed form
                this.MyPathList = originalPathList;
                // this.RebuildTree();
            }
            this.RebuildTree();
        }
        #endregion

        #region External: CreateNewFolder
        private CreateSubfolderResultEnum ExternalCreateNewFolder(string folderPath, string folderName)
        {
            CreateSubfolderResultEnum result = FilesFolders.TryCreateSubfolderInFolder(Path.Combine(FileDatabase.FolderPath, folderPath), folderName);
            if (result == CreateSubfolderResultEnum.Success)
            {
                // No need to update the database, as Timelapse will not have a record of images associated with it
                this.WereEditsMade = true;
            }
            return result;
        }
        #endregion

        #region External:RenameFolder
        private MoveFolderResultEnum ExternalRenameFolder(string oldFolderPath, string newFolderPath, bool isInteriorNode)
        { ;
            MoveFolderResultEnum result = FilesFolders.TryMoveFolderIfExists(
                Path.Combine(FileDatabase.FolderPath, oldFolderPath),
                Path.Combine(FileDatabase.FolderPath, newFolderPath));
            if (result == MoveFolderResultEnum.Success)
            {
                // We assume we can always update the database with no errors. 
                // It would be good if we could get an error code back!
                this.FileDatabase.RelativePathReplacePrefix(oldFolderPath, newFolderPath, isInteriorNode);
                this.WereEditsMade = true;
            }
            return result;
        }
        #endregion

        #region External:MoveFolder
        MoveFolderResultEnum ExternalMoveFolderIntoFolder(string sourceFolderPath, string destinationFolderPath, bool isInteriorNode)
        {
            MoveFolderResultEnum result = FilesFolders.TryMoveFolderIfExists(
                Path.Combine(FileDatabase.FolderPath, sourceFolderPath),
                Path.Combine(FileDatabase.FolderPath, destinationFolderPath));
            if (result == MoveFolderResultEnum.Success)
            {
                // We assume we can always update the database with no errors. 
                // It would be good if we could get an error code back!
                this.FileDatabase.RelativePathReplacePrefix(sourceFolderPath, destinationFolderPath, isInteriorNode);
                this.WereEditsMade = true;
            }
            return result;
        }
        #endregion

        #region Treeview and Visual Helper Utilities
        // Walk up the element tree to the nearest tree view item.
        private TreeViewItem GetNearestContainer(UIElement element)
        {
            TreeViewItem container = element as TreeViewItem;
            while (container == null && element != null)
            {
                element = VisualTreeHelper.GetParent(element) as UIElement;
                container = element as TreeViewItem;
            }
            return container;
        }

        private void TreeViewAutoScrollDuringDrag(double verticalPos)
        {
            ScrollViewer sv = FindVisualChild<ScrollViewer>(this.TreeView);
            if (sv == null)
            {
                return;
            }
            double tolerance = 10;
            double offset = 7;

            if (verticalPos < tolerance) // Top of visible list?
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - offset); //Scroll up.
            }
            else if (verticalPos > this.TreeView.ActualHeight - tolerance) //Bottom of visible list?
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset + offset); //Scroll down.    
            }
        }

        // REPLACE THIS WITH TIMELAPSE ONE
        public static childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            // Search immediate children first (breadth-first)
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child is childItem item)
                {
                    return item;
                }

                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);

                    if (childOfChild != null)
                        return childOfChild;
                }
            }

            return null;
        }
        #endregion

        #region String manipulations
        // Utility: Replace the last occurrence the oldSubstring with the newSubString in the source string
        // eg., ReplaceLastOccurence(@"Foo\Bar\Butt\Bar", "Bar", "XXX") -> FooBarButtXXX
        private string ReplaceLastOccurrence(string sourceString, string oldSubString, string newSubString)
        {
            int place = sourceString.LastIndexOf(oldSubString, StringComparison.OrdinalIgnoreCase);
            if (place == -1)
                return sourceString;
            // TODO: MAKE CASE INSENSITIVE
            return sourceString.Remove(place, oldSubString.Length).Insert(place, newSubString);
        }
        private string ReplaceFirstOccurrence(string sourceString, string oldSubString, string newSubString)
        {
            int place = sourceString.IndexOf(oldSubString, StringComparison.InvariantCulture);
            if (place == -1)
                return sourceString;
            // TODO: MAKE CASE INSENSITIVE
            return sourceString.Remove(place, oldSubString.Length).Insert(place, newSubString);
        }

        private string GetParent(string path)
        {
            string parentPath = string.Empty;
            int index = path.LastIndexOf(this.charSeparator);
            if (index >= 0)
            {
                parentPath = path.Substring(0, index);
                if (false == string.IsNullOrEmpty(parentPath))
                {
                    return parentPath;
                }
            }
            return parentPath;
        }

        #endregion

        #region Flash animations
        private void Flash(TextBox tb)
        {
            if (tb == null)
            {
                return;
            }
            ColorAnimation animation = GetFlashAnimation();
            tb.Background = new SolidColorBrush(Colors.White);
            tb.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void Flash(TextBlock tb)
        {
            if (tb == null)
            {
                return;
            }
            ColorAnimation animation = GetFlashAnimation();
            tb.Background = new SolidColorBrush(Colors.White);
            tb.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private ColorAnimation GetFlashAnimation()
        {
            return new ColorAnimation
            {
                To = Colors.Red,
                Duration = new Duration(TimeSpan.FromSeconds(0.1)),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1)
            };
        }
        #endregion

        #region Dialogs
        private void DisplayFeedback(string title, string reason)
        {
           new Dialog.MessageBox(title, this.ParentDialogWindow)
            {
                Message =
                {
                    Title = title,
                    Reason = reason,
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog(); 
        }
        #endregion

        #region Node class
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
            public string Name { get; private set; } = string.Empty;
            public string Path { get; private set; } = string.Empty;
            public bool ContainsPhotos { get; private set; }
            public bool FolderExists { get; set; }

            private readonly char[] charSeparators = new char[] { '\\' };
            public void AddPath(PathItem pathItem)
            {
                string path = pathItem.Path;
                bool containsPhotos = pathItem.ContainsPhotos;
                // Split the path into its constituent names e.g., a\b would be a, b
                string[] names = pathItem.Path.Split(this.charSeparators, StringSplitOptions.RemoveEmptyEntries);

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
                        childNode = new Node
                        {
                            Name = name,
                            Path = pathSoFar,
                            ContainsPhotos = containsPhotos && pathSoFar == path
                        };
                        currentNode.Children[name] = childNode;
                    }
                    // The name already exists, so lets go to that node
                    currentNode = childNode;

                    // If the path is the same, update the currentPhotos attribute to match the original PathItem
                    if (pathItem.Path.Equals(pathSoFar, StringComparison.OrdinalIgnoreCase))
                    {
                        currentNode.ContainsPhotos = containsPhotos;
                    }
                }
            }
        }
        #endregion

        #region PathList and PathItem Classes

        private class PathItem
        {
            public string Path { get; }
            public bool ContainsPhotos { get; }
            public PathItem(string path, bool containsPhotos)
            {
                Path = path;
                ContainsPhotos = containsPhotos;
            }
        }

        private class PathList
        {
            public List<PathItem> Items { get; set; }

            public PathList()
            {
                this.Items = new List<PathItem>();
            }

            public void OrderInPlace()
            {
                this.Items = this.Items.OrderBy(s => s.Path).ToList();
            }

            public PathList DeepClone()
            {
                PathList clonedPathList = new PathList();
                foreach (PathItem item in this.Items)
                {
                    clonedPathList.Items.Add(new PathItem(item.Path, item.ContainsPhotos));
                }
                return clonedPathList;
            }
        }
        #endregion
    }
}
