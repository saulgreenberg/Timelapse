using System;
namespace Timelapse.QuickPaste
{
    /// <summary>
    /// The QuickPaste event argument contains 
    /// - a reference to the QuickPasteEntry
    /// </summary>
    public class QuickPasteEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the MetaTag
        /// </summary>
        public QuickPasteEntry QuickPasteEntry { get; set; }
        public QuickPasteEventIdentifierEnum EventType { get; set; }

        /// <summary>
        /// The QuickPast event argument contains 
        /// - a reference to the QuickPasteEntry
        /// </summary>
        public QuickPasteEventArgs(QuickPasteEntry quickPasteEntry, QuickPasteEventIdentifierEnum eventType)
        {
            this.QuickPasteEntry = quickPasteEntry;
            this.EventType = eventType;
        }
    }
}
