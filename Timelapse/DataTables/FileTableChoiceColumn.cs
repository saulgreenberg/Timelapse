using System.Collections.Generic;
using Timelapse.DataStructures;
using Timelapse.Util;

namespace Timelapse.DataTables
{
    /// <summary>
    /// FileTableChoiceColumn - A FileTable Column holding a list of chices as well as a default value.
    /// These will be used to construct a pop-up menu with the default selected.
    /// </summary>
    public class FileTableChoiceColumn : FileTableColumn
    {
        #region Private Variables
        private readonly List<string> choices;
        private readonly string defaultValue;
        #endregion

        #region Constructors
        public FileTableChoiceColumn(ControlRow control)
            : base(control)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));
            choices = Choices.ChoicesFromJson(control.List).ChoiceList;
            defaultValue = control.DefaultValue;
        }
        #endregion

        #region Public Methods - IsContentValid
        /// <summary>
        /// Valid Choice values are string that are either the same as teh defaultValue or that is in the choices list
        /// </summary>
        public override bool IsContentValid(string value)
        {
            // the editor doesn't currently enforce the default value as one of the choices, so accept it as valid independently
            if (value == defaultValue)
            {
                return true;
            }
            return choices.Contains(value);
        }
        #endregion
    }
}
