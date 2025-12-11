using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using TimelapseWpf.Toolkit;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.ControlsDataEntry
{
    // A multiline lays out a stack panel containing
    // - a label containing the descriptive label)
    // - an editable textbox (containing the content) at the given width
    public class DataEntryMultiLine : DataEntryControl<MultiLineText, Label>
    {
        private readonly MultiLineControlCore core;

        #region Constructor
        public DataEntryMultiLine(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.MultiLineTextBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new MultiLineControlCore(ContentControl);

            // Wire up event handler for Shift+navigation key handling
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
        }
        #endregion

        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets the content of the multiline</summary>
        public override string Content => core.GetContent();

        public bool ContentChanged
        {
            get => core.ContentChanged;
            set => core.ContentChanged = value;
        }

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

        // Now configure the various elements

        #endregion

        #region Event Handlers
        protected override bool HandleKeyboardNavigationInBase()
        {
            return false; // We handle our own keyboard navigation
        }

        // Manages shortcut keys, and drop-down open/closing/navigation
        // Handle Shift + navigation keys and plain arrow keys to navigate between images
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (sender is not MultiLineText)
            {
                // Unlikely to happen
                return;
            }

            // Possible shortcut keys (delegated to main window):
            // - any Control key press could indicate a Shortcut key, and
            // - a few very specific keys that don't require a Control key press
            if (IsCondition.IsKeyControlDown() ||
                IsCondition.IsKeyPageUpDown(keyEvent.Key))
            {
                // Commit the text in the editor if its open (which also closes it)
                // then deligate the shortcut to the main window
                if (ContentControl.EditorPopup is { IsOpen: true })
                {
                    ContentControl.Commit();
                }
                DelegateKeyEventToMainWindow(keyEvent, true);
                return;
            }

            // Left/right/up/down without ctl:
            // - popup is closed: noop,
            // - popup is open: will navigate text
            // Reminder: ctl-left/right handled before this
            if (IsCondition.IsKeyLeftRightUpDownArrow(keyEvent.Key) && ContentControl.EditorPopup is { IsOpen: false })
            {
                keyEvent.Handled = true;
            }
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn.
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            // TODO: See the hack in MultiLineText as I couldn't figure out how to do it here.
            //  SetContentAndTooltip is not invoked when we close the popup without editing, where the ellipsis would vanish.
            //  The MultiLineText hack handles this by checking the contents of the DisplayText if its an ellipsis
            if (value == null)
            {
                ContentControl.DisplayText = Unicode.Ellipsis;
                // If we are displaying an ellipsis, make the text in the popup empty so it can be edited
                // properly
                ContentControl.Text = ContentControl.DisplayText == Unicode.Ellipsis
                    ? string.Empty
                    : ContentControl.DisplayText;
                ContentControl.ToolTip = "Edit to change the " + Label + " for all selected images";
                return;
            }

            // Otherwise, the multiline will be set to the provided value 
            // If the value to be empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentChanged = ContentControl.Text != value;
            ContentControl.DisplayText = value;
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? LabelControl.ToolTip : value;
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl(FlashEnum flashEnum)
        {
            if (ContentControl.MainDisplayField != null)
            {
                ContentControl.MainDisplayField.Background = new SolidColorBrush(Colors.White);
                ContentControl.MainDisplayField.Background.BeginAnimation(SolidColorBrush.ColorProperty,
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
                Thickness padding = new(7, 4, 0, 0);

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

