using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ControlsMetadata
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class MetadataDataEntryMultiLine : MetadataDataEntryControl<MultiLineText, Label>
    {
        #region Public Properties

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => ContentControl.Text;

        public bool ContentChanged { get; set; }
        #endregion

        #region Constructor
        public MetadataDataEntryMultiLine(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.MultiLineTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
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
            ContentControl.DisplayText = value;
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}