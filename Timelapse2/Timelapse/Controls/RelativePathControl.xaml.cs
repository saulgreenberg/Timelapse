using System;
using System.Collections.Generic;
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
        #region Constants
        private readonly char charSeparator = '\\';
        private readonly string RootFolderName = "Root folder";
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

        private string RootFolder => FileDatabase.FolderPath;

        public Window ParentDialogWindow { get; set; }

        // Whether or not a user has made any edits to the folder structure
        public bool WereEditsMade { get; set; } = false;
        #endregion

        #region Variables
        // When triggered, this timer indicates a Rename operation.
        // Usually trigger if the user left-clicks and hold the mouse down on a tree view item for a given duration.
        private readonly DispatcherTimer TimerRenameItemAfterExtendedMousePress = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(0.75)
        };

        // A list of the relative paths contained by the Timelapse database, passed into the control.
        // As its in the database, each of these complete relative paths must be associated with one or more images. 
        private List<string> RelativePaths = new List<string>();

        // PathList will eventually contain an entry for each subfolder (and entries for each of its subfolders etc) under the root folder
        // As only the complete paths (not path portions) in the RelativePaths list are associated with images, we can distinguish  
        // them from other paths which are not associated with Timelapse images.
        private PathList MyPathList = new PathList();

        // For recording mouse down and dragging state during certain click and drag and drop operations
        private Point _lastMouseDown;
        private TreeViewItem draggedItem, _target;

        // A list of buttons that we can temporarily disable while actively renaming
        private List<Button> Buttons;
        #endregion

        #region Constructors and Loading
        public RelativePathControl()
        {
            InitializeComponent();
            TimerRenameItemAfterExtendedMousePress.Tick += TimerRenameItemAfterExtendedMousePress_Tick;
        }

        private void RelativePathControl_OnLoaded(object sender, RoutedEventArgs e)
        {
        }
        #endregion

        #region Public Methods
        // Given a list of relative paths, build the tree from it and display it.
        public void Initialize(Window owner, FileDatabase fileDatabase, List<Button> buttons)
        {
            if (null == fileDatabase)
            {
                // Can't really do anything, so display that.
                TreeViewItem tvi = new TreeViewItem { Header = "No files appear to be loaded" };
                TreeView.Items.Add(tvi);
                return;
            }

            this.FileDatabase = fileDatabase;
            this.ParentDialogWindow = owner;
            this.Buttons = buttons;

            // Get the relative paths from the database
            this.RelativePaths = this.FileDatabase.GetRelativePaths();

            // Build the initial relative path list from the relative paths
            // These are the ones that Timelapse associates with images, so containsImages will be true.
            foreach (string path in this.RelativePaths)
            {
                this.MyPathList.Items.Add(new PathItem(path, true));
            }

            // Now build addition paths based on the parent subfolders found in each relativePath.
            // If they are not present in the path list, then we know that these are folders that Timelapse does not associate with photos
            PathList pathListKnownToTimelapse = new PathList();
            foreach (PathItem item in this.MyPathList.Items)
            {
                pathListKnownToTimelapse.Items.Add(item);
                string parent = this.GetParent(item.Path);
                while (parent != string.Empty)
                {
                    if (false == pathListKnownToTimelapse.Items.Any(s =>
                            s.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
                    {
                        pathListKnownToTimelapse.Items.Add(new PathItem(parent, false));
                    }

                    parent = this.GetParent(parent);
                }
            }

            this.MyPathList.Items.AddRange(pathListKnownToTimelapse.Items);
            // At this point, we should have a pathlist that distinguishes :
            // - all relative paths and their component paths that are somehow covered in the Timelapse Database 
            // - whether a particular complete path contains or does not contain images known to Timelapse

            // Now, lets add those folders (if any) that are under the root folder but not currently known to the Timelapse Database
            // We do this by getting all folders, and adding those that are not in the my PathList.
            List<string> physicalFolders = new List<string>();
            FilesFolders.GetAllFoldersExceptBackupAndDeletedFolders(this.RootFolder, physicalFolders,
                this.FileDatabase.FolderPath);
            foreach (string folder in physicalFolders)
            {
                if (this.MyPathList.Items.Any(s => s.Path.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                MyPathList.Items.Add(new PathItem(folder, false));
            }

            // Finally, ensure that we sort the paths, as otherwise edited items will appear in the wrong place
            this.RebuildTree(true);
        }
        #endregion

        #region Build the Node and TreeView structures
        // Rebuild everything from scratch. This populates the node structure and the treeView with the items in the MyPathList
        public void RebuildTree(bool sortTree = false)
        {
            if (null == FileDatabase)
            {
                return;
            }

            if (sortTree)
            {
                // Sort the tree if direccted to do so
                this.MyPathList.OrderInPlace();
            }

            // Create the initial Root Folder node.
            // This node is a special case, where we independently have to check and set
            // - if the root folder exists(it should always exist)
            // - if it containsImages (indicated by an "" in the list) 
            bool rootNodeContainsPhotos = this.MyPathList.Items.Any(s => string.IsNullOrWhiteSpace(s.Path));
            Node rootNode = new Node
            {
                FolderExists = Directory.Exists(this.RootFolder),
                ContainsPhotos = rootNodeContainsPhotos,
            };

            // Add the nodes to it to mirror the PathList correctly as children as needed
            // - AddPath breaks down a path into its components (i.e., strings separated by '\') to children sub-nodes as well
            //   - For example, if a path is a/b/c, nodes would be created for a, a/b, a/b/c
            //   - AddPath also checks to see if that path or path component was previously created, and if so it does not duplicate it.
            foreach (PathItem nodePathItem in this.MyPathList.Items)
            {
                // Add the nodes to the root node to mirror the PathList hierarchy
                rootNode.AddPath(nodePathItem);
            }

            // Create the initial TreeViewItem representing the root folder.
            StackPanel sp =
                CreateTreeViewItemHeaderAsStackPanel(this.RootFolderName, rootNodeContainsPhotos,
                    rootNode.FolderExists);
            TreeViewItem tvi = new TreeViewItem
            {
                Header = sp,
                IsExpanded = true,
                Tag = rootNode,
            };

            // Create a special context menu for the Root folder that doesn't include the Rename item,
            // as we don't allow the root folder to be renamed
            tvi.ContextMenu = TreeViewItemCreateContextMenu(tvi, rootNode.FolderExists, rootNode.ContainsPhotos);

            // Add children to the root TreeViewItem to match the node structure hierarchy
            this.TraverseNodeAndAddToTreeViewItem(tvi, rootNode);

            // Clear the treeView, in case it has been previously populated;
            // and add the (now populated) TreeViewItem to the treeView
            this.TreeView.Items.Clear();
            this.TreeView.Items.Add(tvi);
        }

        // Given a node and a treeViewItem, recursively traverse this node's hierarchy
        // and populate a treeview item to reflect each node and its position in the hierarchy.
        private void TraverseNodeAndAddToTreeViewItem(TreeViewItem currentTvi, Node node)
        {
            TreeViewItem tvi;
            if (node.Name != string.Empty)
            {
                // This node is a child item as the path isn't empty
                // So we need to create a new TreeViewItem representing that path, which will be added as a child the existing TreeViewItem
                // The tag is used to associate a node with its respective TreeViewItem
                node.FolderExists = Directory.Exists(Path.Combine(this.RootFolder, node.Path));
                StackPanel sp =
                    this.CreateTreeViewItemHeaderAsStackPanel(node.Name, node.ContainsPhotos, node.FolderExists);
                tvi = new TreeViewItem
                {
                    Header = sp,
                    Tag = node
                };
                tvi.ContextMenu = TreeViewItemCreateContextMenu(tvi, node.FolderExists, true);
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
                TraverseNodeAndAddToTreeViewItem(tvi, child);
            }

            tvi.IsExpanded = true;
        }
        #endregion

        #region UI interactions: Initiate Rename Action after a mouse-left hold for a particular timer duration
        // Initiate renaming when an item is selected.
        // Rename is invoked when the mouse is held down for more than the TimerRenameItemAfterExtendedMousePress's duration
        // Rename initiation is cancelled if the mouse is moved more than a certain amount (see TreeViewItem_MouseMove, which stops the timer)
        private void TreeViewItem_Selected(object Sender, RoutedEventArgs e)
        {
            this.TimerRenameItemAfterExtendedMousePress.Start();
        }

        private void TreeViewItem_MouseUp(object sender, MouseEventArgs e)
        {
            // Cancel Rename initiation if a mouse up is detected before the rename operation was invokde.
            this.TimerRenameItemAfterExtendedMousePress.Stop();
            if (this.TreeView.SelectedItem is TreeViewItem tvi)
            {
                tvi.IsSelected = false;
            }
        }

        // Enable renaming:
        // - A mouse down on a selected item occured for more than the TimerRenameItemAfterExtendedMousePress's duration
        private void TimerRenameItemAfterExtendedMousePress_Tick(object sender, EventArgs e)
        {
            TreeViewItem selectedItem = (TreeViewItem)this.TreeView.SelectedItem;
            TimerRenameItemAfterExtendedMousePress.Stop();
            this.InitiateRenameUI(selectedItem);
        }
        #endregion

        #region UI interactions: Rename TextBox
        private double textBoxEditNodeMinWidth = -1;
        private string originalTextInHeader;
        private string originalPathInHeader;

        // Initiate renaming:
        // - position and size the rename texbox atop the node
        // - set its value and visibility, 
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
                // Note that other error checking means that we really should never get to this point.
                this.Flash(tvi);
                return;
            }

            // Bring the tree view item into view. If that requires scrolling, we don't want the scroll change event to fire.
            // Afterwards, we do want it to fire as then it will handle positioning the TextBoxEditNode as it is scrolled,
            // and cancelling the edit if it is scrolled out of view..
            this.ScrollViewerActivateScrollChangedEvent(false);
            tvi.BringIntoView(new Rect(new Size(10, 40)));
            this.ScrollViewerActivateScrollChangedEvent(true);

            this.TextBoxPositionOverTreeViewItem(tvi);
            this.TextBoxEditNode.Visibility = Visibility.Visible;
            this.TextBoxEditNode.Text = node.Name;
            this.TextBoxEditNode.Tag = tvi;
            this.textBoxEditNodeMinWidth = -1;
            this.TextBoxEditNode.BorderThickness = new Thickness(2);
            this.TextBoxEditNode.CaretIndex = node.Path.Length;
            this.TextBoxEditNode.Focus();
            this.originalTextInHeader = node.Name;
            this.originalPathInHeader = node.Path;

            this.RenameStarted();
        }

        private void TextBoxPositionOverTreeViewItem(TreeViewItem tvi)
        {
            // Note that we associate the tag with its corresponding TreeViewItem
            GeneralTransform myTransform = tvi.TransformToAncestor(this.Canvas);
            Point myOffset = myTransform.Transform(new Point(0, 0));

            this.TextBoxEditNode.SetValue(Canvas.TopProperty, myOffset.Y - 1);
            this.TextBoxEditNode.SetValue(Canvas.LeftProperty, myOffset.X + 33);
        }

        // Whenever the textbox is edited, adjust its width to ensure that its never less than its original width
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
                    this.Flash((TreeViewItem)tb.Tag);
                    this.RenameCompleted();
                }
                else if (e.Key == Key.Return || e.Key == Key.Enter)
                {
                    this.RenameCompleted();
                    tb.Text = tb.Text.Trim();
                    if (tb.Text == this.originalTextInHeader)
                    {
                        // If the text is the same, do nothing
                        return;
                    }

                    if (tb.Text.EndsWith("."))
                    {
                        this.RenameCompleted();
                        this.DisplayFeedback("Rename cancelled",
                            "Windows does not allow folder and file names to end with a '.'");

                    }
                    else if (this.invalidFileNames.Contains(tb.Text, StringComparer.OrdinalIgnoreCase))
                    {
                        this.RenameCompleted();
                        this.DisplayFeedback("Rename cancelled",
                            $"Windows does not allow '{tb.Text}' as a folder name as it is a reserved name.");
                    }
                    else if (string.IsNullOrWhiteSpace(tb.Text))
                    {
                        this.Flash((TreeViewItem)tb.Tag);
                    }
                    else
                    {
                        this.RenameCompleted();
                        this.ChangeName(tb.Text, (TreeViewItem)tb.Tag);
                    }
                }
            }
            // If we get here, then the user is still editing the  name
        }

        // If the user does certain actions (e.g., clicking outside the textBox)
        // cancel the textBoxEditNode by turning its visibility off (in case its open for editing)
        private void TreeViewItem_Cancel(object sender, object e)
        {
            this.RenameCompleted();
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
                        // As we are likely dragging rather than initiating a rename sequence, stop the TimerRenameItemAfterExtendedMousePress.
                        TimerRenameItemAfterExtendedMousePress.Stop();

                        draggedItem = (TreeViewItem)this.TreeView.SelectedItem;
                        if (draggedItem != null)
                        {
                            DragDropEffects finalDropEffect = DragDrop.DoDragDrop(this.TreeView,
                                this.TreeView.SelectedValue, DragDropEffects.Move);
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
                this.DisplayFeedback("The new folder was not created",
                    $"A folder called {newFolderPath} already exists.");
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
                    bool containsPhotos = item.ContainsImages;
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
            if (result != CreateSubfolderResultEnum.Success &&
                result != CreateSubfolderResultEnum.FailAsDestinationFolderExists)
            {
                Dialog.Dialogs.RenameRelativePathError(this.ParentDialogWindow, result, node.Path, this.NewFolder);
                // Restore the tree etc to its pre-renamed form
                this.MyPathList = originalPathList;
            }

            this.RebuildTree();

            // Find the just created 'New folder' tree view item in the tree view
            TreeViewItem foundTvi = TreeViewFindMatchingPath(this.TreeView, Path.Combine(node.Path, this.NewFolder));
            if (foundTvi != null)
            {
                this.Flash(foundTvi);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.Flash(foundTvi);
                    this.InitiateRenameUI(foundTvi);
                }), DispatcherPriority.ContextIdle, null);
            }
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
            bool isMatchToOtherPath = tmpPathItemListWithoutMatchingPath.Any(s =>
                                          s.Path.StartsWith(newString + this.charSeparator,
                                              StringComparison.OrdinalIgnoreCase))
                                      || tmpPathItemListWithoutMatchingPath.Any(s =>
                                          s.Path.Equals(newString, StringComparison.OrdinalIgnoreCase));
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
                    if (item.Path.StartsWith(node.Path + this.charSeparator, StringComparison.OrdinalIgnoreCase) ||
                        item.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        // this is a renamed item
                        newPathList.Items.Add(new PathItem(this.ReplaceFirstOccurrence(item.Path, node.Path, newString),
                            item.ContainsImages));
                    }
                    else
                    {
                        // this is an unchanged item
                        newPathList.Items.Add(new PathItem(item.Path, item.ContainsImages));
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
                    bool containsPhotos = item.ContainsImages;
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
                    alteredPathItemList.Add(new PathItem(newEntry, item.ContainsImages));
                }
                else
                {
                    unalteredPathItemList.Add(item);
                }
            }

            bool alteredPathItemListAdded = false;
            foreach (PathItem item in unalteredPathItemList)
            {
                if (alteredPathItemListAdded == false &&
                    item.Path.StartsWith(destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    newRelativePathItemList.AddRange(alteredPathItemList);
                    alteredPathItemListAdded = true;
                }

                newRelativePathItemList.Add(item);
            }

            // If we have an 'orphanded' subtree (e.g., a/b where we move b but a has no photos so there is no 'a' in the list)
            // put it in the list as a new list it with no photos in it.
            string parent = this.GetParent(sourcePath);
            if (false == string.IsNullOrEmpty(parent) && false ==
                newRelativePathItemList.Any(s => s.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
            {
                newRelativePathItemList.Add(new PathItem(parent, false));
            }

            this.MyPathList.Items = newRelativePathItemList;
            // To move a folder into a destination folder: create a new destination folder path comprising the destination path and the source folder name
            string destinationFolderPathAndFolderName = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
            MoveFolderResultEnum result = this.ExternalMoveFolderIntoFolder(sourcePath,
                destinationFolderPathAndFolderName, isSourceInteriorNode);
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
            CreateSubfolderResultEnum result =
                FilesFolders.TryCreateSubfolderInFolder(Path.Combine(this.RootFolder, folderPath), folderName);
            if (result == CreateSubfolderResultEnum.Success)
            {
                // No need to update the database, as Timelapse will not have a record of images associated with it
                this.WereEditsMade = true;
            }

            return result;
        }

        #endregion

        #region External:RenameFolder

        private MoveFolderResultEnum ExternalRenameFolder(string oldFolderPath, string newFolderPath,
            bool isInteriorNode)
        {
            ;
            MoveFolderResultEnum result = FilesFolders.TryMoveFolderIfExists(
                Path.Combine(this.RootFolder, oldFolderPath),
                Path.Combine(this.RootFolder, newFolderPath));
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

        MoveFolderResultEnum ExternalMoveFolderIntoFolder(string sourceFolderPath, string destinationFolderPath,
            bool isInteriorNode)
        {
            MoveFolderResultEnum result = FilesFolders.TryMoveFolderIfExists(
                Path.Combine(this.RootFolder, sourceFolderPath),
                Path.Combine(this.RootFolder, destinationFolderPath));
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

        #region TreeViewItem Context Menu Callbacks

        private void MenuItemNewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {

                this.CreateNewFolder((TreeViewItem)mi.Tag);
            }
        }

        private void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                this.InitiateRenameUI((TreeViewItem)mi.Tag);
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
                    ProcessExecution.TryProcessStartUsingFileExplorer(Path.Combine(this.RootFolder,
                        node.Path));
                }
                else
                {
                    //FLASH FLASH FLASH
                }
            }
        }

        #endregion

        #region Treeview and TreeViewItem helpers
        // A TreeViewItem's header is a stackpanel comprising an icon and a textbox
        private StackPanel CreateTreeViewItemHeaderAsStackPanel(string text, bool containsPhotos, bool folderExists)
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
                Foreground = containsPhotos
                    ? Brushes.Black
                    : (SolidColorBrush)new BrushConverter().ConvertFromString("#FF3A3A3A") //Brushes.DarkSlateGray
            };
            sp.Children.Add(image);
            sp.Children.Add(tb);
            return sp;
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
            ScrollViewer sv = VisualChildren.GetVisualChild<ScrollViewer>(this.TreeView);
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

        // Create a context menu to be used by the treeview, as indicated by the parameters
        private ContextMenu TreeViewItemCreateContextMenu(TreeViewItem tvi, bool folderExists, bool includeRenameItem)
        {
            ContextMenu contextMenu = new ContextMenu();

            // New folder item
            MenuItem menuCreateNewFolderAfterItem = new MenuItem
            {
                Header = "New folder...",
                Tag = tvi,
                IsEnabled = folderExists
            };
            menuCreateNewFolderAfterItem.Click += MenuItemNewFolder_Click;
            contextMenu.Items.Add(menuCreateNewFolderAfterItem);

            // Rename folder item (this is optional, as its not allowed for the root folder)
            if (includeRenameItem)
            {
                MenuItem menuItemRename = new MenuItem
                {
                    Header = "Rename...",
                    Tag = tvi,
                    IsEnabled = folderExists
                };
                menuItemRename.Click += MenuItemRename_Click;
                contextMenu.Items.Add(menuItemRename);
            }

            // Show in Explorer menu item
            MenuItem menuOpenInExplorer = new MenuItem
            {
                Header = "Show in Explorer",
                Tag = tvi,
                IsEnabled = folderExists
            };
            menuOpenInExplorer.Click += MenuOpenInExplorer_Click;
            contextMenu.Items.Add(menuOpenInExplorer);

            return contextMenu;
        }

        // Return the TreeViewItem that contains the provided path
        // The initial form is used to get the Root TreeViewItem from the TreeView
        private TreeViewItem TreeViewFindMatchingPath(TreeView tv, string path)
        {
            if (this.TreeView.Items.Count != 1)
            {
                // there should be a single item representing the root tvi
                return null;
            }
            TreeViewItem rootTreeViewItem = (TreeViewItem)this.TreeView.Items[0];
            return TreeViewFindMatchingPath(rootTreeViewItem, path);
        }

        // Recurse through the TreeViewItem hierarchy to find the first match.
        private TreeViewItem TreeViewFindMatchingPath(TreeViewItem tvi, string path)
        {
            Node node = (Node)tvi.Tag;
            if (node.Path == path)
            {
                return tvi;
            }
            foreach (TreeViewItem childTvi in tvi.Items)
            {
                TreeViewItem returnedTvi = TreeViewFindMatchingPath(childTvi, path);
                if (returnedTvi != null)
                {
                    return returnedTvi;
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

        private void Flash(TreeViewItem tvi)
        {
            if (tvi == null)
            {
                return;
            }

            TextBlock headerTextBlock = GetTextBlockFromTreeViewItem(tvi);
            if (headerTextBlock != null)
            {
                ColorAnimation animation = GetFlashAnimation();
                headerTextBlock.Background = new SolidColorBrush(Colors.White);
                headerTextBlock.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
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

        #region Rename helpers: Scrolling 
        // Activate or deactivate the ScrollViewer's ScrollChanged callback
        private void ScrollViewerActivateScrollChangedEvent(bool activateScrollChangedEvent)
        {
            ScrollViewer sv = VisualChildren.GetVisualChild<ScrollViewer>(this.TreeView);
            if (sv == null)
            {
                return;
            }

            if (activateScrollChangedEvent)
            {
                sv.ScrollChanged += Sv_ScrollChanged;
            }
            else
            {
                sv.ScrollChanged -= Sv_ScrollChanged;
            }
        }

        // If a user is scrolling during the rename operation, move the textbox position to stay over its TreeViewItem
        // However, if the textbox scrolls outside the canvas, then consider the edit complete
        // Note that the ybottom is not quite right, but its close enough to more or less acheive the desired effect.
        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (this.TextBoxEditNode.Visibility == Visibility.Visible && (sender is ScrollViewer sv))
            {
                double y = Canvas.GetTop(TextBoxEditNode);
                double ybottom = y + TextBoxEditNode.ActualHeight - 25;
                if (y < 0 || ybottom >= sv.ViewportHeight)
                {
                    // Treeviewitem has scrolled out of view, so end the edit.
                    this.RenameCompleted();
                }
                else
                {
                    // Treeviewitem is still in view, so reposition the textbox
                    this.TextBoxPositionOverTreeViewItem((TreeViewItem)TextBoxEditNode.Tag);
                }
            }
        }
        #endregion

        #region Rename helpers: External Button enablement 
        private void RenameStarted()
        {
            this.EnableOrDisableButtons(false);
        }

        private void RenameCompleted()
        {
            this.TextBoxEditNode.Visibility = Visibility.Collapsed;
            this.EnableOrDisableButtons(true);
        }

        private void EnableOrDisableButtons(bool isEnabled)
        {
            foreach (Button button in this.Buttons)
            {
                button.IsEnabled = isEnabled;
            }
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
            public bool ContainsPhotos { get; set; }
            public bool FolderExists { get; set; }

            private readonly char[] charSeparators = new char[] { '\\' };

            public void AddPath(PathItem pathItem)
            {
                string path = pathItem.Path;
                bool containsPhotos = pathItem.ContainsImages;
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
            public bool ContainsImages { get; }

            public PathItem(string path, bool containsImages)
            {
                Path = path;
                ContainsImages = containsImages;
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
                    clonedPathList.Items.Add(new PathItem(item.Path, item.ContainsImages));
                }

                return clonedPathList;
            }
        }
        #endregion
    }
}
