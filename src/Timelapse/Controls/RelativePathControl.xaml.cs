using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;

// ReSharper disable EmptyGeneralCatchClause

namespace Timelapse.Controls
{
    /// <summary>
    /// Create a treeview that displays
    /// - the relative paths held in the database
    /// - the actual folder/subfolder structure in the file system, including subfolders that may not be represented in the database
    /// - indicate relatives paths in the database that appear to be missing from the file system (by icon)
    /// - indicate relative paths that are associated with images (by icon)
    /// Also build a 'node' hierarchy that contains all the data in the above. This should have been done as an MVVM model,
    /// but I couldn't really figure out the best way to do that when I started.
    /// Essentially, any change needs to be reflected in the following:
    /// - the RelativePaths in the database needs to be updated
    /// - the TreeView needs to be updated and rebuilt with modified TreeViewItems
    /// - the Nodes structure needs to be rebuilt
    /// - the actual file system needs to be updated
    /// Likely way more complex than if I were using MVVM, but it works.
    /// </summary>
    public partial class RelativePathControl
    {
        #region Constants
        private readonly char charSeparator = '\\';
        private readonly string RootFolderName = "Root folder";
        private readonly string NewFolder = "New folder";

        // Invalid folder name characters and folder names. Used for checking renamed relative paths
        private readonly char[] invalidChars = Path.GetInvalidFileNameChars();

        private readonly string[] invalidFileNames =
        [
            "CON", "PRN", "AUX", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
            "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
            "LPT8", "LPT9"
        ];
        #endregion

        #region Properties
        public FileDatabase FileDatabase { get; set; }

        // The path to the actual root folder containing images
        private string RootPathToImages => FileDatabase.RootPathToImages;

        public Window ParentDialogWindow { get; set; }

        // Whether or not a user has made any edits to the folder structure
        public bool WereEditsMade { get; set; }
        #endregion

        #region Variables
        // A list of the relative paths contained by the Timelapse database, passed into the control.
        // As its in the database, each of these complete relative paths must be associated with one or more images. 
        private List<string> RelativePaths = [];

        // PathList will eventually contain an entry for each subfolder (and entries for each of its subfolders etc) under the root folder
        // As only the complete paths (not path portions) in the RelativePaths list are associated with images, we can distinguish  
        // them from other paths which are not associated with Timelapse images.
        private PathList MyPathList = new();

        // For recording mouse down and dragging state during certain click and drag and drop operations
        private Point _lastMouseDown;
        private TreeViewItem draggedItem, _target;
        #endregion

        #region Constructors
        public RelativePathControl()
        {
            InitializeComponent();
        }
        #endregion

        #region Initialization
        // Initialize everything.  Retrieve a list of relative paths from the database and the actual file subfolders under the root folder,
        // then build the tree and corresponding node data structure from it and display it.
        // Needs to be invoked externally, usually immediately after the control is created. However, it can be re-invoked 
        // any time, which is usually used for testing purposes (e.g., to see if the changes to the treeview mirror the changes in the database)
        public async Task<bool> AsyncInitialize(Window owner, FileDatabase fileDatabase, IProgress<ProgressBarArguments> progressHandler, CancellationTokenSource cancelTokenSource)
        {
            if (null == fileDatabase)
            {
                // Can't really do anything, so display that.
                TreeViewItem tvi = new() { Header = "No files appear to be loaded" };
                TreeView.Items.Add(tvi);
                return false;
            }

            FileDatabase = fileDatabase;
            ParentDialogWindow = owner;

            // Start: Wrap long operations to show progress: 
            // Get the relative paths from the database
            RelativePaths = await BusyCancelIndicator.ProgressWrapper(FileDatabase.AsyncGetRelativePaths, progressHandler, cancelTokenSource, "Retrieving folders. Please wait...", true);
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
            //bool rootNodeContainsPhotos = this.MyPathList.Items.Any(s => string.IsNullOrWhiteSpace(s.Path));
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
                RecordIsExpandedStateInDict((TreeViewItem) TreeView.Items[0], "", dictIsExpandedState);
            }

            // Create the initial TreeViewItem representing the root folder.
            StackPanel sp = CreateTreeViewItemHeaderAsStackPanel(RootFolderName, rootNodeContainsImages, rootNode.FolderExists);
            TreeViewItem tvi = new()
            {
                Header = sp,
                IsExpanded = true,
                Tag = rootNode,
            };

            // Create a special context menu for the Root folder that doesn't include the Rename item,
            // as we don't allow the root folder to be renamed
            tvi.ContextMenu = TreeViewItemCreateContextMenu(tvi, rootNode.FolderExists, rootNode.ContainsImages, false, true);

            // Add children to the root TreeViewItem to match the node structure hierarchy
            TraverseNodeAndAddToTreeViewItem(tvi, rootNode, dictIsExpandedState);

            // Clear the treeView, in case it has been previously populated;
            // and add the (now populated) TreeViewItem to the treeView

            TreeView.Items.Clear();
            TreeView.Items.Add(tvi);

        }

