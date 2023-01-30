using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// DataTableBackedList is a list of DataRowBackedObjects associated with a DataTable
    /// i.e.,  DataTable rows that can be indexed and that have an associated Database ID
    /// </summary>
    public class DataTableBackedList<TRow> : IDisposable, IEnumerable<TRow> where TRow : DataRowBackedObject
    {
        #region Protected Properties
        protected DataTable DataTable { get; private set; }
        #endregion

        #region Private Properties
        private readonly Func<DataRow, TRow> createRow;
        private bool disposed;
        #endregion

        #region Constructors
        public DataTableBackedList(DataTable dataTable, Func<DataRow, TRow> createRow)
        {
            this.createRow = createRow;
            this.DataTable = dataTable;
            this.disposed = false;
        }
        #endregion

        #region Public Methods - Modify the DataTable: NewRow, RemoveAt
        public TRow NewRow()
        {
            DataRow row = this.DataTable.NewRow();
            return this.createRow(row);
        }

        public void RemoveAt(int index)
        {
            this.DataTable.Rows.RemoveAt(index);
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
                foreach (DataColumn column in this.DataTable.Columns)
                {
                    yield return column.ColumnName;
                }
            }
        }

        /// <summary>
        /// Return a count of the number of rows in the DataTable
        /// </summary>
        public int RowCount => this.DataTable.Rows.Count;

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
                if (index < this.DataTable.Rows.Count)
                {
                    return this.createRow(this.DataTable.Rows[index]);
                }
                else
                {
                    Debug.Print(
                        $"in DataTableBackedList:this. Datatable count is {this.DataTable.Rows.Count}, but index is out of bounds at: {index}");
                    return null;
                }
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
                DataRow row = this.DataTable.Rows.Find(id);
                if (row == null)
                {
                    return null;
                }
                return this.createRow(row);
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
            return row.GetIndex(this.DataTable);
        }
        #endregion

        #region Public Methods - Binding  triggers an onRowChanged event
        public void BindDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (dataGrid != null)
            {
                dataGrid.DataContext = this.DataTable;
                dataGrid.ItemsSource = this.DataTable.DefaultView;
            }
            // refresh data grid binding
            if (onRowChanged != null)
            {
                this.DataTable.RowChanged += onRowChanged;
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
            int rowCount = this.DataTable.Rows.Count;
            for (int rowIndex = 0; rowIndex < rowCount; ++rowIndex)
            {
                yield return this.createRow(this.DataTable.Rows[rowIndex]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        #endregion

        #region Disposing
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing && this.DataTable != null)
            {
                this.DataTable.Dispose();
            }
            this.disposed = true;
        }

        public void DisposeAsNeeded(DataRowChangeEventHandler eventHandler)
        {
            try
            {
                // Release the DataTable
                this.DataTable.Rows.Clear();
                if (eventHandler != null)
                {
                    this.DataTable.RowChanged -= eventHandler;
                }
                this.Dispose();
                this.DataTable = null;
            }
            catch
            {
                Debug.Print("Failed in DataTableBackedList - DisposeAsNeeded");
            }
        }
        #endregion
    }
}
