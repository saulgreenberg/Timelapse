using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for DateTime picker (WatermarkDateTimePicker) controls.
    /// Contains all logic common to both DataEntry and Metadata DateTime/Date controls.
    /// </summary>
    public class DateTimePickerControlCore(WatermarkDateTimePicker control)
    {
        #region Content
        /// <summary>
        /// Gets the text content from the DateTimePicker
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
