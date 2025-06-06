﻿using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ImageProcessor;
using ImageProcessor.Imaging;
using ImageProcessor.Imaging.Filters.EdgeDetection;

namespace Timelapse.Images
{
    // Process an image according to various image processig parameters (brightness, gamma, etc)
    public static class ImageProcess
    {
        #region Public Static Methods
        // Given a stream and various image-processing parameters, generate a bitmap frame processed according to those parameters
        // Arguments also indicates orientation as read from the exif orientaton flag:
        // - Angle gives the angle in degrees (eg., 90) while rotateflip gives it as a rotatefliptype. 
        public static async Task<BitmapFrame> StreamToImageProcessedBitmap(MemoryStream inImageStream, int brightness, int contrast, bool sharpen, bool detectEdges, bool useGamma, float gammaValue, int rotation, RotateFlipType rotateFlip)
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
                        // Note that there are two different ways we use to generate the image based upon what options were set,
                        // as ImageProcessor doesn't do the gamma option 
                        try
                        {
                            if (useGamma)
                            {
                                Image drawingImage = Image.FromStream(inImageStream);
                                drawingImage.RotateFlip(rotateFlip);
                                Bitmap bitmap = AdjustGamma(drawingImage, gammaValue);
                                bitmap.Save(outImageStream, ImageFormat.Bmp);
                            }
                            else
                            {
                                if (detectEdges)
                                {
                                    // Load, resize, set the format and quality and save an image.
                                    ScharrEdgeFilter edger = new ScharrEdgeFilter();
                                    imageFactory.Load(inImageStream)
                                                .DetectEdges(edger)
                                                .Contrast(contrast)
                                                .Brightness(brightness)
                                                .Rotate(rotation)
                                                .Save(outImageStream);
                                }
                                else if (sharpen)
                                {
                                    GaussianLayer gaussian = new GaussianLayer(5, 3);
                                    // Load, resize, set the format and quality and save an image.
                                    imageFactory.Load(inImageStream)
                                                .GaussianSharpen(gaussian)
                                                .Contrast(contrast)
                                                .Brightness(brightness)
                                                .Rotate(rotation)
                                                .Save(outImageStream);
                                }
                                else
                                {
                                    // Load, resize, set the format and quality and save an image.
                                    imageFactory.Load(inImageStream)
                                                .Contrast(contrast)
                                                .Brightness(brightness)
                                                .Rotate(rotation)
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
        private static Bitmap AdjustGamma(Image image, float gamma)
        {
            if (gamma <= 0)
            {
                // Just in case...
                gamma = .1f;
            }

            // Set the ImageAttributes object's gamma value.
            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetGamma(gamma);

                // Draw the image onto the new bitmap
                // while applying the new gamma value.
                Point[] points =
                {
                new Point(0, 0),
                new Point(image.Width, 0),
                new Point(0, image.Height),
            };
                Rectangle rect =
                    new Rectangle(0, 0, image.Width, image.Height);

                // Make the result bitmap.
                Bitmap bm = new Bitmap(image.Width, image.Height);
                using (Graphics gr = Graphics.FromImage(bm))
                {
                    gr.DrawImage(image, points, rect,
                        GraphicsUnit.Pixel, attributes);
                }

                // Return the result.
                return bm;
            }
        }
        #endregion
    }
}
