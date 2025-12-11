using System.Data;
using System.IO;
using System.Windows.Media.Imaging;
using Timelapse.Enums;
using Timelapse.Images;

namespace Timelapse.DataTables
{
    // A VideoRow is an ImageRow specialized to videos instead of images.
    // In particular, it knows how to retrieve a bitmap from a video file
    // See ImageRow for details
    public class VideoRow(DataRow row) : ImageRow(row)
    {
        #region Public Methods - Boolean tests
        // We can't easily tell if a video is displayable. Instead, just see if the file exists.
        public override bool IsDisplayable(string pathToRootFolder)
        {
            return System.IO.File.Exists(Path.Combine(pathToRootFolder, RelativePath, File));
        }

        // This will be invoked only on a video file, so always returns true
        public override bool IsVideo => true;

        #endregion

        #region Public Methods - LoadBitmap from Video File
        // Get the bitmap representing a video file
        public override BitmapSource LoadBitmap(string imageFolderPath, int? desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension,
            out bool isCorruptOrMissing)
        {
            // when invoked from here, we set the frameTime to null.
            return LoadVideoBitmap(imageFolderPath, desiredWidthOrHeight, displayIntent, imageDimension, null, out isCorruptOrMissing);
        }

        // Implementation note: I would have prefered to do this as an over-ride to the above, but wasn't sure how to do that.
        public BitmapSource LoadVideoBitmap(string imageFolderPath, int? desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, float? frameTime, out bool isCorruptOrMissing)
        {
            // Invoke the static version. The only change is that we get the full file path and pass that as a parameter
            return BitmapUtilities.GetBitmapFromVideoFile(GetFilePath(imageFolderPath), desiredWidthOrHeight, displayIntent, imageDimension, frameTime, out isCorruptOrMissing);
        }
        #endregion
    }
}
