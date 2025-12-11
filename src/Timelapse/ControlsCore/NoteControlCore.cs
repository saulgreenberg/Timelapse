using System.Windows.Controls;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Note (TextBox-based) controls.
    /// Contains all logic common to both DataEntryNote and MetadataDataEntryNote.
    /// Works with any TextBox-derived control (AutocompleteTextBox, ImprintAutoCompleteTextBox, etc.)
    /// </summary>
    public class NoteControlCore(TextBox control)
    {
        #region Content
        /// <summary>
        /// Gets the text content of the note
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
