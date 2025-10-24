using System;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableCounterCollumn - A FileTable Column holding Counter values
    /// </summary>
    public class FileTableCounterColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid counter values are parsable as integers
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return Int64.TryParse(value, out _);
        }
        #endregion
    }
}
