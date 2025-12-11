using System.Windows.Controls;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Choice (ComboBox-based) controls.
    /// Contains all logic common to both DataEntryChoice and MetadataDataEntryFixedChoice.
    /// Works with ComboBox and WatermarkComboBox (which inherits from ComboBox).
    /// </summary>
    public class ChoiceControlCore(ComboBox control)
    {
        #region Content
        /// <summary>
        /// Gets the content as text
        /// </summary>
        public string GetContent()
        {
            return control.Text;
        }

        /// <summary>
        /// Tracks whether content has changed since last set
        /// </summary>
        public bool ContentChanged { get; set; }
        #endregion
    }
}
