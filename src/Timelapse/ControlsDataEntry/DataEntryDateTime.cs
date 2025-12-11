using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDateTime : DataEntryControl<WatermarkDateTimePicker, Label>
    {
        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl =>
            // We return the textbox part of the content control, as otherwise focus does not work properly when we try to set it on the content control
            (UIElement)ContentControl.Template.FindName("PART_TextBox", ContentControl);

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        public override string Content => ContentControl.Text;

        public DataEntryNote DateControl { get; set; }
        public DataEntryNote TimeControl { get; set; }
        #endregion

        #region Constructor
        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // configure the various elements
            CreateControls.Configure(ContentControl, DateTimeFormatEnum.DateAndTime, null);
            ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
            ContentControl.Watermark = Unicode.Ellipsis;
        }
        #endregion

        #region Event Handlers

        // Highlight the border whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new(Control.BorderThicknessHighlight);
            ContentControl.BorderBrush = Control.BorderColorHighlight;
        }

        private void ContentControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new(Control.BorderThicknessNormal);
            ContentControl.BorderBrush = Control.BorderColorNormal;
        }
        #endregion

        #region Setting Content and Tooltip

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
                ContentControl.Text = value;
                if (DateTimeHandler.TryParseDisplayDateTime(value, out DateTime dateTime))
                {
                    ContentControl.Value = dateTime;
                }
                ContentControl.ToolTip = value;
            }
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl(FlashEnum _)
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }
        public override void ShowPreviewControlValue(string value)
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }
        public override void HidePreviewControlValue()
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }

        public override void FlashPreviewControlValue()
        {
            // DateTime is never copyable or a candidate for quickpaste, so we do nothing
        }
        #endregion
    }
}
