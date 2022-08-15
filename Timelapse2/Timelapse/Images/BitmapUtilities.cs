using NReco.VideoConverter;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;

namespace Timelapse.Images
{
    public static class BitmapUtilities
    {
        #region Public - Get Bitmap from Image File
        // All bitmap laoding eventually invokes this static function
        public static BitmapSource GetBitmapFromImageFile(string filePath, Nullable<int> desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, out bool isCorruptOrMissing)
        {
            isCorruptOrMissing = true;

            // BitmapCacheOption.None is significantly faster than other options. 
            // However, it locks the file as it is being accessed (rather than a memory copy being created when using a cache)
            // This means we cannot do any file operations on it (such as deleting the currently displayed image) as it will produce an access violation.
            // This is ok for TransientLoading, which just temporarily displays the image
            BitmapCacheOption bitmapCacheOption = (displayIntent == ImageDisplayIntentEnum.Ephemeral) ? BitmapCacheOption.None : BitmapCacheOption.OnLoad;
            if (!System.IO.File.Exists(filePath))
            {
                return Constant.ImageValues.FileNoLongerAvailable.Value;
            }
            try
            {
                // Exception workarounds to consider: see  http://faithlife.codes/blog/2010/07/exceptions_thrown_by_bitmapimage_and_bitmapframe/ 
                if (desiredWidthOrHeight.HasValue == false)
                {
                    // returns the full size bitmap
                    BitmapFrame frame = BitmapFrame.Create(new Uri(filePath), BitmapCreateOptions.None, bitmapCacheOption);
                    frame.Freeze();
                    isCorruptOrMissing = false;
                    return frame;
                }

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (imageDimension == ImageDimensionEnum.UseWidth)
                {
                    bitmap.DecodePixelWidth = desiredWidthOrHeight.Value;
                }
                else
                {
                    bitmap.DecodePixelHeight = desiredWidthOrHeight.Value;
                }
                bitmap.CacheOption = bitmapCacheOption;
                bitmap.UriSource = new Uri(filePath);
                bitmap.EndInit();
                bitmap.Freeze();

                isCorruptOrMissing = false;
                return bitmap;
            }
            catch (Exception exception)
            {
                // Optional messages for eventual debugging of catch errors, 
                if (exception is InsufficientMemoryException)
                {
                    TracePrint.PrintMessage(String.Format("ImageRow/LoadBitmap: exception getting bitmap from file: {0}\n.** Insufficient Memory Exception: {1}.\n--------------\n**StackTrace: {2}.\nXXXXXXXXXXXXXX\n\n", filePath, exception.Message, exception.StackTrace));
                }
                else
                {
                    // TraceDebug.PrintMessage(String.Format("ImageRow/LoadBitmap: General exception: {0}\n.**Unknown exception getting bitmap from file: {1}.\n--------------\n**StackTrace: {2}.\nXXXXXXXXXXXXXX\n\n", filePath, exception.Message, exception.StackTrace));
                }
                isCorruptOrMissing = true;
                return Constant.ImageValues.Corrupt.Value;
            }
        }
        #endregion

