using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // DecimalPositive: Any npositive real number as input. Comprises:
    // - a label containing the descriptive label)
    // - a DoubleUpDownControl containing the content
    // Identical to DecimalAny except it sets a minimum value
    public class MetadataDataEntryDecimalBase : MetadataDataEntryControl<DoubleUpDown, Label>
    {
        private readonly DecimalControlCore core;

        #region Public Properties
        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets the content of the note</summary>
        public override string Content => core.GetContent();

        public bool ContentChanged
        {
            get => core.ContentChanged;
            set => core.ContentChanged = value;
        }
        #endregion

        #region Constructor
        public MetadataDataEntryDecimalBase(MetadataControlRow control, DataEntryControls styleProvider, string tooltip, bool allowPositiveNumbersOnly) :
            base(control, styleProvider, ControlContentStyleEnum.DoubleTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new DecimalControlCore(ContentControl);

            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;

            // Configure behavior based on whether we allow positive numbers only
            if (allowPositiveNumbersOnly)
            {
                ContentControl.Minimum = 0;
                ContentControl.PreviewTextInput += Util.ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                DataObject.AddPastingHandler(ContentControl, Util.ValidationCallbacks.Paste_OnlyIfDecimalPositive);
            }
            else
            {
                ContentControl.PreviewTextInput += Util.ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                DataObject.AddPastingHandler(ContentControl, Util.ValidationCallbacks.Paste_OnlyIfDecimalAny);
            }

            ContentControl.FormatString = ControlDefault.DecimalFormatString;
            ContentControl.CultureInfo = CultureInfo.InvariantCulture;
            ContentControl.Watermark = allowPositiveNumbersOnly ? "decimal\u22650 or blank" : "decimal or blank";
            ContentControl.GotKeyboardFocus += ControlsDataHelpers.Control_GotFocus;
            ContentControl.LostKeyboardFocus += ControlsDataHelpers.Control_LostFocus;
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
                ControlsDataHelpers.TextBoxHandleKeyDownForSpace(contentHost, e, true);
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