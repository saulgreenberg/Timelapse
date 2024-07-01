using System;
using System.Windows.Controls;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Timelapse.ControlsDataCommon;

namespace Timelapse.ControlsMetadata
{
    // Date_ lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable date control (containing the content) at the given width
    public class MetadataDataEntryDate : MetadataDataEntryControl<DateTimePicker, Label>
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

        #region Constructor
        public MetadataDataEntryDate(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            this.Tooltip = tooltip;
            this.ControlType = control.Type;
            this.ContentChanged = false;
            this.ContentControl.GotKeyboardFocus += ControlsDataHelpersCommon.Control_GotFocus;
            this.ContentControl.LostKeyboardFocus += ControlsDataHelpersCommon.Control_LostFocus;
        }
        #endregion

        #region Setting Content and Tooltip
        public void SetContentAndTooltip(DateTime? value)
        {
            // Set the note to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            this.ContentChanged = this.ContentControl.Value != value;

            string displayText = null == value
                ? "Blank entry"
                : DateTimeHandler.ToStringDisplayDatePortion((DateTime)value);
            this.ContentControl.Text = displayText;
            this.ContentControl.ToolTip = displayText;
        }

        // Need to implement this as a stub as its defined in the base class as an abstract
        public override void SetContentAndTooltip(string ignore)
        {
        }
        #endregion
    }
}

