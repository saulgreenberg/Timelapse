
using System.Collections.Generic;
using System.Data;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    public class MetadataInfoRow : DataRowBackedObject
    {
        #region Public Properties
        public int Level
        {
            get => this.Row.GetIntegerField(Constant.Control.Level);
            set => this.Row.SetField(Constant.Control.Level, value);
        }

        public string Guid
        {
            get => this.Row.GetStringField(Constant.Control.Guid);
            set => this.Row.SetField(Constant.Control.Guid, value);
        }

        public string Alias
        {
            get => this.Row.GetStringField(Constant.Control.Alias);
            set => this.Row.SetField(Constant.Control.Alias, value);
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


            if (this.Level != controlRowToMatch.Level)
            {
                this.Level = controlRowToMatch.Level;
                synchronizationMadeChanges = true;
            }
            if (this.Guid != controlRowToMatch.Guid)
            {
                this.Guid = controlRowToMatch.Guid;
                synchronizationMadeChanges = true;
            }
            if (this.Alias != controlRowToMatch.Alias)
            {
                this.Alias = controlRowToMatch.Alias;
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
                new ColumnTuple(Constant.Control.Level, this.Level),
                new ColumnTuple(Constant.Control.Guid, this.Guid),
                new ColumnTuple(Constant.Control.Alias, this.Alias)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion
    }
}