        // Record whether or not a treeviewitem (as denoted by its complete path) in a dictionary
        // We will use this to set the expanded state when the tree view item is rebuilt
        public static void RecordIsExpandedStateInDict(TreeViewItem parentItem, string path, Dictionary<string,bool> dictIsExpandedState)
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
                node.FolderExists = Directory.Exists(Path.Combine(this.RootPathToImages, node.Path));
                StackPanel sp = CreateTreeViewItemHeaderAsStackPanel(node.Name, node.ContainsImages, node.FolderExists);
                tvi = new()
                {
                    Header = sp,
                    Tag = node
                };
                tvi.ContextMenu = TreeViewItemCreateContextMenu(tvi, node.FolderExists, node.ContainsImages, true, false);

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
        private static void ExpandTreeView(TreeViewItem item, bool expandTheTreeViewItem)
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

        #region Section: Rename a Folder 

        #region UI interactions: Rename TextBox
        private double textBoxEditNodeMinWidth = -1;
        private string originalTextInHeader;
        private string originalPathInHeader;

        // Initiate renaming:
        // - position and size the rename texbox atop the node
        // - set its value and visibility, 
        private void InitiateRenameUI(TreeViewItem tvi)
        {
            try
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
                    Flash(tvi);
                    return;
                }

                // Bring the tree view item into view. If that requires scrolling, we don't want the scroll change event to fire.
                // Afterwards, we do want it to fire as then it will handle positioning the TextBoxEditNode as it is scrolled,
                // and cancelling the edit if it is scrolled out of view..
                ScrollViewerActivateScrollChangedEvent(false);
                tvi.BringIntoView(new(new(10, 40)));
                ScrollViewerActivateScrollChangedEvent(true);

