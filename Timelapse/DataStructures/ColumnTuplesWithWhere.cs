using System.Collections.Generic;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.DataStructures
{
    /// <summary>
    /// A tuple comprising:
    /// - a list of ColumnTuples 
    /// - a string indicating where to apply the updates contained in the tuples
    /// For example, if we were doing an update, a columnTuplesWithWhere could be
    /// UPDATE table_name SET 
    /// colname1 = value1, 
    /// colname2 = value2,
    /// ...
    /// colnameN = valueN
    /// WHERE
    /// condition e.g., ID=1;
    /// </summary>
    public class ColumnTuplesWithWhere
    {
        #region Public Properties
        public List<ColumnTuple> Columns { get; }
        public string Where { get; private set; }
        #endregion

        #region Constructors
        public ColumnTuplesWithWhere()
        {
            Columns = [];
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns)
        {
            Columns = columns;
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, long id)
            : this(columns)
        {
            SetWhere(id);
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, ColumnTuple tuple)
            : this(columns)
        {
            SetWhere(tuple);
        }

        public ColumnTuplesWithWhere(ColumnTuple column, string field)
        {
            SetWhere(column, field);
            Columns = [column];
        }

        public ColumnTuplesWithWhere(ColumnTuple column, string field, bool useNotEqualCondition)
        {
            if (useNotEqualCondition)
            {
                SetWhereNotEquals(column, field);
            }
            else
            {
                SetWhere(column, field);
            }
            Columns = [column];
        }
        #endregion

        #region Public Methods - SetWhere, SetWhereNotEqualsvarious forms
        // Long: ID = Long
        public void SetWhere(long id)
        {
            Where = DatabaseColumn.ID + " = " + id;
        }

        // ColumnTuple: columnName = Value
        public void SetWhere(ColumnTuple columnTuple)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuple, nameof(columnTuple));
            Where = $"{columnTuple.Name} = {Sql.Quote(columnTuple.Value)}";
        }

        // ColumnTuple: columnName = field
        public void SetWhere(ColumnTuple columnTuple, string field)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuple, nameof(columnTuple));

            Where = $"{columnTuple.Name} = {Sql.Quote(field)}";
        }

        // FILE = file AND RELATIVEPATH = relativePath
        public void SetWhere(string relativePath, string file)
        {
            Where = $"{DatabaseColumn.File} = {Sql.Quote(file)}";
            Where += $" AND {DatabaseColumn.RelativePath} = {Sql.Quote(relativePath)}";
        }

        // FILE = file
        public void SetWhere(string file)
        {
            Where = $"{DatabaseColumn.File} = {Sql.Quote(file)}";
        }

        // ColumnTuple: columnName <> field
        public void SetWhereNotEquals(ColumnTuple columnTuple, string field)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuple, nameof(columnTuple));

            Where = $"{columnTuple.Name} <> {Sql.Quote(field)}";
        }
        #endregion
    }
}
