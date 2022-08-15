using System;

namespace Timelapse.Database
{
    /// <summary>
    /// FileTableCounterCollumn - A FileTable Column holding Counter values
    /// </summary>
    public class FileTableCounterColumn : FileTableColumn
    {
        #region Constructors
        public FileTableCounterColumn(ControlRow control)
            : base(control)
        {
        }
        #endregion

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
