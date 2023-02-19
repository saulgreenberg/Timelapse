namespace DialogUpgradeFiles.DataTables
{
    public class FileTableUtcOffsetColumn : FileTableColumn
    {
        /// <summary>
        /// FileTableUtcOffsetColumn - A FileTable Column holding a UTCOffset (a double)
        /// </summary>
        #region Constructors
        public FileTableUtcOffsetColumn(ControlRow control)
            : base(control)
        {
        }
        #endregion

        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid UtcOffsets values are parsable as doubles
        /// </summary>
        public override bool IsContentValid(string value)
        {
            return double.TryParse(value, out _);
        }
        #endregion
    }
}
