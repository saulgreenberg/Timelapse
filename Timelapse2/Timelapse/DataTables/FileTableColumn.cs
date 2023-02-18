using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableColumn: An abstract class that 
    /// - Stores the column type as well as its associated DataLable
    /// - Types are Choice, Counter, Note, Flag, DateTime. Note that other columns
    /// - creates columns of various types, each comprising a single column in the FileTable (aka the DataTable in DataBase)
    /// </summary>
    public abstract class FileTableColumn
    {
        #region Public Properties
        public string ControlType { get; }

        public string DataLabel { get; }

        public abstract bool IsContentValid(string content);
        #endregion

        #region Constructors
        /// <summary>
        /// Given a ControlRow (i.e., a template row definitions) construct a column for its data based on the 
        /// - the control type (Note, Date, File etc)
        /// - its DataLabel
        /// </summary>
        /// <param name="control"></param>
        protected FileTableColumn(ControlRow control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            this.ControlType = control.Type;
            this.DataLabel = control.DataLabel;
        }
        #endregion

        #region Public Static Methods - CreateColumnMatchingControlRowsType
        // Given a ControlRow (i.e., a template row definitions), create a column depending upon its type
        public static FileTableColumn CreateColumnMatchingControlRowsType(ControlRow control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            switch (control.Type)
            {
                case Constant.Control.Note:
                case Constant.DatabaseColumn.File:
                case Constant.DatabaseColumn.RelativePath:
                    return new FileTableNoteColumn(control);
                case Constant.Control.Counter:
                    return new FileTableCounterColumn(control);
                case Constant.DatabaseColumn.DateTime:
                    return new FileTableDateTimeColumn(control);
                case Constant.DatabaseColumn.DeleteFlag:
                case Constant.Control.Flag:
                    return new FileTableFlagColumn(control);
                case Constant.Control.FixedChoice:
                    return new FileTableChoiceColumn(control);
                default:
                    return null;
                    //throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
        #endregion
    }
}
