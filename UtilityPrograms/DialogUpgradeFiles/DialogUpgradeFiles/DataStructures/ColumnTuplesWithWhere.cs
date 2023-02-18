using DialogUpgradeFiles.Util;
using System.Collections.Generic;
namespace DialogUpgradeFiles.Database
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
    /// <condition> e.g., ID=1;
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
            this.Columns = new List<ColumnTuple>();
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns)
        {
            this.Columns = columns;
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, long id)
            : this(columns)
        {
            this.SetWhere(id);
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, ColumnTuple tuple)
            : this(columns)
        {
            this.SetWhere(tuple);
        }

        public ColumnTuplesWithWhere(ColumnTuple column, string field)
        {
            this.SetWhere(column, field);
            this.Columns = new List<ColumnTuple>
            {
                column
            };
        }

        public ColumnTuplesWithWhere(ColumnTuple column, string field, bool useNotEqualCondition)
        {
            if (useNotEqualCondition)
            {
                this.SetWhereNotEquals(column, field);
            }
            else
            {
                this.SetWhere(column, field);
            }
            this.Columns = new List<ColumnTuple>
            {
                column
            };
        }
        #endregion

        #region Public Methods - SetWhere, SetWhereNotEqualsvarious forms
        // Long: ID = Long
        public void SetWhere(long id)
        {
            this.Where = Constant.DatabaseColumn.ID + " = " + id;
        }

        // ColumnTuple: columnName = Value
        public void SetWhere(ColumnTuple columnTuple)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuple, nameof(columnTuple));
            this.Where = $"{columnTuple.Name} = {Sql.Quote(columnTuple.Value)}";
        }

        // ColumnTuple: columnName = field
        public void SetWhere(ColumnTuple columnTuple, string field)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuple, nameof(columnTuple));

            this.Where = $"{columnTuple.Name} = {Sql.Quote(field)}";
        }

        // FILE = file AND RELATIVEPATH = relativePath AND FOLDER = folder
        public void SetWhere(string folder, string relativePath, string file)
        {
            this.Where = $"{Constant.DatabaseColumn.File} = {Sql.Quote(file)}";
            this.Where += $" AND {Constant.DatabaseColumn.RelativePath} = {Sql.Quote(relativePath)}";
            this.Where += $" AND {Constant.DatabaseColumn.Folder} = {Sql.Quote(folder)}";
        }

        // FILE = file AND RELATIVEPATH = relativePath
        public void SetWhere(string relativePath, string file)
        {
            this.Where = $"{Constant.DatabaseColumn.File} = {Sql.Quote(file)}";
            this.Where += $" AND {Constant.DatabaseColumn.RelativePath} = {Sql.Quote(relativePath)}";
        }

        // FILE = file
        public void SetWhere(string file)
        {
            this.Where = $"{Constant.DatabaseColumn.File} = {Sql.Quote(file)}";
        }

        // ColumnTuple: columnName <> field
        public void SetWhereNotEquals(ColumnTuple columnTuple, string field)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(columnTuple, nameof(columnTuple));

            this.Where = $"{columnTuple.Name} <> {Sql.Quote(field)}";
        }
        #endregion
    }
}
