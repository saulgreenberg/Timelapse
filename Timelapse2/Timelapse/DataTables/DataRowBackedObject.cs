using System.Data;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// A DataRowBackedObject is a DataRow in a DataTable, which also has a Database ID. 
    /// Each row is associated with 
    /// - an index (the location of that row in a datatable)
    /// - an ID (the Database ID associated with that row)
    /// The two are not necessarily the same. For example, a selection will change the indexes, but not the ID
    /// It comprises
    /// - a Row (which is a stock DataRow)
    /// - an ID (i.e., the Database ID associated with that row)
    /// - an Index (i.e., the index of this row in the DataTable asociated with that row)
    /// = GetIndex - returns the index of a row in a given data table
    /// </summary>
    public abstract class DataRowBackedObject
    {
        #region Public / Protected Properties
        // Get the Database ID associated with this row
        public long ID
        {
            get { return this.Row.GetID(); }
        }

        protected DataRow Row { get; private set; }
        #endregion

        #region Constructor
        protected DataRowBackedObject(DataRow row)
        {
            this.Row = row;
        }
        #endregion

        #region Public / Protected Methods
        // Return a ColumnTuples data structure, where the 'Where' is the ID
        public abstract ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID();

        // Return the index of this row in the dataTable
        public int GetIndex(DataTable dataTable)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dataTable, nameof(dataTable));

            return dataTable.Rows.IndexOf(this.Row);
        }
        #endregion
    }
}
