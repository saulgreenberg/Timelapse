using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;
using TimelapseTemplateEditor.Dialog;
using TimelapseTemplateEditor.EditorCode;
using Constant=Timelapse.Constant;

namespace TimelapseTemplateEditor.Controls
{
    /// <summary>
    /// Interaction logic for TemplateDataGridControl.xaml
    /// </summary>
    public partial class TemplateDataGridControl
    {
        public TemplateDataGridControl()
        {
            InitializeComponent();
        }

        #region TemplateDataGrid callbacks
        // Whenever a row changes, save that row to the database, which also updates the grid colors.
        // Note that bulk changes due to code update defers this, so that updates can be done collectively and more efficiently later
        // This is public, as its set externally when a new data grid is loaded
        public void TemplateDataGrid_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            // Utilities.PrintMethodName();
            if (Globals.Root.dataGridBeingUpdatedByCode == false)
            {
                Globals.Root.TemplateDoSyncControlToDatabase(new ControlRow(e.Row));
            }
        }
        #endregion

        /// <summary>
        /// Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        /// Another method re-enables the cell immediately afterwards.
        /// The reason for this implementation is because disabled cells cannot be single clicked, which is needed for row actions.
        /// </summary>
        private void TemplateDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (this.TryGetCurrentCell(out DataGridCell currentCell, out _) == false)
            {
                return;
            }
            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = false;
                this.DataGrid.CancelEdit();
            }
        }

        // After editing is complete, validate the data labels, default values, and widths as needed.
        // Also commit the edit, which will raise the RowChanged event
        private bool manualCommitEdit; 
        private void TemplateDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Stop re-entering, which can occur after we manually perform a CommitEdit (see below) 
            if (this.manualCommitEdit)
            {
                this.manualCommitEdit = false;
                return;
            }

            DataGridRow editedRow = e.Row;
            if (editedRow == null)
            {
                return;
            }
            DataGridColumn editedColumn = e.Column;
            if (editedColumn == null)
            {
                return;
            }

            switch ((string)editedColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    this.ValidateDataLabel(e);
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    this.ValidateDefaults(e, editedRow);
                    break;
                case Constant.Control.Label:
                    this.ValidateLabels(e, editedRow);
                    break;
                case EditorConstant.ColumnHeader.Width:
                    ValidateWidths(e, editedRow);
                    break;
                    //default:
                    //  no restrictions on any of the other editable columns
                    //  break;
            }

            // While hitting return after editing (say) a note will raise a RowChanged event, 
            // clicking out of that cell does not raise a RowChangedEvent, even though the cell has been edited. 
            // Thus we manually commit the edit. 
            Globals.RootEditor.dataGridBeingUpdatedByCode = false;
            this.manualCommitEdit = true;
            this.DataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        /// <summary>
        /// After cell editing ends (prematurely or no), re-enable disabled cells.
        /// See TemplateDataGrid_BeginningEdit for full explanation.
        /// </summary>
        private void TemplateDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (this.TryGetCurrentCell(out DataGridCell currentCell, out _) == false)
            {
                return;
            }
            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = true;
            }
        }

        // Cell editing: Preview character by character entry to disallow spaces in particular fields (DataLabels, Width, Counters
        private void TemplateDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((this.TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.DataGrid.CurrentColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    // Datalabels should not accept spaces - display a warning if needed
                    if (e.Key == Key.Space)
                    {
                        EditorDialogs.EditorDataLabelRequirementsDialog(Globals.RootEditor);
                        e.Handled = true;
                    }
                    // If its a tab, commit the edit before going to the next cell
                    if (e.Key == Key.Tab)
                    {
                        Globals.RootEditor.TemplateDoApplyPendingEdits();
                    }
                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Width should  not accept spaces 
                    e.Handled = e.Key == Key.Space;
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    // Default value for Counters should not accept spaces 
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView)?.Row);
                    if (control.Type == Constant.Control.Counter)
                    {
                        e.Handled = e.Key == Key.Space;
                    }
                    break;
            }
        }

        // Cell editing: Preview final string entry
        // Accept only numbers in counters and widths, t and f in flags (translated to true and false), and alphanumeric or _ in datalabels
        private void TemplateDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if ((this.TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.DataGrid.CurrentColumn.Header)
            {
                // EditorConstant.Control.ControlOrder is not editable
                case EditorConstant.ColumnHeader.DataLabel:
                    // Only allow alphanumeric and '_' in data labels
                    if ((!IsCondition.IsLetterOrDigit(e.Text)) && !e.Text.Equals("_"))
                    {
                        EditorDialogs.EditorDataLabelRequirementsDialog(Globals.RootEditor);
                        e.Handled = true;
                    }
                    break;

                case EditorConstant.ColumnHeader.DefaultValue:
                    // Restrict certain default values for counters, flags (and perhaps fixed choices in the future)
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView)?.Row);
                    switch (control.Type)
                    {
                        case Constant.Control.Counter:
                            // Only allow numbers in counters 
                            e.Handled = !IsCondition.IsDigits(e.Text);
                            break;
                        case Constant.Control.Flag:
                            // Only allow t/f and translate to true/false
                            if (e.Text == "t" || e.Text == "T")
                            {
                                control.DefaultValue = Constant.BooleanValue.True;
                                Globals.RootEditor.TemplateDoSyncControlToDatabase(control);
                            }
                            else if (e.Text == "f" || e.Text == "F")
                            {
                                control.DefaultValue = Constant.BooleanValue.False;
                                Globals.RootEditor.TemplateDoSyncControlToDatabase(control);
                            }
                            e.Handled = true;
                            break;
                        case Constant.Control.FixedChoice:
                            // The default value should be constrained to one of the choices, but that introduces a chicken and egg problem
                            // So lets just ignore it for now.
                            break;
                            // case Constant.Control.Note:
                            // default:
                            // no restrictions on Notes 
                            // break;
                    }
                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Only allow digits in widths as they must be parseable as integers
                    e.Handled = !IsCondition.IsDigits(e.Text);
                    break;
                    //default:
                    //   no restrictions on any of the other editable coumns
                    //   break;
            }
        }

        /// <summary>
        /// Updates colors when rows are added, moved, or deleted.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            if (Globals.TemplateUI == null) return;
            // Greys out cells and updates the visibility of particular rows as defined by logic. 
            // This is to  show the user uneditable cells. Color is also used by code to check whether a cell can be edited.
            // This method should be called after row are added/moved/deleted to update the colors. 
            // This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
            // Finally, it collapses or shows various date-associated rows.
            for (int rowIndex = 0; rowIndex < this.DataGrid.Items.Count; rowIndex++)
            {
                // In order for ItemContainerGenerator to work, we need to set the TemplateGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                // Alternately, we could just do the following, which may be more efficient for large grids (which we normally don't have)
                // this.TemplateDataGrid.UpdateLayout();
                // this.TemplateDataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)this.DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    return;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.DataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    ControlRow control = new ControlRow(((DataRowView)this.DataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;

                    // These columns should always be editable
                    // Note that Width is normally editable unless it is a Flag (as the checkbox is set to the optimal width)
                    string columnHeader = (string)this.DataGrid.Columns[column].Header;
                    if (columnHeader == Constant.Control.Label ||
                        columnHeader == Constant.Control.Tooltip ||
                        columnHeader == Constant.Control.Visible ||
                        (columnHeader == EditorConstant.ColumnHeader.Width && control.Type != Constant.DatabaseColumn.DeleteFlag && control.Type != Constant.Control.Flag))
                    {
                        cell.SetValue(DataGridCell.IsTabStopProperty, true); // Allow tabbing in non-editable fields
                        continue;
                    }

                    // The following attributes should NOT be editable
                    ContentPresenter cellContent = cell.Content as ContentPresenter;
                    string sortMemberPath = this.DataGrid.Columns[column].SortMemberPath;

                    if (String.Equals(sortMemberPath, Constant.DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.Type, StringComparison.OrdinalIgnoreCase) ||
                        controlType == Constant.DatabaseColumn.DateTime ||
                        controlType == Constant.DatabaseColumn.DeleteFlag ||
                        controlType == Constant.DatabaseColumn.File ||
                        controlType == Constant.DatabaseColumn.RelativePath ||
                        (controlType == Constant.Control.Flag && columnHeader == EditorConstant.ColumnHeader.Width) ||
                        (controlType == Constant.Control.Counter && columnHeader == Constant.Control.List) ||
                        (controlType == Constant.Control.Flag && columnHeader == Constant.Control.List) ||
                        (controlType == Constant.Control.Note && columnHeader == Constant.Control.List))
                    {
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;
                        cell.SetValue(DataGridCell.IsTabStopProperty, false);  // Disallow tabbing in non-editable fields

                        // if cell has a checkbox, also disable it.
                        if (cellContent != null)
                        {
                            if (cellContent.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                            {
                                checkbox.IsEnabled = false;
                            }
                        }
                    }
                    else
                    {
                        cell.ClearValue(DataGridCell.BackgroundProperty); // otherwise when scrolling cells offscreen get colored randomly
                        cell.SetValue(DataGridCell.IsTabStopProperty, true);
                        // if cell has a checkbox, enable it.
                        if (cellContent != null)
                        {
                            if (cellContent.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                            {
                                checkbox.IsEnabled = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Logic to enable/disable editing buttons depending on there being a row selection
        /// Also sets the text for the remove row button.
        /// </summary>
        private void TemplateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataGrid.SelectedItem is DataRowView selectedRowView)
            {
                ControlRow control = new ControlRow(selectedRowView.Row);
                Globals.TemplateUI.RowControls.RemoveControlButton.IsEnabled = !Constant.Control.StandardTypes.Contains(control.Type);
            }
            else
            {
                Globals.TemplateUI.RowControls.RemoveControlButton.IsEnabled = false;
            }
        }


        #region Choice List Box Handlers
        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void ChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            ControlRow choiceControl = Globals.TemplateDatabase.Controls.FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));
            if (choiceControl == null)
            {
                TracePrint.PrintMessage($"Control named {button.Tag} not found.");
                return;
            }

            Choices choices = Choices.ChoicesFromJson(choiceControl.List);
            EditChoiceList choiceListDialog = new EditChoiceList(button, choices, Globals.RootEditor);
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                // Ensure that choices disallowing empties have an appropriate default value
                if (false == choiceListDialog.Choices.IncludeEmptyChoice && (string.IsNullOrEmpty(choiceControl.DefaultValue) || false == choiceListDialog.Choices.Contains(choiceControl.DefaultValue)))
                {
                    EditorDialogs.EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(Globals.RootEditor, choiceControl.DefaultValue);
                    choiceControl.DefaultValue = choiceListDialog.Choices.ChoiceList[0];
                }
                // Ensure that non-empty default values matches an entry on the edited choice menu
                else if (!string.IsNullOrEmpty(choiceControl.DefaultValue) && choiceListDialog.Choices.Contains(choiceControl.DefaultValue) == false)
                {
                    EditorDialogs.EditorDefaultChoiceValuesMustMatchChoiceListsDialog(Globals.RootEditor, choiceControl.DefaultValue);
                    choiceControl.DefaultValue = string.Empty;
                }
                choiceControl.List = choiceListDialog.Choices.GetAsJson;
                Globals.RootEditor.TemplateDoSyncControlToDatabase(choiceControl);
            }
        }
        #endregion


        #region Retrieving cells from the datagrid
        // If we can, return the curentCell and the current Row
        private bool TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow)
        {
            if ((this.DataGrid.SelectedIndex == -1) || (this.DataGrid.CurrentColumn == null))
            {
                currentCell = null;
                currentRow = null;
                return false;
            }

            currentRow = (DataGridRow)this.DataGrid.ItemContainerGenerator.ContainerFromIndex(this.DataGrid.SelectedIndex);
            DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);
            currentCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.DataGrid.CurrentColumn.DisplayIndex);
            return currentCell != null;
        }
        #endregion

        #region Validation of Cell contents
        // Validate the data label to correct for empty, duplicate, or non-legal naming
        private void ValidateDataLabel(DataGridCellEditEndingEventArgs e)
        {
            // Check to see if the data label entered is a reserved word or if its a non-unique label
            if (!(e.EditingElement is TextBox textBox))
            {
                TracePrint.NullException();
                return;
            }

            string dataLabel = textBox.Text;

            // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
            if (string.IsNullOrWhiteSpace(dataLabel))
            {
                EditorDialogs.EditorDataLabelsCannotBeEmptyDialog(Globals.RootEditor);
                textBox.Text = Globals.TemplateDatabase.GetNextUniqueDataLabelInControls("DataLabel");
            }

            // Check to see if the data label is unique. If not, generate a unique data label and warn the user
            for (int row = 0; row < Globals.TemplateDatabase.Controls.RowCount; row++)
            {
                ControlRow control = Globals.TemplateDatabase.Controls[row];
                if (dataLabel.Equals(control.DataLabel))
                {
                    if (this.DataGrid.SelectedIndex == row)
                    {
                        continue; // Its the same row, so its the same key, so skip it
                    }
                    EditorDialogs.EditorDataLabelsMustBeUniqueDialog(Globals.RootEditor, textBox.Text);
                    textBox.Text = Globals.TemplateDatabase.GetNextUniqueDataLabelInControls(dataLabel);
                    break;
                }
            }

            // Check to see if the label (if its not empty, which it shouldn't be) has any illegal characters.
            // Note that most of this is redundant, as we have already checked for illegal characters as they are typed. However,
            // we have not checked to see if the first letter is alphabetic.
            if (dataLabel.Length > 0)
            {
                Regex alphanumdash = new Regex("^[a-zA-Z0-9_]*$");
                Regex alpha = new Regex("^[a-zA-Z]*$");

                string firstCharacter = dataLabel[0].ToString();

                if (!(alpha.IsMatch(firstCharacter) && alphanumdash.IsMatch(dataLabel)))
                {
                    string replacementDataLabel = dataLabel;

                    if (!alpha.IsMatch(firstCharacter))
                    {
                        replacementDataLabel = "X" + replacementDataLabel.Substring(1);
                    }
                    replacementDataLabel = Regex.Replace(replacementDataLabel, @"[^A-Za-z0-9_]+", "X");

                    EditorDialogs.EditorDataLabelIsInvalidDialog(Globals.RootEditor, textBox.Text, replacementDataLabel);
                    textBox.Text = replacementDataLabel;
                }
            }

            // Check to see if its a reserved word
            foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
            {
                if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                {
                    EditorDialogs.EditorDataLabelIsAReservedWordDialog(Globals.RootEditor, textBox.Text);
                    textBox.Text += "_";
                    break;
                }
            }
        }

        // Check to see if the current label is a duplicate of another label
        private void ValidateLabels(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            bool editorDialogAlreadyShown = false;

            // ControlRow currentControl = new ControlRow((currentRow.Item as DataRowView).Row);
            if (e.EditingElement is TextBox textBox)
            {
                string label = textBox.Text;
                // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
                if (string.IsNullOrWhiteSpace(label))
                {
                    EditorDialogs.EditorLabelsCannotBeEmptyDialog(Globals.RootEditor);
                    if (currentRow != null)
                    {
                        DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);

                        // try to get the cell but it may possibly be virtualized
                        DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(6);
                        string s = (cell == null)
                            ? "Label"
                            : ((TextBlock)cell.Content).Text;
                        textBox.Text = s; //this.TemplateDatabase.GetNextUniqueLabelInControls(s);
                        label = s;
                        editorDialogAlreadyShown = true;
                    }
                }

                // Check to see if the data label is unique. If not, generate a unique data label and warn the user
                for (int row = 0; row < Globals.TemplateDatabase.Controls.RowCount; row++)
                {
                    ControlRow control = Globals.TemplateDatabase.Controls[row];
                    if (label.Equals(control.Label))
                    {
                        if (this.DataGrid.SelectedIndex == row)
                        {
                            continue; // Its the same row, so its the same key, so skip it
                        }
                        if (false == editorDialogAlreadyShown)
                        {
                            EditorDialogs.EditorLabelsMustBeUniqueDialog(Globals.RootEditor, label);
                        }
                        textBox.Text = Globals.TemplateDatabase.GetNextUniqueLabelInControls(label);
                        break;
                    }
                }
            }
        }

        // Validation of Defaults: Particular defaults (Flags) cannot be empty 
        // There is no need to check other validations here (e.g., unallowed characters) as these will have been caught previously 
        private void ValidateDefaults(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            ControlRow control = new ControlRow((currentRow.Item as DataRowView)?.Row);
            if (!(e.EditingElement is TextBox textBox))
            {
                TracePrint.NullException();
                return;
            }
            switch (control.Type)
            {
                case Constant.Control.Flag:
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = Constant.ControlDefault.FlagValue;
                    }
                    break;
                case Constant.Control.FixedChoice:
                    // Check to see if the value matches one of the items on the menu
                    Choices choices = Choices.ChoicesFromJson(control.List);
                    if (choices.IncludeEmptyChoice == false && string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        // Can't have an empty default if the IncludeEmptyChoice is unselecteds
                        EditorDialogs.EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(Globals.RootEditor, textBox.Text);
                        if (choices.ChoiceList.Count > 0)
                        {
                            // Set it to the first item
                            textBox.Text = choices.ChoiceList[0];
                        }
                        // Note: Undefined if we have an empty choice list with IncludeEmptyChoice of false!
                    }
                    else if (string.IsNullOrWhiteSpace(textBox.Text) == false)
                    {
                        List<string> choicesList = choices.ChoiceList;
                        if (choicesList.Contains(textBox.Text) == false)
                        {
                            EditorDialogs.EditorDefaultChoiceValuesMustMatchChoiceListsDialog(Globals.RootEditor, textBox.Text);
                            if (choices.IncludeEmptyChoice)
                            {
                                textBox.Text = string.Empty;
                            }
                            else
                            {
                                if (choices.ChoiceList.Count > 0)
                                {
                                    // Set it to the first item
                                    textBox.Text = choices.ChoiceList[0];
                                }
                                // Note: Undefined if we have an empty choice list with IncludeEmptyChoice of false!
                            }
                        }
                    }
                    break;
                    //case Constant.Control.Counter:
                    //case Constant.Control.Note:
                    //default:
                    // empty fields are allowed in these control types
                    //   break;
            }
        }

        // Validation of Widths: if a control's width is empty, reset it to its corresponding default width
        private static void ValidateWidths(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            if (currentRow?.Item == null || !(e.EditingElement is TextBox textBox))
            {
                TracePrint.NullException();
                return;
            }

            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                return;
            }

            ControlRow control = new ControlRow((currentRow.Item as DataRowView)?.Row);
            switch (control.Type)
            {
                case Constant.DatabaseColumn.File:
                    textBox.Text = Constant.ControlDefault.FileWidth;
                    break;
                case Constant.DatabaseColumn.DateTime:
                    textBox.Text = Constant.ControlDefault.DateTimeWidth;
                    break;
                case Constant.DatabaseColumn.RelativePath:
                    textBox.Text = Constant.ControlDefault.RelativePathWidth;
                    break;
                case Constant.DatabaseColumn.DeleteFlag:
                case Constant.Control.Flag:
                    textBox.Text = Constant.ControlDefault.FlagWidth.ToString();
                    break;
                case Constant.Control.FixedChoice:
                    textBox.Text = Constant.ControlDefault.FixedChoiceWidth.ToString();
                    break;
                case Constant.Control.Counter:
                    textBox.Text = Constant.ControlDefault.CounterWidth.ToString();
                    break;
                // case Constant.Control.Note:
                default:
                    textBox.Text = Constant.ControlDefault.NoteWidth.ToString();
                    break;
            }
        }
        #endregion
    }

    public class CellTextBlockConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valString = value as string;
            if (!string.IsNullOrEmpty(valString))
            {
                return valString.Trim();
            }
            return string.Empty;
        }
    }
}
