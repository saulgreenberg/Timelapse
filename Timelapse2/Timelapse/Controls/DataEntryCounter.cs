using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<IntegerUpDown, RadioButton>
    {
        #region Public Properties and Private variables
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft
        {
            get { return this.ContentControl.PointToScreen(new Point(0, 0)); }
        }

        public override UIElement GetContentControl
        {
            get { return this.ContentControl; }
        }

        public override bool IsContentControlEnabled
        {
            get { return this.ContentControl.IsEnabled; }
        }

        /// <summary>Gets or sets the content of the counter.</summary>
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

        public bool IsSelected
        {
            get { return this.LabelControl.IsChecked.HasValue && (bool)this.LabelControl.IsChecked; }
        }

        // Holds the DataLabel of the previously clicked counter control across all counters
        private static string previousControlDataLabel = String.Empty;
        #endregion

        #region Constructor
        public DataEntryCounter(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.CounterTextBox, ControlLabelStyleEnum.CounterButton)
        {
            // Configure the various elements if needed
            // Assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            this.LabelControl.GroupName = "DataEntryCounter";
            this.LabelControl.Click += this.LabelControl_Click;
            this.ContentControl.Width += 18; // to account for the width of the spinner
            this.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
            this.ContentControl.PreviewTextInput += this.ContentControl_PreviewTextInput;
            this.ContentControl.GotKeyboardFocus += this.ContentControl_GotKeyboardFocus;
            this.ContentControl.LostKeyboardFocus += this.ContentControl_LostKeyboardFocus;
        }
        #endregion

        #region Event Handlers
        // Behaviour: enable the counter textbox for editing
        // SAULXX The textbox in the IntegerUpDown is, for some unknown reason, disabled and thus disallows text input.
        // This hack seems to fix it. 
        //  A better solution is to find out where it is being disabled and fix it there.
        private void ContentControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs keyEvent)
        {
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                // If we are in viewonly state, this ensures that the number textbox can't be edited.
                textBox.IsReadOnly = Util.GlobalReferences.TimelapseState.IsViewOnly;
            }
        }

        // Behaviour: Ignore any non-numeric input (but backspace delete etc work just fine)
        private void ContentControl_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (int.TryParse(e.Text, out _) == false)
            {
                e.Handled = true;
            }
        }

        // Behaviour: If the currently clicked counter is deselected, it will be selected and all other counters will be deselected,
        // If the currently clicked counter is selected, it will be deselected along with all other counters will be deselected,
        private void LabelControl_Click(object sender, RoutedEventArgs e)
        {
            if (previousControlDataLabel == null)
            {
                this.LabelControl.IsChecked = true;
                previousControlDataLabel = this.DataLabel;
            }
            else if (previousControlDataLabel == this.DataLabel)
            {
                this.LabelControl.IsChecked = false;
                previousControlDataLabel = String.Empty;
            }
            else
            {
                this.LabelControl.IsChecked = true;
                previousControlDataLabel = this.DataLabel;
            }
            // Also set the keyboard focus to this control 
            Keyboard.Focus(this.ContentControl);
        }

        // Behaviour: Highlight the border and make the text caret appear whenever the control gets the keyboard focus
        private void ContentControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorHighlight;
            if (this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl) is Xceed.Wpf.Toolkit.WatermarkTextBox textBox)
            {
                textBox.IsReadOnlyCaretVisible = true;
            }
        }

        // Behaviour: Revert the border whenever the control loses the keyboard focus
        private void ContentControl_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.ContentControl.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
            this.ContentControl.BorderBrush = Constant.Control.BorderColorNormal;

            // This is a hack to ensure the ellipsis appears as needed. 
            WatermarkTextBox textBox = (WatermarkTextBox)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);
            if (textBox != null)
            {
                if ((string)textBox.Watermark == Constant.Unicode.Ellipsis)
                {
                    textBox.Text = String.Empty;
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
            this.SetContentAndTooltip(int.MaxValue.ToString());
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
            WatermarkTextBox textBox = (WatermarkTextBox)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);
            // When we get a null value, just show the ellipsis symbol in the textbox. 
            if (value == null)
            {
                this.ContentControl.AllowSpin = false;
                if (textBox != null)
                {
                    textBox.Watermark = !string.IsNullOrEmpty(textBox.Text) ? Constant.Unicode.Ellipsis : String.Empty;
                    textBox.Text = String.Empty;
                }
            }
            else
            {
                // We have a valid value, so reset the control and watermark
                this.ContentControl.AllowSpin = true;
                if (textBox != null)
                {
                    textBox.Watermark = String.Empty;
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
                    this.ContentControl.Value = intvalue;
                }
                else
                {
                    // If its not a number, blank out the text
                    this.ContentControl.Text = String.Empty;
                    if (textBox != null)
                    {
                        textBox.Text = value;
                    }
                }
            }
            this.ContentControl.ToolTip = value ?? "Edit to change the " + this.Label + " for all selected images";
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
