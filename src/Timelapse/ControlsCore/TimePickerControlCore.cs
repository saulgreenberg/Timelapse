using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Time picker (WatermarkTimePicker) controls.
    /// Contains all logic common to both DataEntry and Metadata Time controls.
    /// </summary>
    public class TimePickerControlCore(WatermarkTimePicker control)
    {
        #region Content
        /// <summary>
        /// Gets the text content from the TimePicker
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
