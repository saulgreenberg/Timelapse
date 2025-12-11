using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for MultiLine (MultiLineText) controls.
    /// Contains all logic common to both DataEntryMultiLine and MetadataDataEntryMultiLine.
    /// </summary>
    public class MultiLineControlCore(MultiLineText control)
    {
        #region Content
        /// <summary>
        /// Gets the multiline text content
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
