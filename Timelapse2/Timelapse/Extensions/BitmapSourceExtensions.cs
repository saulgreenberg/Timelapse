using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    internal static class BitmapSourceExtensions
    {
        public static WriteableBitmap AsWriteable(this BitmapSource bitmapSource)
        {
            if (bitmapSource is WriteableBitmap)
            {
                return bitmapSource as WriteableBitmap;
            }

            WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapSource);
            writeableBitmap.Freeze();
            return writeableBitmap;
        }
    }
}
