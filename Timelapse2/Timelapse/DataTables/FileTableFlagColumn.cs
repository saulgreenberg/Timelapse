using System;

namespace Timelapse.Database
{
    /// <summary>
    /// FileTableFlagColumn - A FileTable Column holding Boolean true/false values
    /// </summary>
    public class FileTableFlagColumn : FileTableColumn
    {
        #region Constructors
        public FileTableFlagColumn(ControlRow control)
            : base(control)
        {
        }
        #endregion

        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid flag values are parsable as false or true, regardless of case
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return String.Equals(value, Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, Constant.BooleanValue.True, StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