        #region Public - Get Bitmap from Video File
        // Get the bitmap representing a video file
        // Note that displayIntent is ignored as it's specific to interaction with WCF's bitmap cache, which doesn't occur in rendering video preview frames
        public static BitmapSource GetBitmapFromVideoFile(string filePath, Nullable<int> desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, out bool isCorruptOrMissing)
        {
            if (!System.IO.File.Exists(filePath))
            {
                isCorruptOrMissing = true;
                return Constant.ImageValues.FileNoLongerAvailable.Value;
            }
            // Our FFMPEG installation is the 64 bit version. In case someone is using a 32 bit machine, we use the MediaEncoder instead.
            if (Environment.Is64BitOperatingSystem == false)
            {
                // System.Diagnostics.Debug.Print("Can't use ffmpeg as this is a 32 bit machine. Using MediaEncoder instead");
                return BitmapUtilities.GetVideoBitmapFromFileUsingMediaEncoder(filePath, desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
            }
            try
            {

                //Saul TO DO:
                // Note: not sure of the cost of creating a new converter every time. May be better to reuse it?
                Stream outputBitmapAsStream = new MemoryStream();
                FFMpegConverter ffMpeg = new NReco.VideoConverter.FFMpegConverter();
                ffMpeg.GetVideoThumbnail(filePath, outputBitmapAsStream);

                // Scale the video to the desired dimension
                outputBitmapAsStream.Position = 0;
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                if (desiredWidthOrHeight != null)
                {
                    if (imageDimension == ImageDimensionEnum.UseWidth)
                    {
                        bitmap.DecodePixelWidth = desiredWidthOrHeight.Value;
                    }
                    else
                    {
                        bitmap.DecodePixelHeight = desiredWidthOrHeight.Value;
                    }
                }
                bitmap.CacheOption = BitmapCacheOption.None;
                bitmap.StreamSource = outputBitmapAsStream;
                bitmap.EndInit();
                bitmap.Freeze();
                isCorruptOrMissing = false;
                return bitmap;
            }
            catch // (FFMpegException e)
            {
                // Couldn't get the thumbnail using FFMPEG. Fallback to try getting it using the MediaEncoder
                return GetVideoBitmapFromFileUsingMediaEncoder(filePath, desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
                // We don't print the exception // (Exception exception)
                // TraceDebug.PrintMessage(String.Format("VideoRow/LoadBitmap: Loading of {0} failed in Video - LoadBitmap. {0}", imageFolderPath));
            }
        }

        // This alternate way to get an image from a video file used the media encoder. 
        // While it works, its ~twice as slow as using NRECO FFMPeg.
        // We do include it as a fallback for the odd case where ffmpeg doesn't work (I had that with a single video).
        public static BitmapSource GetVideoBitmapFromFileUsingMediaEncoder(string filePath, Nullable<int> desiredWidth, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum _, out bool isCorruptOrMissing)
        {
            isCorruptOrMissing = true;
            // System.Diagnostics.Debug.Print("FFMPEG failed for some reason, so using MediaEncoder Instead on " + filePath);

            if (!System.IO.File.Exists(filePath))
            {
                return Constant.ImageValues.FileNoLongerAvailable.Value;
            }

            MediaPlayer mediaPlayer = new MediaPlayer
            {
                Volume = 0.0
            };
            try
            {
                // In this method, we open  mediaplayer and play it until we actually get a video frame.
                // Unfortunately, its very time inefficient...
                mediaPlayer.Open(new Uri(filePath));
                mediaPlayer.Play();

                // MediaPlayer is not actually synchronous despite exposing synchronous APIs, so wait for it get the video loaded.  Otherwise
                // the width and height properties are zero and only black pixels are drawn.  The properties will populate with just a call to
                // Open() call but without also Play() only black is rendered

                // TODO Rapidly show videos as it is too slow now, where:
                // - ONLOAD It currently loads a blank video image when scouring thorugh the videos 
                // - Rapid navigation: loads a blank video image in the background, then the video on pause
                // - Multiview: very slow as only loads the  video.
                // This will be fixed when we pre-process thumbnails
                int timesTried = (displayIntent == ImageDisplayIntentEnum.Persistent) ? 1000 : 0;
                while ((mediaPlayer.NaturalVideoWidth < 1) || (mediaPlayer.NaturalVideoHeight < 1))
                {
                    // back off briefly to let MediaPlayer do its loading, which typically takes perhaps 75ms
                    // a brief Sleep() is used rather than Yield() to reduce overhead as 500k to 1M+ yields typically occur
                    Thread.Sleep(Constant.ThrottleValues.PollIntervalForVideoLoad);
                    if (timesTried-- <= 0)
                    {
                        isCorruptOrMissing = false;
                        mediaPlayer.Stop();
                        return BitmapUtilities.GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
                    }
                }

                // sleep one more time as MediaPlayer has a tendency to still return black frames for a moment after the width and height have populated
                Thread.Sleep(Constant.ThrottleValues.PollIntervalForVideoLoad);

                int pixelWidth = mediaPlayer.NaturalVideoWidth;
                int pixelHeight = mediaPlayer.NaturalVideoHeight;
                if (desiredWidth.HasValue)
                {
                    double scaling = desiredWidth.Value / (double)pixelWidth;
                    pixelWidth = (int)(scaling * pixelWidth);
                    pixelHeight = (int)(scaling * pixelHeight);
                }

                // set up to render frame from the video
                mediaPlayer.Pause();
                mediaPlayer.Position = TimeSpan.FromMilliseconds(1.0);

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawVideo(mediaPlayer, new Rect(0, 0, pixelWidth, pixelHeight));
                }

                // render and check for black frame
                // it's assumed the camera doesn't yield all black frames
                for (int renderAttempt = 1; renderAttempt <= Constant.ThrottleValues.MaximumRenderAttempts; ++renderAttempt)
                {
                    // try render
                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Default);
                    renderBitmap.Render(drawingVisual);
                    renderBitmap.Freeze();

                    // check if render succeeded
                    // hopefully it did and most of the overhead here is WriteableBitmap conversion though, at 2-3ms for a 1280x720 frame, this 
                    // is not an especially expensive operation relative to the  O(175ms) cost of this function
                    WriteableBitmap writeableBitmap = renderBitmap.AsWriteable();
                    if (writeableBitmap.IsBlack() == false)
                    {
                        // if the media player is closed before Render() only black is rendered
                        // TraceDebug.PrintMessage(String.Format("Video render returned a non-black frame after {0} times.", renderAttempt - 1));
                        mediaPlayer.Close();
                        isCorruptOrMissing = false;
                        mediaPlayer.Stop();
                        return writeableBitmap;
                    }
                    // black frame was rendered; backoff slightly to try again
                    Thread.Sleep(TimeSpan.FromMilliseconds(Constant.ThrottleValues.VideoRenderingBackoffTime.TotalMilliseconds));
                }
                // We failed, so just return a blank video.
                mediaPlayer.Stop();
                return BitmapUtilities.GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
                //throw new ApplicationException(String.Format("Limit of {0} render attempts was reached.", Constant.ThrottleValues.MaximumRenderAttempts));
            }
            catch
            {
                // We don't print the exception // (Exception exception)
                // TraceDebug.PrintMessage(String.Format("VideoRow/LoadBitmap: Loading of {0} failed in Video - LoadBitmap. {0}", imageFolderPath));
                mediaPlayer.Stop();
                return BitmapUtilities.GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
            }
        }
        #endregion

