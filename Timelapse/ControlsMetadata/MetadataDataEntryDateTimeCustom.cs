﻿using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.ControlsDataCommon;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // CustomDateTime lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class MetadataDataEntryDateTimeCustom : MetadataDataEntryControl<DateTimePicker, Label>
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
        public MetadataDataEntryDateTimeCustom(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.DateTimeBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
            ContentControl.GotKeyboardFocus += ControlsDataHelpersCommon.Control_GotFocus;
            ContentControl.LostKeyboardFocus += ControlsDataHelpersCommon.Control_LostFocus;
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
            : DateTimeHandler.ToStringDisplayDateTime((DateTime) value);
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