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
            this.ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
            DataObject.AddPastingHandler(this.ContentControl, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfIntegerPositive);
        }
        #endregion`
    }
}
