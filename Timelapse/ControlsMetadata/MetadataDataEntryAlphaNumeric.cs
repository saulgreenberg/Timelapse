using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.ControlsDataCommon;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.ControlsMetadata
{
    public class MetadataDataEntryAlphaNumeric : MetadataDataEntryControl<TextBox, Label>
    {
        #region Public Properties
        public override UIElement GetContentControl => ContentControl;
        public override bool IsContentControlEnabled => ContentControl.IsEnabled;
        /// <summary>Gets  the content of the Alphanumeric</summary>
        public override string Content => ContentControl.Text;
        public bool ContentChanged { get; set; }
        #endregion

        #region Private variables
        private bool processEvents = true;
        #endregion

        #region Constructor
        public MetadataDataEntryAlphaNumeric(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            ContentControl.PreviewTextInput += ContentControl_PreviewTextInput;
            ContentControl.TextChanged += ContentControl_TextChanged;
        }
        #endregion

        #region Callbacks: Limit text entry (including pasting) to alphanumeric text
        // Limit how spaces are used. (PreviewTextInput allows spaces to go through so we have to do it here) 
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (processEvents)
            {
                ControlsDataHelpersCommon.AlphaNumericHandleKeyDownForSpace(this, args);
            }
        }

        // Allow only alphanumeric characters (although editing characters like backspace etc still go through)
        private void ContentControl_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            if (processEvents)
            {
                ControlsDataHelpersCommon.AlphaNumericHandleAlphaNumericInputOnly(this, args);
            }
        }

        // Check final text - most will be ok, except for pasted Ctl-V text which could be anything
        private void ContentControl_TextChanged(object sender, TextChangedEventArgs args)
        {
            if (processEvents)
            {
                processEvents = false;
                if (sender is TextBox textBox)
                {
                    Window window = textBox.FindParentOfType<Window>();
                    ControlsDataHelpersCommon.AlphaNumericHandleAlphaNumericTextChange(window, this, args);
                }
                processEvents = true;
            }
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            if (value == null)
            {
                return;
            }

            // Set the alphanumeric to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentChanged = ContentControl.Text != value;
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}
