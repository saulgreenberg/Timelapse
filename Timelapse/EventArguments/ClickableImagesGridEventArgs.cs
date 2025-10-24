using System;
using Timelapse.Controls;
using Timelapse.DataTables;

namespace Timelapse.EventArguments
{
    // Event indicates which image was double clicked on in the ThumbnailGrid 
    public class ThumbnailGridEventArgs(ThumbnailGrid grid, ImageRow imageRow) : EventArgs
    {
        public ThumbnailGrid Grid { get; set; } = grid;
        public ImageRow ImageRow { get; set; } = imageRow;
    }
}
