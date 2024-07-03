using System.Windows.Controls;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.ControlsDataCommon;
using Timelapse.Util;

namespace Timelapse.ControlsMetadata
{
    public class MetadataDataEntryAlphaNumeric : MetadataDataEntryControl<TextBox, Label>
    {
        #region Public Properties
        public override UIElement GetContentControl => this.ContentControl;
        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;
        /// <summary>Gets  the content of the Alphanumeric</summary>
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

        #region Private variables
        private bool processEvents = true;
        #endregion

        #region Constructor
        public MetadataDataEntryAlphaNumeric(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Now configure the various elements
            this.ControlType = control.Type;
            this.ContentChanged = false;
            this.ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            this.ContentControl.PreviewTextInput += ContentControl_PreviewTextInput;
            this.ContentControl.TextChanged += ContentControl_TextChanged;
        }
        #endregion

        #region Callbacks: Limit text entry (including pasting) to alphanumeric text
        // Limit how spaces are used. (PreviewTextInput allows spaces to go through so we have to do it here) 
        private void ContentControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if (processEvents)
            {
                ControlsDataHelpersCommon.AlphaNumericHandleKeyDownForSpace(this, args);
            }
        }

        // Allow only alphanumeric characters (although editing characters like backspace etc still go through)
        private void ContentControl_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs args)
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
                this.processEvents = false;
                if (sender is TextBox textBox)
                {
                    Window window = textBox.FindParentOfType<Window>();
                    ControlsDataHelpersCommon.AlphaNumericHandleAlphaNumericTextChange(window, this, args);
                }
                this.processEvents = true;
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
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}
