using System.Collections.Generic;
using System.Data;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    public class CommonControlRow(DataRow row) : DataRowBackedObject(row)
    {
        #region Public Properties

        public long ControlOrder
        {
            get => Row.GetLongField(Control.ControlOrder);
            set => Row.SetField(Control.ControlOrder, value);
        }

        public string DataLabel
        {
            get => Row.GetStringField(Control.DataLabel);
            set => Row.SetField(Control.DataLabel, value);
        }

        public string DefaultValue
        {
            get => Row.GetStringField(Control.DefaultValue);
            set => Row.SetField(Control.DefaultValue, value);
        }

        public string Label
        {
            get => Row.GetStringField(Control.Label);
            set => Row.SetField(Control.Label, value);
        }
        public string List
        {
            get => Row.GetStringField(Control.List);
            set => Row.SetField(Control.List, value);
        }

        public long SpreadsheetOrder
        {
            get => Row.GetLongField(Control.SpreadsheetOrder);
            set => Row.SetField(Control.SpreadsheetOrder, value);
        }

        public string Tooltip
        {
            get => Row.GetStringField(Control.Tooltip);
            set => Row.SetField(Control.Tooltip, value);
        }

        public string Type
        {
            get => Row.GetStringField(Control.Type);
            set => Row.SetField(Control.Type, value);
        }

        public bool Visible
        {
            get => Row.GetBooleanField(Control.Visible);
            set => Row.SetField(Control.Visible, value);
        }

        public bool ExportToCSV
        {
            get => Row.GetBooleanField(Control.ExportToCSV);
            set => Row.SetField(Control.ExportToCSV, value);
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
        protected bool UpdateThisControlRowToMatch(CommonControlRow controlRowToMatch)
        {
            // Check the arguments for null 
            // Not  needed as done by parent
            // ThrowIf.IsNullArgument(controlRowToMatch, nameof(controlRowToMatch));

            bool synchronizationMadeChanges = false;


            if (ControlOrder != controlRowToMatch.ControlOrder)
            {
                ControlOrder = controlRowToMatch.ControlOrder;
                synchronizationMadeChanges = true;
            }
            if (DefaultValue != controlRowToMatch.DefaultValue)
            {
                DefaultValue = controlRowToMatch.DefaultValue;
                synchronizationMadeChanges = true;
            }
            if (Type != controlRowToMatch.Type)
            {
                Type = controlRowToMatch.Type;
                synchronizationMadeChanges = true;
            }
            if (Label != controlRowToMatch.Label)
            {
                Label = controlRowToMatch.Label;
                synchronizationMadeChanges = true;
            }
            if (List != controlRowToMatch.List)
            {
                List = controlRowToMatch.List;
                synchronizationMadeChanges = true;
            }
            if (SpreadsheetOrder != controlRowToMatch.SpreadsheetOrder)
            {
                SpreadsheetOrder = controlRowToMatch.SpreadsheetOrder;
                synchronizationMadeChanges = true;
            }
            if (Tooltip != controlRowToMatch.Tooltip)
            {
                Tooltip = controlRowToMatch.Tooltip;
                synchronizationMadeChanges = true;
            }
            if (Visible != controlRowToMatch.Visible)
            {
                Visible = controlRowToMatch.Visible;
                synchronizationMadeChanges = true;
            }
            if (ExportToCSV != controlRowToMatch.ExportToCSV)
            {
                ExportToCSV = controlRowToMatch.ExportToCSV;
                synchronizationMadeChanges = true;
            }
            return synchronizationMadeChanges;
        }

        #endregion

        #region Public Methods (can be overriden) - CreateColumnTuplesWithWhereForControlRowByID
        // Create a column tuple for the control based on this row's control attribute values 
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples =
            [
                new(Control.ControlOrder, ControlOrder),
                new(Control.DataLabel, DataLabel),
                new(Control.DefaultValue, DefaultValue),
                new(Control.Label, Label),
                new(Control.List, List),
                new(Control.SpreadsheetOrder, SpreadsheetOrder),

                new(Control.Tooltip, Tooltip),
                new(Control.Type, Type),
                new(Control.Visible, Visible),
                new(Control.ExportToCSV, ExportToCSV)
            ];
            return new(columnTuples, ID);
        }
        #endregion
    }
}
