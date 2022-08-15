using System;

namespace Timelapse.Database
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
            this.DataLabel = String.Empty;
            this.DisplayLabel = String.Empty;
            this.ControlType = String.Empty;
            this.IsAscending = Constant.BooleanValue.True;
        }

        public SortTerm(string dataLabel, string label, string controlType, string isAscending)
        {
            this.DataLabel = dataLabel;
            this.DisplayLabel = label;
            this.ControlType = controlType;
            this.IsAscending = isAscending;
        }
        #endregion
    }
}