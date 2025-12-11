using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// DataTableBackedList is a list of DataRowBackedObjects associated with a DataTable
    /// i.e.,  DataTable rows that can be indexed and that have an associated Database ID
    /// </summary>
    public class DataTableBackedList<TRow>(DataTable dataTable, Func<DataRow, TRow> createRow) : IDisposable, IEnumerable<TRow>
        where TRow : DataRowBackedObject
    {
        #region Protected Properties
        protected DataTable DataTable { get; private set; } = dataTable;

        #endregion

        #region Private Properties

        private bool disposed;
        #endregion

        #region Public Methods - Modify the DataTable by adding/removing a row: NewRow, RemoveAt
        public TRow NewRow()
        {
            DataRow row = DataTable.NewRow();
            return createRow(row);
        }

        // Given a populated row, add it
        public void AddRow(DataRow row)
        {
            DataTable.Rows.Add(row.ItemArray);
        }

        public void InsertRow(int index, DataRow row)
        {
            if (null == row?.ItemArray)
            {
                return;
            }
            DataRow clonedRow = DataTable.NewRow();
            // ReSharper disable once AssignNullToNotNullAttribute
            clonedRow.ItemArray = row.ItemArray.Clone() as object[];

            DataTable.Rows.InsertAt(clonedRow, index);
        }

        public void RemoveAt(int index)
        {
            DataTable.Rows.RemoveAt(index);
        }
        #endregion

        #region Public Methods - DataTable info: ColumnNames, RowCount 
        /// <summary>
        /// Return a IEnumerable list of ColumnNames found in the DataTable
        /// </summary>
        public IEnumerable<string> ColumnNames
        {
            get
            {
                foreach (DataColumn column in DataTable.Columns)
                {
                    yield return column.ColumnName;
                }
            }
        }

        /// <summary>
        /// Return a count of the number of rows in the DataTable
        /// </summary>
        public int RowCount => DataTable?.Rows.Count ?? 0;

        #endregion

        #region Public Methods - Finding a Row: DataTable[index], Find, IndexOf
        /// <summary>
        /// return a row corresponding to the row in the DataTable identified by its index
        /// </summary>
        /// <param name="index">An index into a row in the DataTable</param>
        public TRow this[int index]
        {
            get
            {
                if (index < DataTable.Rows.Count)
                {
                    return createRow(DataTable.Rows[index]);
                }

                Debug.Print(
                    $"in DataTableBackedList:this. Datatable count is {DataTable.Rows.Count}, but index is out of bounds at: {index}");
                return null;
            }
        }

        public TRow Find(long id)
        {
            //This check should no longer be needed, as we now check to see if the database file is corrupt or not. 
            // And this should only be invoked if the database was determined to be non-corrupt. 
            // Still... its worth keeping this bit of code handy just in case we have to revisit it.
            //if (this.DataTable.PrimaryKey.Length == 0)
            //{
            //    // Check if there is a primary key. This will fail if the database was somehow corrupt i.e., if the table is not there or unreadable.
            //    throw new MissingPrimaryKeyException("No Primary key. The database may be corrupt");
            //}
            try
            {
                DataRow row = DataTable.Rows.Find(id);
                if (row == null)
                {
                    return null;
                }
                return createRow(row);
            }
            catch
            {
                return null;
            }
        }

        public int IndexOf(DataRowBackedObject row)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(row, nameof(row));
            return row.GetIndex(DataTable);
        }
        #endregion

        #region Public Methods - Binding  triggers an onRowChanged event
        public void BindDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (dataGrid != null)
            {

                dataGrid.DataContext = DataTable;
                dataGrid.ItemsSource = DataTable.DefaultView;
            }
            // refresh data grid binding
            if (onRowChanged != null)
            {
                DataTable.RowChanged -= onRowChanged;
                DataTable.RowChanged += onRowChanged;
            }
        }


        #endregion

        #region Public Methods - Enumerators
        public IEnumerator<TRow> GetEnumerator()
        {
            // use a row index rather than a foreach loop as, if the caller modifies the DataRow, the DataRowCollection enumerator under the foreach may lose its place
            // Manipulation of data in a DataTable from within a foreach is common practice, suggesting whatever framework issue which invalidates the enumerator 
            // manifests only infrequently, but MSDN is ambiguous as to the level of support.  Enumerators returning the same row multiple times has been observed,
            // skipping of rows has not been.
            if (null == DataTable?.Rows)
            {
                yield return null;
            }
            else
            {
                int rowCount = DataTable.Rows.Count;
                for (int rowIndex = 0; rowIndex < rowCount; ++rowIndex)
                {
                    yield return createRow(DataTable.Rows[rowIndex]);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        #region Disposing
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing && DataTable != null)
            {
                DataTable.Dispose();
            }
            disposed = true;
        }

        public void DisposeAsNeeded(DataRowChangeEventHandler eventHandler)
        {
            try
            {
                // Release the DataTable
                DataTable.Rows.Clear();
                if (eventHandler != null)
                {
                    DataTable.RowChanged -= eventHandler;
                }
                Dispose();
                DataTable = null;
            }
            catch
            {
                Debug.Print("Failed in DataTableBackedList - DisposeAsNeeded");
            }
        }
        #endregion
    }
}
