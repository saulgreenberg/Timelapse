using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ControlsDataEntry
{
    // A note lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<AutocompleteTextBox, Label>
    {
        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new Point(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => ContentControl.Text;

        public bool ContentChanged { get; set; }
        #endregion

        #region Constructor
        public DataEntryNote(ControlRow control, Dictionary<string, string> autocompletions, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.NoteTextBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Now configure the various elements
            ContentControl.Autocompletions = autocompletions;
            ContentChanged = false;
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                ContentControl.Text = Unicode.Ellipsis;
                ContentControl.ToolTip = "Edit to change the " + Label + " for all selected images";
                return;
            }

            // Otherwise, the note will be set to the provided value 
            // If the value to be empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentChanged = ContentControl.Text != value;
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? LabelControl.ToolTip : value;
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            ScrollViewer contentHost = (ScrollViewer)ContentControl.Template.FindName("PART_ContentHost", ContentControl);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, GetColorAnimationFastRed());
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
                Thickness padding = new Thickness(7, 5.5, 0, 0);

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