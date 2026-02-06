using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.ControlsCore;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ControlsDataEntry
{
    public class DataEntryAlphaNumeric : DataEntryNote
    {
        // There are a few modest differences between the inherited Notes and Alphanumerics.
        // 1. To limit entered characters to the alphanumerics, we need to add several event handles
        // 2. When setting the content, we need to make sure the text does not invoke the handlers,
        //    to ensure that ellipses in the overview would not be filtered out of the textbox
        #region Private variables
        private bool processEvents = true;
        #endregion

        #region Constructor
        public DataEntryAlphaNumeric(ControlRow control, Dictionary<string, string> autocompletions, DataEntryControls styleProvider) :
            base(control, autocompletions, styleProvider)
        {
            // Add these handlers
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
            ContentControl.PreviewTextInput += ContentControl_PreviewTextInput;
            ContentControl.TextChanged += ContentControl_TextChanged;
        }
        #endregion

        #region Event handlers 
        // These handlers limit text entry (including pasting) to alphanumeric text

        // 1. Handle Ctl-C, Ctl-V, Ctl-A keystrokes here so the do the correct thing
        // 2. Limit how spaces are used. (PreviewTextInput allows spaces to go through so we have to do it here) 
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (processEvents == false)
            {
                return;
            }
            // TODO: Handle Ctl-C, Ctl-V, Ctl-A keystrokes here so that they are not intercepted by the base class
            //if (IsCondition.IsKeyControlDown())
            //{
            //    // Handle Ctl-A, Ctl-C
            //    if (keyEvent.Key is Key.A or Key.C)
            //    {
            //        // Let windows handle select all and copy
            //        return;
            //    }
            //    // Handle Ctl-V, ensuring that pasted text is alphanumeric. If not, just flash the control
            //    if (false == Util.IsCondition.IsKeyShiftDown() && keyEvent.Key is Key.V && GlobalReferences.TimelapseState.IsViewOnly == false)
            //    {
            //        try
            //        {
            //            if (Util.IsCondition.IsAlphaNumeric(Clipboard.GetText()))
            //            {
            //                // Pasting works as its alphanumeric
            //                return;
            //            }
            //            this.FlashContentControl(FlashEnum.UseErrorFlash);
            //            keyEvent.Handled = true;
            //            return;
            //        }
            //        catch
            //        {
            //            // Handle clipboard access errors gracefully, by just ignoring the paste operation.
            //            this.FlashContentControl(FlashEnum.UseErrorFlash);
            //            keyEvent.Handled = true;
            //            return;
            //        }
            //    }
            //}
            // Limit how spaces are used
            ControlsDataHelpers.AlphaNumericHandleKeyDownForSpace(this, keyEvent);
        }

        // Allow only alphanumeric characters (although editing characters like backspace etc still go through)
        private void ContentControl_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            if (processEvents)
            {
                ControlsDataHelpers.AlphaNumericHandleAlphaNumericInputOnly(this, args);
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
                    ControlsDataHelpers.AlphaNumericHandleAlphaNumericTextChange(window, this, args);
                }
                processEvents = true;
            }
        }

        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // TODO: See hack in AutoCompleteTextBox, needed as SetContentAndTooltip is not invoked on edit
            //   - edits are done by  NoteControl_TextAutocompleted instead. Could maybe modify that?
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                processEvents = false;
                ContentControl.ShowImprint = true;
                ContentControl.ToolTip = "Edit to change the " + Label + " for all selected images";
                processEvents = true;
                ContentControl.AlwaysInvokeTextChanged = true;
                return;
            }

            // Set the alphanumeric to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentControl.ShowImprint = false;
            ContentChanged = ContentControl.Text != value;
            ContentControl.SetText(value);
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? LabelControl.ToolTip : value;
            ContentControl.AlwaysInvokeTextChanged = false;
        }
        #endregion
    }
}
