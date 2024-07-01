using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.ControlsDataCommon;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryDateTimeBase : DataEntryControl<DateTimePicker, Label>
    {
        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => this.ContentControl.PointToScreen(new Point(0, 0));

        public override UIElement GetContentControl =>
            // We return the textbox part of the content control, as otherwise focus does not work properly when we try to set it on the content control
            (UIElement)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        public override string Content => this.ContentControl.Text;

        public override bool ContentReadOnly
        {
            get => this.ContentControl.IsReadOnly;
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    this.ContentControl.IsReadOnly = true;
                    this.ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    this.ContentControl.IsReadOnly = value;
                }
            }
        }

        private readonly DateTimeFormatEnum dateTimeFormat;

        private TextBox part_TextBox;
        private TextBox Part_textbox
        {
            get
            {
                // For efficiency, we do this so we only need to get the TextBox once
                if (part_TextBox == null)
                {
                    if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is WatermarkTextBox textBox)
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
            this.dateTimeFormat = dateTimeFormat;
            // configure the various elements
            CreateControls.Configure(this.ContentControl, dateTimeFormat, defaultValue);
            this.ContentControl.GotKeyboardFocus += this.ContentControl_GotKeyboardFocus;
            this.ContentControl.LostKeyboardFocus += this.ContentControl_LostKeyboardFocus;
            this.ContentControl.PreviewGotKeyboardFocus += ContentControl_PreviewGotKeyboardFocus;
            this.ContentControl.PreviewGotKeyboardFocus += ContentControl_PreviewLostKeyboardFocus;
        }


        private void ContentControl_PreviewGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            // if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is WatermarkTextBox textBox && textBox.Text == Constant.Unicode.Ellipsis)
            if (this.Part_textbox?.Text == Constant.Unicode.Ellipsis)
            {
                this.ContentControl.AllowSpin = false;
                e.Handled = true;
                //this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
                //this.ContentControl.BorderBrush = Constant.Control.BorderColorHighlight;
                //Debug.Print("GotKBFocus");
            }
        }
        private void ContentControl_PreviewLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            // if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is WatermarkTextBox textBox && textBox.Text == Constant.Unicode.Ellipsis)
            if (this.Part_textbox?.Text != Constant.Unicode.Ellipsis)
            {
                this.ContentControl.AllowSpin = true;
                //this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
                //this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;
                //Debug.Print("LostKBFocus");
            }
        }
        #endregion

        #region Event Handlers

        // Highlight the border whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorHighlight;

        }

        private void ContentControl_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;

        }
        #endregion

        #region Setting Content and Tooltip
        // This works only for the full DateTime format i.e., it can be used by DateTimeCustom only
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
                    textBox.Text = DateTimeHandler.TryParseMetadataDateTaken(value, out DateTime dateTime1)
                        ? DateTimeHandler.ToStringDisplayDateTime(dateTime1)
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

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            TextBox contentHost = (TextBox)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // We want to expose the up/down controls, so subtract its width and move the horizontal offset over
                double integerUpDownWidth = 16;
                double width = this.ContentControl.Width - integerUpDownWidth;
                double horizontalOffset = -integerUpDownWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(7, 5.5, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, width, horizontalOffset);
            }
            // Show the popup
            this.ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            if (this.PopupPreview != null)
            {
                this.HidePopupPreview();
            }
        }

        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }
        #endregion
    }
}
