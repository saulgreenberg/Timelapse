using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLaborsImage = SixLabors.ImageSharp.Image;
using SystemDrawingImage = System.Drawing.Image;
using SystemDrawingPoint = System.Drawing.Point;
using SystemDrawingRectangle = System.Drawing.Rectangle;

namespace Timelapse.Images
{
    // Process an image according to various image processig parameters (brightness, gamma, etc)
    public static class ImageProcess
    {
        #region Public Static Methods
        // Given a stream and various image-processing parameters, generate a bitmap frame processed according to those parameters
        // Arguments also indicates orientation as read from the exif orientaton flag:
        // - Angle gives the angle in degrees (eg., 90) while rotateflip gives it as a rotatefliptype. 
        public static async Task<BitmapFrame> StreamToImageProcessedBitmap(MemoryStream inImageStream, double brightness, int contrast, bool sharpen, bool detectEdges, bool useGamma, float gammaValue, int rotation, RotateFlipType rotateFlip)
        {
            if (inImageStream == null || inImageStream.CanRead == false)
            {
                return null;
            }
            return await Task.Run(() =>
            {
                using MemoryStream outImageStream = new();
                try
                {
                    if (useGamma)
                    {
                        // For gamma adjustment, still use System.Drawing as ImageSharp doesn't have direct gamma support
                        SystemDrawingImage drawingImage = SystemDrawingImage.FromStream(inImageStream);
                        drawingImage.RotateFlip(rotateFlip);
                        Bitmap bitmap = AdjustGamma(drawingImage, gammaValue);
                        bitmap.Save(outImageStream, ImageFormat.Bmp);
                    }
                    else
                    {
                        // Use ImageSharp for other operations
                        inImageStream.Position = 0;
                        using var image = SixLaborsImage.Load(inImageStream);
                        image.Mutate(x =>
                        {
                            // Apply rotation
                            if (rotation != 0)
                            {
                                x.Rotate(rotation);
                            }

                            // Apply brightness (0 to 1 (neutral) to 2 or 3 range)
                            if (brightness != 0)
                            {
                                // Lightness lightens the entire image. Brightness increases already bright areas
                                x.Lightness((float)brightness);
                            }

                            // Apply contrast (ImageSharp uses multiplier, convert from percentage)
                            if (contrast != 0)
                            {
                                float contrastValue = 1.0f + (contrast / 100.0f); // Convert percentage to multiplier
                                x.Contrast(contrastValue);
                            }

                            // Apply edge detection
                            if (detectEdges)
                            {
                                x.DetectEdges(KnownEdgeDetectorKernels.Scharr);
                            }

                            // Apply sharpening
                            if (sharpen)
                            {
                                x.GaussianSharpen(3.0f); // radius 3
                            }
                        });

                        // Save as BMP
                        image.Save(outImageStream, new BmpEncoder());
                    }
                }
                catch
                {
                    return null;
                }

                // Return the stream as a bitmap that can be used in Image.Source
                outImageStream.Position = 0;
                return BitmapFrame.Create(outImageStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private methods - Adjust Gamma
        // Adjust the gamma. Useful values are between .1 and 3, neutral is 1
        // WORKS WELL
        private static Bitmap AdjustGamma(SystemDrawingImage image, float gamma)
        {
            if (gamma <= 0)
            {
                // Just in case...
                gamma = .1f;
            }

            // Set the ImageAttributes object's gamma value.
            using ImageAttributes attributes = new();
            attributes.SetGamma(gamma);

            // Draw the image onto the new bitmap
            // while applying the new gamma value.
            SystemDrawingPoint[] points =
            [
                new(0, 0),
                new(image.Width, 0),
                new(0, image.Height)
            ];
            SystemDrawingRectangle rect = new(0, 0, image.Width, image.Height);

            // Make the result bitmap.
            Bitmap bm = new(image.Width, image.Height);
            using Graphics gr = Graphics.FromImage(bm);
            gr.DrawImage(image, points, rect,
                GraphicsUnit.Pixel, attributes);

            // Return the result.
            return bm;
        }
        #endregion
    }
}
