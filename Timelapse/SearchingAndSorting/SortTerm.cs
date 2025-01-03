﻿using Timelapse.Constant;

namespace Timelapse.SearchingAndSorting
{
    /// <summary>
    /// A SortTerm is a tuple of 4 that indicates various aspects that may be considered when sorting 
    /// </summary>
    public class SortTerm
    {
        #region Public Properties
        public string DataLabel { get; set; }

        // The text representing the sort term, to be displayed in the dropdown menu 
        public string DisplayLabel { get; set; }

        public string ControlType { get; set; }

        // IsAscending  indicating (via Constant.BooleanValue.True or False) if the sort should be ascending or descending
        public string IsAscending { get; set; }
        #endregion

        #region Constructors
        public SortTerm()
        {
            DataLabel = string.Empty;
            DisplayLabel = string.Empty;
            ControlType = string.Empty;
            IsAscending = BooleanValue.True;
        }

        public SortTerm(string dataLabel, string label, string controlType, string isAscending)
        {
            DataLabel = dataLabel;
            DisplayLabel = label;
            ControlType = controlType;
            IsAscending = isAscending;
        }
        #endregion
    }
}