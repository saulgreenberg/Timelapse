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

        // ReSharper disable once UnusedMember.Global
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
                case Constant.Control.AlphaNumeric:
                case Constant.Control.MultiLine:
                case Constant.DatabaseColumn.File:
                case Constant.DatabaseColumn.RelativePath:
                    return new FileTableNoteColumn(control);
                case Constant.Control.Counter:
                case Constant.Control.IntegerAny:
                case Constant.Control.IntegerPositive:
                    return new FileTableCounterColumn(control);
                case Constant.Control.DecimalAny:
                case Constant.Control.DecimalPositive:
                    return new FileTableDecimalColumn(control);
                case Constant.DatabaseColumn.DateTime:
                    return new FileTableDateTimeColumn(control);
                case Constant.DatabaseColumn.DeleteFlag:
                case Constant.Control.Flag:
                    return new FileTableFlagColumn(control);
                case Constant.Control.FixedChoice:
                case Constant.Control.MultiChoice:
                    return new FileTableChoiceColumn(control);
                case Constant.Control.DateTime_:
                    return new FileTableDateTimeColumn(control);
                case Constant.Control.Date_:
                    return new FileTableDateColumn(control);
                case Constant.Control.Time_:
                    return new FileTableTimeColumn(control);
                default:
                    return null;
                    //throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
        #endregion
    }
}
