using System;
using System.Globalization;
using System.Windows;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDate : DataEntryDateTimeBase
    {
        #region Constructor

        public DataEntryDate(ControlRow control, DataEntryControls styleProvider, DateTime defaultValue) :
            base(control, styleProvider, DateTimeFormatEnum.DateOnly, defaultValue)
        {
            this.ContentControl.CultureInfo = CultureInfo.CreateSpecificCulture("en-US");
            this.ContentControl.Format = Xceed.Wpf.Toolkit.DateTimeFormat.Custom;
            this.ContentControl.FormatString = Timelapse.Constant.Time.DateDisplayFormat;
            this.ContentControl.TimePickerVisibility = Visibility.Collapsed;
        }

        public override void SetContentAndTooltip(string value)
        {
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is WatermarkTextBox textBox)
            {
                if (value == null)
                {
                    textBox.Text = Constant.Unicode.Ellipsis;
                }
                else
                {
                    textBox.Text = DateTimeHandler.TryParseDateDatabaseAndDisplayFormats(value, out DateTime dateTime2)
                        ? DateTimeHandler.ToStringDisplayDatePortion(dateTime2)
                        : value;
                    this.ContentControl.Text = textBox.Text;
                }
            }
            else
            {
                this.ContentControl.Text = value;
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for the selected image";
        }
        #endregion
    }
}
