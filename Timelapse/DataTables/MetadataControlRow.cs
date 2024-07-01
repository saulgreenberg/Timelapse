using System.Data;
using Timelapse.DataStructures;
using Timelapse.Extensions;
using Timelapse.Util;

namespace Timelapse.DataTables
{
    public class MetadataControlRow : CommonControlRow
    {
        public int Level
        {
            get => this.Row.GetIntegerField(Constant.Control.Level);
            set => this.Row.SetField(Constant.Control.Level, value);
        }

        #region Constructors
        public MetadataControlRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Try Update This ControlRow To Match
        // Check if a synchronization between the given metadata control row and this instance's metadata control row is needed,
        // which occurs if any field's values differ between the two.
        // Note: Values updated include all the inherited control attributes present in the base class
        public bool TryUpdateThisControlRowToMatch(MetadataControlRow metadataControlRowToMatch)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(metadataControlRowToMatch, nameof(metadataControlRowToMatch));

            // return true if the synchronization resulted in any changes,
            // i.e., differences were detected between at least one value between this control row and the passed in one,
            // in either the base call or for Level value
            return base.UpdateThisControlRowToMatch(metadataControlRowToMatch) ||
                   this.Level != metadataControlRowToMatch.Level;
        }
        #endregion

        #region Public Methods (can be overriden) - CreateColumnTuplesWithWhereForControlRowByID
        // Create a column tuple for the control based on this row's control attribute values
        // Note that the ID is added by the base class
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            ColumnTuplesWithWhere ctww = base.CreateColumnTuplesWithWhereByID();
            ctww.Columns.Add(new ColumnTuple(Constant.Control.Level, this.Level));
            return ctww;
        }
        #endregion
    }
}
