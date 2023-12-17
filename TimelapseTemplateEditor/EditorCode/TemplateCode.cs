using System.Collections.Generic;
using System.Data;
// For debugging
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using Constant=Timelapse.Constant;
using Enums=Timelapse.Enums;
using Timelapse.Database;
using Timelapse.DataTables;
using TimelapseTemplateEditor.Controls;
using TimelapseTemplateEditor.EditorCode;
using System.Windows.Input;
using Timelapse;
using Timelapse.Dialog;
using Timelapse.Util;
using System;

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
            Tuple<bool, TemplateDatabase> tupleResult = await TemplateDatabase.TryCreateOrOpenAsync(templateDatabaseFilePath).ConfigureAwait(true);
            if (!tupleResult.Item1)
            {
                // The template couldn't be loaded
                return false;
            }

            this.templateDatabase = tupleResult.Item2;

            // Map the data table to the data grid, and create a callback which is executed whenever the datatable row changes
            this.templateDatabase.BindToEditorDataGrid(this.TemplateUI.TemplateDataGridControl.DataGrid, Globals.TemplateDataGridControl.TemplateDataGrid_RowChanged);

            // Update the user interface specified by the contents of the table
            Globals.TemplateDataEntryPanelPreviewControl.GeneratePreviewControls(this.TemplateUI.TemplateDataEntryPanelPreview.ControlsPanel, this.templateDatabase.Controls);
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();

            // Enable/disable the various UI elements as needed, including the Add row buttons
            this.userSettings.MostRecentTemplates.SetMostRecent(templateDatabaseFilePath);
            this.ResetUIElements(true, this.templateDatabase.FilePath);
            return true;
        }
        #endregion DataGrid and New Database Initialization

        #region Do Open and close DB Templates 
        // Open and load an existing database file into the UI.
        private async Task TemplateDoOpen(string templateFilePath)
        {
            // This likely isn't needed as the OpenFileDialog won't let us do that anyways. But just in case...
            if (IsCondition.IsPathLengthTooLong(templateFilePath, Enums.FilePathTypeEnum.TDB))
            {
                Dialogs.TemplatePathTooLongDialog(this, templateFilePath);
                return;
            }

            // Initialize the data grid from the template
            if (false == await this.TemplateInitializeFromDBFileAsync(templateFilePath).ConfigureAwait(true))
            {
                Mouse.OverrideCursor = null;
                Dialogs.TemplateFileNotLoadedAsCorruptDialog(this, templateFilePath);
                return;
            }

            // Update the verson number in the templateInfo table to the current version, but only if its a later version
            if (this.templateDatabase != null)
            {
                string timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
                if (VersionChecks.IsVersion1GreaterThanVersion2(timelapseCurrentVersionNumber, TemplateGetVersionCompatability(this.templateDatabase.Database)))
                {
                    this.templateDatabase.UpdateVersionNumber(VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
                }
            }

            // Update the UI state
            this.TemplateUI.HelpMessageInitial.Visibility = Visibility.Collapsed;
            this.MenuFileClose.IsEnabled = true;
        }

        // Close the current template and clear the UI as needed
        private void TemplateDoClose()
        {
            this.TemplateDoApplyPendingEdits();

            // Close the DB file 
            this.templateDatabase = null;
            this.TemplateUI.TemplateDataGridControl.DataGrid.ItemsSource = null;

            // Clear the user interface 
            this.TemplateUI.TemplateDataEntryPanelPreview.ControlsPanel.Children.Clear();
            this.TemplateUI.TemplateSpreadsheetPreviewControl.SpreadsheetPreview.Columns.Clear();

            // Enable/disable the various menus as needed.
            this.ResetUIElements(false, string.Empty);
        }
        #endregion

        #region Add/Remove/Reorder DataRows (ie control definitions)
        // Add a data row (and thus define a new control) to the table
        public void TemplateDoAddNewRow(string controlType)
        {
            // Commit any edits that are in progress
            this.TemplateDoApplyPendingEdits();

            this.dataGridBeingUpdatedByCode = true;
            this.templateDatabase.AddUserDefinedControl(controlType);
            this.TemplateUI.TemplateDataGridControl.DataGrid.DataContext = Globals.TemplateDatabase.Controls;
            this.TemplateUI.TemplateDataGridControl.DataGrid.ScrollIntoView(Globals.TemplateUI.TemplateDataGridControl.DataGrid.Items[Globals.TemplateUI.TemplateDataGridControl.DataGrid.Items.Count - 1]);

            // Update the previews to reflect the added row
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();
            this.TemplateDoUpdateControlOrder();
            this.dataGridBeingUpdatedByCode = false;
        }

        // Remove a data row from the table
        // This also
        // - shifts up the ids on the remaining rows
        // - updates the various previews 
        public void TemplateDoRemoveSelectedRow()
        {
            // Commit any edits that are in progress, which would likely be constrained to edits on the selected row. 
            // This is just in case, as this row will be removed anyways those edits would also be removed.
            this.TemplateDoApplyPendingEdits();

            // Remove the currently selected row (if its exists) from the data grid
            if (this.TemplateUI.TemplateDataGridControl.DataGrid.SelectedItem is DataRowView selectedRowView && selectedRowView.Row != null)
            {
                ControlRow control = new ControlRow(selectedRowView.Row);
                if (TemplateDataEntryPanelPreview.IsStandardControlType(control.Type))
                {
                    // Standard controls cannot be removed
                    // This is just in case: this should not get here as standard control rows are not selectable,
                    return;
                }

                // remove the datagrid row (and thus the control represented by it)
                this.dataGridBeingUpdatedByCode = true;
                this.templateDatabase.RemoveUserDefinedControl(new ControlRow(selectedRowView.Row));

                // Update the view so it reflects the current values in the database
                Globals.TemplateDataEntryPanelPreviewControl.GeneratePreviewControls(Globals.TemplateUI.TemplateDataEntryPanelPreview.ControlsPanel, Globals.TemplateDatabase.Controls);
                Globals.TemplateSpreadsheet.GenerateSpreadsheet();
                this.dataGridBeingUpdatedByCode = false;
            }
        }
        
        public void TemplateDoUpdateControlOrder()
        {
            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            long controlOrder = 1;
            foreach (UIElement element in this.TemplateUI.TemplateDataEntryPanelPreview.ControlsPanel.Children)
            {
                if (!(element is StackPanel stackPanel))
                {
                    continue;
                }
                newControlOrderByDataLabel.Add((string)stackPanel.Tag, controlOrder);
                controlOrder++;
            }
            this.dataGridBeingUpdatedByCode = true;
            this.templateDatabase.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
            this.dataGridBeingUpdatedByCode = false;
            Globals.TemplateDataEntryPanelPreviewControl.GeneratePreviewControls(this.TemplateUI.TemplateDataEntryPanelPreview.ControlsPanel, this.templateDatabase.Controls); // Ensures that the controls panel updates itself
        }
        #endregion

        #region Misc template methods
        // Apply and commit any pending edits that may be pending 
        // e.g., we invoke this to guarantee edits are committed, such as in cases where the enter key was not pressed
        public void TemplateDoApplyPendingEdits()
        {
            this.dataGridBeingUpdatedByCode = false;
            this.TemplateUI.TemplateDataGridControl.DataGrid.CommitEdit();
        }

        /// Update a given control in the database with the control row definition (which is taken from the TemplateDataGrid). 
        public void TemplateDoSyncControlToDatabase(ControlRow control)
        {
            this.dataGridBeingUpdatedByCode = true;
            this.templateDatabase.SyncControlToDatabase(control);
            Globals.TemplateDataEntryPanelPreviewControl.GeneratePreviewControls(this.TemplateUI.TemplateDataEntryPanelPreview.ControlsPanel, this.templateDatabase.Controls);
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();
            this.dataGridBeingUpdatedByCode = false;
        }

        // Return the current version number stored in the TemplateInfo table if it exists . 
        private static string TemplateGetVersionCompatability(SQLiteWrapper db)
        {
            if (db.TableExists(Constant.DBTables.TemplateInfo))
            {
                DataTable table = db.GetDataTableFromSelect(Sql.Select + Constant.DatabaseColumn.VersionCompatabily + Sql.From + Constant.DBTables.TemplateInfo);
                if (table.Rows.Count > 0)
                {
                    return (string)table.Rows[0][Constant.DatabaseColumn.VersionCompatabily];
                }
            }
            return VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
        }
        #endregion
    }
}
