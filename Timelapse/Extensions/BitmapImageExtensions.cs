using System.Windows.Media.Imaging;

namespace Timelapse.Extensions
{
    internal static class BitmapImageExtensions
    {
        public static bool SaveToFile(this BitmapImage bitmapImage, string filePath)
        {
            if (bitmapImage == null || string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                // Write the bitmap image to a file using JPEG format
                BitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                using var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);
                encoder.Save(fileStream);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
