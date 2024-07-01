using System.Data;
using Timelapse.DataStructures;
using Timelapse.Extensions;
using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// ControlRow defines a single control as described in the Template.  
    /// Its property names are the same as those in the template:
    /// - ControlOrder, SpreadsheetOrder, Type, DefaultValue, Label, DataLabel, Width, Tooltip, Copyable, Visible, List   
    /// </summary>
    public class ControlRow : CommonControlRow
    {
        #region Public Properties

        public bool Copyable
        {
            get => this.Row.GetBooleanField(Constant.Control.Copyable);
            set => this.Row.SetField(Constant.Control.Copyable, value);
        }

        public int Width
        {
            get => this.Row.GetIntegerField(Constant.Control.TextBoxWidth);
            set => this.Row.SetField(Constant.Control.TextBoxWidth, value);
        }

        #endregion

        #region Constructors
        public ControlRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Try Update This ControlRow To Match
        /// <summary>
        /// Check if a synchronization between the given control row and this instance's control row is needed,
        /// which occurs if any field's values differ between the two.
        /// Note: Values updated include ControlOrder and SpreadsheetOrder 
        /// </summary>
        /// <param name="controlRowToMatch"></param>
        /// <returns></returns>
        public bool TryUpdateThisControlRowToMatch(ControlRow controlRowToMatch)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlRowToMatch, nameof(controlRowToMatch));

            bool synchronizationMadeChanges = base.UpdateThisControlRowToMatch(controlRowToMatch);

            if (this.Copyable != controlRowToMatch.Copyable)
            {
                this.Copyable = controlRowToMatch.Copyable;
                synchronizationMadeChanges = true;
            }
            if (this.Width != controlRowToMatch.Width)
            {
                this.Width = controlRowToMatch.Width;
                synchronizationMadeChanges = true;
            }
            return synchronizationMadeChanges;
        }
        #endregion

        #region Public Methods (can be overriden) - CreateColumnTuplesWithWhereForControlRowByID
        // Create a column tuple for the control based on this row's control attribute values 
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            // We add the Copyable and TextboxWidth attributes
            ColumnTuplesWithWhere columnTuplesWithWhere = base.CreateColumnTuplesWithWhereByID();
            columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.Control.Copyable, this.Copyable));
            columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.Control.TextBoxWidth, this.Width));

            return columnTuplesWithWhere;
        }
        #endregion
    }

}
