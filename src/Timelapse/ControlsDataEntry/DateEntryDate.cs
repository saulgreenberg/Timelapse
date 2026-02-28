using System;
using System.Globalization;
using System.Windows;
using Timelapse.Constant;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDate : DataEntryDateTimeBase
    {
        #region Constructor

        public DataEntryDate(ControlRow control, DataEntryControls styleProvider, DateTime defaultValue) :
            base(control, styleProvider, DateTimeFormatEnum.DateOnly, defaultValue)
        {
            ContentControl.CultureInfo = CultureInfo.CreateSpecificCulture("en-US");
            ContentControl.Format = DateTimeFormat.Custom;
            ContentControl.FormatString = Time.DateDisplayFormat;
            ContentControl.TimePickerVisibility = Visibility.Collapsed;
        }

        public override void SetContentAndTooltip(string value)
        {
            if (value == null)
            {
                ContentControl.ForceWatermark = true;
                ContentControl.HideOnFocus = false;
                ContentControl.ToolTip = "Edit to change the " + Label + " for the selected image";

            }
            else
            {
                ContentControl.ForceWatermark = false;
                ContentControl.HideOnFocus = true;
                if (DateTimeHandler.TryParseDatabaseOrDisplayDate(value, out DateTime dateTime))
                {
                    ContentControl.Value = dateTime;
                }
                ContentControl.ToolTip = value;
            }
        }
        #endregion
    }
}
