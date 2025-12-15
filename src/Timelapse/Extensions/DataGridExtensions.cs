using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Windows.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Extensions
{
    // Methods to manipulate a datagrid. 
    public static class DataGridExtensions
    {
        #region Public methods
        extension(DataGrid dataGrid)
        {
            /// <summary>
            /// Sort the given data grid by the given column number in ascending order
            /// </summary>
            public void SortByColumnAscending(int columnNumber)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(dataGrid, nameof(dataGrid));

                // Clear current sort descriptions
                dataGrid.Items.SortDescriptions.Clear();

                // Add the new sort description
                DataGridColumn firstColumn = dataGrid.Columns[columnNumber];
                ListSortDirection sortDirection = ListSortDirection.Ascending;
                dataGrid.Items.SortDescriptions.Add(new(firstColumn.SortMemberPath, sortDirection));

                // Apply sort
                foreach (DataGridColumn column in dataGrid.Columns)
                {
                    column.SortDirection = null;
                }
                firstColumn.SortDirection = sortDirection;

                // Refresh items to display sort
                dataGrid.Items.Refresh();
            }

            /// <summary>
            ///  Scroll the last row into view
            /// </summary>
            public void ScrollIntoViewLastRow()
            {
                if (dataGrid is { Items.Count: > 0 })
                {
                    var lastItem = dataGrid.Items[^1];
                    if (lastItem != null)
                    {
                        dataGrid.ScrollIntoView(lastItem);
                    }
                }
            }

            /// <summary>
            ///  Scroll the first row into view
            /// </summary>
            public void ScrollIntoViewFirstRow()
            {
                if (dataGrid is { Items.Count: > 0 })
                {
                    var firstItem = dataGrid.Items[0];
                    if (firstItem != null)
                    {
                        dataGrid.ScrollIntoView(firstItem);
                    }
                }
            }

            public void ScrollIntoViewRow(int rowIndex)
            {
                if (dataGrid is { Items.Count: > 0 } && rowIndex >= 0 && rowIndex < dataGrid.Items.Count)
                {
                    var item = dataGrid.Items[rowIndex];
                    if (item != null)
                    {
                        dataGrid.ScrollIntoView(item);
                    }
                }
            }

            /// <summary>
            ///  Select the rows with the given IDs, discover its rowIndexes, and then scroll the topmost row into view 
            ///  This method is provided with a list of tuples, each containing
            /// - a File ID, 
            /// - a possible row index into the data table containing that File ID
            /// </summary>
            public void SelectAndScrollIntoView(List<Tuple<long, int>> idRowIndexes)
            {
                // We want to select (highlight) each row in the data table matching those IDs. 
                // Typically, the file record identified by the ID will be found in the datagrid row specified by RowIndex,
                // which will occur *unless* the the user has resorted the datagrid by clicking a column header 
                // For efficiency, we check each tuple to see if the ID provided matches the ID in the row specified by rowIndex. If so, 
                // we can quickly highlight those rows.  Otherwise we need to search the datagrid for each ID

                // Check the arguments for null 
                if (dataGrid == null || idRowIndexes == null)
                {
                    // this should not happen
                    TracePrint.StackTrace(1);
                    // throw new ArgumentNullException(nameof(idRowIndexes) + " or " nameof(dataGrid));
                    // Not sure what the consequences of this empty return is, but ...
                    return;
                }

                // If there are no selections, just unselect everything
                if (idRowIndexes.Count.Equals(0))
                {
                    dataGrid.UnselectAll();
                    return;
                }

                int topmostRowIndex = int.MaxValue - 3; // Keeps track of the topmost row index, as this is the one we will want to scroll too

                List<int> rowIndexesToSelect = [];
                foreach (Tuple<long, int> idRowIndex in idRowIndexes)
                {
                    long currentID = idRowIndex.Item1;
                    int currentRowIndexThatMayContainID = idRowIndex.Item2;

                    // Get the row indicated by rowIndex (after first checking that such a row exists)
                    if (dataGrid.Items.Count < currentRowIndexThatMayContainID)
                    {
                        // Debug.Print("row index " + currentRowIndexThatMayContainID + " is not in array sized " + dataGrid.Items.Count);
                        return;
                    }
                    // The current row being examined
                    if (dataGrid.Items[currentRowIndexThatMayContainID] is DataRowView currentRow && (long)currentRow.Row.ItemArray[0]! == currentID)
                    {
                        // The ID is in the row indicated by rowIndex. Add that rowIndex as one of the rows we should select
                        rowIndexesToSelect.Add(currentRowIndexThatMayContainID);
                        if (topmostRowIndex > currentRowIndexThatMayContainID)
                        {
                            topmostRowIndex = currentRowIndexThatMayContainID;
                        }
                    }
                    else
                    {
                        // The ID is not in the row indicated by rowIndex. Search the datagrid for that ID, and then add it as one of the rows we should select
                        bool idFound = false;
                        int dataGridItemsCount = dataGrid.Items.Count;
                        for (int index = 0; index < dataGridItemsCount; index++)
                        {
                            currentRow = dataGrid.Items[index] as DataRowView;
                            if (currentRow != null && (long)currentRow.Row.ItemArray[0]! == currentID)
                            {
                                idFound = true;
                                rowIndexesToSelect.Add(index);
                                if (topmostRowIndex > index)
                                {
                                    topmostRowIndex = index;
                                }
                                break;
                            }
                        }
                        if (idFound == false)
                        {
                            // The id should always be found. But just in case  we ignore IDS that aren't found
                            // Debug.Print("could not find ID: " + currentID + " in array sized " + dataGrid.Items.Count);
                        }
                    }
                }

                // Select the items (which highlights those rows)
                bool indexIncreasing = topmostRowIndex > dataGrid.SelectedIndex;
                SelectRowByIndexes(dataGrid, rowIndexesToSelect);

                // Depending on our selection direction, we scroll to expose the previous or next 2 rows to ensure they are visible beyond the selected row);
                int scrollIndex = indexIncreasing ? Math.Min(topmostRowIndex + 3, dataGrid.Items.Count - 1) : Math.Max(topmostRowIndex - 3, 0);
                if (scrollIndex >= 0 && scrollIndex < dataGrid.Items.Count)
                {
                    dataGrid.ScrollIntoViewRow(scrollIndex);
                }
            }
        }

        #endregion

        #region Private (internal) methods used by the above
        // Select the rows indicated by the (perhaps multple) row indexes
        // Modified from https://blog.magnusmontin.net/2013/11/08/how-to-programmatically-select-and-focus-a-row-or-cell-in-a-datagrid-in-wpf/
        private static void SelectRowByIndexes(DataGrid dataGrid, List<int> rowIndexes)
        {
            if (!dataGrid.SelectionUnit.Equals(DataGridSelectionUnit.FullRow) || !dataGrid.SelectionMode.Equals(DataGridSelectionMode.Extended))
            {
                // This should never be triggered
                throw new ArgumentException("DataGrid issue: SelectionUnit must be FullRow, and  Selection Mode must be  Extended");
            }

            // Clear all selections
            dataGrid.SelectedItems.Clear();

            // If 0 items are selected, there is nothing more to do
            if (rowIndexes.Count.Equals(0) || rowIndexes.Count > dataGrid.Items.Count)
            {
                return;
            }

            // if there is only one item, we can just set it directly
            if (rowIndexes.Count.Equals(1))
            {
                int rowIndex = rowIndexes[0];
                if (rowIndex < 0 || rowIndex > (dataGrid.Items.Count - 1))
                {
                    // This shouldn't happen, but...
                    throw new ArgumentException($"{rowIndex} is an invalid row index.");
                }
                dataGrid.SelectedIndex = rowIndex;  // This used to be for single selection. 
                return;
            }
            // Multiple indexes are selected
            foreach (int rowIndex in rowIndexes)
            {
                if (rowIndex < 0 || rowIndex > (dataGrid.Items.Count - 1))
                {
                    // This shouldn't happen, but...
                    throw new ArgumentException($"{rowIndex} is an invalid row index.");
                }
                dataGrid.SelectedItems.Add(dataGrid.Items[rowIndex]);
            }
        }
        #endregion

        #region Unused methods
        // Get a cell from the DataGrid
        // private static DataGridCell GetCell(DataGrid dataGrid, DataGridRow rowContainer, int column)
        // {
        //    if (rowContainer != null)
        //    {
        //        DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
        //        if (presenter == null)
        //        {
        //            /* if the row has been virtualized away, call its ApplyTemplate() method 
        //             * to build its visual tree in order for the DataGridCellsPresenter
        //             * and the DataGridCells to be created */
        //            rowContainer.ApplyTemplate();
        //            presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
        //        }
        //        if (presenter != null)
        //        {
        //            DataGridCell cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
        //            if (cell == null)
        //            {
        //                /* bring the column into view
        //                 * in case it has been virtualized away */
        //                dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[column]);
        //                cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
        //            }
        //            return cell;
        //        }
        //    }
        //    return null;
        // }
        // 
        // Hsed by above. See replacements in Utiliites
        // Enumerate the members of a visual tree, in order to programmatic access objects in the visual tree.
        //private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        //{
        //    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        //    {
        //        DependencyObject child = VisualTreeHelper.GetChild(obj, i);
        //        if (child != null && child is T)
        //        {
        //            return (T)child;
        //        }
        //        else
        //        {
        //            T childOfChild = FindVisualChild<T>(child);
        //            if (childOfChild != null)
        //            {
        //                return childOfChild;
        //            }
        //        }
        //    }
        //    return null;
        //}
        #endregion
    }
}
