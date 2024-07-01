using System;

namespace Timelapse.EventArguments
{
    /// <summary>
    /// Used by ImageAdjuster - essentially to indicate whether there is a new image available to adjust, and wether we are displaying the single image
    /// </summary>
    public class ImageStateEventArgs : EventArgs
    {
        public bool IsImageView { get; set; }

        public ImageStateEventArgs(bool isImageView)
        {
            this.IsImageView = isImageView;
        }
    }
}
