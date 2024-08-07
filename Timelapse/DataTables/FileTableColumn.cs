using Timelapse.Constant;
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

            ControlType = control.Type;
            DataLabel = control.DataLabel;
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
                case Control.Note:
                case Control.AlphaNumeric:
                case Control.MultiLine:
                case DatabaseColumn.File:
                case DatabaseColumn.RelativePath:
                    return new FileTableNoteColumn(control);
                case Control.Counter:
                case Control.IntegerAny:
                case Control.IntegerPositive:
                    return new FileTableCounterColumn(control);
                case Control.DecimalAny:
                case Control.DecimalPositive:
                    return new FileTableDecimalColumn(control);
                case DatabaseColumn.DateTime:
                    return new FileTableDateTimeColumn(control);
                case DatabaseColumn.DeleteFlag:
                case Control.Flag:
                    return new FileTableFlagColumn(control);
                case Control.FixedChoice:
                case Control.MultiChoice:
                    return new FileTableChoiceColumn(control);
                case Control.DateTime_:
                    return new FileTableDateTimeColumn(control);
                case Control.Date_:
                    return new FileTableDateColumn(control);
                case Control.Time_:
                    return new FileTableTimeColumn(control);
                default:
                    return null;
                    //throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
        #endregion
    }
}
