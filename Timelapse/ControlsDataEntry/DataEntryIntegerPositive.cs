using System.Windows;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryIntegerPositive : DataEntryIntegerBase
    {
        #region Constructor

        public DataEntryIntegerPositive(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, true)
        {
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfIntegerPositive);
        }
        #endregion`
    }
}
