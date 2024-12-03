using System.Collections.Generic;
using System.Data;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    public class MetadataInfoRow : DataRowBackedObject
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

        #region Constructors
        public MetadataInfoRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Try Update This ControlRow To Match
        /// <summary>
        /// TODO Not sure if this is needed as we don't update the MetadataInfo Table from this structure
        /// Check if a synchronization between the given control row and this instance's control row is needed,
        /// which occurs if any field's values differ between the two.
        /// </summary>
        /// <param name="controlRowToMatch"></param>
        /// <returns></returns>
        protected bool UpdateThisControlRowToMatch(MetadataInfoRow controlRowToMatch)
        {
            // Check the arguments for null 
            // Not  needed as done by parent
            // ThrowIf.IsNullArgument(controlRowToMatch, nameof(controlRowToMatch));

            bool synchronizationMadeChanges = false;


            if (Level != controlRowToMatch.Level)
            {
                Level = controlRowToMatch.Level;
                synchronizationMadeChanges = true;
            }
            if (Guid != controlRowToMatch.Guid)
            {
                Guid = controlRowToMatch.Guid;
                synchronizationMadeChanges = true;
            }
            if (Alias != controlRowToMatch.Alias)
            {
                Alias = controlRowToMatch.Alias;
                synchronizationMadeChanges = true;
            }
            return synchronizationMadeChanges;
        }

        #endregion

        #region Public Methods - CreateColumnTuplesWithWhereForControlRowByID
        // Create a column tuple for the control based on this row's control attribute values 
        // TODO Note that the ID is the level - check if this actually works as the base class gets it by ID.
        // TODO Not sure if this is needed as we don't update the MetadataInfo Table from this structure
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>
            {
                new ColumnTuple(Control.Level, Level),
                new ColumnTuple(Control.Guid, Guid),
                new ColumnTuple(Control.Alias, Alias)
            };
            return new ColumnTuplesWithWhere(columnTuples, ID);
        }
        #endregion
    }
}
