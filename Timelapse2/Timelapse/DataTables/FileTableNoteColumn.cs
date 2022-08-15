namespace Timelapse.Database
{
    /// <summary>
    /// FileTableNoteColumn - A FileTable Column holding strings
    /// </summary>
    public class FileTableNoteColumn : FileTableColumn
    {
        #region Constructors
        public FileTableNoteColumn(ControlRow control)
            : base(control)
        {
        }
        #endregion

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
