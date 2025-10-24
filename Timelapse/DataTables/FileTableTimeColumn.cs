using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableDateTimeColumn - A FileTable Column holding DateTime values
    /// </summary>
    public class FileTableTimeColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid

        /// <summary>
        /// Valid Time values are parsable as DateTime
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return DateTimeHandler.TryParseDatabaseTime(value, out _);
        }

        #endregion
    }
}
