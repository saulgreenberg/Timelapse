using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Decimal (DoubleUpDown-based) controls.
    /// Contains all logic common to both DataEntryDecimalBase and MetadataDataEntryDecimalBase.
    /// </summary>
    public class DecimalControlCore(DoubleUpDown control)
    {
        #region Content
        /// <summary>
        /// Gets the decimal content as a string
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
