using System;
using System.Collections.Generic;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.SearchingAndSorting
{
    /// <summary>
    /// A SearchTerm stores the search criteria for each column
    /// </summary>
    [Serializable]
    public class SearchTerm
    {
        #region Public Properties
        public string ControlType { get; set; }

        public string DatabaseValue
        {
            get; 
            set;
        }
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public List<string> List { get; set; }
        public string Operator { get; set; }
        public bool UseForSearching { get; set; }

        // Only set in saved Json, when the term's operator was saved as an equivalent one that older versions of Timelapse know
        // (see UseOperatorOlderVersionsKnow): the term's real operator. Older versions ignore it; this version restores the operator from it
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string SavedOperator { get; set; }
        #endregion

        #region Constructors
        public SearchTerm()
        {
            ControlType = string.Empty;
            DatabaseValue = string.Empty;
            DataLabel = string.Empty;
            Label = string.Empty;
            List = null;
            Operator = string.Empty;
            UseForSearching = false;
        }

        public SearchTerm(SearchTerm other)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(other, nameof(other));

            ControlType = other.ControlType;
            DatabaseValue = other.DatabaseValue;
            DataLabel = other.DataLabel;
            Label = other.Label;
            List = other.List == null 
                ? null 
                : [..other.List];
            Operator = other.Operator;
            UseForSearching = other.UseForSearching;
        }
        #endregion

        #region Public Methods - Get values to Convert DateTime
        public DateTime GetDateTime()
        {
            if (DataLabel != DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(
                    $"Attempt to retrieve date/time from a SearchTerm with data label {DataLabel}.");
            }
            return DateTimeHandler.TryParseDatabaseOrDisplayDateTime(DatabaseValue, out DateTime dateTime)
                ? dateTime
                : DateTime.MinValue;
        }
        #endregion

        #region Public Methods - Set Values - to Convert DateTime
        public void SetDatabaseValue(DateTime dateTime)
        {
            if (DataLabel != DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(
                    $"Attempt to retrieve date/time from a SearchTerm with data label {DataLabel}.");
            }
            DatabaseValue = DateTimeHandler.ToStringDatabaseDateTime(dateTime);
        }

        // Older versions of Timelapse don't know the 'is any of' operator. If they restore a term that uses it, they build an invalid query
        // and shut down. So, when saving, a copy of such a term is given an operator they know, keeping the real one in SavedOperator:
        // - Fixed Choice, with one item or none: '=' (the same meaning). With several items it is left as is, as older versions reject
        //   a value that isn't a single menu item, and don't restore the term.
        // - MultiChoice: Includes (the same meaning for one item; for several it isn't the same, see IsAnyOfWithSeveralItems),
        //   or '=' an empty value if nothing is selected.
        // Only call this on a copy that is about to be saved
        public void UseOperatorOlderVersionsKnow()
        {
            if (Operator != SearchTermOperator.IsAnyOf || (ControlType != Control.FixedChoice && ControlType != Control.MultiChoice))
            {
                return;
            }
            List<string> items = CustomSelection.SplitIntoChoiceItems(DatabaseValue, List);
            items.RemoveAll(item => item == SearchTermOperator.EmptyChoiceItem && ControlType == Control.MultiChoice);
            if (ControlType == Control.FixedChoice && items.Count > 1)
            {
                return;
            }
            SavedOperator = Operator;
            if (ControlType == Control.FixedChoice || items.Count == 0)
            {
                Operator = SearchTermOperator.Equal;
                DatabaseValue = items.Count == 0 || items[0] == SearchTermOperator.EmptyChoiceItem ? string.Empty : items[0];
            }
            else
            {
                Operator = SearchTermOperator.Includes;
            }
        }

        // Whether this is an 'is any of' term with several items, which older versions of Timelapse can't search for in the same way.
        // If such a term is in use, the saved selection is marked so that they select all files instead (see CustomSelection.ToJsonForSaving)
        public bool IsAnyOfWithSeveralItems()
        {
            return Operator == SearchTermOperator.IsAnyOf
                   && ControlType is Control.FixedChoice or Control.MultiChoice
                   && CustomSelection.SplitIntoChoiceItems(DatabaseValue, List).Count > 1;
        }

        // When restoring a saved term, use its real operator if it was saved as an equivalent one (see UseOperatorOlderVersionsKnow)
        public void RestoreSavedOperator()
        {
            if (false == string.IsNullOrEmpty(SavedOperator))
            {
                Operator = SavedOperator;
                SavedOperator = null;
            }
        }

        // Return a cloned copy of the provided search term
        public SearchTerm Clone()
        {
            return new()
            {
                ControlType = ControlType,
                DatabaseValue = DatabaseValue,
                DataLabel = DataLabel,
                Label = Label,
                List = List == null ? null : [..List],
                Operator = Operator,
                UseForSearching = UseForSearching
            };
        }
        #endregion


    }
}
