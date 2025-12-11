using System.Collections.Generic;
using System.Data;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    // A MarkerRow is a table comprising 
    // - Id column which matches to the ID of the FileData (DataTable)
    // - Counter columns that match the counters in the template, identified by their data label e.g.
    // Id Goats Hikers
    // Each value comprises a list of points x,y|x,y that correspond to the relative location of one or markers in the image. 
    // The | separates the different points e.g. 0.298,0.618|0.304,0.601
    public class MarkerRow(DataRow row) : DataRowBackedObject(row)
    {
        #region Public Properties
        // Given a datalabel of the counter column, return its value (0 or more points separated with a '|')
        public string this[string dataLabel]
        {
            get => Row.GetStringField(dataLabel);
            set => Row.SetField(dataLabel, value);
        }

        // Get as an IEnumerable a list of datalabels (the column names excepting the ID columnname) held in the Markers Table
        public IEnumerable<string> DataLabels
        {
            get
            {
                foreach (DataColumn column in Row.Table.Columns)
                {
                    if (column.ColumnName != DatabaseColumn.ID)
                    {
                        // Yield returns each element one at a time in an IEnumrable
                        yield return column.ColumnName;
                    }
                }
            }
        }
        #endregion

        #region Public Methods- Create a CreateColumnTuplesWithWhereByID 
        // Create a CreateColumnTuplesWithWhereByID that will create a complete row
        // Where is the ID
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples = [];
            foreach (string dataLabel in DataLabels)
            {
                columnTuples.Add(new(dataLabel, this[dataLabel]));
            }
            return new(columnTuples, ID);
        }
        #endregion
    }
}
