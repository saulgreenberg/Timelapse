using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.ControlsMetadata
{
    /// <summary>
    /// Alphanumeric control that inherits from MetadataDataEntryNote but restricts input to alphanumeric characters only.
    /// Autocomplete functionality is disabled for this control type.
    /// </summary>
    public class MetadataDataEntryAlphaNumeric : MetadataDataEntryNote
    {
        #region Private variables
        private bool processEvents = true;
        #endregion

        #region Constructor
        public MetadataDataEntryAlphaNumeric(MetadataControlRow control, Dictionary<string,string> autocompletions, DataEntryControls styleProvider, string tooltip) :
            base(control, autocompletions, styleProvider, tooltip) // Pass null for autocompletions - not used for alphanumeric
        {
            // Disable autocomplete features since we don't use them for alphanumeric input
            //ContentControl.AutocompletionsAsPopup = false;

            // Add alphanumeric validation event handlers
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
                ControlsDataHelpers.AlphaNumericHandleKeyDownForSpace(this, args);
            }
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
    }
}
