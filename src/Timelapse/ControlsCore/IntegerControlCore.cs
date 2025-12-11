using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Integer (IntegerUpDown-based) controls.
    /// Contains all logic common to both DataEntryIntegerBase and MetadataDataEntryIntegerBase.
    /// </summary>
    public class IntegerControlCore(IntegerUpDown control)
    {
        #region Content
        /// <summary>
        /// Gets the integer content as a string
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
