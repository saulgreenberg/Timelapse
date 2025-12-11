using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // Date_ lays out a stack panel containing
    // - a label containing the descriptive label)
    // - an editable date control (containing the content) at the given width
    public class MetadataDataEntryDate : MetadataDataEntryControl<WatermarkDateTimePicker, Label>
    {
        private readonly DateTimePickerControlCore core;

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
        public MetadataDataEntryDate(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new DateTimePickerControlCore(ContentControl);

            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
            ContentControl.GotKeyboardFocus += ControlsDataHelpers.Control_GotFocus;
            ContentControl.LostKeyboardFocus += ControlsDataHelpers.Control_LostFocus;
        }
        #endregion

        #region Setting Content and Tooltip
        public void SetContentAndTooltip(DateTime? value)
        {
            // Set the note to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentChanged = ContentControl.Value != value;

            string displayText = null == value
                ? "Blank entry"
                : DateTimeHandler.ToStringDisplayDatePortion((DateTime)value);
            ContentControl.Text = displayText;
            ContentControl.ToolTip = displayText;
        }

        // Need to implement this as a stub as its defined in the base class as an abstract
        public override void SetContentAndTooltip(string ignore)
        {
        }
        #endregion
    }
}

