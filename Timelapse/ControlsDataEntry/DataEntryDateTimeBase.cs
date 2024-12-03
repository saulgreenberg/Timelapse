using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    public class DataEntryDateTimeBase : DataEntryControl<DateTimePicker, Label>
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

        private TextBox part_TextBox;
        private TextBox Part_textbox
        {
            get
            {
                // For efficiency, we do this so we only need to get the TextBox once
                if (part_TextBox == null)
                {
                    if (ContentControl.Template.FindName("PART_TextBox", ContentControl) is WatermarkTextBox textBox)
                    {
                        part_TextBox = textBox;
                    }
                }
                return part_TextBox;
            }
        }
        #endregion

        #region Constructor
        public DataEntryDateTimeBase(ControlRow control, DataEntryControls styleProvider, DateTimeFormatEnum dateTimeFormat, DateTime defaultValue) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // configure the various elements
            CreateControls.Configure(ContentControl, dateTimeFormat, defaultValue);
            ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
            ContentControl.PreviewGotKeyboardFocus += ContentControl_PreviewGotKeyboardFocus;
            ContentControl.PreviewGotKeyboardFocus += ContentControl_PreviewLostKeyboardFocus;
        }


        private void ContentControl_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is WatermarkTextBox textBox && textBox.Text == Constant.Unicode.Ellipsis)
            if (Part_textbox?.Text == Unicode.Ellipsis)
            {
                ContentControl.AllowSpin = false;
                e.Handled = true;
                //this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
                //this.ContentControl.BorderBrush = Constant.Control.BorderColorHighlight;
                //Debug.Print("GotKBFocus");
            }
        }
        private void ContentControl_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is WatermarkTextBox textBox && textBox.Text == Constant.Unicode.Ellipsis)
            if (Part_textbox?.Text != Unicode.Ellipsis)
            {
                ContentControl.AllowSpin = true;
                //this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
                //this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;
                //Debug.Print("LostKBFocus");
            }
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
        // This works only for the full DateTime format i.e., it can be used by DateTimeCustom only
        public override void SetContentAndTooltip(string value)
        {
            if (ContentControl.Template.FindName("PART_TextBox", ContentControl) is WatermarkTextBox textBox)
            {
                if (value == null)
                {
                    textBox.Text = Unicode.Ellipsis;
                }
                else
                {
                    textBox.Text = DateTimeHandler.TryParseMetadataDateTaken(value, out DateTime dateTime1)
                        ? DateTimeHandler.ToStringDisplayDateTime(dateTime1)
                        : value;
                    ContentControl.Text = textBox.Text;
                }
            }
            else
            {
                ContentControl.Text = value;
            }
            ContentControl.ToolTip = value ?? "Edit to change the " + Label + " for the selected image";
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            TextBox contentHost = (TextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, GetColorAnimation());
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
                Thickness padding = new Thickness(7, 5.5, 0, 0);

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
