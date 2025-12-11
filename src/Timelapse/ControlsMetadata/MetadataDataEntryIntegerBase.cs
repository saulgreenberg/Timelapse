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
    // IntegerBase: Base control for integer as input. Comprises:
    // - a label containing the descriptive label)
    // - an IntegerControl containing the content
    // Identical to an IntegerAny except that the ContentControl Minimum is set to 0
    public class MetadataDataEntryIntegerBase : MetadataDataEntryControl<IntegerUpDown, Label>
    {
        private readonly IntegerControlCore core;

        #region Public Properties
        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets the content of the data field</summary>
        public override string Content => core.GetContent();

        public bool ContentChanged
        {
            get => core.ContentChanged;
            set => core.ContentChanged = value;
        }
        #endregion

        #region Constructor
        public MetadataDataEntryIntegerBase(MetadataControlRow control, DataEntryControls styleProvider, string tooltip, bool allowPositiveNumbersOnly) :
            base(control, styleProvider, ControlContentStyleEnum.IntegerTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new IntegerControlCore(ContentControl);

            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;

            // Configure behavior based on whether we allow positive numbers only
            if (allowPositiveNumbersOnly)
            {
                ContentControl.Minimum = 0;
                ContentControl.PreviewTextInput += Util.ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                DataObject.AddPastingHandler(ContentControl, Util.ValidationCallbacks.Paste_OnlyIfIntegerPositive);
            }
            else
            {
                ContentControl.PreviewTextInput += Util.ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
                DataObject.AddPastingHandler(ContentControl, Util.ValidationCallbacks.Paste_OnlyIfIntegerAny);
            }

            ContentControl.FormatString = ControlDefault.IntegerFormatString;
            ContentControl.Watermark = allowPositiveNumbersOnly ? "number\u22650 or blank" : "number or blank";
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