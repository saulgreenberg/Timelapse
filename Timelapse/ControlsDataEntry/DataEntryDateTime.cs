using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsDataCommon;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDateTime : DataEntryControl<DateTimePicker, Label>
    {
        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new Point(0, 0));

        public override UIElement GetContentControl =>
            // We return the textbox part of the content control, as otherwise focus does not work properly when we try to set it on the content control
            (UIElement)ContentControl.Template.FindName("PART_TextBox", ContentControl);

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        public override string Content => ContentControl.Text;

        public override bool ContentReadOnly
        {
            get => ContentControl.IsReadOnly;
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    ContentControl.IsReadOnly = true;
                    ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    ContentControl.IsReadOnly = value;
                }
            }
        }

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
        }
        #endregion

        #region Event Handlers

        // Highlight the border whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new Thickness(Control.BorderThicknessHighlight);
            ContentControl.BorderBrush = Control.BorderColorHighlight;
        }

        private void ContentControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new Thickness(Control.BorderThicknessNormal);
            ContentControl.BorderBrush = Control.BorderColorNormal;
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            if (ContentControl.Template.FindName("PART_TextBox", ContentControl) is WatermarkTextBox textBox)
            {
                textBox.Text = value ?? Unicode.Ellipsis;
            }
            else
            {
                ContentControl.Text = value;
            }

            // For date time controls, we also need to set the actual Value, as updating the text field by itself doesn't do that.
            if (DateTimeHandler.TryParseDisplayDateTime(value, out DateTime dateTime))
            {
                // This hack is done so that the content control updates if the value hasn't changed.
                // So we set it twice: the first time with a different value to guarantee that it has changed, and the second time with the
                // desired value ot actually display it.
                ContentControl.Value = dateTime + TimeSpan.FromHours(1);
                ContentControl.Value = dateTime;
            }
            ContentControl.ToolTip = value ?? "Edit to change the " + Label + " for the selected image";
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
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
