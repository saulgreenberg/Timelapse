using System;

namespace Timelapse.DataTables
{
    public class FileTableDecimalColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid Decimal values are parsable as doubles
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return Double.TryParse(value, out _);
        }
        #endregion
    }
}
