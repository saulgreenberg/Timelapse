namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableNoteColumn - A FileTable Column holding strings
    /// </summary>
    public class FileTableNoteColumn(ControlRow control) : FileTableColumn(control)
    {
        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid note values are strings, so we always return true
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return true;
        }
        #endregion
    }
}
