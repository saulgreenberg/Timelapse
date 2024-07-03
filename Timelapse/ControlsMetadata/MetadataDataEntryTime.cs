using System.Windows.Controls;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;
using Timelapse.ControlsDataCommon;

namespace Timelapse.ControlsMetadata
{
    // Time_ lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable time control (containing the content) at the given width
    public class MetadataDataEntryTime : MetadataDataEntryControl<TimePicker, Label>
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
        public MetadataDataEntryTime(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.TimeBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            this.ControlType = control.Type;
            this.ContentChanged = false;
            this.ContentControl.GotKeyboardFocus += ControlsDataHelpersCommon.Control_GotFocus;
            this.ContentControl.LostKeyboardFocus += ControlsDataHelpersCommon.Control_LostFocus;
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
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}
