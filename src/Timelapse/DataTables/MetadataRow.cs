using System.Collections.Generic;
using System.Data;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    // A MetadataRow is a table comprising 
    // - Id column which matches to the ID of the given metadata table 
    // - FolderDataPath which matches the relevant portion of the RelativePath (starting from the root folder)
    // - metadata values that match the metadata specified in the given metadata table template, identified by the column which matches their data label e.g.
    // Id FolderDataPath Flag0 Counter0 etc.

    public class MetadataRow(DataRow row) : DataRowBackedObject(row)
    {
        #region Public Properties
        // Given a label of a column header, return its value)
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
