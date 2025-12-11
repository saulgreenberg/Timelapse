using System;
using Timelapse.Constant;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableFlagColumn - A FileTable Column holding Boolean true/false values
    /// </summary>
    public class FileTableFlagColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid flag values are parsable as false or true, regardless of case
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return String.Equals(value, BooleanValue.False, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, BooleanValue.True, StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
