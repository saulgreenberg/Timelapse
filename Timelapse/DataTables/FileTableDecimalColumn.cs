using System;

namespace Timelapse.DataTables
{
    public class FileTableDecimalColumn : FileTableColumn
    {
        #region Constructors
        public FileTableDecimalColumn(ControlRow control)
            : base(control)
        {
        }
        #endregion

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
