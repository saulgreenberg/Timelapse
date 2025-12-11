using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.ControlsMetadata
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label)
    // - checkbox (the content) at the given width
    public class MetadataDataEntryFlag : MetadataDataEntryControl<CheckBox, Label>
    {
        private readonly FlagControlCore core;

        #region Public Properties
        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the Content of the Flag</summary>
        public override string Content => core.GetContent();

        protected override bool GetContentControlReadOnly() => core.ContentReadOnly;

        protected override void SetContentControlReadOnly(bool isReadOnly) => core.ContentReadOnly = isReadOnly;
        #endregion

        #region Constructor
        public MetadataDataEntryFlag(MetadataControlRow control, DataEntryControls styleProvider, string tooltip)
            : base(control, styleProvider, ControlContentStyleEnum.FlagCheckBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            ControlType = control.Type;

            // Create core shared implementation
            core = new FlagControlCore(ContentControl);

            // Callback used to allow Enter to select the highlit item
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
        }
        #endregion

        #region Event Handlers
        // Delegate to core for navigation key handling
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            core.HandleNavigationKeys(keyEvent, false);
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // Ensure that we always have a valid true/false value, wehre anything other than true is considered false. 
            bool newBoolValue = null != value && value.ToLower() == BooleanValue.True;

            // The checkbox will be checked depending on whether the value is true or false 
            // and the tooltip will be set to true or false
            ContentControl.IsChecked = newBoolValue;
            ContentControl.ToolTip = newBoolValue.ToString(); 
        }
        #endregion
    }
}

