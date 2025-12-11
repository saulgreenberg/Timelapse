using System.Windows;
using System.Windows.Controls;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // Time_ lays out a stack panel containing
    // - a label containing the descriptive label)
    // - an editable time control (containing the content) at the given width
    public class MetadataDataEntryTime : MetadataDataEntryControl<WatermarkTimePicker, Label>
    {
        private readonly TimePickerControlCore core;

        #region Public Properties

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => core.GetContent();

        public bool ContentChanged
        {
            get => core.ContentChanged;
            set => core.ContentChanged = value;
        }
        #endregion

        #region Constructor
        public MetadataDataEntryTime(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.TimeBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new TimePickerControlCore(ContentControl);

            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
            ContentControl.GotKeyboardFocus += ControlsDataHelpers.Control_GotFocus;
            ContentControl.LostKeyboardFocus += ControlsDataHelpers.Control_LostFocus;
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            if (value == null)
            {
                return;
            }

            // Set the note to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentChanged = ContentControl.Text != value;
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}
