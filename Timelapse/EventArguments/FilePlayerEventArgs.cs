using System;
using Timelapse.Enums;

namespace Timelapse.EventArguments
{
    /// <summary>
    /// Event indicates what action was requested on the fileplayer and which direction to navigate 
    /// </summary>
    public class FilePlayerEventArgs(DirectionEnum direction, FilePlayerSelectionEnum selection) : EventArgs
    {
        public DirectionEnum Direction { get; internal set; } = direction;

        public FilePlayerSelectionEnum Selection { get; internal set; } = selection;
    }
}
