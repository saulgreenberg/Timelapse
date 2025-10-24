using Timelapse.Constant;

namespace Timelapse.SearchingAndSorting
{
    /// <summary>
    /// A SortTerm is a tuple of 4 that indicates various aspects that may be considered when sorting 
    /// </summary>
    public class SortTerm(string dataLabel, string label, string controlType, string isAscending)
    {
        #region Public Properties
        public string DataLabel { get; set; } = dataLabel;

        // The text representing the sort term, to be displayed in the dropdown menu 
        public string DisplayLabel { get; set; } = label;

        public string ControlType { get; set; } = controlType;

        // IsAscending  indicating (via Constant.BooleanValue.True or False) if the sort should be ascending or descending
        public string IsAscending { get; set; } = isAscending;

        #endregion

        #region Constructors
        public SortTerm() : this(string.Empty, string.Empty, string.Empty, BooleanValue.True)
        {
        }

        #endregion
    }
}