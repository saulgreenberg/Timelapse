using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// ControlRow defines a single control as described in the Template.  
    /// Its property names are the same as those in the template:
    /// - ControlOrder, SpreadsheetOrder, Type, DefaultValue, Label, DataLabel, Width, Tooltip, Copyable, Visible, List   
    /// </summary>
    public class ControlRow : DataRowBackedObject
    {
        private static readonly char[] BarDelimiter = { '|' };

        #region Public Properties
        public long ControlOrder
        {
            get { return this.Row.GetLongField(Constant.Control.ControlOrder); }
            set { this.Row.SetField(Constant.Control.ControlOrder, value); }
        }

        public bool Copyable
        {
            get { return this.Row.GetBooleanField(Constant.Control.Copyable); }
            set { this.Row.SetField(Constant.Control.Copyable, value); }
        }

        public string DataLabel
        {
            get { return this.Row.GetStringField(Constant.Control.DataLabel); }
            set { this.Row.SetField(Constant.Control.DataLabel, value); }
        }

        public string DefaultValue
        {
            get { return this.Row.GetStringField(Constant.Control.DefaultValue); }
            set { this.Row.SetField(Constant.Control.DefaultValue, value); }
        }

        public string Label
        {
            get { return this.Row.GetStringField(Constant.Control.Label); }
            set { this.Row.SetField(Constant.Control.Label, value); }
        }

        public string List
        {
            get { return this.Row.GetStringField(Constant.Control.List); }
            set { this.Row.SetField(Constant.Control.List, value); }
        }

        public long SpreadsheetOrder
        {
            get { return this.Row.GetLongField(Constant.Control.SpreadsheetOrder); }
            set { this.Row.SetField(Constant.Control.SpreadsheetOrder, value); }
        }


        public string Tooltip
        {
            get { return this.Row.GetStringField(Constant.Control.Tooltip); }
            set { this.Row.SetField(Constant.Control.Tooltip, value); }
        }

        public string Type
        {
            get { return this.Row.GetStringField(Constant.Control.Type); }
            set { this.Row.SetField(Constant.Control.Type, value); }
        }

        public bool Visible
        {
            get { return this.Row.GetBooleanField(Constant.Control.Visible); }
            set { this.Row.SetField(Constant.Control.Visible, value); }
        }
        public int Width
        {
            get { return this.Row.GetIntegerField(Constant.Control.TextBoxWidth); }
            set { this.Row.SetField(Constant.Control.TextBoxWidth, value); }
        }
        #endregion

        #region Constructors
        public ControlRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - GetChoices (also GetChoicesForQuickpateMenu), various forms parse the choice string as a list
        // Parce and return the choice string into a list of items. 
        // Overload: the caller is uninterested in knowing if there are any empty items in the list, and wants the empty item removed
        public List<string> GetChoices(bool removeEmptyChoiceItem)
        {
            return this.GetChoices(out _, removeEmptyChoiceItem);
        }

        // Overload: the caller is interested in knowing if there are any empty items in the list, 
        // and wants the empty item removed (usually because they will add it themselves to a menu
        public List<string> GetChoices(out bool includesEmptyChoice)
        {
            bool removeEmptyChoiceItem = true;
            return this.GetChoices(out includesEmptyChoice, removeEmptyChoiceItem);
        }

        // Parce the choice string into a list of items. 
        // If it includes an empty choice item, set the includesEmptyChoice flag. 
        // Delete the empty choice item if the removeEmptyChoice flag is set
        public List<string> GetChoices(out bool includesEmptyChoice, bool removeEmptyChoiceItem)
        {
            List<string> list = this.List.Split(ControlRow.BarDelimiter).ToList();
            if (list.Contains(Constant.ControlMiscellaneous.EmptyChoiceItem))
            {
                includesEmptyChoice = true;
                if (removeEmptyChoiceItem)
                {
                    list.Remove(Constant.ControlMiscellaneous.EmptyChoiceItem);
                }
            }
            else
            {
                includesEmptyChoice = false;
            }
            return list;
        }

        // Parce the choice string into a list of items. 
        // If it includes an empty choice item, remove the abstract <Empty> 
        // and insert an empty string as the first menu item
        public List<string> GetChoicesForQuickPasteMenu()
        {
            List<string> list = this.List.Split(ControlRow.BarDelimiter).ToList();
            if (list.Contains(Constant.ControlMiscellaneous.EmptyChoiceItem))
            {
                list.Remove(Constant.ControlMiscellaneous.EmptyChoiceItem);
                list.Insert(0, String.Empty);
            }
            return list;
        }
        #endregion

        #region Public Methods - SetChoices: Given a List of choices, convert it into '|' separated string
        public void SetChoices(List<string> choices)
        {
            this.List = String.Join("|", choices);
        }
        #endregion

        #region Public Methods - Try Update This ControlRow To Match

        /// <summary>
        /// Check if a synchronization between the given control row and this instance's control row is needed,
        /// which wojld occur if any field differs.
        /// Note: As a side effect it also re-orders this instance's ControlOrder and SpreadsheetOrder to the other's order if needed
        /// </summary>
        /// <param name="controlRowToMatch"></param>
        /// <returns></returns>
        public bool TryUpdateThisControlRowToMatch(ControlRow controlRowToMatch)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlRowToMatch, nameof(controlRowToMatch));

            bool synchronizationMadeChanges = false;
            if (this.Copyable != controlRowToMatch.Copyable)
            {
                this.Copyable = controlRowToMatch.Copyable;
                synchronizationMadeChanges = true;
            }
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
            if (this.Width != controlRowToMatch.Width)
            {
                this.Width = controlRowToMatch.Width;
                synchronizationMadeChanges = true;
            }
            return synchronizationMadeChanges;
        }

        #endregion

        #region Public Methods (can be overriden) - CreateColumnTuplesWithWhereForControlRowByID
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, this.ControlOrder),
                new ColumnTuple(Constant.Control.Copyable, this.Copyable),
                new ColumnTuple(Constant.Control.DataLabel, this.DataLabel),
                new ColumnTuple(Constant.Control.DefaultValue, this.DefaultValue),
                new ColumnTuple(Constant.Control.Label, this.Label),
                new ColumnTuple(Constant.Control.List, this.List),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, this.SpreadsheetOrder),
                new ColumnTuple(Constant.Control.TextBoxWidth, this.Width),
                new ColumnTuple(Constant.Control.Tooltip, this.Tooltip),
                new ColumnTuple(Constant.Control.Type, this.Type),
                new ColumnTuple(Constant.Control.Visible, this.Visible)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion
    }
}
