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

        // Return a cloned copy of the provided search term
        public SearchTerm Clone()
        {
            return new()
            {
                ControlType = ControlType,
                DatabaseValue = DatabaseValue,
                DataLabel = DataLabel,
                Label = Label,
                List = [..List],
                Operator = Operator,
                UseForSearching = UseForSearching
            };
        }
        #endregion


    }
}
