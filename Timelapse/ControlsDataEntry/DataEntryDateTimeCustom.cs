using System;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDateTimeCustom(ControlRow control, DataEntryControls styleProvider, DateTime defaultValue)
        : DataEntryDateTimeBase(control, styleProvider, DateTimeFormatEnum.DateAndTime, defaultValue);
}
