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

            return control.Type switch
            {
                Control.Note or Control.AlphaNumeric or Control.MultiLine or DatabaseColumn.File or DatabaseColumn.RelativePath => new FileTableNoteColumn(control),
                Control.Counter or Control.IntegerAny or Control.IntegerPositive => new FileTableCounterColumn(control),
                Control.DecimalAny or Control.DecimalPositive => new FileTableDecimalColumn(control),
                DatabaseColumn.DateTime => new FileTableDateTimeColumn(control),
                DatabaseColumn.DeleteFlag or Control.Flag => new FileTableFlagColumn(control),
                Control.FixedChoice or Control.MultiChoice => new FileTableChoiceColumn(control),
                Control.DateTime_ => new FileTableDateTimeColumn(control),
                Control.Date_ => new FileTableDateColumn(control),
                Control.Time_ => new FileTableTimeColumn(control),
                _ => null,
            };
        }
        #endregion
    }
}
