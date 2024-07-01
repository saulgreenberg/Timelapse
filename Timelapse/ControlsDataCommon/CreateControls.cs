using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsDataCommon
{
    public static class CreateControls
    {
        // There are two  versions of each Create method to handle two different argument types:
        // - a ControlRow
        // - a MetadataControlRow

        #region Create AlphaNumeric
        public static TextBox CreateAlphaNumberic(ControlRow control, string defaultValue)
        {
            return CreateAlphaNumeric(control.Tooltip, defaultValue);
        }

        public static TextBox CreateAlphaNumberic(MetadataControlRow control, string defaultValue)
        {
            return CreateAlphaNumeric(control.Tooltip, defaultValue);
        }

        public static TextBox CreateAlphaNumeric(string tooltip, string defaultValue)
        {
            TextBox alphaNumeric = new TextBox
            {
                ToolTip = tooltip,
                Height = 26,
                Width = Constant.ControlDefault.NoteDefaultWidth,
            };

            CreateControls.Configure(alphaNumeric,  defaultValue);
            alphaNumeric.GotFocus += Control_GotFocus;
            alphaNumeric.LostFocus += Control_LostFocus;
            return alphaNumeric;
        }

        public static void Configure(TextBox alphaNumeric, string defaultValue)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(alphaNumeric, nameof(alphaNumeric));
            alphaNumeric.Text = defaultValue ?? Constant.ControlDefault.NoteDefaultValue;
        }
        #endregion

        #region Create DateTimePicker
        public static DateTimePicker CreateDateTimePicker(ControlRow control, DateTimeFormatEnum dateTimeFormat, DateTime defaultValue)
        {
            return CreateDateTimePicker(control.Tooltip, dateTimeFormat, defaultValue);
        }

        public static DateTimePicker CreateDateTimePicker(MetadataControlRow control, DateTimeFormatEnum dateTimeFormat, DateTime defaultValue)
        {
            return CreateDateTimePicker(control.Tooltip, dateTimeFormat, defaultValue);
        }

        public static DateTimePicker CreateDateTimePicker(string tooltip, DateTimeFormatEnum dateTimeFormat, DateTime defaultValue)
        {
            DateTimePicker dateTimePicker = new DateTimePicker
            {
                ToolTip =tooltip,
                Height = 26,
                CultureInfo = CultureInfo.CreateSpecificCulture("en-US")
            };
            switch (dateTimeFormat)
            {
                case DateTimeFormatEnum.DateOnly:
                    dateTimePicker.Width = Constant.ControlDefault.Date_DefaultWidth;
                    dateTimePicker.TimePickerVisibility = Visibility.Collapsed;
                    break; 
                case DateTimeFormatEnum.DateAndTime:
                default:
                    dateTimePicker.Width = Constant.ControlDefault.DateTimeDefaultWidth;
                    break;
            }
            CreateControls.Configure(dateTimePicker, dateTimeFormat, defaultValue);
            dateTimePicker.GotFocus += Control_GotFocus;
            dateTimePicker.LostFocus += Control_LostFocus;
            return dateTimePicker;
        }

        public static void Configure(DateTimePicker dateTimePicker, DateTimeFormatEnum dateTimeFormat, DateTime? defaultValue)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");

            switch (dateTimeFormat)
            {
                case DateTimeFormatEnum.DateOnly:
                    dateTimePicker.FormatString = Constant.Time.DateDisplayFormat;
                    break;
                case DateTimeFormatEnum.DateAndTime:
                default:
                    dateTimePicker.FormatString = Constant.Time.DateTimeDisplayFormat;
                    dateTimePicker.TimeFormat = DateTimeFormat.Custom;
                    dateTimePicker.TimeFormatString = Constant.Time.TimeFormat;
                    break;
            }
            dateTimePicker.Value = defaultValue ?? Constant.ControlDefault.DateTimeCustomDefaultValue;
        }
        #endregion

        #region Create TimePicker
        public static TimePicker CreateTimePicker(ControlRow control, DateTime defaultValue)
        {
            return CreateTimePicker(control.Tooltip, defaultValue);
        }

        public static TimePicker CreateTimePicker(MetadataControlRow control, DateTime defaultValue)
        {
            return CreateTimePicker(control.Tooltip, defaultValue);
        }

        public static TimePicker CreateTimePicker(string tooltip, DateTime defaultValue)
        {
            TimePicker timePicker = new TimePicker
            {
                ToolTip = tooltip,
                Width = ControlDefault.Time_Width, 
                AllowTextInput = true,
                Height = 26,
                CultureInfo = CultureInfo.CreateSpecificCulture("en-US"),
                Format = DateTimeFormat.Custom,
                FormatString = Time.TimeFormat,
                Value = defaultValue,
                TimeInterval = TimeSpan.FromMinutes(15),
                StartTime = TimeSpan.FromHours(9),
                MaxDropDownHeight = 250
            };
            CreateControls.Configure(timePicker, defaultValue);
            timePicker.GotFocus += Control_GotFocus;
            timePicker.LostFocus += Control_LostFocus;
            return timePicker;
        }

        public static void Configure(TimePicker timePicker,  DateTime? defaultValue)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(timePicker, nameof(timePicker));
            timePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            timePicker.Format = DateTimeFormat.Custom;
            timePicker.FormatString = Time.TimeFormat;
            timePicker.Value = defaultValue ?? Constant.ControlDefault.Time_DefaultValue;
            timePicker.TimeInterval = TimeSpan.FromMinutes(15);
        }
        #endregion

        #region Create Checkbox 
        public static CheckBox CreateFlag(DataEntryControls styleProvider, ControlRow control)
        {
            return CreateFlag(styleProvider, control.Tooltip);
        }
        public static CheckBox CreateFlag(DataEntryControls styleProvider, MetadataControlRow control)
        {
            return CreateFlag(styleProvider, control.Tooltip);
        }
        private static CheckBox CreateFlag(DataEntryControls styleProvider, string tooltip)
        {
            CheckBox checkBox = new CheckBox
            {
                Visibility = Visibility.Visible,
                ToolTip = tooltip,
                Style = styleProvider.FindResource(ControlContentStyleEnum.FlagCheckBox.ToString()) as Style
            };
            checkBox.GotFocus += Control_GotFocus;
            checkBox.LostFocus += Control_LostFocus;
            return checkBox;
        }
        #endregion

        #region Private Callbacks for above control types
        // Highlight control when it gets the focus (simulates aspects of tab control in Timelapse)
        private static void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Control control)
            {
                control.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
                control.BorderBrush = Constant.Control.BorderColorHighlight;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }

        // Remove the highlight by restoring the original border appearance
        private static void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Control control)
            {
                control.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
                control.BorderBrush = Constant.Control.BorderColorNormal;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }
        #endregion
    }
}
