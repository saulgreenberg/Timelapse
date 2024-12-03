using System.Windows;
using Timelapse.DataTables;
using Timelapse.Util;

// IntegerAny: Any negative or positive integer as input. Comprises:
// - a label containing the descriptive label) 
// - an IntegerControl containing the content 
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryIntegerAny : DataEntryIntegerBase
    {
        #region Constructor

        public DataEntryIntegerAny(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, false)
        {
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfIntegerAny);
        }
        #endregion
    }
}
