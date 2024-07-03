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
    // IntegerBase: Base control for integer as input. Comprises:
    // - a label containing the descriptive label) 
    // - an IntegerControl containing the content 
    // Identical to an IntegerAny except that the ContentControl Minimum is set to 0
    public class MetadataDataEntryIntegerBase : MetadataDataEntryControl<IntegerUpDown, Label>
    {
        #region Public Properties

        public override UIElement GetContentControl => this.ContentControl;

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        /// <summary>Gets  the content of the data field</summary>
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


        #region Constructor
        public MetadataDataEntryIntegerBase(MetadataControlRow control, DataEntryControls styleProvider, string tooltip, bool allowPositiveNumbersOnly) :
            base(control, styleProvider, ControlContentStyleEnum.IntegerTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            this.ControlType = control.Type;
            this.ContentChanged = false;
            // This is the only real difference between an IntegerAny and an IntegerPositive
            if (allowPositiveNumbersOnly)
            {
                this.ContentControl.Minimum = 0;
            }
            this.ContentControl.FormatString = Constant.ControlDefault.IntegerFormatString;
            this.ContentControl.Watermark = allowPositiveNumbersOnly ? "number\u22650 or blank" : "number or blank";
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