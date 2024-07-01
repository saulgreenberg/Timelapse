using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsDataEntry
{
    // A multiline lays out a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryMultiLine : DataEntryControl<MultiLineTextEditor, Label>
    {
        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => this.ContentControl.PointToScreen(new Point(0, 0));

        public override UIElement GetContentControl => this.ContentControl;

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        /// <summary>Gets  the content of the multiline</summary>
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

        #region Constructor
        public DataEntryMultiLine(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyleEnum.MultiLineBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Now configure the various elements
            this.ContentChanged = false;
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                this.ContentControl.Text = Constant.Unicode.Ellipsis;
                this.ContentControl.ToolTip = "Edit to change the " + this.Label + " for all selected images";
                return;
            }

            // Otherwise, the multiline will be set to the provided value 
            // If the value to be empty, we just make it the same as the tooltip so something meaningful is displayed..
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = string.IsNullOrEmpty(value) ? this.LabelControl.ToolTip : value;
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl()
        {
            ScrollViewer contentHost = (ScrollViewer)this.ContentControl.Template.FindName("PART_ContentHost", this.ContentControl);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.GetColorAnimation());
            }
        }
        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (this.PopupPreview == null)
            {
                // No adjustment is needed as the popup is directly over the entire note control
                double horizontalOffset = 0;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new Thickness(7, 5.5, 0, 0);

                this.PopupPreview = this.CreatePopupPreview(this.ContentControl, padding, this.ContentControl.Width, horizontalOffset);
            }
            // Show the popup
            this.ShowPopupPreview(value);
        }
        public override void HidePreviewControlValue()
        {
            this.HidePopupPreview();
        }
        public override void FlashPreviewControlValue()
        {
            this.FlashPopupPreview();
        }
        #endregion
    }
}
