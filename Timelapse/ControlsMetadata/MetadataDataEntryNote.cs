﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
        public MetadataDataEntryNote(MetadataControlRow control, Dictionary<string,string> autocompletions, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            ControlType = control.Type;
            ContentControl.Autocompletions = autocompletions;
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
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}