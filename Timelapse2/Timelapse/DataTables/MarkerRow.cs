using System.Collections.Generic;
using System.Data;

namespace Timelapse.Database
{
    // A MarkerRow is a table comprising 
    // - Id column which matches to the ID of the FileData (DataTable)
    // - Counter columns that match the counters in the template, identified by their data label e.g.
    // Id Goats Hikers
    // Each value comprises a list of points x,y|x,y that correspond to the relative location of one or markers in the image. 
    // The | separates the different points e.g. 0.298,0.618|0.304,0.601
    public class MarkerRow : DataRowBackedObject
    {
        #region Public Properties
        // Given a datalabel of the counter column, return its value (0 or more points separated with a '|')
        public string this[string dataLabel]
        {
            get { return this.Row.GetStringField(dataLabel); }
            set { this.Row.SetField(dataLabel, value); }
        }

        // Get as an IEnumerable a list of datalabels (the column names excepting the ID columnname) held in the Markers Table
        public IEnumerable<string> DataLabels
        {
            get
            {
                foreach (DataColumn column in this.Row.Table.Columns)
                {
                    if (column.ColumnName != Constant.DatabaseColumn.ID)
                    {
                        // Yield returns each element one at a time in an IEnumrable
                        yield return column.ColumnName;
                    }
                }
            }
        }
        #endregion

        #region Constructor
        public MarkerRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods- Create a CreateColumnTuplesWithWhereByID 
        // Create a CreateColumnTuplesWithWhereByID that will create a complete row
        // Where is the ID
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            foreach (string dataLabel in this.DataLabels)
            {
                columnTuples.Add(new ColumnTuple(dataLabel, this[dataLabel]));
            }
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion
    }
}
