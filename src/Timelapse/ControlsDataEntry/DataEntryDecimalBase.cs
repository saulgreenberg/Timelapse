using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace Timelapse.ControlsDataEntry
{
    // A baseclass for decimals comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable decimal updown box (containing the content) at the given width
    public class DataEntryDecimalBase : DataEntryControl<DoubleUpDown, Label>
    {
        private readonly DecimalControlCore core;

        #region Public Properties and Private variables
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the content of the counter.</summary>
        public override string Content => core.GetContent();

        public bool ContentChanged
        {
            get => core.ContentChanged;
            set => core.ContentChanged = value;
        }

        private WatermarkTextBox WatermarkTextBox
        {
            get { return field ??= (WatermarkTextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl); }
        }

        private readonly bool AllowPositiveNumbersOnly;
        #endregion

        #region Constructor
        public DataEntryDecimalBase(ControlRow control, DataEntryControls styleProvider, bool allowPositiveNumbersOnly) :
            base(control, styleProvider, ControlContentStyleEnum.DoubleTextBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new DecimalControlCore(ContentControl);

            AllowPositiveNumbersOnly = allowPositiveNumbersOnly;
            ContentChanged = false;

            // Configure behavior based on whether we allow positive numbers only
            if (AllowPositiveNumbersOnly)
            {
                ContentControl.Minimum = 0;
                ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfDecimalPositive);
            }
            else
            {
                ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfDecimalAny);
            }

            // Configure the various elements if needed
            // Assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            ContentControl.Width += 18; // to account for the width of the spinner
            ContentControl.FormatString = ControlDefault.DecimalFormatString;
            ContentControl.CultureInfo = CultureInfo.InvariantCulture;
            ContentControl.UpdateValueOnEnterKey = true;
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
        }
        #endregion

        #region Event Handlers
        protected override bool HandleKeyboardNavigationInBase()
        {
            return false; // We handle our own keyboard navigation
        }

        // Handle readonly states, enter, return, shift-arrow/home keys entered into the textbox
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            TextBox textBox = ContentControl.Template.FindName("PART_TextBox", ContentControl) as TextBox;
            if (textBox != null)
            {
                // If we are in viewonly state, this ensures that the number textbox can't be edited.
                textBox.IsReadOnly = GlobalReferences.TimelapseState.IsViewOnly;
            }

            // I'm not sure if this is actually needed, as Enter/Return seems to update correctly. But it doesn't hurt...
            // We need to handle Enter/Return key presses here, as otherwise wrong values are displayed in the text box when we hit enter
            if (IsCondition.IsKeyReturnOrEnter(keyEvent.Key))
            {
                UpdateValueIfNeeded(keyEvent);
                return;
            }

            if (keyEvent.Key == Key.Space)
            {
                //WatermarkTextBox wTextBox = (WatermarkTextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl);
                if (false == string.IsNullOrEmpty((string)WatermarkTextBox.Watermark))
                {
                    SetContentAndTooltip(string.Empty, true);
                }
                else
                {
                    ControlsDataHelpers.TextBoxHandleKeyDownForSpace(textBox, keyEvent, true);
                }
                return;
            }

            // Possible shortcut keys (delegated to main window):
            // - any Control key press could indicate a Shortcut key, and
            // - a few very specific keys that don't require a Control key press
            if (IsCondition.IsKeyControlDown() || 
                IsCondition.IsKeyPageUpDown(keyEvent.Key))
            {
                // Edited values aren't guaranteed to be updated, so we force an update here 
                // We also check for success just in case the value could not be updated, where
                // we would remain on the same image
                bool success = UpdateValueIfNeeded(keyEvent);
                keyEvent.Handled = true;
                if (success)
                {
                    GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
                }
            }
        }
        #endregion

        #region Focus
        // Behaviour: Highlight the border and make the text caret appear whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new(Control.BorderThicknessHighlight);
            ContentControl.BorderBrush = Control.BorderColorHighlight;
            if (ContentControl.Template.FindName("PART_TextBox", ContentControl) is WatermarkTextBox textBox)
            {
                textBox.IsReadOnlyCaretVisible = true;
            }
        }

        // Behaviour: Revert the border whenever the control loses the keyboard focus
        private void ContentControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new(Control.BorderThicknessNormal);
            ContentControl.BorderBrush = Control.BorderColorNormal;

            // This is a hack to ensure the ellipsis appears as needed. 
            WatermarkTextBox textBox = (WatermarkTextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl);
            if (textBox != null)
            {
                if ((string)textBox.Watermark == Unicode.Ellipsis)
                {
                    textBox.Text = string.Empty;
                }
            }
        }
        #endregion

        #region Setting Content and Tooltip
        // Changing a counter value may not always trigger a ValueChanged event when using the overview.
        // For example, consider the case where two images are selected with the values '5',  '7'.
        // We see its watermark ... 
        // If we change the value to '5' on the control (which actually thinks its a watermark on the 1st image)
        // because its already '5', no ValueChanged event is triggered. So the 2nd image is not updated to '7'..
        // To get around this, we set a bogus value and then the real value, which means that the
        // ValueChanged event will be triggered. Inefficient, but works.

        public void SetContentAndTooltip(string value, bool forceUpdate)
        {
            SetContentAndTooltip(int.MaxValue.ToString());
            SetContentAndTooltip(value);
        }

        // If value is null, then show and ellipsis. If its a number, show that. Otherwise blank.
        public override void SetContentAndTooltip(string value)
        {
            // The control supplied with the WPFToolkit only allows numbers
            // It also can do watermarks, but only indirectly via its textbox portion
            // As we want it to also show both ellipsis and blanks, we have to coerce it to show those.

            // We access the textbox portion of the DoubleUpDown, so we can set its watermark and write directly into it if needed.
           // WatermarkTextBox textBox = (WatermarkTextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl);
            if (value == null)
            {
                // When we get a null value, set the watermark (an ellipsis symbol) in the textbox,
                // and the text to empty, which then displays the watermark
                ContentControl.AllowSpin = false;
                if (WatermarkTextBox != null)
                {
                    WatermarkTextBox.Watermark = !string.IsNullOrEmpty(WatermarkTextBox.Text) ? Unicode.Ellipsis : string.Empty;
                    WatermarkTextBox.Text = string.Empty;
                }
            }
            else
            {
                // We have a valid value, so reset the control and watermark
                ContentControl.AllowSpin = true;
                if (WatermarkTextBox != null)
                {
                    WatermarkTextBox.Watermark = string.Empty;
                }
                value = value.Trim();
                
                // The value is non-null, so its either a number or blank.
                // If its a number, just set it to that number
                if (Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
                {
                    if (WatermarkTextBox != null)
                    {
                        WatermarkTextBox.Text = doubleValue.ToString(CultureInfo.InvariantCulture);
                    }
                    ContentControl.Value = doubleValue;
                }
                else
                {
                    // If its not a number, blank out the text
                    ContentControl.Text = string.Empty;
                    if (WatermarkTextBox != null)
                    {
                        WatermarkTextBox.Text = value;
                    }
                }
            }
            ContentControl.ToolTip = value ?? "Edit to change the " + Label + " for all selected images";
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
                double doubleUpDownWidth = 16;
                double width = ContentControl.Width - doubleUpDownWidth;
                double horizontalOffset = -doubleUpDownWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new(0, 5.5, 5, 0);

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

        #region UpdateValueIfNeeded Helper for updating the text field
        // We need to force an update on the control when the user moves to another image instead of tabbing/enter etc,
        // And when Enter is pressed (as enters are disallowed later)
        private bool UpdateValueIfNeeded(KeyEventArgs args)
        {
            // Empty text should update the value to a null value
            if (string.IsNullOrEmpty(ContentControl.Text))
            {
                if (null != ContentControl.Value)
                {
                    ContentControl.Value = null;
                }
                return true;
            }

            if (false == Double.TryParse(ContentControl.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newValueAsDouble))
            {
                // We should never get this, as checks for the input will have already been done, including empty input as handled above
                Dialogs.InvalidDataFieldInput(GlobalReferences.MainWindow, AllowPositiveNumbersOnly ? Control.DecimalPositive : Control.DecimalAny, Content);
                if (args != null)
                {
                    args.Handled = true;
                }
                return false;
            }
            if (null == ContentControl.Value)
            {
                // replace the null control value with the new number value
                ContentControl.Value = newValueAsDouble;
                return true;
            }
            if (Math.Abs(newValueAsDouble - (Double)ContentControl.Value) > 0.00000001)
            {
                // The number has changed so update it
                ContentControl.Value = newValueAsDouble;
            }
            return true;
        }
        #endregion
    }
}
