using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;
using TimelapseTemplateEditor.ControlsMetadata;
using TimelapseTemplateEditor.EditorCode;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        // Add a metadata row to an existing level by 
        // - updating the database
        // - updating or creating the tab control for that level
        public void DoAddNewMetadataControlToLevel(int level, string controlType)
        {
            // Add the metadata control row to the metadata table in the database
            UpdateStateEnum updateState = templateDatabase.AddMetadataControlToDataTableAndDatabase(level, controlType);

            // Get the metadataTabControl for the just added row. 
            // If it doesn't exist, create it.
            MetadataTabControl metadataTabControl = MetadataUI.GetMetadataTabControlByLevel(level);
            if (null == metadataTabControl)
            {
                MetadataUI.RemoveAllMetadataLevelTabs();
                MetadataUI.CreateNewTabLevelIfNeeded(level);
            }
            // Bind the data table to the DataGrid, but only if a new data table was created
            if (updateState == UpdateStateEnum.Created)//|| updateState == UpdateStateEnum.Modified)
            {
                MetadataUI.BindMetadataToDataGrid(level);
            }

            // Scroll the last element of the datagrid into view (as we are adding something at the end)
            DataGrid dataGrid = metadataTabControl?.MetadataGridControl?.DataGridInstance;
            dataGrid.ScrollIntoViewLastRow();

            // Update the previews to reflect the added row, also scrolling to the end
            Globals.Root.MetadataTemplateDoGeneratePreviews(level, true);
        }

        // Remove a metadata row from the database table, the corresponding data structures and from the UI
        // - the control and spreadsheet order are adjusted to fill in the gap, if any, on the remaining rows.
        public async Task DoRemoveSelectedMetadataRow(int level)
        {
            // Check for null conditions

            MetadataTabControl metadataTabControl = MetadataUI?.GetMetadataTabControlByLevel(level);
            if (metadataTabControl?.MetadataGridControl?.DataGridInstance == null) return;
            DataGrid dataGrid = metadataTabControl.MetadataGridControl.DataGridInstance;

            // Commit any edits that are in progress, which would likely be constrained to edits on the selected row. 
            // This is just in case. Since this row will be removed anyways, those edits would also be removed.
            DataGridCommonCode.ApplyPendingEdits(dataGrid);

            // Remove the currently selected row (if it exists) 
            if (dataGrid.SelectedItem is DataRowView selectedRowView)
            {
                // Remove the row from the datatables and database (and thus the control)
                dataGridBeingUpdatedByCode = true;
                await templateDatabase.RemoveMetadataControlFromDataTableAndDatabase(level, new(selectedRowView.Row));

                // Update the datagrid view so it reflects the current values in the database
                if (Globals.TemplateDatabase.MetadataControlsByLevel.ContainsKey(level))
                {
                    MetadataUI.BindMetadataToDataGrid(level);
                }
                else
                {
                    dataGrid.ItemsSource = null;
                }

                // Update the previews
                MetadataTemplateDoGeneratePreviews(level);
                dataGridBeingUpdatedByCode = false;
            }
        }

        public void DoUpdateMetadataControlOrder(MetadataEntryPreviewPanel metadataEntryPreviewPanel, int level)
        {
            Dictionary<string, long> newControlOrderByDataLabel = [];
            long controlOrder = 1;
            foreach (UIElement element in metadataEntryPreviewPanel.ControlsPanel.Children)
            {
                if (element is not Grid grid)
                {
                    continue;
                }
                newControlOrderByDataLabel.Add((string)grid.Tag, controlOrder);
                controlOrder++;
            }
            dataGridBeingUpdatedByCode = true;
            templateDatabase.UpdateMetadataControlDisplayOrder(level, Control.ControlOrder, newControlOrderByDataLabel);
            MetadataUI.BindMetadataToDataGrid(level);
            dataGridBeingUpdatedByCode = false;
            MetadataTemplateDoGeneratePreviews(level);
        }

        #region Misc template methods
        /// Update a given control in the database with the control row definition (which is taken from the TemplateDataGrid). 
        public void DoSyncMetadataControlToDatabase(MetadataControlRow control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            dataGridBeingUpdatedByCode = true;
            templateDatabase.SyncMetadataControlsToDatabase(control);

            // Update the corresponding preview panel to match the modified controls
            MetadataTemplateDoGeneratePreviews(control.Level);
            dataGridBeingUpdatedByCode = false;
        }

        // Generate the previews without scrolling
        public void MetadataTemplateDoGeneratePreviews(int level)
        {
            MetadataTemplateDoGeneratePreviews(level, false);
        }
        public void MetadataTemplateDoGeneratePreviews(int level, bool scrollUIPanelToEnd)
        {
            // Update the corresponding preview panel to match the modified controls
            MetadataTabControl metadataTabControl = MetadataUI.GetMetadataTabControlByLevel(level);
            if (null == metadataTabControl?.MetadataDataEntryPreviewPanel || null == metadataTabControl.MetadataSpreadsheetPreviewControl)
            {
                // this shouldn't happen
                return;
            }
            metadataTabControl.MetadataDataEntryPreviewPanel.GeneratePreviewControls(level);
            metadataTabControl.MetadataSpreadsheetPreviewControl.GenerateSpreadsheet(level);

            if (scrollUIPanelToEnd)
            {
                metadataTabControl.MetadataDataEntryPreviewPanel.ScrollIntoViewLastRow();
            }

        }
    }
    #endregion
}
