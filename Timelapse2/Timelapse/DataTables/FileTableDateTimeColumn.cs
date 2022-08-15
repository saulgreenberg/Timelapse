using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// FileTableDateTimeColumn - A FileTable Column holding DateTime values
    /// </summary>
    public class FileTableDateTimeColumn : FileTableColumn
    {
        #region Constructors
        public FileTableDateTimeColumn(ControlRow control)
            : base(control)
        {
        }
        #endregion

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
