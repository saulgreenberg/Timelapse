using System.Windows.Controls;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;
using System.Windows.Input;
using Timelapse.ControlsDataCommon;


namespace Timelapse.ControlsMetadata
{
    // DecimalPositive: Any npositive real number as input. Comprises:
    // - a label containing the descriptive label) 
    // - a DoubleUpDownControl containing the content 
    // Identical to DecimalAny except it sets a minimum value
    public class MetadataDataEntryDecimalBase : MetadataDataEntryControl<DoubleUpDown, Label>
    {
        #region Public Properties

        public override UIElement GetContentControl => this.ContentControl;

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => this.ContentControl.Text;

        public bool ContentChanged { get; set; }

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
        #endregion

        #region Private variables

        private readonly bool AllowPositiveNumbersOnly;
        #endregion

        #region Constructor
        public MetadataDataEntryDecimalBase(MetadataControlRow control, DataEntryControls styleProvider, string tooltip, bool allowPositiveNumbersOnly) :
            base(control, styleProvider, ControlContentStyleEnum.DoubleTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            this.AllowPositiveNumbersOnly = allowPositiveNumbersOnly;

            // Now configure the various elements
            this.Tooltip = tooltip;
            this.ControlType = control.Type;
            this.ContentChanged = false;
            // This is the only real difference between an DecimalAny and an DecimalPositive
            if (this.AllowPositiveNumbersOnly)
            {
                this.ContentControl.Minimum = 0;
            }

            this.ContentControl.FormatString = Timelapse.Constant.ControlDefault.DecimalFormatString;
            this.ContentControl.Watermark = this.AllowPositiveNumbersOnly ? "decimal\u22650 or blank" : "decimal or blank";
            this.ContentControl.GotKeyboardFocus += ControlsDataHelpersCommon.Control_GotFocus;
            this.ContentControl.LostKeyboardFocus += ControlsDataHelpersCommon.Control_LostFocus;
            this.ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
        }
        #endregion

        #region Event Handlers
        // Spaces should either be prohibited or have special meaning as described below
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                TextBox contentHost = (TextBox)this.ContentControl.Template.FindName("PART_TextBox", this.ContentControl);
                ControlsDataHelpersCommon.TextBoxHandleKeyDownForSpace(contentHost, e, true);
            }
        }

        // Because its a KeyDown vs a PreviewKeyDown, the editing characters have already been processed e.g.
        // Tab, Delete, etc
        private void ContentControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.D0:
                case Key.NumPad0:
                case Key.D1:
                case Key.NumPad1:
                case Key.D2:
                case Key.NumPad2:
                case Key.D3:
                case Key.NumPad3:
                case Key.D4:
                case Key.NumPad4:
                case Key.D5:
                case Key.NumPad5:
                case Key.D6:
                case Key.NumPad6:
                case Key.D7:
                case Key.NumPad7:
                case Key.D8:
                case Key.NumPad8:
                case Key.D9:
                case Key.NumPad9:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.NumLock:
                case Key.OemPeriod:
                case Key.Decimal:
                    // case Key.OemPlus: // We disallow '+'  as it serves no purpose
                    e.Handled = false;
                    break;

                case Key.OemMinus:

                    if (this.AllowPositiveNumbersOnly)
                    {
                        // Disallow '-' if this control was configured to allow only positive integers
                        FlashContentControl();
                        e.Handled = true;
                    }
                    break;
                default:
                    FlashContentControl();
                    e.Handled = true;
                    break;
            }
        }

        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // Set the number to the value provided, or to empty (which makes this somewhat messy))

            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed.
            this.ContentChanged = this.ContentControl.Text != value;

            // It the user has cleared the control while the value is zero, then the user is trying to set it to an empty value
            // Makeing the control's value null will clear it i.e., to empty. Otherwise just set it to the entered value.
            if (null != this.ContentControl.Text && string.IsNullOrWhiteSpace(this.ContentControl.Text) && value == "0")
            {
                this.ContentControl.Value = null;
            }
            else
            {
                this.ContentControl.Text = value;
            }
            // The tooltip either shows the value, or 'Blank entry' if there is nothing in it.
            this.ContentControl.ToolTip = string.IsNullOrEmpty(this.ContentControl.Text) ? "Blank entry" : value;
        }
        #endregion
    }
}