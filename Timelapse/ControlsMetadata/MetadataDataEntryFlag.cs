using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.DataTables;

namespace Timelapse.ControlsMetadata
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbox (the content) at the given width
    public class MetadataDataEntryFlag : MetadataDataEntryControl<CheckBox, Label>
    {
        #region Public Properties
        public override UIElement GetContentControl => this.ContentControl;

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        /// <summary>Gets or sets the Content of the Flag</summary>
        public override string Content => (this.ContentControl.IsChecked != null && (bool)this.ContentControl.IsChecked) 
            ? Constant.BooleanValue.True 
            : Constant.BooleanValue.False;

        // This override has slightly different code compared to the other DataEntry types.. 
        private bool contentReadOnly;
        public override bool ContentReadOnly
        {
            get => this.contentReadOnly;
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    this.contentReadOnly = true;
                    this.ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    this.contentReadOnly = value;
                }
            }
        }
        #endregion

        #region Constructor
        public MetadataDataEntryFlag(MetadataControlRow control, DataEntryControls styleProvider, string tooltip)
            : base(control, styleProvider, ControlContentStyleEnum.FlagCheckBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            this.ControlType = control.Type;
            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
        }
        #endregion

        #region Event Handlers
        // Ignore these navigation key events, as otherwise they act as tabs which does not conform to how we navigate
        // between other control types
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown)
            {
                // the right/left arrow keys normally cycle through the menu items.
                // However, we want to retain the arrow keys - as well as the PageUp/Down keys - for cycling through the image.
                // So we mark the event as handled, and we cycle through the images anyways.
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                keyEvent.Handled = true;
                GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
            else if (keyEvent.Key == Key.Up || keyEvent.Key == Key.Down)
            {
                // Ignore as it otherwise handled as a tab
                keyEvent.Handled = true;
            }
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // Ensure that we always have a valid true/false value, wehre anything other than true is considered false. 
            bool newBoolValue = null != value && value.ToLower() == Constant.BooleanValue.True;

            // The checkbox will be checked depending on whether the value is true or false 
            // and the tooltip will be set to true or false
            this.ContentControl.IsChecked = newBoolValue;
            this.ContentControl.ToolTip = newBoolValue.ToString(); 
        }
        #endregion
    }
}

