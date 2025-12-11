using System.Data;
using Timelapse.Constant;
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
    public class ControlRow(DataRow row) : CommonControlRow(row)
    {
        #region Public Properties

        public bool Copyable
        {
            get => Row.GetBooleanField(Control.Copyable);
            set => Row.SetField(Control.Copyable, value);
        }

        public int Width
        {
            get => Row.GetIntegerField(Control.TextBoxWidth);
            set => Row.SetField(Control.TextBoxWidth, value);
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

            if (Copyable != controlRowToMatch.Copyable)
            {
                Copyable = controlRowToMatch.Copyable;
                synchronizationMadeChanges = true;
            }
            if (Width != controlRowToMatch.Width)
            {
                Width = controlRowToMatch.Width;
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
            columnTuplesWithWhere.Columns.Add(new(Control.Copyable, Copyable));
            columnTuplesWithWhere.Columns.Add(new(Control.TextBoxWidth, Width));

            return columnTuplesWithWhere;
        }
        #endregion
    }

}
