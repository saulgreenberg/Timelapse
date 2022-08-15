using ImageProcessor;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    // Process an image according to various image processig parameters (brightness, gamma, etc)
    public static class ImageProcess
    {
        #region Public Static Methods
        // Given a stream and various image-processing parameters, generate a bitmap frame processed according to those parameters
        public static async Task<BitmapFrame> StreamToImageProcessedBitmap(MemoryStream inImageStream, int brightness, int contrast, bool sharpen, bool detectEdges, bool useGamma, float gammaValue)
        {
            if (inImageStream == null || inImageStream.CanRead == false)
            {
                return null;
            }
            return await Task.Run(() =>
            {
                using (MemoryStream outImageStream = new MemoryStream())
                {
                    // Initialize the ImageFactory using the overload to preserve EXIF metadata.
                    using (ImageFactory imageFactory = new ImageFactory(preserveExifData: false))
                    {
                        try
                        {
                            if (useGamma)
                            {
                                System.Drawing.Image drawingImage = System.Drawing.Image.FromStream(inImageStream);
                                System.Drawing.Bitmap bitmap = AdjustGamma(drawingImage, gammaValue);
                                bitmap.Save(outImageStream, System.Drawing.Imaging.ImageFormat.Bmp);
                            }
                            else
                            {
                                if (detectEdges)
                                {
                                    // Load, resize, set the format and quality and save an image.
                                    ImageProcessor.Imaging.Filters.EdgeDetection.ScharrEdgeFilter edger = new ImageProcessor.Imaging.Filters.EdgeDetection.ScharrEdgeFilter();
                                    imageFactory.Load(inImageStream)
                                                .DetectEdges(edger)
                                                .Contrast(contrast)
                                                .Brightness(brightness)
                                                .Save(outImageStream);
                                }
                                else if (sharpen)
                                {
                                    ImageProcessor.Imaging.GaussianLayer gaussian = new ImageProcessor.Imaging.GaussianLayer(5, 3, 0);
                                    // Load, resize, set the format and quality and save an image.
                                    imageFactory.Load(inImageStream)
                                                .GaussianSharpen(gaussian)
                                                .Contrast(contrast)
                                                .Brightness(brightness)
                                                .Save(outImageStream);
                                }
                                else
                                {
                                    // Load, resize, set the format and quality and save an image.
                                    imageFactory.Load(inImageStream)
                                                .Contrast(contrast)
                                                .Brightness(brightness)
                                                .Save(outImageStream);
                                }
                            }
                        }
                        catch
                        {
                            return null;
                        }
                    }
                    // Return the stream as a bitmap that can be used in Image.Source
                    return BitmapFrame.Create(outImageStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private methods - Adjust Gamma
        // Adjust the gamma. Useful values are between .1 and 3, neutral is 1
        // WORKS WELL
        private static System.Drawing.Bitmap AdjustGamma(System.Drawing.Image image, float gamma)
        {
            if (gamma <= 0)
            {
                // Just in case...
                gamma = .1f;
            }

            // Set the ImageAttributes object's gamma value.
            using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetGamma(gamma);

                // Draw the image onto the new bitmap
                // while applying the new gamma value.
                System.Drawing.Point[] points =
                {
                new System.Drawing.Point(0, 0),
                new System.Drawing.Point(image.Width, 0),
                new System.Drawing.Point(0, image.Height),
            };
                System.Drawing.Rectangle rect =
                    new System.Drawing.Rectangle(0, 0, image.Width, image.Height);

                // Make the result bitmap.
                System.Drawing.Bitmap bm = new System.Drawing.Bitmap(image.Width, image.Height);
                using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(bm))
                {
                    gr.DrawImage(image, points, rect,
                        System.Drawing.GraphicsUnit.Pixel, attributes);
                }

                // Return the result.
                return bm;
            }
        }
        #endregion
    }
}