                if (false == TextBoxPositionOverTreeViewItem(tvi))
                {
                    // We could not position the textbox, so make this a no-op.
                    Flash(tvi);
                    return;
                }
                TextBoxEditNode.Visibility = Visibility.Visible;
                TextBoxEditNode.Text = node.Name;
                TextBoxEditNode.Tag = tvi;
                textBoxEditNodeMinWidth = -1;
                TextBoxEditNode.BorderThickness = new(2);
                TextBoxEditNode.CaretIndex = node.Path.Length;
                TextBoxEditNode.Focus();
                originalTextInHeader = node.Name;
                originalPathInHeader = node.Path;
            }
            catch (Exception e)
            {
                TracePrint.CatchException($"Catch in initiateRenameUI: {e.Message}");
                throw;
            }
        }

        // Position the rename texbox atop the TreeViewItem
        // This can sometimes fail. If it does, just make it all a no-op.
        private bool TextBoxPositionOverTreeViewItem(TreeViewItem tvi)
        {
            try
            {
                // Note that we associate the tag with its corresponding TreeViewItem
                GeneralTransform myTransform = tvi.TransformToAncestor(Canvas);
                Point myOffset = myTransform.Transform(new(0, 0));

                TextBoxEditNode.SetValue(Canvas.TopProperty, myOffset.Y - 1);
                TextBoxEditNode.SetValue(Canvas.LeftProperty, myOffset.X + 33);
                return true;
            }
            catch (Exception e)
            {
                TracePrint.CatchException($"Catch in TextBoxPositionOverTreeViewItem: {e.Message}");
                return false;
            }
        }

        // Whenever the textbox is edited, adjust its width to ensure that its never less than its original width
        private void TextBoxEditNode_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                //Debug.Print("TextChanged:" + tb.Text);
                if (textBoxEditNodeMinWidth < 0)
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
                        Flash(tb);
                        e.Handled = true;
                    }
                }
            }
        }

        // Handle particular keystrokes while editing.
        private void TextBoxEditNode_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb)
            {
                TextBoxEditNode_DoKeyDown(tb, e.Key);
            }
        }

        private void TextBoxEditNode_DoKeyDown(TextBox tb, Key key)
        {
            //Debug.Print("Before:" + tb.Text);
            if (key == Key.Escape)
            {
                // Escape aborts the edit
                Flash((TreeViewItem)tb.Tag);
                RenameHideTextBox();
                return;
            }

            if (key == Key.Enter) // both keys are equivalent, so no need for this || key == Key.Return )
            {
                // Editing is considered completed on Enter or return or Tab.
                // Check if the folder name is a legal one
                RenameHideTextBox();
                tb.Text = tb.Text.Trim();
                if (tb.Text == originalTextInHeader)
                {
                    // If the text is the same, do nothing
                    return;
                }
                if (tb.Text.EndsWith('.'))
                {
                    // Disallowed ending with a period folder name - abort and display error message
                    RenameHideTextBox();
                    DisplayFeedback("Rename cancelled",
                        "Windows does not allow folder and file names to end with a '.'");
                    return;
                }
                if (invalidFileNames.Contains(tb.Text, StringComparer.OrdinalIgnoreCase))
                {
                    // Disallowed reserved word folder name - abort and display error message
                    RenameHideTextBox();
                    DisplayFeedback("Rename cancelled",
                        $"Windows does not allow '{tb.Text}' as a folder name as it is a reserved name.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    // Disallowed empty folder name - abort and display error message
                    Flash((TreeViewItem)tb.Tag);
                    return;
                }

                // We have a valid folder name. Change the name as needed.
                RenameHideTextBox();
                InvokeRename(tb.Text, (TreeViewItem)tb.Tag);
            }
            //Debug.Print("Processing:" + tb.Text);
            // If we get here, then the user is still editing the  name
        }

        // If the user does certain actions (e.g., clicking outside the textBox)
        // accept the edit
        private void TreeViewItem_Cancel(object sender, object e)
        {
            // Accept the edit, if any
            RenameEditCompleted();
        }

        private void RenameHideTextBox()
        {
            TextBoxEditNode.Visibility = Visibility.Collapsed;
        }

        private void RenameEditCompleted()
        {
            // Simulate an enter key
            // Note that we don't have to hide the textbox here as that will be done in DoKeyDown.
            if (TextBoxEditNode.Visibility == Visibility.Visible)
            {
                TextBoxEditNode_DoKeyDown(TextBoxEditNode, Key.Enter);
            }
        }

        private void TextBoxEditNode_OnLostFocus(object sender, RoutedEventArgs e)
        {
            RenameEditCompleted();
        }
        #endregion

        #region Invoke and Do: Rename a subfolder
        private void InvokeRename(string newName, TreeViewItem tvi)
        {
            bool isInteriorNode = false;
            //Debug.Print("TextInvoked:" + newName);
            // Get the node corresponding to that TreeViewItem, but exit if null
            Node node = (Node)tvi.Tag;
            if (node == null)
            {
                return;
            }

            // Create a deep copy of the current path list.
            // In case of error, this will let us restore things to their original state 
            PathList originalPathList = MyPathList.DeepClone();

            string newString = node.Path.ReplaceLastOccurrence(node.Name, newName);

            // We don't allow renaming if:
            // - the new path portion (as delimited with a \) matches an existing path portion, as that would be a duplicate
            // - the complete path matches an existing complete path portion, as that would also be a duplicate

            // Create a list where its paths don't begin with the exactly matching original path item
            List<PathItem> tmpPathItemListWithoutMatchingPath = [];
            foreach (PathItem pathItem in MyPathList.Items)
            {
                if (pathItem.Path.StartsWith(originalPathInHeader + charSeparator)
                    || pathItem.Path.Equals(originalPathInHeader))
                {
                    continue;
                }

                tmpPathItemListWithoutMatchingPath.Add(pathItem);
            }

            // Check to see if there are other paths/nodes partialy or wholey matching the original path item. If so, we shouldn't copy it.
            bool isMatchToOtherPath = tmpPathItemListWithoutMatchingPath.Any(s =>
                                          s.Path.StartsWith(newString + charSeparator,
                                              StringComparison.OrdinalIgnoreCase))
                                      || tmpPathItemListWithoutMatchingPath.Any(s =>
                                          s.Path.Equals(newString, StringComparison.OrdinalIgnoreCase));
            if (isMatchToOtherPath)
            {
                DisplayFeedback("Rename cancelled", $"A path with that name already exists: {newString}");
                return;
            }

            // If its under the Root folder, we have to special case it as there is no leading path to make it different from other instances
            // of the root folder
            if (newString.IndexOf(charSeparator) < 0)
            {
                PathList newPathList = new();
                foreach (PathItem item in MyPathList.Items)
                {
                    if (item.Path.StartsWith(node.Path + charSeparator, StringComparison.OrdinalIgnoreCase) ||
                        item.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        // this is a renamed item
                        //newPathList.Items.Add(new PathItem(this.ReplaceFirstOccurrence(item.Path, node.Path, newString),
                        //    item.ContainsImages));
                        newPathList.Items.Add(new(item.Path.ReplaceFirstOccurrence(node.Path, newString),
                            item.ContainsImages));
                    }
                    else
                    {
                        // this is an unchanged item
                        newPathList.Items.Add(new(item.Path, item.ContainsImages));
                    }
                }
                MyPathList = newPathList;
            }
            else
            {
                // Change the elements as needed in the path list to reflect the new path 
                PathList newPathList = new();
                foreach (PathItem item in MyPathList.Items)
                {
                    string path = item.Path;
                    bool containsPhotos = item.ContainsImages;
                    newPathList.Items.Add(new(path.Replace(node.Path, newString), containsPhotos));
                }
                MyPathList = newPathList;
            }


            // Check to see if the original node was an interior node (i.e., path prefix). If its a '/' terminating prefix for even one of the paths, then it must be
            // This could be done in one of the other existing loops for efficiencys, but its easier this way
            foreach (PathItem item in originalPathList.Items)
            {
                if (item.Path.StartsWith(node.Path + charSeparator))
                {
                    isInteriorNode = true;
                    break;
                }
            }

            // Now try to actually rename the folder, both in the folder structure and the database
            MoveFolderResultEnum result = DoRenameFolder(node.Path, newString, isInteriorNode);
            if (result != MoveFolderResultEnum.Success)
            {
                Dialogs.RenameRelativePathError(ParentDialogWindow, result, node.Path, newString);
                // Restore the tree etc to its pre-renamed form
                MyPathList = originalPathList;
            }
            RebuildTreeAndNodes();
        }

        private MoveFolderResultEnum DoRenameFolder(string oldFolderPath, string newFolderPath,
            bool isInteriorNode)
        {
            MoveFolderResultEnum result = FilesFolders.TryMoveFolderIfExists(
                Path.Combine(this.RootPathToImages, oldFolderPath),
                Path.Combine(this.RootPathToImages, newFolderPath));
            if (result == MoveFolderResultEnum.Success)
            {
                // We assume we can always update the database with no errors. 
                // It would be good if we could get an error code back!
                FileDatabase.UpdateRelativePathByReplacingPrefix(oldFolderPath, newFolderPath, isInteriorNode);
                WereEditsMade = true;
            }
            return result;
        }

        #endregion

        #endregion

        #region Section: Moving Folder into another Folder

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

        // Drag/drop actions on a mouse move
        // - Only allows an existing folder to be dragged.
        // - If it doesn't exist (i.e., a missing folder), its a no-op
        private void TreeViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point currentPosition = e.GetPosition(TreeView);

                    if ((Math.Abs(currentPosition.X - _lastMouseDown.X) > 10.0) ||
                        (Math.Abs(currentPosition.Y - _lastMouseDown.Y) > 10.0))
                    {
                        draggedItem = (TreeViewItem)TreeView.SelectedItem;
                        // Only allow non-null folders that actually exist to be dragged
                        if (draggedItem != null && ((Node)draggedItem.Tag).FolderExists)
                        {
                            DragDropEffects finalDropEffect = DragDrop.DoDragDrop(TreeView,
                                TreeView.SelectedValue, DragDropEffects.Move);
                            // Checking target is not null and item is dragging(moving)
                            if (finalDropEffect == DragDropEffects.Move && _target != null)
                            {
                                // A Move drop was accepted
                                string draggedItemText = ((Node)draggedItem.Tag).Path;
                                string targetItemText = ((Node)_target.Tag).Path;
                                if (!draggedItemText.Equals(targetItemText))
                                {
                                    MoveDraggedItem(draggedItem, _target);
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
                Point currentPosition = e.GetPosition(TreeView);
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

        // Drop action
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

        private void TreeViewItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            draggedItem = null;
        }

        // Check if a valid drop traget. 
        private bool IsValidDropTarget(TreeViewItem _sourceItem, TreeViewItem _targetItem)
        {
            if (_sourceItem == null || false == _sourceItem.Tag is Node || _targetItem == null)
            {
                // For some reason, the source is not valid.
                return false;
            }

            string destPath = ((Node)_targetItem.Tag).Path;
            string sourcePath = ((Node)_sourceItem.Tag).Path;
            string sourceName = ((Node)_sourceItem.Tag).Name;

            // Case 1: Disallow drop when the destination is an immediate subfolder of the source,
            // We shouldn't be able to move a folder into its subfolder
            if (destPath.Equals(GetParent(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Case 2: Disallow drop to a destination that already has a subfolder of the same name,
            // A folder cannot have two folders with the same name
            foreach (KeyValuePair<string, Node> kvp in ((Node)_targetItem.Tag).Children)
            {
                if (kvp.Key.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Case 3: Disallow drop of a folder onto itself
            if (destPath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;

            }
            // Case 4: Disallow a drop from a folder into one of its subfolders
            if ((destPath + charSeparator).StartsWith(sourcePath + charSeparator))
            {
                return false;
            }

            // Case 5: Disallow a drop onto a missing folder
            if (false == ((Node)_targetItem.Tag).FolderExists)
            {
                return false;
            }
            return true;
        }
        #endregion

        #region Invoke and Do: Move the folder

        // Invoke moving the dragged item
        private void MoveDraggedItem(TreeViewItem _sourceItem, TreeViewItem _targetItem)
        {
            try
            {
                Node sourceNode = (Node)_sourceItem.Tag;
                Node targetNode = (Node)_targetItem.Tag;
                MoveRelativePath(sourceNode.Path, sourceNode.Name, targetNode.Path);
                RebuildTreeAndNodes();
            }
            catch
            {
            }
        }

        // Move the relative path as directed. This mostly updates the PathList, and then invokes the actual move
        private void MoveRelativePath(string sourcePath, string sourceName, string destinationPath)
        {
            string newPath = Path.Combine(destinationPath, sourceName);
            List<PathItem> newPathItemList = [];
            List<PathItem> alteredPathItemList = [];
            List<PathItem> unalteredPathItemList = [];

            // Create a deep copy of the current path list in case there is an error, as this will let us restore 
            // things to their original state
            PathList originalPathList = MyPathList.DeepClone();

            // Create three lists:
            // - alteredPathItemList is a list containing only those paths that were altered
            // - unalteredPathItemList is a list containing only those paths that were not altered
            // - newPathItemList is a list that will contain the correct path items after the move is done.
            // We do this so we can insert the altered paths into a location that makes sense when displaying the tree
            foreach (PathItem item in MyPathList.Items)
            {
                if (
                    (item.Path.StartsWith(sourcePath + charSeparator, StringComparison.OrdinalIgnoreCase))
                    || (item.Path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)))
                {
                    //string newEntry = this.ReplaceFirstOccurrence(item.Path, sourcePath, newPath);
                    string newEntry = item.Path.ReplaceFirstOccurrence(sourcePath, newPath);
                    alteredPathItemList.Add(new(newEntry, item.ContainsImages));
                }
                else
                {
                    unalteredPathItemList.Add(item);
                }
            }

            // Combine the unaltered and altered PathItem lists into the newPathItemList,
            // where the insertion of the altered PathItems is done in the correct place
            // so that the rebuilt tree doesn't jump around
            bool alteredPathItemListAdded = false;
            foreach (PathItem item in unalteredPathItemList)
            {
                if (alteredPathItemListAdded == false &&
                    item.Path.StartsWith(destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    newPathItemList.AddRange(alteredPathItemList);
                    alteredPathItemListAdded = true;
                }
                newPathItemList.Add(item);
            }

            // However, we need a further possible addition to the newPathItemList.
            // If the move resulted in an 'orphaned' parent subtree (e.g., a/b where we move b but a has no photos so there is no longer an 'a' in the list)
            // put the orphaned parent in the list as a new list,  with no photos in it.
            string parent = GetParent(sourcePath);
            if (false == string.IsNullOrEmpty(parent) && false ==
                newPathItemList.Any(s => s.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
            {
                newPathItemList.Add(new(parent, false));
            }

            // Now set the main path list so that it contains these new lists.
            MyPathList.Items = newPathItemList;

            // Check to see if the original DB entry was an interior node (i.e., path prefix). If its a '/' terminating prefix for even one of the paths, then it must be
            // Note that this is differnt than the above, as we only include paths that have a photo in it.
            bool isDatabaseInteriorNode = false;
            foreach (PathItem item in originalPathList.Items)
            {
                if (item.ContainsImages && item.Path.StartsWith(sourcePath + charSeparator))
                {
                    isDatabaseInteriorNode = true;
                    break;
                }
            }

            // To move a folder into a destination folder (both physically and in the database):
            // create a new destination folder path comprising the destination path and the source folder name
            string destinationFolderPathAndFolderName = Path.Combine(destinationPath, Path.GetFileName(sourcePath)!);
            MoveFolderResultEnum result = ExternalMoveFolderIntoFolder(sourcePath,
                destinationFolderPathAndFolderName, isDatabaseInteriorNode);

            if (result != MoveFolderResultEnum.Success)
            {
                Dialogs.RenameRelativePathError(ParentDialogWindow, result, sourcePath, destinationPath);
                // Restore the tree etc to its pre-renamed form
                MyPathList = originalPathList;
            }
            RebuildTreeAndNodes();
        }

        // Actualy move the physical folder into another folder, and update the relative paths in the database
        MoveFolderResultEnum ExternalMoveFolderIntoFolder(string sourceFolderPath, string destinationFolderPath, bool isDatabaseInteriorNode)
        {
            MoveFolderResultEnum result = FilesFolders.TryMoveFolderIfExists(
                Path.Combine(this.RootPathToImages, sourceFolderPath),
                Path.Combine(this.RootPathToImages, destinationFolderPath));
            if (result == MoveFolderResultEnum.Success)
            {
                // We assume we can always update the database with no errors. 
                // It would be good if we could get an error code back!
                FileDatabase.UpdateRelativePathByReplacingPrefix(sourceFolderPath, destinationFolderPath, isDatabaseInteriorNode);
                WereEditsMade = true;
            }
            return result;
        }
        #endregion

        #endregion

        #region Section: DeleteFolder

        // Delete the indicated folder from the path list and the physical file system
        // We don't have to delete it from the Database, as the UI doesn't allow deletion of folders in the database (i.e., those with images in it)
        private void DeleteFolder(TreeViewItem tvi)
        {
            // Get the node corresponding to that TreeViewItem. But noop the node is null
            Node node = (Node)tvi.Tag;
            if (node == null)
            {
                return;
            }
            string fullFolderPath = Path.Combine(this.RootPathToImages, node.Path);

            // Try to delete the folder
            if (!Directory.EnumerateFileSystemEntries(fullFolderPath).Any())
            {
                try
                {
                    Directory.Delete(fullFolderPath);
                }
                catch (Exception e)
                {
                    // We don't have to repair anything, since the deletion was not done.
                    Flash(tvi);
                    DisplayFeedback("The folder was not deleted.",
                        $"For some reason, Windows could not delete this folder: {Environment.NewLine}   {fullFolderPath}."
                            + $"{Environment.NewLine}{Environment.NewLine}The Windows error message was: {e.Message}");
                    return;
                }
            }
            else
            {
                Flash(tvi);
                DisplayFeedback("The folder was not deleted.",
                    $"Deletions are only allowed for empty folders. This folder has files and/or other folders in it: {Environment.NewLine}   {fullFolderPath}.");
                return;
            }
            // Deletions will only work on folders that are not currently in the TImelapse database.
            // Thus rebuilding the tree should suffice, as it would no longer be present when we search for other
            // folders under the root folder
            MyPathList.Items.RemoveAll(p => p.Path.Equals(node.Path, StringComparison.OrdinalIgnoreCase));
            RebuildTreeAndNodes();
        }
        #endregion

        #region Setion: NewFolder
        // Create a new folder under the TVI folder
        private TreeViewItem CreateNewFolder(TreeViewItem tvi, bool allowRename)
        {
            // Get the node corresponding to that TreeViewItem. Treat as a noop if the node is null
            Node node = (Node)tvi.Tag;
            if (node == null)
            {
                return null;
            }

            // Generate a unique name for the New folder (e.g., if a 'New folder' already exists, it will return (and check) New folder_1 etc).
            FilesFolders.GenerateFileNameIfNeeded(Path.Combine(this.RootPathToImages, node.Path), NewFolder,
                out string newFolderName);

            string newFullFolderPath = Path.Combine(node.Path, newFolderName);
            if (MyPathList.Items.Any(p => p.Path.Equals(newFullFolderPath, StringComparison.OrdinalIgnoreCase)))
            {
                // A folder already exists in the Path List. This could happen if, for example, a missing folder has the same name.
                // We could, of course, test for this and redo the GenerateFileNameIfNeeded, but it does seem like its a rare case.
                DisplayFeedback("The new folder was not created",
                    $"A folder called {newFullFolderPath} already exists.");
                return null;
            }

            // Create a deep copy of the current path list in case there is an error, as this will let us restore 
            // things to their original state
            PathList originalPathList = MyPathList.DeepClone();
            PathList newPathList = new();
            if (string.IsNullOrWhiteSpace(node.Path))
            {
                // We are adding the new folder to the Root Folder, so insert it at the beginning
                // The new folder does not contain any photos
                newPathList.Items.Add(new(newFolderName, false));
                newPathList.Items.AddRange(MyPathList.Items);
            }
            else
            {
                // Insert the new folder in the correct place in the list i.e., just before the current item
                bool inserted = false;
                int index = 0;
                foreach (PathItem item in MyPathList.Items)
                {
                    string path = item.Path;
                    bool containsPhotos = item.ContainsImages;
                    newPathList.Items.Add(new(path, containsPhotos));
                    if (inserted == false && path.StartsWith(node.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        // The new folder does not contain any photos
                        newPathList.Items.Insert(index, new(Path.Combine(node.Path, newFolderName), false));
                        inserted = true;
                    }
                    index++;
                }
            }
            // We now have the updated path list
            MyPathList = newPathList;

            // Actualy create the new folder in the file system. Note that we don't have to update the
            // database, as this folder would not yet exist in it, nor does the new folder have any images
            CreateSubfolderResultEnum result = DoCreateNewFolder(node.Path, newFolderName);
            if (result != CreateSubfolderResultEnum.Success &&
                result != CreateSubfolderResultEnum.FailAsDestinationFolderExists)
            {
                Dialogs.RenameRelativePathError(ParentDialogWindow, result, node.Path, newFolderName);
                // Restore the tree etc to its pre-renamed form
                MyPathList = originalPathList;
            }

            RebuildTreeAndNodes();

            // We want the person to be able to rename the New folder, so invoke Rename on it. 
            // Find the just created 'New folder' tree view item in the tree view
            TreeViewItem foundTvi = TreeViewFindMatchingPath(Path.Combine(node.Path, newFolderName));
            if (foundTvi != null)
            {
                Flash(foundTvi);
                if (allowRename)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Flash(foundTvi);
                        InitiateRenameUI(foundTvi);
                    }), DispatcherPriority.ContextIdle, null);
                }
            }
            return foundTvi;
        }

        // Actually create the new folder in the physical file structure
        private CreateSubfolderResultEnum DoCreateNewFolder(string folderPath, string folderName)
        {
            CreateSubfolderResultEnum result =
                FilesFolders.TryCreateSubfolderInFolder(Path.Combine(this.RootPathToImages, folderPath), folderName);
            if (result == CreateSubfolderResultEnum.Success)
            {
                // No need to update the database, as Timelapse will not have a record of images associated with it
                WereEditsMade = true;
            }

            return result;
        }
        #endregion

        #region Section: Move images/ videos from source folder into a newly created new folder
        private void MoveImagesToNewFolder(TreeViewItem sourceTvi, Node sourceNode)
        {
            string fullSourcePath = Path.Combine(this.RootPathToImages, sourceNode.Path);

            // Create the destination folder (usually a variation of 'New folder')
            TreeViewItem destinationTvi = CreateNewFolder(sourceTvi, false);

            if (destinationTvi == null)
            {
                // Failed for some reason so abort. No need to do anything else, as the CreateNewFolder would have displayed an error message dialog
                return;
            }

            // Get the destination node. 
            Node destinationNode = (Node)destinationTvi.Tag;
            if (destinationNode == null)
            {
                // This would create the new folder but not move the images into it. 
                DisplayFeedback(
                    "Could not move images into a new folder",
                    "While a new folder was created, for some reasons we could not move the images into it.");
                return;
            }

            destinationTvi.IsSelected = true;
            string fullDestinationPath = Path.Combine(this.RootPathToImages, destinationNode.Path);

            // Get all the image/video files in the source folder and try to move them to the destination
            List<string> imageAndVideoFiles = FilesFolders.GetAllImageAndVideoFilesInASingleFolder(fullSourcePath);
            int filesMoved = 0;
            try
            {
                foreach (string file in imageAndVideoFiles)
                {
                    File.Move(file, Path.Combine(fullDestinationPath, Path.GetFileName(file)));
                    filesMoved++;
                }

                // Now update the database entries for those files
                // We assume we can always update the database with no errors. 
                // It would be good if we could get an error code back!
                // Note that this would not be an interior node as its a new folder without any children, and we know its not in the database
                FileDatabase.UpdateRelativePathByReplacingPrefix(sourceNode.Path, destinationNode.Path, false);
                bool isRootFolder = string.IsNullOrWhiteSpace(sourceNode.Path);

                // Reset the source parent so that it doesn't contain images )
                // and the destination so it does (as we have moved the images from the source to the destinaton)
                foreach (PathItem pathItem in MyPathList.Items)
                {
                    if (isRootFolder)
                    {
                        if (string.IsNullOrWhiteSpace(pathItem.Path))
                        {
                            pathItem.ContainsImages = false;
                        }
                        else if (pathItem.Path.Equals(destinationNode.Path))
                        {
                            pathItem.ContainsImages = true;
                        }
                    }
                    else
                    {
                        string parent = string.IsNullOrWhiteSpace(pathItem.Path)
                            ? string.Empty
                            : Path.GetDirectoryName(pathItem.Path);
                        if (pathItem.Path!.Equals(parent) || pathItem.Path.Equals(sourceNode.Path))
                        {
                            pathItem.ContainsImages = false;
                            // Debug.Print($"Setting '{pathItem.Path}' images to {pathItem.ContainsImages}");
                        }
                        else if (pathItem.Path.Equals(destinationNode.Path))
                        {
                            pathItem.ContainsImages = true;
                            //Debug.Print($"Setting '{pathItem.Path}' images to {pathItem.ContainsImages}");
                        }
                    }
                }
                WereEditsMade = true;
                RebuildTreeAndNodes();
            }
            catch (Exception e)
            {
                // We could try to move the successfully files back to their original location.
                // but not done, as its likely a rare error (hopefully)
                DisplayFeedback(
                    "Moving files were interrupted!",
                    $"{filesMoved}/{imageAndVideoFiles.Count} of your files were moved into the subfolder '{destinationNode.Path}'."
                        + (filesMoved == 0
                            ? string.Empty
                            : $"{Environment.NewLine}We recommend moving those files back into {sourceNode.Path} using Windows Explorer, as otherwise Timelapse will not be able to find them.")
                        + $"{Environment.NewLine}{Environment.NewLine}The Windows error message was: {e.Message}"
                );
            }
        }
        #endregion

        #region TreeViewItem Context Menu Creation and Callbacks
        // Create a context menu to be used by the treeview, as indicated by the parameters
        private ContextMenu TreeViewItemCreateContextMenu(TreeViewItem tvi, bool folderExists, bool photosExists, bool includeDeleteItem, bool isRootFolder)
        {
            ContextMenu contextMenu = new();
            // New folder item
            MenuItem menuCreateNewFolderAfterItem = new()
            {
                Header = "New folder...",
                Tag = tvi,
                IsEnabled = folderExists
            };
            menuCreateNewFolderAfterItem.Click += MenuItemNewFolder_Click;
            contextMenu.Items.Add(menuCreateNewFolderAfterItem);


            // Rename folder item (this is optional, as its not allowed for the root folder)
            MenuItem menuItemRename = new()
            {
                Header = "Rename folder...",
                Tag = tvi,
                IsEnabled = folderExists && !isRootFolder
            };
            menuItemRename.Click += MenuItemRenameFolder_Click;
            contextMenu.Items.Add(menuItemRename);

            // Delete folder item
            // Delete folder item (this is optional, as its not allowed for the root folder)
            if (includeDeleteItem)
            {
                MenuItem menuItemDeleteFolder = new()
                {
                    Name = "DeleteMenuItem",
                    Header = "Delete folder (but only if its empty)",
                    Tag = tvi,
                    IsEnabled = folderExists
                };
                menuItemDeleteFolder.Click += MenuItemDeleteFolder_Click;
                contextMenu.Items.Add(menuItemDeleteFolder);
            }

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


            MenuItem menuItemMoveImagesToNewFolder = new()
            {
                Header = "Create a new folder and move this folder's image/video files into it",
                Tag = tvi,
                IsEnabled = folderExists && photosExists
            };
            menuItemMoveImagesToNewFolder.Click += MenuItemMoveImagesToNewFolder_Click;
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuItemMoveImagesToNewFolder);

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

                Node node = (Node)tvi.Tag;

                string newFolderPath = Path.Combine(this.RootPathToImages, node.Path);
                if (LogicalTreeHelper.FindLogicalNode(cm, "DeleteMenuItem") is MenuItem deleteMenuItem)
                {
                    // If the folder doesn't exist, or if the folder isn't empty (or flagged as containing photos), disable the Delete MenuItem
                    if (node.FolderExists == false || node.ContainsImages || false == Directory.Exists(newFolderPath))
                    {
                        deleteMenuItem.IsEnabled = false;
                        return;
                    }
                    // Check to ensure that the folder is totally empty, including subfolders and any files
                    deleteMenuItem.IsEnabled = !Directory.EnumerateFileSystemEntries(newFolderPath).Any();
                }
            }
        }

        private void MenuItemNewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                CreateNewFolder((TreeViewItem)mi.Tag, true);
            }
        }

        private void MenuItemMoveImagesToNewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                TreeViewItem tvi = (TreeViewItem)mi.Tag;
                Node node = (Node)tvi.Tag;
                MoveImagesToNewFolder(tvi, node);
            }
        }

        private void MenuItemRenameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                InitiateRenameUI((TreeViewItem)mi.Tag);
            }
        }

        private void MenuItemDeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                DeleteFolder((TreeViewItem)mi.Tag);
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
                    ProcessExecution.TryProcessStartUsingFileExplorerOnFolder(Path.Combine(this.RootPathToImages,
                        node.Path));
                }
                //FLASH FLASH FLASH
            }
        }
        #endregion

        #region Treeview and TreeViewItem helpers
        // A TreeViewItem's header is a stackpanel comprising an icon and a textbox
        private static StackPanel CreateTreeViewItemHeaderAsStackPanel(string text, bool containsPhotos, bool folderExists)
        {
            StackPanel sp = new()
            {
                Name = "StackPanel",
                Orientation = Orientation.Horizontal
            };
            BitmapImage bi;
            if (containsPhotos)
            {
                bi = folderExists
                    ? new(new(@"/Icons/FolderPhotos.png", UriKind.Relative))
                    : new BitmapImage(new(@"/Icons/FolderPhotos_Missing.png", UriKind.Relative));
            }
            else
            {
                bi = folderExists
                    ? new(new(@"/Icons/FolderEmpty.png", UriKind.Relative))
                    : new BitmapImage(new(@"/Icons/FolderEmpty_Missing.png", UriKind.Relative));
            }

            Image image = new()
            {
                Source = bi
            };

            TextBlock tb = new()
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

        private static TextBlock GetTextBlockFromTreeViewItem(TreeViewItem tvi)
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
        private static TreeViewItem GetNearestContainer(UIElement element)
        {
            TreeViewItem container = element as TreeViewItem;
            while (container == null && element != null)
            {
                element = VisualTreeHelper.GetParent(element) as UIElement;
                container = element as TreeViewItem;
            }
            return container;
        }

        // Automatically scroll the treeview if needed during a drag operation
        private void TreeViewAutoScrollDuringDrag(double verticalPos)
        {
            ScrollViewer sv = VisualChildren.GetVisualChild<ScrollViewer>(TreeView);
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
            else if (verticalPos > TreeView.ActualHeight - tolerance) //Bottom of visible list?
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset + offset); //Scroll down.    
            }
        }


        // Return the TreeViewItem that contains the provided path
        // The initial form is used to get the Root TreeViewItem from the TreeView
        private TreeViewItem TreeViewFindMatchingPath(string path)
        {
            if (TreeView.Items.Count != 1)
            {
                // there should be a single item representing the root sourceTvi
                return null;
            }
            TreeViewItem rootTreeViewItem = (TreeViewItem)TreeView.Items[0];
            return TreeViewFindMatchingPath(rootTreeViewItem, path);
        }

        // Recurse through the TreeViewItem hierarchy to find the first match.
        private static TreeViewItem TreeViewFindMatchingPath(TreeViewItem tvi, string path)
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

        #region Path manipulations

        private string GetParent(string path)
        {
            string parentPath = string.Empty;
            int index = path.LastIndexOf(charSeparator);
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

        #region Flash animations
        private static void Flash(TextBox tb)
        {
            if (tb == null)
            {
                return;
            }

            ColorAnimation animation = GetFlashAnimation();
            tb.Background = new SolidColorBrush(Colors.White);
            tb.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private static void Flash(TreeViewItem tvi)
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

        private static ColorAnimation GetFlashAnimation()
        {
            return new()
            {
                To = Colors.Red,
                Duration = new(TimeSpan.FromSeconds(0.1)),
                AutoReverse = true,
                RepeatBehavior = new(1)
            };
        }

        #endregion

        #region Scrolling helpers, used during Rename operations
        // Activate or deactivate the ScrollViewer's ScrollChanged callback.
        private void ScrollViewerActivateScrollChangedEvent(bool activateScrollChangedEvent)
        {
            ScrollViewer sv = VisualChildren.GetVisualChild<ScrollViewer>(TreeView);
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
            if (TextBoxEditNode.Visibility == Visibility.Visible && (sender is ScrollViewer sv))
            {
                double y = Canvas.GetTop(TextBoxEditNode);
                double ybottom = y + TextBoxEditNode.ActualHeight - 25;
                if (y < 0 || ybottom >= sv.ViewportHeight)
                {
                    // Treeviewitem has scrolled out of view, so accept and end the edit.
                    RenameEditCompleted();
                }
                else
                {
                    // Treeviewitem is still in view, so reposition the textbox
                    TextBoxPositionOverTreeViewItem((TreeViewItem)TextBoxEditNode.Tag);
                }
            }
        }
        #endregion

        #region Feedback Dialog
        private void DisplayFeedback(string title, string reason)
        {
            Dialogs.DisplayError(ParentDialogWindow, title, reason);
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
            public bool ContainsImages { get; set; } = containsImages;
        }

        private class PathList
        {
            public List<PathItem> Items { get; set; } = [];

            public void OrderInPlace()
            {
                Items = [.. Items.OrderBy(s => s.Path)];
            }

            public PathList DeepClone()
            {
                PathList clonedPathList = new();
                foreach (PathItem item in Items)
                {
                    clonedPathList.Items.Add(new(item.Path, item.ContainsImages));
                }

                return clonedPathList;
            }
        }
        #endregion

    }
}
