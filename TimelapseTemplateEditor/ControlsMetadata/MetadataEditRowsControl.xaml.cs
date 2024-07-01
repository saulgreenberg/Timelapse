using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimelapseTemplateEditor.Dialog;
using TimelapseTemplateEditor.EditorCode;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBox = System.Windows.Controls.TextBox;

namespace TimelapseTemplateEditor.ControlsMetadata
{
    /// <summary>
    /// Interaction logic for MetadataEditRowsControl.xaml
    /// </summary>
    public partial class MetadataEditRowsControl
    {
        private bool updating;

        public MetadataTabControl ParentTab { get; set; }

        #region Constructor, Loaded

        public MetadataEditRowsControl()
        {
            InitializeComponent();
        }


        private void MetadataEditRowsControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (null == ParentTab)
            {
                return;
            }

            updating = true;
            TextBoxLevelLabel.Text = ParentTab.LevelAlias;
            // Note that we have to make the level names 0-based...
            TextBlockLevel.Text = (ParentTab.Level - 1).ToString();
            updating = false;
        }

        #endregion

        #region Callbacks: Alias Text Changed and KeyDown

        private void TextBoxLevelLabel_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (null == ParentTab || updating)
            {
                return;
            }

            ParentTab.LevelAlias = TextBoxLevelLabel.Text;
            Globals.TemplateDatabase.UpsertMetadataInfoTableRow(ParentTab.Level, alias: ParentTab.LevelAlias);
        }


        private void TextBoxLevelLabel_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox && (e.Key == Key.Return || e.Key == Key.Enter || e.Key == Key.Tab))
            {
                Keyboard.ClearFocus();
            }
        }

        #endregion

        #region Callbacks: Add/Remove data row

        // Add a data row (i.e. a control definition) to the datagrid and thus the template.
        // - the row type is decided by the button tags.
        // - default values are set for the added row, and differ depending on type.
        private void MenuItemAddDataRow_Click(object sender, RoutedEventArgs e)
        {
            if (null == ParentTab)
            {
                return;
            }

            if (sender is MenuItem mi)
            {
                // Retrieve the button's tag, which specifies the control type
                string controlType = (string)mi.Tag;
                DoAddNewRow(ParentTab.Level, controlType);
            }
        }

        // Remove a metadata row from the database table, the corresponding data structures and from the UI
        // - the control and spreadsheet order are adjusted to fill in the gap, if any, on the remaining rows.
        private async void ButtonRemoveDataRow_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Root.DoRemoveSelectedMetadataRow(ParentTab.Level);
            RemoveControlButton.IsEnabled = false;
        }

        // Add a data row (and thus define a new control) to the table
        public static void DoAddNewRow(int level, string controlType)
        {
            Globals.Root.DoAddNewMetadataControlToLevel(level, controlType);

        }

        #endregion

        #region Callbacks: Edit levels
        // Delete the current level, but ask first
        private async void MenuItemDeleteLevel_Click(object sender, RoutedEventArgs e)
        {
            string aliasToUse = string.IsNullOrEmpty(this.ParentTab.LevelAlias)
                ? $"{MetadataUIControl.CreateTemporaryAliasIfNeeded(this.ParentTab.Level, this.ParentTab.LevelAlias)}"
                : $"{this.ParentTab.LevelAlias} ({this.ParentTab.Level})";
            if (false == EditorDialogs.EditorDeleteFolderLevelWarning(Globals.Root, aliasToUse))
            {
                return;
            }

            Globals.Root.templateDatabase.MetadataDeleteLevelFromDatabase(ParentTab.Level);
            await Globals.Root.MetadataUI.SyncMetadataTabsFromMetadataTableAsync();
        }

        // Enable or disable the Backwards/Forwards buttons depending on whether the current level is at the beginning or the end
        private void MenuEditLevel_Click(object sender, RoutedEventArgs e)
        {
            int level = this.ParentTab.Level;
            int maxLevel = Globals.Root.templateDatabase.MetadataInfo.RowCount; // as rowcount is zero based
            this.MenuItemMoveLevelBackwards.IsEnabled = level > 1;
            this.MenuItemMoveLevelForwards.IsEnabled = level < maxLevel;
        }

        // Move the level forwards or backwards. Assumes it will only be invoked if it can move in that direction (see above, which disables the forward / backwards button as needed)
        private async void MenuItemMoveLevelForwardsOrBackwards_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                bool backwards = mi.Name == "MenuItemMoveLevelBackwards";
                int correction = backwards ? -1 : 1;
                int level = this.ParentTab.Level;
                int maxLevel = Globals.Root.templateDatabase.MetadataInfo.RowCount;
                Globals.Root.templateDatabase.MetadataMoveLevelForwardsOrBackwardsInDatabase(level, maxLevel, backwards);
                await Globals.Root.MetadataUI.SyncMetadataTabsFromMetadataTableAsync();
                // DISPLAY THE TAB
                Globals.Root.MetadataUI.GetTabItemByLevel(level + correction).IsSelected = true;
            }
        }
        #endregion

    }
}
