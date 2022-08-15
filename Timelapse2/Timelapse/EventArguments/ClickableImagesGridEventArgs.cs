using System;
using Timelapse.Controls;
using Timelapse.Database;

namespace Timelapse.EventArguments
{
    // Event indicates which image was double clicked on in the ThumbnailGrid 
    public class ThumbnailGridEventArgs : EventArgs
    {
        public ThumbnailGrid Grid { get; set; }
        public ImageRow ImageRow { get; set; }
        public ThumbnailGridEventArgs(ThumbnailGrid grid, ImageRow imageRow)
        {
            this.Grid = grid;
            this.ImageRow = imageRow;
        }
    }
}
