using System;
using System.Data;
using System.IO;
using System.Windows.Media.Imaging;
using Timelapse.Enums;
using Timelapse.Images;
namespace Timelapse.Database
{
    // A VideoRow is an ImageRow specialized to videos instead of images.
    // In particular, it knows how to retrieve a bitmap from a video file
    // See ImageRow for details
    public class VideoRow : ImageRow
    {
        #region Constructors
        public VideoRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Boolean tests
        // We can't easily tell if a video is displayable. Instead, just see if the file exists.
        public override bool IsDisplayable(string pathToRootFolder)
        {
            return System.IO.File.Exists(Path.Combine(pathToRootFolder, this.RelativePath, this.File));
        }

        // This will be invoked only on a video file, so always returns true
        public override bool IsVideo
        {
            get { return true; }
        }
        #endregion

        #region Public Methods - LoadBitmap from Video File
        // Get the bitmap representing a video file
        public override BitmapSource LoadBitmap(string imageFolderPath, Nullable<int> desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, out bool isCorruptOrMissing)
        {
            // Invoke the static version. The only change is that we get the full file path and pass that as a parameter
            return BitmapUtilities.GetBitmapFromVideoFile(this.GetFilePath(imageFolderPath), desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
        }
        #endregion
    }
}
