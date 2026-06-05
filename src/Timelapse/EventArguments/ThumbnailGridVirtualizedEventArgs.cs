using System;
using Timelapse.DataTables;

namespace Timelapse.EventArguments
{
    public class ThumbnailGridVirtualizedEventArgs(ImageRow imageRow) : EventArgs
    {
        public ImageRow ImageRow { get; set; } = imageRow;
    }
}
