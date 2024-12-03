using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsDataCommon;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace Timelapse.ControlsDataEntry
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<IntegerUpDown, RadioButton>
    {
        #region Public Properties and Private variables
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new Point(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the content of the counter.</summary>
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

        public bool IsSelected => LabelControl.IsChecked.HasValue && (bool)LabelControl.IsChecked;

        // Holds the DataLabel of the previously clicked counter control across all counters
        private static string previousControlDataLabel = string.Empty;
        #endregion

        #region Constructor
        public DataEntryCounter(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.CounterTextBox, ControlLabelStyleEnum.CounterButton)
        {
            // ConfigureFormatForDateTimeCustom the various elements if needed
            // Assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            LabelControl.GroupName = "DataEntryCounter";
            LabelControl.Click += LabelControl_Click;
            ContentControl.Width += 18; // to account for the width of the spinner
            ContentControl.UpdateValueOnEnterKey = true;
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            ContentControl.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
            ContentControl.GotKeyboardFocus += ContentControl_GotKeyboardFocus;
            ContentControl.LostKeyboardFocus += ContentControl_LostKeyboardFocus;
            DataObject.AddPastingHandler(ContentControl, ValidationCallbacks.Paste_OnlyIfCounter);
        }
        #endregion

        #region Event Handlers
        // Behaviour: enable the counter textbox for editing
        // SAULXX The textbox in the IntegerUpDown is, for some unknown reason, disabled and thus disallows text input.
        // This hack seems to fix it. 
        //  A better solution is to find out where it is being disabled and fix it there.
        // Behaviour: enable the integer textbox for editing
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            TextBox textBox = ContentControl.Template.FindName("PART_TextBox", ContentControl) as TextBox;
            if (textBox != null)
            {
                // If we are in viewonly state, this ensures that the number textbox can't be edited.
                textBox.IsReadOnly = GlobalReferences.TimelapseState.IsViewOnly;
            }

            // We need to handle Enter/Return key presses here, as otherwise wrong values are displayed in the text box when we hit enter
            if (keyEvent.Key == Key.Enter || keyEvent.Key == Key.Return)
            {
                UpdateValueIfNeeded(keyEvent);
                return;
            }

            if (keyEvent.Key == Key.Space)
            {
                ControlsDataHelpersCommon.TextBoxHandleKeyDownForSpace(textBox, keyEvent, true);
                return;
            }

            if (IsCondition.IsShiftKeyDown() && (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown))
            {
                // the right/left arrow keys normally moves the text cursor.
                // However, we want to retain the arrow keys - as well as the PageUp/Down keys - for cycling through the image.
                // So we mark the event as handled, and we cycle through the images anyways.
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                bool success = UpdateValueIfNeeded(keyEvent);
                UpdateValueIfNeeded(keyEvent);
                keyEvent.Handled = true;
                if (success)
                {
                    GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
                }
            }
        }
        
        // Behaviour: If the currently clicked counter is deselected, it will be selected and all other counters will be deselected,
        // If the currently clicked counter is selected, it will be deselected along with all other counters will be deselected,
        private void LabelControl_Click(object sender, RoutedEventArgs e)
        {
            if (previousControlDataLabel == null)
            {
                LabelControl.IsChecked = true;
                previousControlDataLabel = DataLabel;
            }
            else if (previousControlDataLabel == DataLabel)
            {
                LabelControl.IsChecked = false;
                previousControlDataLabel = string.Empty;
            }
            else
            {
                LabelControl.IsChecked = true;
                previousControlDataLabel = DataLabel;
            }
            // Also set the keyboard focus to this control 
            Keyboard.Focus(ContentControl);
        }
        #endregion

        #region Focus
        // Behaviour: Highlight the border and make the text caret appear whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new Thickness(Control.BorderThicknessHighlight);
            ContentControl.BorderBrush = Control.BorderColorHighlight;
            if (ContentControl.Template.FindName("PART_TextBox", ContentControl) is WatermarkTextBox textBox)
            {
                textBox.IsReadOnlyCaretVisible = true;
            }
        }

        // Behaviour: Revert the border whenever the control loses the keyboard focus
        private void ContentControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ContentControl.BorderThickness = new Thickness(Control.BorderThicknessNormal);
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
        // Changing a counter value does not trigger a ValueChanged event if the values are the same.
        // which means multiple images may not be updated even if other images have the same value.
        // To get around this, we set a bogus value and then the real value, which means that the
        // ValueChanged event will be triggered. Inefficient, but seems to work.
        public void SetBogusCounterContentAndTooltip()
        {
            SetContentAndTooltip(int.MaxValue.ToString());
        }

        // If value is null, then show and ellipsis. If its a number, show that. Otherwise blank.
        public override void SetContentAndTooltip(string value)
        {
            // CODECLEANUP this hack for counters, perhaps by updating to latest version of xceed.
            // This is a hack, but it works (sort of).
            // To explain, the IntegerUpDown control supplied with the WPFToolkit only allows numbers. 
            // As we want it to also show both ellipsis and blanks, we have to coerce it to show those.
            // Ideally, we should modify the IntegerUpDown control to allow ellipsis and blanks instead of these hacks.
            // Its further complicated by the the way we have to set the bogus counter... 

            // We access the textbox portion of the IntegerUpDown, so we can write directly into it if needed.
            WatermarkTextBox textBox = (WatermarkTextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl);
            // When we get a null value, just show the ellipsis symbol in the textbox. 
            if (value == null)
            {
                ContentControl.AllowSpin = false;
                if (textBox != null)
                {
                    textBox.Watermark = !string.IsNullOrEmpty(textBox.Text) ? Unicode.Ellipsis : string.Empty;
                    textBox.Text = string.Empty;
                }
            }
            else
            {
                // We have a valid value, so reset the control and watermark
                ContentControl.AllowSpin = true;
                if (textBox != null)
                {
                    textBox.Watermark = string.Empty;
                }

                value = value.Trim();
                // The value is non-null, so its either a number or blank.
                // If its a number, just set it to that number
                if (int.TryParse(value, out int intvalue))
                {
                    if (textBox != null)
                    {
                        textBox.Text = intvalue.ToString();
                    }
                    ContentControl.Value = intvalue;
                }
                else
                {
                    // If its not a number, blank out the text
                    ContentControl.Text = string.Empty;
                    if (textBox != null)
                    {
                        textBox.Text = value;
                    }
                }
            }
            ContentControl.ToolTip = value ?? "Edit to change the " + Label + " for all selected images";
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

            if (false == Int32.TryParse(ContentControl.Text, out int newValueAsInteger))
            {
                // Error if its not an integer
                Dialogs.InvalidDataFieldInput(GlobalReferences.MainWindow, Control.Counter, Content);
                if (args != null)
                {
                    args.Handled = true;
                }
                return false;
            }
            if (null == ContentControl.Value)
            {
                // replace the null control value with the new number value
                ContentControl.Value = newValueAsInteger;
                return true;
            }
            if (newValueAsInteger != ContentControl.Value)
            {
                // The number has changed so update it
                ContentControl.Value = newValueAsInteger;
            }
            return true;
        }
        #endregion
    }
}
