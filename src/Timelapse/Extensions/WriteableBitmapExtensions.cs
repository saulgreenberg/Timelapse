using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.Extensions
{
    /// <summary>
    /// Extends <see cref="WriteableBitmap"/> with differencing calculations.
    /// </summary>
    /// <remarks>This class consumes WriteableBitmap.BackBuffer in unsafe operations for speed.  Other Windows Presentation Foundation bitmap classes do not
    /// expose in memory content of bitmaps, requiring somewhat expensive double buffering for image calculations, and using Marshal to obtain pixels from
    /// the backing buffer is substantially slower than direct access.</remarks>
    internal static class WriteableBitmapExtensions
    {
        #region Public Static Extensions - Differences
        // Given three images, return an image that highlights the differences in common betwen the main image and the first image
        // and the main image and a second image.
        public static unsafe WriteableBitmap CombinedDifference(this WriteableBitmap unaltered, WriteableBitmap previous, WriteableBitmap next, byte threshold)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(unaltered, nameof(unaltered));
            ThrowIf.IsNullArgument(previous, nameof(previous));
            ThrowIf.IsNullArgument(next, nameof(next));

            if (BitmapsMismatched(unaltered, previous) ||
                BitmapsMismatched(unaltered, next))
            {
                return null;
            }

            GetColorOffsets(unaltered, out int blueOffset, out int greenOffset, out int redOffset);

            int totalPixels = unaltered.PixelWidth * unaltered.PixelHeight;
            int pixelSizeInBytes = unaltered.Format.BitsPerPixel / 8;
            byte* unalteredIndex = (byte*)unaltered.BackBuffer.ToPointer();
            byte* previousIndex = (byte*)previous.BackBuffer.ToPointer();
            byte* nextIndex = (byte*)next.BackBuffer.ToPointer();
            byte[] differencePixels = new byte[totalPixels * pixelSizeInBytes];
            int differenceIndex = 0;
            for (int pixel = 0; pixel < totalPixels; ++pixel)
            {
                byte b1 = (byte)Math.Abs(*(unalteredIndex + blueOffset) - *(previousIndex + blueOffset));
                byte g1 = (byte)Math.Abs(*(unalteredIndex + greenOffset) - *(previousIndex + greenOffset));
                byte r1 = (byte)Math.Abs(*(unalteredIndex + redOffset) - *(previousIndex + redOffset));

                byte b2 = (byte)Math.Abs(*(unalteredIndex + blueOffset) - *(nextIndex + blueOffset));
                byte g2 = (byte)Math.Abs(*(unalteredIndex + greenOffset) - *(nextIndex + greenOffset));
                byte r2 = (byte)Math.Abs(*(unalteredIndex + redOffset) - *(nextIndex + redOffset));

                byte b = DifferenceIfAboveThreshold(threshold, b1, b2);
                byte g = DifferenceIfAboveThreshold(threshold, g1, g2);
                byte r = DifferenceIfAboveThreshold(threshold, r1, r2);

                byte averageDifference = (byte)((b + g + r) / 3);
                differencePixels[differenceIndex + blueOffset] = averageDifference;
                differencePixels[differenceIndex + greenOffset] = averageDifference;
                differencePixels[differenceIndex + redOffset] = averageDifference;

                unalteredIndex += pixelSizeInBytes;
                previousIndex += pixelSizeInBytes;
                nextIndex += pixelSizeInBytes;
                differenceIndex += pixelSizeInBytes;
            }

            WriteableBitmap difference = new(BitmapSource.Create(unaltered.PixelWidth, unaltered.PixelHeight, unaltered.DpiX, unaltered.DpiY, unaltered.Format, unaltered.Palette, differencePixels, unaltered.BackBufferStride));
            return difference;
        }

        // Return whether the image is mostly dark. This is done by counting the number of pixels that are
        // below a certain tolerance (i.e., mostly black), and seeing if the % of mostly dark pixels are
        // at or higher than the given darkPercent.
        // We also check to see if the image is predominantly color. We do this by checking to see if pixels are grey scale
        // (where r=g=b, with a bit of slop added) and then check that against a threshold.
        // ReSharper disable once UnusedMember.Global
        public static bool ClassifyImageDarkness(this WriteableBitmap image, int darkPixelThreshold, double darkPixelRatio)
        {
            return image.IsDark(darkPixelThreshold, darkPixelRatio, out _, out _);
        }

        // Return whether the image is mostly dark. This is done by counting the number of pixels that are
        // below a certain tolerance (i.e., mostly black), and seeing if the % of mostly dark pixels are
        // at or higher than the given darkPercent.
        // We also check to see if the image is predominantly color. We do this by checking to see if pixels are grey scale
        // (where r=g=b, with a bit of slop added) and then check that against a threshold.
        // Return the type of ImageSelection. Note that the ImageSelection is set to ImageSelection.Corrupted when this routing throws and exception,
        // which notifies its callers that there was an error.
        // Note: HandleProcessCorruptedStateExceptions attribute removed as it's obsolete in .NET 8
        public static unsafe bool IsDark(this WriteableBitmap image, int darkPixelThreshold, double darkPixelRatio, out double darkPixelFraction, out bool isColor)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(image, nameof(image));

            // The RGB offsets from the beginning of the pixel (i.e., 0, 1 or 2)
            GetColorOffsets(image, out int blueOffset, out int greenOffset, out int redOffset);

            // various counters that we will use in calculation of image darkness
            int darkPixels = 0;
            int uncoloredPixels = 0;
            int countedPixels = 0;

            // There was a nasty bug that occurred when checking for dark pixels on loading, which happened only (I think) on older machines and operating systems.
            // I suspect its due to a threading issue, but it proved impossible to debug as only release (vs. debug) versions would crash
            // The try / catch mitigates this, although there is a chance that the bug may overwrite some vital memory. 
            try
            {
                // Examine only a subset of pixels as otherwise this is a very expensive operation
                // TODO: Calculate pixelStride as a function of image resolution so future high res images will still be processed quickly.
                byte* currentPixel = (byte*)image.BackBuffer.ToPointer(); // the imageIndex will point to a particular byte in the pixel array
                int pixelSizeInBytes = image.Format.BitsPerPixel / 8;
                int pixelStride = ImageValues.DarkPixelSampleStrideDefault;
                int totalPixels = image.PixelHeight * image.PixelWidth; // total number of pixels in the image

                for (int pixelIndex = 0; pixelIndex < totalPixels; pixelIndex += pixelStride)
                {
                    // get next pixel of interest
                    byte b = *(currentPixel + blueOffset);
                    byte g = *(currentPixel + greenOffset);
                    byte r = *(currentPixel + redOffset);

                    // The numbers below convert a particular color to its greyscale equivalent, and then checked against the darkPixelThreshold
                    // Colors are not weighted equally. Since pure green is lighter than pure red and pure blue, it has a higher weight. 
                    // Pure blue is the darkest of the three, so it receives the least weight.
                    int humanPercievedLuminosity = (int)Math.Round(0.299 * r + 0.5876 * g + 0.114 * b);
                    if (humanPercievedLuminosity <= darkPixelThreshold)
                    {
                        ++darkPixels;
                    }

                    // Check if the pixel is a grey scale vs. color pixel, using a heuristic. 
                    // In precise grey scales, r = g = b. However, we allow a bit of slop as some cameras actually
                    // have a bit of color in their dark shots (don't ask me why, it just happens). 
                    // i.e. if the total delta is less than the color slop, then we consider it a grey level.
                    // Given a pixel's rgb values, calculate the delta between all those values. This is used to determine if its a grey scale pixel.
                    // In practice a grey scale pixel's rgb are all equal (i.e., delta = 0) but we need the value as we want to see how 'close' the pixel 
                    // actually is to 0, i.e., to allow some slop in determining grey versus color pixels.
                    int rgbDelta = Math.Abs(r - g) + Math.Abs(g - b) + Math.Abs(b - r);
                    if (rgbDelta <= ImageValues.GreyscalePixelThreshold)
                    {
                        ++uncoloredPixels;
                    }

                    // update other loop variables
                    ++countedPixels;
                    currentPixel += pixelSizeInBytes * pixelStride; // Advance the pointer to the beginning of the next pixel of interest
                }
                // Check if its a grey scale image, i.e., at least 90% of the pixels in this image (given this slop) are grey scale.
                // If not, its a color image so judge it as not dark
                double uncoloredPixelFraction = 1d * uncoloredPixels / countedPixels;
                if (uncoloredPixelFraction < ImageValues.GreyscaleImageThreshold)
                {
                    darkPixelFraction = 1 - uncoloredPixelFraction;
                    isColor = true;
                    return false;
                }

                // It is a grey scale image. If the fraction of dark pixels are higher than the threshold, it is dark.
                darkPixelFraction = 1d * darkPixels / countedPixels;
                isColor = false;
                if (darkPixelFraction >= darkPixelRatio)
                {
                    return true;
                }
                return false;
            }
            //#pragma warning disable CA2153 // Do Not Catch Corrupted State Exceptions
            catch
            //#pragma warning restore CA2153 // Do Not Catch Corrupted State Exceptions
            {
                // Without this catch, Timelapse will crash on some machines and OS during loads (see notes in method comments) 
                // Note that we have to set isColor and darkPixelFraction to some value, although these are nonsensical as 
                // the method has not completed. 
                // Returning a Corrupted state informs the caller that the check for dark has not completed successfully.
                isColor = true;
                darkPixelFraction = 0;
                return false;
            }
        }

        // Checks whether the image is completely black
        public static unsafe bool IsBlack(this WriteableBitmap image)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(image, nameof(image));

            // The RGB offsets from the beginning of the pixel (i.e., 0, 1 or 2)
            GetColorOffsets(image, out int blueOffset, out int greenOffset, out int redOffset);

            // examine only a subset of pixels as otherwise this is an expensive operation
            // check pixels from last to first as most cameras put a non-black status bar or at least non-black text at the bottom of the frame,
            // so reverse order may be a little faster on average in cases of nighttime images with black skies
            // TODO  Calculate pixelStride as a function of image size so future high res images will still be processed quickly.
            byte* currentPixel = (byte*)image.BackBuffer.ToPointer(); // the imageIndex will point to a particular byte in the pixel array
            int pixelStride = ImageValues.DarkPixelSampleStrideDefault;
            int totalPixels = image.PixelHeight * image.PixelWidth; // total number of pixels in the image
            for (int pixelIndex = totalPixels - 1; pixelIndex > 0; pixelIndex -= pixelStride)
            {
                // get next pixel of interest
                byte b = *(currentPixel + blueOffset);
                byte g = *(currentPixel + greenOffset);
                byte r = *(currentPixel + redOffset);

                if (r != 0 || b != 0 || g != 0)
                {
                    return false;
                }
            }
            return true;
        }

        // Given two images, return an image containing the visual difference between them
        public static unsafe WriteableBitmap Subtract(this WriteableBitmap image1, WriteableBitmap image2)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(image1, nameof(image1));
            ThrowIf.IsNullArgument(image2, nameof(image2));

            if (BitmapsMismatched(image1, image2))
            {
                return null;
            }
            GetColorOffsets(image1, out int blueOffset, out int greenOffset, out int redOffset);

            int totalPixels = image1.PixelWidth * image1.PixelHeight;
            int pixelSizeInBytes = image1.Format.BitsPerPixel / 8;
            byte* image1Index = (byte*)image1.BackBuffer.ToPointer();
            byte* image2Index = (byte*)image2.BackBuffer.ToPointer();
            byte[] differencePixels = new byte[totalPixels * pixelSizeInBytes];
            int differenceIndex = 0;
            for (int pixel = 0; pixel < totalPixels; ++pixel)
            {
                byte b = (byte)Math.Abs(*(image1Index + blueOffset) - *(image2Index + blueOffset));
                byte g = (byte)Math.Abs(*(image1Index + greenOffset) - *(image2Index + greenOffset));
                byte r = (byte)Math.Abs(*(image1Index + redOffset) - *(image2Index + redOffset));

                byte averageDifference = (byte)((b + g + r) / 3);
                differencePixels[differenceIndex + blueOffset] = averageDifference;
                differencePixels[differenceIndex + greenOffset] = averageDifference;
                differencePixels[differenceIndex + redOffset] = averageDifference;

                image1Index += pixelSizeInBytes;
                image2Index += pixelSizeInBytes;
                differenceIndex += pixelSizeInBytes;
            }

            WriteableBitmap difference = new(BitmapSource.Create(image1.PixelWidth, image1.PixelHeight, image1.DpiX, image1.DpiY, image1.Format, image1.Palette, differencePixels, image1.BackBufferStride));
            return difference;
        }
        #endregion

        #region Private methods
        private static bool BitmapsMismatched(WriteableBitmap image1, WriteableBitmap image2)
        {
            return (image1.PixelWidth != image2.PixelWidth) ||
                   (image1.PixelHeight != image2.PixelHeight) ||
                   (image1.Format != image2.Format);
        }

        private static byte DifferenceIfAboveThreshold(byte threshold, byte p, byte n)
        {
            return p > threshold && n > threshold ? (byte)((p + n) / 2) : Byte.MinValue;
        }

        private static void GetColorOffsets(WriteableBitmap image, out int blueOffset, out int greenOffset, out int redOffset)
        {
            if (image.Format == PixelFormats.Bgr24 ||
                image.Format == PixelFormats.Bgr32 ||
                image.Format == PixelFormats.Bgra32 ||
                image.Format == PixelFormats.Pbgra32)
            {
                blueOffset = 0;
                greenOffset = 1;
                redOffset = 2;
            }
            else if (image.Format == PixelFormats.Rgb24)
            {
                redOffset = 0;
                greenOffset = 1;
                blueOffset = 2;
            }
            else
            {
                throw new NotSupportedException($"Unhandled image format {image.Format}.");
            }
        }
        #endregion
    }
}
