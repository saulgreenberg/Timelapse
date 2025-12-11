using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;
using TimelapseTemplateEditor.EditorCode;
using Control = Timelapse.Constant.Control;
// For debugging

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        #region Initialize the TemplateDataGrid from a new or existing database template
        // Given a database file path,
        // - create a new DB file if one does not exist, or load a DB file if there is one.
        // - map the data table to the data grid
        // - create callbacks for datatable row changes
        private async Task<bool> TemplateInitializeFromDBFileAsync(string templateDatabaseFilePath)
        {
            // Create a new DB file if one does not exist, or load a DB file if there is one.
            Tuple<bool, CommonDatabase> tupleResult = await CommonDatabase.TryCreateOrOpenAsync(templateDatabaseFilePath).ConfigureAwait(true);
            if (!tupleResult.Item1)
            {
                // The template couldn't be loaded
                return false;
            }

            templateDatabase = tupleResult.Item2;

            // Map the data table to the data grid, and create a callback which is executed whenever the datatable row changes
            templateDatabase.BindToEditorDataGrid(TemplateUI.TemplateDataGridControl.DataGridInstance, Globals.TemplateDataGridControl.TemplateDataGrid_RowChanged);

            // Update the user interface specified by the contents of the table
            Globals.TemplateDataEntryPreviewPanelControl.GeneratePreviewControls(TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel, templateDatabase.Controls);
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();

            // Enable/disable the various UI elements as needed, including the Add row buttons
            State.RecentlyOpenedTemplateFiles.SetMostRecent(templateDatabaseFilePath);
            ResetUIElements(true, templateDatabase.FilePath);
            return true;
        }
        #endregion DataGrid and New Database Initialization

        #region Do Open and close DB Templates 
        // Open and load an existing database file into the UI.
        private async Task TemplateDoOpen(string templateFilePath)
        {
            // This likely isn't needed as the OpenFileDialog won't let us do that anyways. But just in case...
            if (IsCondition.IsPathLengthTooLong(templateFilePath, FilePathTypeEnum.TDB))
            {
                Dialogs.TemplatePathTooLongDialog(this, templateFilePath);
                return;
            }

            // Initialize the data grid from the template
            if (false == await TemplateInitializeFromDBFileAsync(templateFilePath).ConfigureAwait(true))
            {
                Mouse.OverrideCursor = null;
                Dialogs.TemplateFileNotLoadedAsCorruptDialog(this, templateFilePath);
                return;
            }

            if (templateDatabase != null)
            {
                // Update the verson number in the templateInfo table to the current version, but only if its a later version
                string timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
                if (VersionChecks.IsVersion1GreaterThanVersion2(timelapseCurrentVersionNumber, TemplateGetVersionCompatability(templateDatabase.Database)))
                {
                    templateDatabase.SetTemplateVersionCompatibility(VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
                }

                // Set the standard being used, if any. This avoids excessive calls to the database
                this.standardType = templateDatabase.GetTemplateStandard();
            }
            
            // Update the UI states
            MenuFileClose.IsEnabled = true;
            MenuMetadata.IsEnabled = true;

            // Handle Metadata: Sync the metadata by creating metadata tabs that reflect each level (if any) in the Metadata table
            await MetadataUI.SyncMetadataTabsFromMetadataTableAsync();
        }

        // Close the current template and clear the UI as needed
        private void TemplateDoClose()
        {
            DataGridCommonCode.ApplyPendingEdits(Globals.Root.TemplateUI.TemplateDataGridControl.DataGridInstance);

            // Close the DB file 
            templateDatabase = null;
            TemplateUI.TemplateDataGridControl.DataGridInstance.ItemsSource = null;
            standardType = string.Empty;

            // Clear the user interface 
            TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel.Children.Clear();
            TemplateUI.TemplateSpreadsheetPreviewControl.SpreadsheetPreview.Columns.Clear();
            MetadataUI.RemoveAllMetadataLevelTabs();

            // Enable/disable the various menus / controls as needed.
            ResetUIElements(false, string.Empty);
            MenuMetadata.IsEnabled = false;
            this.TemplateUI.RowControls.IsEnabled = true; 
            
            // Reset a few state variables
            State.AlreadyWarnedAboutOpenWithOlderVersionOfTimelapse = false;
        }
        #endregion

        #region Add/Remove/Reorder DataRows (ie control definitions)
        // Add a data row (and thus define a new control) to the table
        public void TemplateDoAddNewRow(string controlType)
        {
            DataGrid dataGrid = TemplateUI.TemplateDataGridControl.DataGridInstance;
            // Commit any edits that are in progress
            DataGridCommonCode.ApplyPendingEdits(dataGrid);

            dataGridBeingUpdatedByCode = true;
            templateDatabase.AddControlToDataTableAndDatabase(controlType);

            dataGrid.DataContext = Globals.TemplateDatabase.Controls;

            // Update the previews to reflect the added row
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();
            TemplateDoUpdateControlOrder();

            // Scroll the last element of the datagrid into view (as we are adding something at the end)
            dataGrid.ScrollIntoViewLastRow();

            dataGridBeingUpdatedByCode = false;
        }

        // Remove a data row from the table
        // This also
        // - shifts up the ids on the remaining rows
        // - updates the various previews 
        public void TemplateDoRemoveSelectedRow()
        {
            // Commit any edits that are in progress, which would likely be constrained to edits on the selected row. 
            // This is just in case, as this row will be removed anyways those edits would also be removed.
            DataGrid dataGrid = TemplateUI.TemplateDataGridControl.DataGridInstance;
            DataGridCommonCode.ApplyPendingEdits(dataGrid);

            // Remove the currently selected row (if its exists) from the data grid
            if (TemplateUI.TemplateDataGridControl.DataGridInstance.SelectedItem is DataRowView selectedRowView)
            {
                ControlRow control = new(selectedRowView.Row);
                if (PreviewControlCommon.IsStandardControlType(control.Type))
                {
                    // Standard controls cannot be removed
                    // This is just in case: this should not get here as standard control rows are not selectable,
                    return;
                }

                // remove the datagrid row (and thus the control represented by it)
                dataGridBeingUpdatedByCode = true;
                templateDatabase.RemoveControlFromDataTableAndDatabase(new(selectedRowView.Row));

                // Update the view so it reflects the current values in the database
                Globals.TemplateDataEntryPreviewPanelControl.GeneratePreviewControls(Globals.TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel, Globals.TemplateDatabase.Controls);
                Globals.TemplateSpreadsheet.GenerateSpreadsheet();
                dataGridBeingUpdatedByCode = false;
            }
        }
        
        public void TemplateDoUpdateControlOrder()
        {
            Dictionary<string, long> newControlOrderByDataLabel = [];
            long controlOrder = 1;
            foreach (UIElement element in TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel.Children)
            {
                if (element is not StackPanel stackPanel)
                {
                    continue;
                }
                newControlOrderByDataLabel.Add((string)stackPanel.Tag, controlOrder);
                controlOrder++;
            }
            dataGridBeingUpdatedByCode = true;
            templateDatabase.UpdateControlDisplayOrder(Control.ControlOrder, newControlOrderByDataLabel);
            dataGridBeingUpdatedByCode = false;
            Globals.TemplateDataEntryPreviewPanelControl.GeneratePreviewControls(TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel, templateDatabase.Controls); // Ensures that the controls panel updates itself
        }
        #endregion

        #region Misc template methods
        /// Update a given control in the database with the control row definition (which is taken from the TemplateDataGrid). 
        public void TemplateDoSyncControlToDatabase(ControlRow control)
        {
            dataGridBeingUpdatedByCode = true;
            templateDatabase.SyncControlToDatabase(control);
            Globals.TemplateDataEntryPreviewPanelControl.GeneratePreviewControls(TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel, templateDatabase.Controls);
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();
            dataGridBeingUpdatedByCode = false;
        }

        // Return the current version number stored in the TemplateInfo table if it exists . 
        public static string TemplateGetVersionCompatability(SQLiteWrapper db)
        {
            if (db.TableExists(DBTables.TemplateInfo))
            {
                DataTable table = db.GetDataTableFromSelect(Sql.Select + DatabaseColumn.VersionCompatibility + Sql.From + DBTables.TemplateInfo);
                if (table.Rows.Count > 0)
                {
                    return (string)table.Rows[0][DatabaseColumn.VersionCompatibility];
                }
            }

            string version = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
            // TODO: FIGURE OUT THIS ISSUE
            return string.IsNullOrWhiteSpace(version) ? "2.3.3.0" : version;
        }
        #endregion
    }
}
