using System.Collections.Generic;
using System.Data;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    public class MetadataInfoRow(DataRow row) : DataRowBackedObject(row)
    {
        #region Public Properties
        public int Level
        {
            get => Row.GetIntegerField(Control.Level);
            set => Row.SetField(Control.Level, value);
        }

        public string Guid
        {
            get => Row.GetStringField(Control.Guid);
            set => Row.SetField(Control.Guid, value);
        }

        public string Alias
        {
            get => Row.GetStringField(Control.Alias);
            set => Row.SetField(Control.Alias, value);
        }
        #endregion

        #region Public Methods - CreateColumnTuplesWithWhereForControlRowByID
        // Create a column tuple for the control based on this row's control attribute values 
        // TODO Note that the ID is the level - check if this actually works as the base class gets it by ID.
        // TODO Not sure if this is needed as we don't update the MetadataInfo Table from this structure
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples =
            [
                new(Control.Level, Level),
                new(Control.Guid, Guid),
                new(Control.Alias, Alias)
            ];
            return new(columnTuples, ID);
        }
        #endregion
    }
}
