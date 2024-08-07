using System.Windows;
using Timelapse.DataTables;
using Timelapse.Util;

// DecimalAny: Any negative or positive decimal as input. Comprises:
// - a label containing the descriptive label) 
// - a DoubleUpDown control containing the content 
namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDecimalPositive : DataEntryDecimalBase
    {
        #region Constructor

        public DataEntryDecimalPositive(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, true)
        {
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfDecimalPositive);
        }
        #endregion
    }
}