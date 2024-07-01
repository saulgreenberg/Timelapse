using System.Collections.Generic;
using System.Data;
using Timelapse.DataStructures;
using Timelapse.Extensions;

namespace Timelapse.DataTables
{
    public class CommonControlRow : DataRowBackedObject
    {
        #region Public Properties

        public long ControlOrder
        {
            get => this.Row.GetLongField(Constant.Control.ControlOrder);
            set => this.Row.SetField(Constant.Control.ControlOrder, value);
        }

        public string DataLabel
        {
            get => this.Row.GetStringField(Constant.Control.DataLabel);
            set => this.Row.SetField(Constant.Control.DataLabel, value);
        }

        public string DefaultValue
        {
            get => this.Row.GetStringField(Constant.Control.DefaultValue);
            set => this.Row.SetField(Constant.Control.DefaultValue, value);
        }

        public string Label
        {
            get => this.Row.GetStringField(Constant.Control.Label);
            set => this.Row.SetField(Constant.Control.Label, value);
        }
        public string List
        {
            get => this.Row.GetStringField(Constant.Control.List);
            set => this.Row.SetField(Constant.Control.List, value);
        }

        public long SpreadsheetOrder
        {
            get => this.Row.GetLongField(Constant.Control.SpreadsheetOrder);
            set => this.Row.SetField(Constant.Control.SpreadsheetOrder, value);
        }

        public string Tooltip
        {
            get => this.Row.GetStringField(Constant.Control.Tooltip);
            set => this.Row.SetField(Constant.Control.Tooltip, value);
        }

        public string Type
        {
            get => this.Row.GetStringField(Constant.Control.Type);
            set => this.Row.SetField(Constant.Control.Type, value);
        }

        public bool Visible
        {
            get => this.Row.GetBooleanField(Constant.Control.Visible);
            set => this.Row.SetField(Constant.Control.Visible, value);
        }

        public bool ExportToCSV
        {
            get => this.Row.GetBooleanField(Constant.Control.ExportToCSV);
            set => this.Row.SetField(Constant.Control.ExportToCSV, value);
        }


        #endregion

        #region Constructors
        public CommonControlRow(DataRow row)
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
        protected bool UpdateThisControlRowToMatch(CommonControlRow controlRowToMatch)
        {
            // Check the arguments for null 
            // Not  needed as done by parent
            // ThrowIf.IsNullArgument(controlRowToMatch, nameof(controlRowToMatch));

            bool synchronizationMadeChanges = false;


            if (this.ControlOrder != controlRowToMatch.ControlOrder)
            {
                this.ControlOrder = controlRowToMatch.ControlOrder;
                synchronizationMadeChanges = true;
            }
            if (this.DefaultValue != controlRowToMatch.DefaultValue)
            {
                this.DefaultValue = controlRowToMatch.DefaultValue;
                synchronizationMadeChanges = true;
            }
            if (this.Type != controlRowToMatch.Type)
            {
                this.Type = controlRowToMatch.Type;
                synchronizationMadeChanges = true;
            }
            if (this.Label != controlRowToMatch.Label)
            {
                this.Label = controlRowToMatch.Label;
                synchronizationMadeChanges = true;
            }
            if (this.List != controlRowToMatch.List)
            {
                this.List = controlRowToMatch.List;
                synchronizationMadeChanges = true;
            }
            if (this.SpreadsheetOrder != controlRowToMatch.SpreadsheetOrder)
            {
                this.SpreadsheetOrder = controlRowToMatch.SpreadsheetOrder;
                synchronizationMadeChanges = true;
            }
            if (this.Tooltip != controlRowToMatch.Tooltip)
            {
                this.Tooltip = controlRowToMatch.Tooltip;
                synchronizationMadeChanges = true;
            }
            if (this.Visible != controlRowToMatch.Visible)
            {
                this.Visible = controlRowToMatch.Visible;
                synchronizationMadeChanges = true;
            }
            if (this.ExportToCSV != controlRowToMatch.ExportToCSV)
            {
                this.ExportToCSV = controlRowToMatch.ExportToCSV;
                synchronizationMadeChanges = true;
            }
            return synchronizationMadeChanges;
        }

        #endregion

        #region Public Methods (can be overriden) - CreateColumnTuplesWithWhereForControlRowByID
        // Create a column tuple for the control based on this row's control attribute values 
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, this.ControlOrder),
                new ColumnTuple(Constant.Control.DataLabel, this.DataLabel),
                new ColumnTuple(Constant.Control.DefaultValue, this.DefaultValue),
                new ColumnTuple(Constant.Control.Label, this.Label),
                new ColumnTuple(Constant.Control.List, this.List),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, this.SpreadsheetOrder),

                new ColumnTuple(Constant.Control.Tooltip, this.Tooltip),
                new ColumnTuple(Constant.Control.Type, this.Type),
                new ColumnTuple(Constant.Control.Visible, this.Visible),
                new ColumnTuple(Constant.Control.ExportToCSV, this.ExportToCSV),
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion
    }
}
