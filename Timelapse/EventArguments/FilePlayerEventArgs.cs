using System;
using Timelapse.Enums;

namespace Timelapse.EventArguments
{
    /// <summary>
    /// Event indicates what action was requested on the fileplayer and which direction to navigate 
    /// </summary>
    public class FilePlayerEventArgs : EventArgs
    {
        public DirectionEnum Direction { get; internal set; }

        public FilePlayerSelectionEnum Selection { get; internal set; }

        public FilePlayerEventArgs(DirectionEnum direction, FilePlayerSelectionEnum selection)
        {
            this.Direction = direction;
            this.Selection = selection;
        }
    }
}
