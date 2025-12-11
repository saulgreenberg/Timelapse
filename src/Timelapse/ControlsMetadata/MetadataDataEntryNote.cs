using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label)
    // - an editable textbox (containing the content) at the given width
    public class MetadataDataEntryNote : MetadataDataEntryControl<ImprintAutoCompleteTextBox, Label>
    {
        private readonly NoteControlCore core;

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
        public MetadataDataEntryNote(MetadataControlRow control, Dictionary<string,string> autocompletions, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.ImprintNoteTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new NoteControlCore(ContentControl);

            // Now configure the various elements
            ControlType = control.Type;
            ContentControl.AddToAutocompletions(autocompletions);
            ContentControl.AutocompletionsAsPopup = false;
            ContentControl.AutocompletePopupMaxSize = 10;
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
            ContentControl.Text = value;
            if (string.IsNullOrWhiteSpace(value))
            {
                ContentControl.ToolTip = "Blank entry";
            }
            else
            {
                ContentControl.ToolTip = value;
                ContentControl.AutoCompletionsAddValuesIfNeeded(value);
            }
        }
        #endregion
    }
}