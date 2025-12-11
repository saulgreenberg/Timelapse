using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableDateTimeColumn - A FileTable Column holding DateTime values
    /// </summary>
    public class FileTableDateColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid
        /// <summary>
        /// Valdi Date values are parsable as DateTime
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return DateTimeHandler.TryParseDatabaseDate(value, out _);
        }
        #endregion
    }
}
