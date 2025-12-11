using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryTime : DataEntryControl<WatermarkTimePicker, Label>
    {
        private readonly TimePickerControlCore core;

        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl =>
            // We return the textbox part of the content control, as otherwise focus does not work properly when we try to set it on the content control
            (UIElement)ContentControl.Template.FindName("PART_TextBox", ContentControl);

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        public override string Content => core.GetContent();

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
        #endregion

        #region Constructor
        public DataEntryTime(ControlRow control, DataEntryControls styleProvider, DateTime defaultValue) :
            base(control, styleProvider, ControlContentStyleEnum.TimeBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new TimePickerControlCore(ContentControl);

            // configure the various elements
            CreateControls.Configure(ContentControl, defaultValue);
            ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
            ContentControl.Watermark = Unicode.Ellipsis;
            this.ContentControl.PreviewGotKeyboardFocus += ContentControl_PreviewGotKeyboardFocus;
            this.ContentControl.PreviewLostKeyboardFocus += ContentControl_PreviewLostKeyboardFocus;
        }

        private void ContentControl_PreviewGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (ContentControl.IsWatermarkVisible)
            {
                ContentControl.AllowSpin = false;
                return;
            }
            ContentControl.AllowSpin = true;
        }
        private void ContentControl_PreviewLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (false == this.ContentControl.IsWatermarkVisible)
            {
                ContentControl.AllowSpin = true;
            }
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
        // This works only for the full DateTime format i.e., it can be used by DateTimeCustom only
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
                if (DateTimeHandler.TryParseDatabaseTime(value, out DateTime dateTime))
                {
                    ContentControl.Value = dateTime;
                }
                ContentControl.ToolTip = value;
            }
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl(FlashEnum flashEnum)
        {
            if (ContentControl?.MainDisplayField is { } primaryDisplay)
            {
                primaryDisplay.Background = new SolidColorBrush(Colors.White);
                primaryDisplay.Background.BeginAnimation(SolidColorBrush.ColorProperty,
                    flashEnum == FlashEnum.UsePasteFlash
                        ? GetColorAnimationForPasting()
                        : GetColorAnimationWarning());
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (PopupPreview == null)
            {
                // We want to expose the up/down controls, so subtract its width and move the horizontal offset over
                double integerUpDownWidth = 16;
                double width = ContentControl.Width - integerUpDownWidth;
                double horizontalOffset = -integerUpDownWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new(7, 5.5, 0, 0);

                PopupPreview = CreatePopupPreview(ContentControl, padding, width, horizontalOffset);
            }
            // Show the popup
            ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            if (PopupPreview != null)
            {
                HidePopupPreview();
            }
        }

        public override void FlashPreviewControlValue()
        {
            FlashPopupPreview();
        }
        #endregion
    }

}
