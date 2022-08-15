using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimePicker, Label>
    {
        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft
        {
            get { return this.ContentControl.PointToScreen(new Point(0, 0)); }
        }
        public override UIElement GetContentControl
        {
            // We return the textbox part of the content control, as otherwise focus does not work properly when we try to set it on the content control
            get { return (UIElement)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl); }
        }

        public override bool IsContentControlEnabled
        {
            get { return this.ContentControl.IsEnabled; }
        }

        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set
            {
                if (Util.GlobalReferences.TimelapseState.IsViewOnly)
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

        public DataEntryNote DateControl { get; set; }
        public DataEntryNote TimeControl { get; set; }
        #endregion

        #region Constructor
        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // configure the various elements
            DataEntryHandler.Configure(this.ContentControl, null);
            this.ContentControl.GotKeyboardFocus += this.ContentControl_GotKeyboardFocus;
            this.ContentControl.LostKeyboardFocus += this.ContentControl_LostKeyboardFocus;
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
        public override void SetContentAndTooltip(string value)
        {
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.Text = value ?? Constant.Unicode.Ellipsis;
            }
            else
            {
                this.ContentControl.Text = value;
            }

            // For date time controls, we also need to set the actual Value, as updating the text field by itself doesn't do that.
            if (DateTimeHandler.TryParseDisplayDateTime(value, out DateTime dateTime))
            {
                // This hack is done so that the content control updates if the value hasn't changed.
                // So we set it twice: the first time with a different value to guarantee that it has changed, and the second time with the
                // desired value ot actually display it.
                this.ContentControl.Value = dateTime + TimeSpan.FromHours(1);
                this.ContentControl.Value = dateTime;
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for the selected image";
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
