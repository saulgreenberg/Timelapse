using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataTables;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsDataEntry
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label)
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<ImprintAutoCompleteTextBox, Label>
    {
        private readonly NoteControlCore core;

        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

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
        public DataEntryNote(ControlRow control, Dictionary<string, string> autocompletions, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.ImprintNoteTextBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new NoteControlCore(ContentControl);

            // Enable behavior: keep imprint visible when focused, clear on first keystroke
            ContentControl.ClearOnFirstKeystrokeWhenShowingImprint = true;

            // Now configure the various elements
            ContentControl.AddToAutocompletions(autocompletions);
            ContentChanged = false;
            ContentControl.AutocompletionsAsPopup = false;
            ContentControl.AutocompletePopupMaxSize = 10;
            ContentControl.Imprint = Unicode.Ellipsis;


        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // TODO: See hack in AutoCompleteTextBox, needed as SetContentAndTooltip is not invoked on edits.
            //   - edits are done by  AlphaNumericControl_TextAutocompleted instead. Could maybe modify that?
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                ContentControl.ShowImprint = true;
                ContentControl.ToolTip = "Edit to change the " + Label + " for all selected images";
                ContentControl.AlwaysInvokeTextChanged = true;
                return;
            }

            // Otherwise, the note will be set to the provided value
            // If the value to be empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentControl.ShowImprint = false;
            ContentChanged = ContentControl.Text != value;
            ContentControl.SetText(value);
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? LabelControl.ToolTip : value;
            ContentControl.AlwaysInvokeTextChanged = false;
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl(FlashEnum flashEnum)
        {
            if (ContentControl?.MainDisplayField is { } primaryDisplay)
            {
                primaryDisplay.Background = new SolidColorBrush(Colors.White);
                primaryDisplay.Background.BeginAnimation(SolidColorBrush.ColorProperty,
                    flashEnum == FlashEnum.UsePasteFlash
                        ? GetColorAnimationForPasting()
                        : GetColorAnimationWarning());
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (PopupPreview == null)
            {
                // No adjustment is needed as the popup is directly over the entire note control
                double horizontalOffset = 0;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new(7, 5.5, 0, 0);

                PopupPreview = CreatePopupPreview(ContentControl, padding, ContentControl.Width, horizontalOffset);
            }
            // Show the popup
            ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            HidePopupPreview();
        }
        public override void FlashPreviewControlValue()
        {
            FlashPopupPreview();
        }
        #endregion
    }
}