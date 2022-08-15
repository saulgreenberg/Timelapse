using System;
using Timelapse.Images;

namespace Timelapse.EventArguments
{
    /// <summary>
    /// The Marker event argument contains 
    /// - a reference to the marker
    /// - an indication if this is a new just created marker (if true), or if its been deleted (if false)
    /// </summary>
    public class MarkerEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets a value indicating whether this is a new just-created marker (if true), or if its been deleted (if false)
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// Gets or sets the MetaTag
        /// </summary>E:\@Timelapse\GithubTimelapse\Timelapse2\Timelapse\Dialog\QuickPasteEventArgs.cs
        public Marker Marker { get; set; }

        /// <summary>
        /// The Marker event argument contains 
        /// - a reference to the Marker
        /// - an indication if this is a new just-created Marker (if true), or if its been deleted (if false)
        /// </summary>
        public MarkerEventArgs(Marker tag, bool isNew)
        {
            this.Marker = tag;
            this.IsNew = isNew;
        }
    }
}
