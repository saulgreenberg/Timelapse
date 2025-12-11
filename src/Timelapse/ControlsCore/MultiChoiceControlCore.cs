using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for MultiChoice (WatermarkCheckComboBox) controls.
    /// Contains all logic common to both DataEntryMultiChoice and MetadataDataEntryMultiChoice.
    /// </summary>
    public class MultiChoiceControlCore(WatermarkCheckComboBox control)
    {
        #region Content
        /// <summary>
        /// Gets the selected items as a comma-separated string
        /// </summary>
        public string GetContent()
        {
            return control.Text;
        }

        /// <summary>
        /// Tracks whether content has changed since last set
        /// </summary>
        public bool ContentChanged { get; set; }

        /// <summary>
        /// WatermarkCheckComboBox doesn't have IsReadOnly, it uses IsEditable instead
        /// </summary>
        public bool ContentReadOnly { get; set; }

        #endregion
    }
}
