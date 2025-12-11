using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.DataTables;
using TimelapseTemplateEditor.EditorCode;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.ControlsMetadata
{
    /// <summary>
    /// Interaction logic for MetadataUIControl.xaml
    /// </summary>

    public partial class MetadataUIControl
    {
        #region Variables
        private CommonDatabase TemplateDatabase => Globals.TemplateDatabase;
        #endregion

        #region Constructor and Loading
        public MetadataUIControl()
        {
            InitializeComponent();
        }
        #endregion

        #region Sync all metadata tabs to all corresponding metadata table levels in the database
        // Build (or rebuild) the metadata tabs and its data tables to reflect the MetadataTable in the database
        public async Task SyncMetadataTabsFromMetadataTableAsync()
        {
            // Clear any existing the Metadata tabs
            this.RemoveAllMetadataLevelTabs();

            //if (null == this.Levels || 0 == this.Levels.Count)
            //if (0 == TemplateDatabase.GetMetadataInfoTableMaxLevel())
            //{
            //    // There is no metadata, so don't create any tabs
            //    return;
            //}

            // Reload the metadata from the database to ensure we have the most up to data copy
            // While it may not be necessary, the table is fairly small so this comes at low cost
            await TemplateDatabase.LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();

            // Create a tab representing each level up to the maximum level recorded in the data table
            // and bind the metadata at that level to the datagrid
            int maxLevel = TemplateDatabase.GetMetadataInfoTableMaxLevel();
            for (int level = 1; level <= maxLevel; level++)
            {
                // Create the new tab for each level.
                // If no metadata exists for a particular level, the tab will contain an 'empty' datatable that can be added to.
                TabItem tabItem = CreateNewTabLevelIfNeeded(level);

                if (null != tabItem && TemplateDatabase.MetadataControlsByLevel.ContainsKey(level))
                {
                    // If there are rows for this level in the metadata table,
                    // bind it to its corresponding data grid control.
                    // The test below is redundant, but put in for now just in case
                    if (false == BindMetadataToDataGrid(level))

                        MessageBox.Show("$Could not bind the datagrid for level {level}");
                }
            }
        }
        #endregion

        #region Remove MetadataTabs
        // Remove all the metadata tabs except the one containing the instructions
        public void RemoveAllMetadataLevelTabs()
        {
            for (int i = MetadataTabs.Items.Count - 1; i >= 0; i--)
            {
                if (MetadataTabs.Items[i] is TabItem { Header: TextBlock tb } tabItem &&
                    tb.Text != MetadataInstructionsHeaderText.Text)
                {
                    // To try to eliminate this non-fatal error message (but not sure if it works - keep monitoring)
                    // System.Windows.Data Error: 4 : Cannot find source for binding with reference 'RelativeSource FindAncestor, AncestorType='System.Windows.Controls.TabControl'
                    tabItem.Template = null;
                    MetadataTabs.Items.RemoveAt(i);
                }
            }
        }
        #endregion

        #region Show/Hide columns

        public void ExtraColumnsVisibility(Visibility visibility)
        {
            for (int i = MetadataTabs.Items.Count - 1; i >= 0; i--)
            {
                if (MetadataTabs.Items[i] is TabItem tabItem &&
                    ((TextBlock)tabItem.Header).Text != MetadataInstructionsHeaderText.Text &&
                    tabItem.Content is MetadataTabControl metadataTabControl)
                {
                    foreach (DataGridColumn column in metadataTabControl.MetadataGridControl.Columns)
                    {
                        if (column.Header == null)
                        {
                            // Just in case we have a column with no header
                            continue;
                        }
                        if (column.Header.Equals(EditorConstant.ColumnHeader.ID) ||
                            column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) ||
                            column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                        {
                            column.Visibility = visibility;
                        }
                    }
                    metadataTabControl.MetadataGridControl.DoLayoutUpdated(true);
                }
            }
        }
        #endregion

        #region Utilities
        // Return the MetadataTabControl for a particular level, else null
        public MetadataTabControl GetMetadataTabControlByLevel(int level)
        {
            for (int i = MetadataTabs.Items.Count - 1; i >= 0; i--)
            {
                if (MetadataTabs.Items[i] is TabItem { Content: MetadataTabControl metadataTabControl } &&
                    metadataTabControl.Level == level)
                {
                    return metadataTabControl;
                }
            }
            return null;
        }

        public TabItem GetTabItemByLevel(int level)
        {
            for (int i = MetadataTabs.Items.Count - 1; i >= 0; i--)
            {
                if (MetadataTabs.Items[i] is TabItem { Content: MetadataTabControl metadataTabControl } tabItem &&
                    metadataTabControl.Level == level)
                {
                    return tabItem;
                }
            }
            return null;
        }

        // Bind the metadata to the datagrid responsible for a particular level
        public bool BindMetadataToDataGrid(int level)
        {
            MetadataTabControl metadataTabControl = GetMetadataTabControlByLevel(level);
            if (null == metadataTabControl )
            {
                return false;
            }

            if (null == TemplateDatabase?.MetadataControlsByLevel)
            {
                return false;
            }
            if (false == TemplateDatabase.MetadataControlsByLevel.ContainsKey(level))
            {
                return false;
            }

            DataTableBackedList<MetadataControlRow> row = TemplateDatabase.MetadataControlsByLevel[level];
            row.BindDataGrid(metadataTabControl.MetadataGridControl.DataGridInstance, metadataTabControl.MetadataGridControl.MetadataDataGrid_RowChanged);
            return true;
        }
        #endregion

        #region Tab Creation and manipulation
        // Create a new empty tab that represents a new level
        public TabItem CreateEmptyTabForNextLevel()
        {
            // Determine the next level
            // and then create the new empty tab for that level
            int nextLevel = 1 + TemplateDatabase.GetMetadataInfoTableMaxLevel();
            return CreateNewTabLevelIfNeeded(nextLevel);
        }

        // Create a new tab representing a particular level
        // If a tab item already exists for that level, just return it.
        public TabItem CreateNewTabLevelIfNeeded(int level)
        {
            string alias;
            // The tab for that level already exists
            TabItem tabItem = GetTabItemByLevel(level);
            if (null != tabItem)
            {
                return tabItem;
            }

            // Check if we already have an info row for that level
            DataTable metadataRow = TemplateDatabase.GetMetadataInfoTableRow(level);
            if (metadataRow.Rows.Count > 0)
            {
                alias = (string)metadataRow.Rows[0][Control.Alias];
            }
            else
            {
                alias = CreateTemporaryAliasIfNeeded(level, "");
            }
            // The tab for that level does not exist. Create it, along with its contained MetadataTabControl
            // Headers need to be TextBlocks so we can adjust its font weight when selected
            TextBlock tb = new()
            {
                Text = level.ToString()
            };

            tabItem = new()
            {
                Header = tb,
                Content = new MetadataTabControl(level, alias)
            };

            MetadataTabs.Items.Add(tabItem);
            MetadataTabControl mtb = (MetadataTabControl)tabItem.Content;
            if (false == string.IsNullOrWhiteSpace(alias))
            {
                // We have an alias.
                // As a side effect of setting it in the MetadataTabControl, it will also set the tab header
                // and upsert it which we don't want...
                mtb.LevelAlias = alias;
            }
            else
            {
                // Otherwise we have to do it explicitly
                tb.Text = CreateTemporaryAliasIfNeeded(level, alias);
                mtb.LevelAlias = string.Empty;
            }

            if (metadataRow.Rows.Count == 0)
            {
                TemplateDatabase.UpsertMetadataInfoTableRow(mtb.Level, guid: mtb.Guid, alias: mtb.LevelAlias);
            }
            return tabItem;
        }

        #endregion

        #region Callback: OnSelectionChanged, ActivateTab
        // Go through the tabs
        // - selected tab: bold that tab header and build the tab content previews as needed,
        // - make other tab headers a normal font.
        private void MetadataTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // We test originalSource as a tab control, as other unwanted selection changed events
            // can bubble up from other controls contained by the tab control eg datagrid selections
            // Alternately, we could ensure that those child controls are done where e.Handled == true
            // but that would be a pain to do and somewhat buggy.
            if (!(e.OriginalSource is TabControl tabControl)) return;

            foreach (TabItem tabItem in tabControl.Items)
            {
                if (tabItem == tabControl.SelectedItem)
                {
                    // activating the selected tab bolds the header and builds the previews
                    ActivateTab(tabItem);
                }
                else if (tabItem.Header is TextBlock header)
                {
                    // Other tab headers are normal font weight
                    header.FontWeight = FontWeights.Normal;
                }
            }
        }

        // Whenever a metadata level tab is activated,
        // - bold the active tab header
        // - generate the preview controls
        // - Enable/Disable remove button as needed 
        // Todo: Alternately, we could generate the preview for all tabs ahead of time. To consider.
        public void ActivateTab(TabItem tabItem)
        {
            if (null == tabItem) return;

            if (tabItem.Header is TextBlock header)
            {
                // Bold the tab header
                header.FontWeight = FontWeights.Bold;
            }

            if (tabItem.Content is MetadataTabControl metadataTabControl)
            {
                // Enable or disable the remove button
                if (null != metadataTabControl.MetadataGridControl?.DataGridInstance)
                {
                    metadataTabControl.MetadataEditRowControls.RemoveControlButton.IsEnabled = metadataTabControl.MetadataGridControl.DataGridInstance.SelectedIndex >= 0;
                }

                BindMetadataToDataGrid(metadataTabControl.Level);

                // Generate the preview controls
                Globals.Root.MetadataTemplateDoGeneratePreviews(metadataTabControl.Level);
            }
        }
        #endregion

        #region Utilities
        public static string CreateTemporaryAliasIfNeeded(int level, string alias)
        {
            if (false == string.IsNullOrEmpty(alias))
            {
                return alias;
            }
            // As there is as yet no alias, we need to create one
            // Note that we decrement level as the level numbers should be 0-based rather than 1-based
                return level == 1
                    ? "Root-0"
                    : $"Level-{level - 1}";
        }
        #endregion
    }
}
