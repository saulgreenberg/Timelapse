using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsDataCommon;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // DecimalPositive: Any npositive real number as input. Comprises:
    // - a label containing the descriptive label) 
    // - a DoubleUpDownControl containing the content 
    // Identical to DecimalAny except it sets a minimum value
    public class MetadataDataEntryDecimalBase : MetadataDataEntryControl<DoubleUpDown, Label>
    {
        #region Public Properties

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => ContentControl.Text;

        public bool ContentChanged { get; set; }

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
        public MetadataDataEntryDecimalBase(MetadataControlRow control, DataEntryControls styleProvider, string tooltip, bool allowPositiveNumbersOnly) :
            base(control, styleProvider, ControlContentStyleEnum.DoubleTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
            // This is the only real difference between an DecimalAny and an DecimalPositive
            if (allowPositiveNumbersOnly)
            {
                ContentControl.Minimum = 0;
            }

            ContentControl.FormatString = ControlDefault.DecimalFormatString;
            ContentControl.Watermark = allowPositiveNumbersOnly ? "decimal\u22650 or blank" : "decimal or blank";
            ContentControl.GotKeyboardFocus += ControlsDataHelpersCommon.Control_GotFocus;
            ContentControl.LostKeyboardFocus += ControlsDataHelpersCommon.Control_LostFocus;
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
        }
        #endregion

        #region Event Handlers
        // Spaces should either be prohibited or have special meaning as described below
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                TextBox contentHost = (TextBox)ContentControl.Template.FindName("PART_TextBox", ContentControl);
                ControlsDataHelpersCommon.TextBoxHandleKeyDownForSpace(contentHost, e, true);
            }
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // Set the number to the value provided, or to empty (which makes this somewhat messy))

            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed.
            ContentChanged = ContentControl.Text != value;

            // It the user has cleared the control while the value is zero, then the user is trying to set it to an empty value
            // Makeing the control's value null will clear it i.e., to empty. Otherwise just set it to the entered value.
            if (null != ContentControl.Text && string.IsNullOrWhiteSpace(ContentControl.Text) && value == "0")
            {
                ContentControl.Value = null;
            }
            else
            {
                ContentControl.Text = value;
            }
            // The tooltip either shows the value, or 'Blank entry' if there is nothing in it.
            ContentControl.ToolTip = string.IsNullOrEmpty(ContentControl.Text) ? "Blank entry" : value;
        }
        #endregion
    }
}