        #region Public - Get Bitmap from Image File with play button drawn centered on it
        // This just overlays a Play button atop a bitmap image (note that the path must be to a valid image, not video)
        // For now, it is only used with "pack://application:,,,/Resources/BlankVideo.jpg as the path argument.
        // SAULXXX: Modify, as not needed.
        // Either hard-code all this (or - even simpler - just create blank video with a play button. I think this function is a hangover of when I wanted to 
        // put a big play button in the middle of any video thumbnail, so I wrote a general purpose method to do it. BUt I don't need that any more.
        // Still, it at least shows how to draw atop a bitmap.
        public static BitmapSource GetBitmapFromFileWithPlayButton(string path, Nullable<int> desiredWidth = null, ImageDisplayIntentEnum displayIntent = ImageDisplayIntentEnum.Persistent)
        {
            BitmapSource bmp = BitmapUtilities.GetBitmapFromImageFile(path, desiredWidth, displayIntent, ImageDimensionEnum.UseWidth, out _);
            RenderTargetBitmap target = new RenderTargetBitmap(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, PixelFormats.Pbgra32);
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext r = visual.RenderOpen())
            {
                float radius = 20;

                // We will draw based on the center of the bitmap
                Point center = new Point(bmp.Width / 2, bmp.Height / 2);
                PointCollection trianglePoints = GetTriangleVerticesInscribedInCircle(center, radius);

                // Construct the triangle
                StreamGeometry triangle = new StreamGeometry();
                using (StreamGeometryContext geometryContext = triangle.Open())
                {
                    geometryContext.BeginFigure(trianglePoints[0], true, true);
                    PointCollection points = new PointCollection
                                             {
                                                trianglePoints[1],
                                                trianglePoints[2]
                                             };
                    geometryContext.PolyLineTo(points, true, true);
                }

                // Define the translucent bruches for the triangle an circle
                SolidColorBrush triangleBrush = new SolidColorBrush(Colors.LightBlue)
                {
                    Opacity = 0.5
                };

                SolidColorBrush circleBrush = new SolidColorBrush(Colors.White)
                {
                    Opacity = 0.5
                };

                // Draw everything
                r.DrawImage(bmp, new Rect(0, 0, bmp.Width, bmp.Height));
                r.DrawGeometry(triangleBrush, null, triangle);
                r.DrawEllipse(circleBrush, null, center, radius + 5, radius + 5);
            }
            target.Render(visual);
            return target;
        }

        // Return  3 points (vertices) that inscribe a triangle into the circle defined by a center point and a radius, 
        public static PointCollection GetTriangleVerticesInscribedInCircle(Point center, float radius)
        {
            PointCollection points = new PointCollection();
            for (int i = 0; i < 3; i++)
            {
                Point v = new Point
                {
                    X = center.X + radius * (float)Math.Cos(i * 2 * Math.PI / 3),
                    Y = center.Y + radius * (float)Math.Sin(i * 2 * Math.PI / 3)
                };
                points.Add(v);
            }
            return points;
        }
        #endregion

        #region Public -  Bitmap tests: IsBitmapFileDisplayable,  GetBitmapAspectRatioFromImageFile
        // Return true only if the file exists and we can actually create a bitmap image from it
        public static bool IsBitmapFileDisplayable(string path)
        {
            if (File.Exists(path) == false)
            {
                return false;
            }
            try
            {
                // we assume its a valid path
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = 1; // We try to generate a trivial thumbnail, as that suffices to know if this is a valid jpg;
                bitmap.DecodePixelHeight = 1; // We try to generate a trivial thumbnail, as that suffices to know if this is a valid jpg;
                // TODO Check this, as we changed the bitmap cache options from Default to OnLoad to ensure file deletions would work
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Return the aspect ratio (as Width/Height) of an image file
        /// </summary>
        public static double GetBitmapAspectRatioFromImageFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return Constant.ImageValues.FileNoLongerAvailable.Value.Width / Constant.ImageValues.FileNoLongerAvailable.Value.Height;
            }
            try
            {
                // Timing tests: this is fast i.e., 0 - 10 msecs by using the options below
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = 0;
                bitmap.CacheOption = BitmapCacheOption.None;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.UriSource = new Uri(filePath);
                bitmap.EndInit();
                bitmap.Freeze();
                return (bitmap.Width / bitmap.Height);
            }
            catch
            {
                return Constant.ImageValues.Corrupt.Value.Width / Constant.ImageValues.Corrupt.Value.Height;
            }
        }
        #endregion
    }
}
