using System.Windows;
using Timelapse.DataTables;
using Timelapse.Util;

// DecimalAny: Any negative or positive decimal as input. Comprises:
// - a label containing the descriptive label) 
// - a DoubleUpDown control containing the content 
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDecimalAny : DataEntryDecimalBase
    {
        #region Constructor

        public DataEntryDecimalAny(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, false)
        {
            this.ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
            DataObject.AddPastingHandler(this.ContentControl, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfDecimalAny);
        }
        #endregion
    }
}