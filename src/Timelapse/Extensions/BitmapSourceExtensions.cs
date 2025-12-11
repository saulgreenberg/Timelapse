using System.Windows.Media.Imaging;

namespace Timelapse.Extensions
{
    internal static class BitmapSourceExtensions
    {
        public static WriteableBitmap AsWriteable(this BitmapSource bitmapSource)
        {
            if (bitmapSource is WriteableBitmap bitmap)
            {
                return bitmap;
            }

            WriteableBitmap writeableBitmap = new(bitmapSource);
            writeableBitmap.Freeze();
            return writeableBitmap;
        }
    }
}
