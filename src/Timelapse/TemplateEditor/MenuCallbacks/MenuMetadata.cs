using System.Windows;
using System.Windows.Controls;
using TimelapseTemplateEditor.EditorCode;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        private void Metadata_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.MenuItemAddLevel.IsEnabled = this.standardType != Timelapse.Constant.Standards.CamtrapDPStandard;
        }

        // Create and select a new empty tab that represents a new level
        private void MenuItemAddLevel_Click(object sender, RoutedEventArgs e)
        {
            // As its a new level, we know there is no data associated with it 
            // However, creating the empty tab means the user can add controls to it if desired
            TabItem tabItem = MetadataUI.CreateEmptyTabForNextLevel();
            if (null != tabItem)
            {
                Globals.Root.MetadataPane.IsSelected = true;
                tabItem.IsSelected = true;
            }
        }

        #region Test Menu Items for Metadata

        //private void MenuItemAlbertaMetadataStandard_Click(object sender, RoutedEventArgs e)
        //{
        //    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        //    List<ColumnTuplesWithWhere> columnsTuplesWithWhereList = new List<ColumnTuplesWithWhere>(); // holds columns which have changed for the current control

        //    this.MetadataUI.RemoveAllMetadataLevelTabs();

        //    int currentLevel = 0;
        //    int id = 1;
        //    foreach (StandardsRow sr in Standards.AlbertaMetadataStandard.FolderMetadataRows)
        //    {
        //        if (currentLevel != sr.Level)
        //        {
        //            // a new level
        //            if (Standards.AlbertaMetadataStandard.Aliases.ContainsKey(sr.Level))
        //            {
        //                Globals.TemplateDatabase.UpsertMetadataInfoTableRow(sr.Level, alias: Standards.AlbertaMetadataStandard.Aliases[sr.Level]);
        //            }

        //            currentLevel = sr.Level;

        //            // We are now on a new level, so create it
        //            this.MetadataUI.CreateNewTabLevelIfNeeded(sr.Level);
        //        }

        //        // Add a row of a particular type, with its default values
        //        MetadataEditRowsControl.DoAddNewRow(sr.Level, sr.Type);

        //        // Update the row to the values specified in the standard
        //        // Also create the where condition with the ID (which starts at 1 and is incremented by 1)
        //        List<ColumnTuple> columnTupleList = new List<ColumnTuple>
        //        {
        //            new ColumnTuple(Constant.Control.DefaultValue, sr.DefaultValue),
        //            new ColumnTuple(Constant.Control.Label, sr.Label),
        //            new ColumnTuple(Constant.Control.DataLabel, sr.DataLabel),
        //            new ColumnTuple(Constant.Control.Tooltip, sr.Tooltip)
        //        };
        //        if (null != sr.Choice)
        //        {
        //            columnTupleList.Add(new ColumnTuple(Constant.Control.List, sr.Choice));
        //        }
        //        columnsTuplesWithWhereList.Add(new ColumnTuplesWithWhere(columnTupleList, id++));

        //        // Now add it all to the database
        //        Globals.TemplateDatabase.Database.Update(Constant.DBTables.MetadataTemplate, columnsTuplesWithWhereList);
        //    }

        //    // Refresh the metadata controls based on the new database contents
        //    Globals.TemplateDatabase.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();

        //    // Switch to the metadata pane
        //    Globals.Root.MetadataPane.IsSelected = true;

        //    // Ensure the preview panels are all scrolled to the tap (which matches the datagrid scrolling position)
        //    if (null != Globals.Root?.MetadataUI?.MetadataTabs?.Items)
        //    {
        //        for (int i = 1; i < Globals.Root.MetadataUI.MetadataTabs.Items.Count; i++)
        //        {
        //            if (Globals.Root.MetadataUI.MetadataTabs.Items[i] is TabItem tabItem)
        //            {
        //                if (null != tabItem.Content && tabItem.Content is MetadataTabControl metadataTabControl)
        //                {
        //                    if (null != metadataTabControl?.MetadataDataEntryPreviewPanel)
        //                    {
        //                        metadataTabControl.MetadataDataEntryPreviewPanel.ScrollIntoViewFirstRow();
        //                    }
        //                }
        //            }
        //        }
        //        //foreach MetadataEntryPreviewPanel
        //        //ScrollIntoViewFirstRow()
        //    }
        //    Mouse.OverrideCursor = null;
        //}
        #endregion
    }
}
