using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableDateTimeColumn - A FileTable Column holding DateTime values
    /// </summary>
    public class FileTableDateTimeColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid
        /// <summary>
        /// Valdi DateTime values are parsable as DateTime
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return DateTimeHandler.TryParseDatabaseDateTime(value, out _);
        }
        #endregion
    }
}
