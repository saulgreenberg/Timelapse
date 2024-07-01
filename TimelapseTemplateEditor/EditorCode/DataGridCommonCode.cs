using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.DataTables;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.EditorCode
{
    // These methods are used by both the MetadataDataGridControl and the TemplateDataGridControl
    public static class DataGridCommonCode
    {
        #region ApplyPendingEdits
        // Apply and commit any pending edits that may be pending 
        // e.g., this is invoke to guarantee that current edits, if any, are committed
        // Examples include cases where the enter key was not pressed, a menu being selected during editing, etc.
        public static void ApplyPendingEdits(DataGrid dataGrid)
        {
            Globals.Root.dataGridBeingUpdatedByCode = false;
            dataGrid.CommitEdit();
        }
        #endregion

        #region Callback actions (names reflect Callback) BeginningEdit, CurrentCellChanged
        // Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        // Another method will re-enable the cell immediately afterwards.
        // The reason for this implementation is because disabled cells cannot be single clicked,
        // but we still need to recognize the single click for row actions.
        public static void BeginningEdit(DataGrid dataGrid)
        {
            if (TryGetCurrentCell(dataGrid, out DataGridCell currentCell, out _) == false)
            {
                return;
            }

            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = false;
                dataGrid.CancelEdit();
            }
        }

        // After cell editing ends (prematurely or no), re-enable the cell, if its disabled.
        // See MetadataDataGrid_BeginningEdit below for full explanation.
        public static void CurrentCellChanged(DataGrid dataGrid)
        {
            if (TryGetCurrentCell(dataGrid, out DataGridCell currentCell, out _) == false)
            {
                return;
            }
            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = true;
            }
        }

        public static void UpdateCellEditabilityAndVisibility(DataGrid dataGrid)
        {
            // Greys out cells and updates the visibility of particular rows as defined by logic. 
            // This is to  show the user uneditable cells. Color is also used by code to check whether a cell can be edited.
            // This method should be called after row are added/moved/deleted to update the colors. 
            // This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
            // Finally, it collapses or shows various date-associated rows.
            for (int rowIndex = 0; rowIndex < dataGrid.Items.Count; rowIndex++)
            {

                // Approach :  In order for ItemContainerGenerator to work, we need to set the dataGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                //             but that didn't seem to work.
                // As an alternate, we  do the UpdataLayout and ScrollIntoView which forces it to appear
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    continue;
                    //return;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < dataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    MetadataControlRow control = new MetadataControlRow(((DataRowView)dataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;

                    // These columns should always be editable
                    // Note that Width is normally editable unless it is a Flag (as the checkbox is set to the optimal width)
                    string columnHeader = (string)dataGrid.Columns[column].Header;
                    if (columnHeader == Control.Label ||
                        columnHeader == Control.Tooltip ||
                        columnHeader == Control.Visible
                        )
                    {
                        cell.SetValue(System.Windows.Controls.Control.IsTabStopProperty, true); // Allow tabbing in non-editable fields
                        continue;
                    }

                    // The following attributes should NOT be editable
                    ContentPresenter cellContent = cell.Content as ContentPresenter;
                    string sortMemberPath = dataGrid.Columns[column].SortMemberPath;
                    if (
                        // Types are never editable
                        String.Equals(sortMemberPath, DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Control.Type, StringComparison.OrdinalIgnoreCase) ||

                        // These four standard controls are treated as special cases, where certain columns are never editable
                        (controlType == DatabaseColumn.File &&
                            (columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable ||
                             columnHeader == EditorConstant.ColumnHeader.Export)) ||
                        (controlType == DatabaseColumn.RelativePath &&
                            (columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable ||
                             columnHeader == EditorConstant.ColumnHeader.Export)) ||
                        (controlType == DatabaseColumn.DateTime &&
                            (columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable)) ||
                        (controlType == DatabaseColumn.DeleteFlag &&
                            (columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable ||
                             columnHeader == EditorConstant.ColumnHeader.Width)) ||

                        // These default values are  editable but if we want to change that, uncomment this
                        //(controlType == Control.DateTime_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||
                        //(controlType == Control.Date_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||
                        //(controlType == Control.Time_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||
                        //(controlType == Control.MultiChoice && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||

                        // Flag widths are never editable
                        (controlType == Control.Flag && columnHeader == EditorConstant.ColumnHeader.Width) ||

                        // Any control's List except for FixedChoice and MultiChoice are never editable
                        ((controlType != Control.FixedChoice && controlType != Control.MultiChoice) && columnHeader == Control.List)

                    )
                    {
                        if (controlType == Control.Date_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue)
                        {
                            // Date_ values are the complete date/time. As we only want to display the date portion, we trim the Time_ portion off here.
                            if (cell.Content is TextBlock textBlock && false == string.IsNullOrWhiteSpace(textBlock.Text))
                            {
                                textBlock.Text = textBlock.Text.Split()[0];
                            }
                        }
                        else if (controlType == Control.Time_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue)
                        {
                            // Time_ values are the complete date/time. As we only want to display the time portion, we trim the Date_ portion here.
                            if (cell.Content is TextBlock textBlock && false == string.IsNullOrWhiteSpace(textBlock.Text))
                            {
                                string[] splitText = textBlock.Text.Split();
                                if (splitText.Length > 1)
                                {
                                    textBlock.Text = textBlock.Text.Split()[1];
                                }
                            }
                        }
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;
                        cell.SetValue(System.Windows.Controls.Control.IsTabStopProperty, false);  // Disallow tabbing in non-editable fields

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
                        cell.ClearValue(System.Windows.Controls.Control.BackgroundProperty); // otherwise when scrolling cells offscreen get colored randomly
                        cell.SetValue(System.Windows.Controls.Control.IsTabStopProperty, true);
                        // if cell has a checkbox, enable it.
                        if (cellContent != null)
                        {
                            if (cellContent.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                            {
                                checkbox.IsEnabled = true;
                            }

                            // For some odd reason, the style setting which should do this is not working,
                            // So I have to hard code making the List button for Choice types visible.
                            if (controlType == Control.FixedChoice &&
                                columnHeader == Control.List &&
                                cellContent.ContentTemplate.FindName("btnButton", cellContent) is Button button)
                            {
                                button.Visibility = Visibility.Visible;
                                button.Background = Brushes.White;
                            }
                            if (controlType == Control.MultiChoice &&
                                columnHeader == Control.List &&
                                cellContent.ContentTemplate.FindName("btnButton", cellContent) is Button button2)
                            {
                                button2.Visibility = Visibility.Visible;
                                button2.Background = Brushes.White;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Retrieving cells from the datagrid
        // If we can, return the curentCell and the current Row
        public static bool TryGetCurrentCell(DataGrid dataGrid, out DataGridCell currentCell, out DataGridRow currentRow)
        {
            if (dataGrid.SelectedIndex == -1 || dataGrid.CurrentColumn == null)
            {
                currentCell = null;
                currentRow = null;
                return false;
            }

            currentRow = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(dataGrid.SelectedIndex);
            DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);
            currentCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(dataGrid.CurrentColumn.DisplayIndex);
            return currentCell != null;
        }

        public static bool TryGetCurrentEditableCell(DataGrid dataGrid, out DataGridCell currentCell, out DataGridRow currentRow)
        {
            return TryGetCurrentCell(dataGrid, out currentCell, out currentRow)
                   && !currentCell.Background.Equals(EditorConstant.NotEditableCellColor);
        }
        #endregion

        #region Utilities
        public static bool IsRowOrColumNull(DataGridRow row, DataGridColumn column)
        {
            if (row == null)
            {
                return true;
            }

            if (column == null)
            {
                return true;
            }
            return false;
        }
        #endregion
    }

    #region Class CellTextBlockConverter
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
    #endregion
}
