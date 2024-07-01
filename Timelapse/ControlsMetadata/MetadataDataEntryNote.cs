using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ControlsMetadata
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class MetadataDataEntryNote : MetadataDataEntryControl<AutocompleteTextBox, Label>
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
        public MetadataDataEntryNote(MetadataControlRow control, Dictionary<string,string> autocompletions, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            this.Tooltip = tooltip;
            this.ControlType = control.Type;
            this.ContentControl.Autocompletions = autocompletions;
            this.ContentChanged = false;
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