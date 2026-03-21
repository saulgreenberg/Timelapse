using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Timelapse.Controls
{
    public partial class TreeViewSpecieNet : UserControl
    {
        #region Constructor
        public TreeViewSpecieNet()
        {
            InitializeComponent();
        }
        #endregion

        #region Public API

        /// <summary>
        /// Populate the tree using the two classification dictionaries.
        /// Both are keyed by the same category key (guid-like string).
        ///   categories   — key → common name (label shown to the user)
        ///   descriptions — key → "guid;class;order;family;genus;species;..." taxonomy path
        /// </summary>
        public void Populate(Dictionary<string, string> categories, Dictionary<string, string> descriptions)
        {
            SpecieNetTreeView.Items.Clear();
            DebugTextBox.Text = string.Empty;

            var (roots, uncategorized) = BuildTree(categories, descriptions);
            PopulateTreeView(roots, uncategorized);
        }

        /// <summary>
        /// Populate the tree from raw test-data strings of the form:
        ///   guid;class;order;family;genus;species;commonName
        /// The guid becomes the key and the last segment the common name.
        /// </summary>
        public void Populate(IEnumerable<string> rawEntries)
        {
            // Convert the raw strings into the same two-dictionary form so a single
            // BuildTree implementation handles everything.
            var categories    = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            var descriptions  = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var entry in rawEntries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                var cleaned = entry.Trim().Trim('"');
                var parts   = cleaned.Split(';');
                if (parts.Length < 7) continue;

                var key        = parts[0].Trim();
                var commonName = parts[6].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                categories[key]   = commonName;
                descriptions[key] = cleaned;
            }

            Populate(categories, descriptions);
        }

        #endregion

        #region Radio Button Handlers

        private void RadioExpandAll_Checked(object sender, RoutedEventArgs e)
        {
            if (SpecieNetTreeView == null) return;
            SetExpansion(SpecieNetTreeView.Items, expandAll: true);
        }

        private void RadioCollapseAll_Checked(object sender, RoutedEventArgs e)
        {
            if (SpecieNetTreeView == null) return;
            SetExpansion(SpecieNetTreeView.Items, expandAll: false);
        }

        private static void SetExpansion(ItemCollection items, bool expandAll)
        {
            foreach (TreeViewItem item in items)
            {
                item.IsExpanded = expandAll;
                if (item.Items.Count > 0)
                    SetExpansion(item.Items, expandAll);
            }
        }

        #endregion

        #region Selection Handler

        private void SpecieNetTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!(e.NewValue is TreeViewItem selectedItem))
            {
                DebugTextBox.Text = string.Empty;
                return;
            }

            var sb = new StringBuilder();
            CollectTerminalInfo(selectedItem, sb);
            DebugTextBox.Text = sb.ToString();
        }

        /// <summary>
        /// Recursively collect "commonName  |  key" lines for every terminal node
        /// at or below <paramref name="item"/>.
        /// </summary>
        private static void CollectTerminalInfo(TreeViewItem item, StringBuilder sb)
        {
            if (item.Tag is TreeNode node && node.IsTerminal)
                sb.AppendLine($"{node.CommonName}  |  {node.Key}");

            foreach (TreeViewItem child in item.Items)
                CollectTerminalInfo(child, sb);
        }

        #endregion

        #region Mouse-press Popup

        private void SpecieNetTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Ignore clicks on the expand/collapse arrow — only respond to the text label.
            if (IsClickOnExpander(e.OriginalSource as DependencyObject))
            {
                NodeInfoPopup.IsOpen = false;
                return;
            }

            var item = GetTreeViewItemUnderMouse(e.OriginalSource as DependencyObject);
            if (item == null)
            {
                NodeInfoPopup.IsOpen = false;
                return;
            }

            var sb = new StringBuilder();
            CollectTerminalInfo(item, sb);
            var text = sb.ToString().TrimEnd();
            if (string.IsNullOrEmpty(text))
            {
                NodeInfoPopup.IsOpen = false;
                return;
            }

            PopupTextBlock.Text  = text;
            NodeInfoPopup.IsOpen = true;
        }

        private void SpecieNetTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            NodeInfoPopup.IsOpen = false;
        }

        private void SpecieNetTreeView_MouseLeave(object sender, MouseEventArgs e)
        {
            // Guard against the spurious MouseLeave that WPF fires when the Popup
            // opens (its own OS window briefly steals mouse focus from the TreeView).
            if (e.LeftButton != MouseButtonState.Pressed)
                NodeInfoPopup.IsOpen = false;
        }

        /// <summary>
        /// Returns true if <paramref name="source"/> is inside the expander
        /// toggle-button rather than the node's text label.
        /// </summary>
        private static bool IsClickOnExpander(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
            {
                if (source is ToggleButton)
                    return true;
                source = StepUp(source);
            }
            return false;
        }

        /// <summary>
        /// Walks up the visual/logical tree from <paramref name="source"/> to find
        /// the nearest containing <see cref="TreeViewItem"/>.
        /// </summary>
        private static TreeViewItem GetTreeViewItemUnderMouse(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = StepUp(source);
            return source as TreeViewItem;
        }

        /// <summary>
        /// One step up the tree, handling both Visual elements and inline content
        /// elements such as <see cref="Run"/> (which have no visual parent).
        /// </summary>
        private static DependencyObject StepUp(DependencyObject source)
        {
            if (source is FrameworkContentElement fce)
                return fce.Parent;          // e.g. Run → TextBlock
            if (source is Visual)
                return VisualTreeHelper.GetParent(source);
            return null;
        }

        #endregion

        #region Tree Building

        private static (List<TreeNode> roots, TreeNode uncategorized) BuildTree(
            Dictionary<string, string> categories,
            Dictionary<string, string> descriptions)
        {
            var rootDict      = new Dictionary<string, TreeNode>(System.StringComparer.OrdinalIgnoreCase);
            var uncategorized = new TreeNode { TaxonomyName = "Uncategorized" };

            foreach (var kvp in categories)
            {
                var key        = kvp.Key;
                var commonName = kvp.Value;

                // Look up the taxonomy path in descriptions
                if (!descriptions.TryGetValue(key, out var description) ||
                    string.IsNullOrWhiteSpace(description))
                    continue;

                var parts = description.Trim().Trim('"').Split(';');
                if (parts.Length < 2) continue;

                // parts[0] = guid (ignored), parts[1..5] = taxonomy levels
                // parts[6] (if present) is ignored — common name comes from categories
                var path = parts.Skip(1).Take(5)
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();

                if (path.Count == 0)
                {
                    // No taxonomy — goes into Uncategorized
                    uncategorized.Children[key] = new TreeNode
                    {
                        TaxonomyName = commonName,
                        CommonName   = commonName,
                        Key          = key
                    };
                    continue;
                }

                // Walk / create the trie
                var currentChildren = rootDict;
                TreeNode currentNode = null;
                foreach (var segment in path)
                {
                    if (!currentChildren.TryGetValue(segment, out currentNode))
                    {
                        currentNode = new TreeNode { TaxonomyName = segment };
                        currentChildren[segment] = currentNode;
                    }
                    currentChildren = currentNode.Children;
                }

                // Mark the terminal node (first writer wins)
                if (currentNode != null && currentNode.CommonName == null)
                {
                    currentNode.CommonName = commonName;
                    currentNode.Key        = key;
                }
            }

            return (rootDict.Values.ToList(),
                    uncategorized.Children.Count > 0 ? uncategorized : null);
        }

        private void PopulateTreeView(List<TreeNode> roots, TreeNode uncategorized)
        {
            foreach (var root in roots.OrderBy(n => n.TaxonomyName, System.StringComparer.OrdinalIgnoreCase))
                SpecieNetTreeView.Items.Add(CreateTreeViewItem(root));

            if (uncategorized != null)
            {
                var uncatItem = new TreeViewItem { Header = "Uncategorized" };
                foreach (var child in uncategorized.Children.Values
                             .OrderBy(n => n.CommonName, System.StringComparer.OrdinalIgnoreCase))
                {
                    var tb = new TextBlock();
                    tb.Inlines.Add(new Run($"[{child.CommonName}]") { FontWeight = FontWeights.DemiBold });
                    uncatItem.Items.Add(new TreeViewItem { Header = tb, Tag = child });
                }
                SpecieNetTreeView.Items.Add(uncatItem);
            }

            SetExpansion(SpecieNetTreeView.Items, expandAll: RadioExpandAll.IsChecked == true);
        }

        #endregion

        #region TreeViewItem Generation

        private static TreeViewItem CreateTreeViewItem(TreeNode node)
        {
            // Terminal nodes show "taxonomyName [commonName]" with the bracketed part italic.
            // Pure intermediate nodes show only the taxonomy name.
            object header;
            if (node.IsTerminal)
            {
                var tb = new TextBlock();
                tb.Inlines.Add(new Run(node.TaxonomyName));
                tb.Inlines.Add(new Run($" [{node.CommonName}]") { FontWeight = FontWeights.DemiBold });
                header = tb;
            }
            else
            {
                header = node.TaxonomyName;
            }

            var item = new TreeViewItem { Header = header, Tag = node };

            foreach (var child in node.Children.Values
                         .OrderBy(n => n.TaxonomyName, System.StringComparer.OrdinalIgnoreCase))
                item.Items.Add(CreateTreeViewItem(child));

            return item;
        }

        #endregion

        #region TreeNode (private data model)

        private class TreeNode
        {
            public string TaxonomyName { get; set; } = string.Empty;

            /// <summary>Non-null only when this node is the terminal of an entry.</summary>
            public string CommonName { get; set; }

            /// <summary>The category key that produced this terminal node; null for intermediate nodes.</summary>
            public string Key { get; set; }

            public Dictionary<string, TreeNode> Children { get; } =
                new Dictionary<string, TreeNode>(System.StringComparer.OrdinalIgnoreCase);

            public bool IsTerminal => CommonName != null;
            public bool HasChildren => Children.Count > 0;
        }

        #endregion
    }
}
