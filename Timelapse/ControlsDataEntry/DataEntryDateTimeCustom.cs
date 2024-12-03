using System;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDateTimeCustom : DataEntryDateTimeBase
    {
        #region Constructor

        public DataEntryDateTimeCustom(ControlRow control, DataEntryControls styleProvider, DateTime defaultValue) :
            base(control, styleProvider, DateTimeFormatEnum.DateAndTime, defaultValue)
        {
        }
        #endregion
    }
}
