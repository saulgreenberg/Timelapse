using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using NReco.VideoConverter;
using Timelapse.Constant;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Util;
using Directory = MetadataExtractor.Directory;
using File = System.IO.File;
using Point = System.Windows.Point;

namespace Timelapse.Images
{
    public static class BitmapUtilities
    {
        #region Public - Get Bitmap from Image File
        // All bitmap laoding eventually invokes this static function
        public static BitmapSource GetBitmapFromImageFile(string filePath, int? desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, out bool isCorruptOrMissing)
        {
            isCorruptOrMissing = true;

            // BitmapCacheOption.None is significantly faster than other options. 
            // However, it locks the file as it is being accessed (rather than a memory copy being created when using a cache)
            // This means we cannot do any file operations on it (such as deleting the currently displayed image) as it will produce an access violation.
            // This is ok for TransientLoading, which just temporarily displays the image
            BitmapCacheOption bitmapCacheOption = (displayIntent == ImageDisplayIntentEnum.Ephemeral) ? BitmapCacheOption.None : BitmapCacheOption.OnLoad;
            if (IsCondition.IsPathLengthTooLong(filePath, FilePathTypeEnum.DisplayFile))
            {
                // We check this first as 'exists' will return false on a path too long error, and we want to display the correct bitmap
                return ImageValues.FilePathTooLong.Value;
            }
            if (!File.Exists(filePath))
            {
                return ImageValues.FileNoLongerAvailable.Value;
            }
            try
            {
                // Exception workarounds to consider: see  http://faithlife.codes/blog/2010/07/exceptions_thrown_by_bitmapimage_and_bitmapframe/ 
                if (desiredWidthOrHeight.HasValue == false)
                {
                    // returns the full size bitmap
                    BitmapFrame frame = BitmapFrame.Create(new Uri(filePath), BitmapCreateOptions.None, bitmapCacheOption);
                    // See if the image needs rotation. 
                    BitmapSource newFrame = RotateBitmapIfNeeded(frame);
                    newFrame.Freeze();
                    isCorruptOrMissing = false;
                    return newFrame;
                }

                BitmapImage bitmap = new();
                MetadataExtractorGetOrientation(filePath, out _, out Rotation rotation, out _);
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
                bitmap.UriSource = new(filePath);
                bitmap.Rotation = rotation;
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
                    TracePrint.PrintMessage(
                        $"ImageRow/LoadBitmap: exception getting bitmap from file: {filePath}\n.** Insufficient Memory Exception: {exception.Message}.\n--------------\n**StackTrace: {exception.StackTrace}.\nXXXXXXXXXXXXXX\n\n");
                }

                // TraceDebug.PrintMessage(String.Format("ImageRow/LoadBitmap: General exception: {0}\n.**Unknown exception getting bitmap from file: {1}.\n--------------\n**StackTrace: {2}.\nXXXXXXXXXXXXXX\n\n", filePath, exception.Message, exception.StackTrace));
                isCorruptOrMissing = true;
                return ImageValues.Corrupt.Value;
            }
        }
        #endregion

        #region Public - Get Bitmap from Video File
        // Get the bitmap representing a video file
        // Note that displayIntent is ignored as it's specific to interaction with WCF's bitmap cache, which doesn't occur in rendering video preview frames
        public static BitmapSource GetBitmapFromVideoFile(string filePath, int? desiredWidthOrHeight, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum imageDimension, float? frameTime, out bool isCorruptOrMissing)
        {
            if (IsCondition.IsPathLengthTooLong(filePath, FilePathTypeEnum.DisplayFile))
            {
                isCorruptOrMissing = true;
                return ImageValues.FilePathTooLong.Value;
            }
            if (!File.Exists(filePath))
            {
                isCorruptOrMissing = true;
                return ImageValues.FileNoLongerAvailable.Value;
            }

            // Our FFMPEG installation is the 64 bit version. In case someone is using a 32 bit machine, we use the MediaEncoder instead.
            if (Environment.Is64BitOperatingSystem == false)
            {
                // Debug.Print("Can't use ffmpeg as this is a 32 bit machine. Using MediaEncoder instead");
                return GetVideoBitmapFromFileUsingMediaEncoder(filePath, desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
            }

            try
            {

                //Saul TO DO:
                // Note: not sure of the cost of creating a new converter every time. May be better to reuse it?
                Stream outputBitmapAsStream = new MemoryStream();
                FFMpegConverter ffMpeg = new();

                // IMPORTANT: Set the ffmpeg tool directory to the application's directory.
                // For MSI installations in Program Files, we need to copy ffmpeg.exe to a user-writable
                // location because Windows restricts execution permissions in Program Files.
                // For per-user installations (e.g., AppData), we can use the installation directory directly.
                // This fixes two issues:
                // 1. Working directory may be different (e.g., C:\Windows\System32)
                // 2. Program Files requires admin privileges for file operations
                try
                {
                    string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
                    if (dir != null)
                    {
                        string sourceFFmpegPath = Path.Combine(dir, "ffmpeg.exe");

                        // Check if the installation directory is writable by attempting to create a test file
                        bool isInstallDirWritable = false;
                        try
                        {
                            string testFile = Path.Combine(dir, $"test_{Guid.NewGuid()}.tmp");
                            File.WriteAllText(testFile, "test");
                            File.Delete(testFile);
                            isInstallDirWritable = true;
                        }
                        catch
                        {
                            // Installation directory is not writable (likely Program Files)
                        }

                        if (isInstallDirWritable && File.Exists(sourceFFmpegPath))
                        {
                            // Installation directory is writable (probably per-user install in AppData)
                            // Use it directly - no need to copy to temp
                            ffMpeg.FFMpegToolPath = dir;
                        }
                        else
                        {
                            // Installation directory is read-only (probably installed in C:\Program Files
                            // which is non-writeable)
                            // Copy to temp directory that the user has write access to
                            string tempFFmpegDir = Path.Combine(Path.GetTempPath(), "Timelapse", "ffmpeg");
                            string tempFFmpegPath = Path.Combine(tempFFmpegDir, "ffmpeg.exe");

                            // Create temp directory if needed
                            if (!System.IO.Directory.Exists(tempFFmpegDir))
                            {
                                System.IO.Directory.CreateDirectory(tempFFmpegDir);
                            }

                            // Copy ffmpeg.exe to temp location if not already there or if source is newer
                            if (File.Exists(sourceFFmpegPath) &&
                                (!File.Exists(tempFFmpegPath) ||
                                 File.GetLastWriteTime(sourceFFmpegPath) > File.GetLastWriteTime(tempFFmpegPath)))
                            {
                                File.Copy(sourceFFmpegPath, tempFFmpegPath, true);
                            }

                            // Point NReco to the temp directory
                            ffMpeg.FFMpegToolPath = File.Exists(tempFFmpegPath) 
                                ? tempFFmpegDir
                                // Fallback: try using the installation directory directly
                                : dir;
                        }
                    }
                }
                catch
                {
                    // If we can't determine the path, let NReco use its default search mechanism
                }

                if (frameTime == null || frameTime == 0 || float.IsNaN((float)frameTime))
                {
                    ffMpeg.GetVideoThumbnail(filePath, outputBitmapAsStream);
                }
                else
                {
                    // Get a particular frame in the video by its time.
                    // We also test to ensure that the given time is within the actual video duration as otherwise
                    // it would cause an exception. Note that GetVideoDuration returns null if it can't get the actual duration
                    float? actualDuration = FilesFolders.GetVideoDuration(filePath);
                    if (null != actualDuration && actualDuration != 0 && frameTime <= actualDuration)
                    {
                       ffMpeg.GetVideoThumbnail(filePath, outputBitmapAsStream, frameTime);
                    }
                    else
                    {
                        // As we have no valid time, just get the first frame
                        ffMpeg.GetVideoThumbnail(filePath, outputBitmapAsStream);
                    }
                }

                // Scale the video to the desired dimension
                outputBitmapAsStream.Position = 0;
                BitmapImage bitmap = new();
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
            catch
            {
                // Couldn't get the thumbnail using FFMPEG. Fallback to try getting it using the MediaEncoder
                // Note. One of the reasons for this failure can occur if we call ffMpeg.GetVideoThumbnail(filePath, outputBitmapAsStream, frameTime);
                // with a frame time longer than the video.
                return GetVideoBitmapFromFileUsingMediaEncoder(filePath, desiredWidthOrHeight, displayIntent, imageDimension, out isCorruptOrMissing);
                // We don't print the exception // (Exception exception)
                // TraceDebug.PrintMessage(String.Format("VideoRow/LoadBitmap: Loading of {0} failed in Video - LoadBitmap. {0}", imageFolderPath));
            }
        }
        
        // This alternate way to get an image from a video file used the media encoder. 
        // While it works, its ~twice as slow as using NRECO FFMPeg.
        // We do include it as a fallback for the odd case where ffmpeg doesn't work (I had that with a single video).
        public static BitmapSource GetVideoBitmapFromFileUsingMediaEncoder(string filePath, int? desiredWidth, ImageDisplayIntentEnum displayIntent, ImageDimensionEnum _, out bool isCorruptOrMissing)
        {
            isCorruptOrMissing = true;
            // Debug.Print("FFMPEG failed for some reason, so using MediaEncoder Instead on " + filePath);

            if (IsCondition.IsPathLengthTooLong(filePath, FilePathTypeEnum.DisplayFile))
            {
                isCorruptOrMissing = true;
                return ImageValues.FilePathTooLong.Value;
            }

            if (!File.Exists(filePath))
            {
                return ImageValues.FileNoLongerAvailable.Value;
            }

            MediaPlayer mediaPlayer = new()
            {
                Volume = 0.0
            };
            try
            {
                // In this method, we open  mediaplayer and play it until we actually get a video frame.
                // Unfortunately, its very time inefficient...
                mediaPlayer.Open(new(filePath));
                mediaPlayer.Play();

                // MediaPlayer is not actually synchronous despite exposing synchronous APIs, so wait for it get the video loaded.  Otherwise
                // the width and height properties are zero and only black pixels are drawn.  The properties will populate with just a call to
                // Open() call but without also Play() only black is rendered
                int timesTried = (displayIntent == ImageDisplayIntentEnum.Persistent) ? 1000 : 0;
                while ((mediaPlayer.NaturalVideoWidth < 1) || (mediaPlayer.NaturalVideoHeight < 1))
                {
                    // back off briefly to let MediaPlayer do its loading, which typically takes perhaps 75ms
                    // a brief Sleep() is used rather than Yield() to reduce overhead as 500k to 1M+ yields typically occur
                    Thread.Sleep(ThrottleValues.PollIntervalForVideoLoad);
                    if (timesTried-- <= 0)
                    {
                        isCorruptOrMissing = false;
                        mediaPlayer.Stop();
                        return GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
                    }
                }

                // sleep one more time as MediaPlayer has a tendency to still return black frames for a moment after the width and height have populated
                Thread.Sleep(ThrottleValues.PollIntervalForVideoLoad);

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

                DrawingVisual drawingVisual = new();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawVideo(mediaPlayer, new(0, 0, pixelWidth, pixelHeight));
                }

                // render and check for black frame
                // it's assumed the camera doesn't yield all black frames
                for (int renderAttempt = 1; renderAttempt <= ThrottleValues.MaximumRenderAttempts; ++renderAttempt)
                {
                    // try render
                    RenderTargetBitmap renderBitmap = new(pixelWidth, pixelHeight, 96, 96, PixelFormats.Default);
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
                    Thread.Sleep(TimeSpan.FromMilliseconds(ThrottleValues.ProgressBarSleepInterval.TotalMilliseconds));
                }
                // We failed, so just return a blank video.
                mediaPlayer.Stop();
                return GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
                //throw new ApplicationException(String.Format("Limit of {0} render attempts was reached.", Constant.ThrottleValues.MaximumRenderAttempts));
            }
            catch
            {
                // We don't print the exception // (Exception exception)
                // TraceDebug.PrintMessage(String.Format("VideoRow/LoadBitmap: Loading of {0} failed in Video - LoadBitmap. {0}", imageFolderPath));
                mediaPlayer.Stop();
                return GetBitmapFromFileWithPlayButton("pack://application:,,,/Resources/BlankVideo.jpg", desiredWidth);
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
        public static BitmapSource GetBitmapFromFileWithPlayButton(string path, int? desiredWidth = null, ImageDisplayIntentEnum displayIntent = ImageDisplayIntentEnum.Persistent)
        {
            BitmapSource bmp = GetBitmapFromImageFile(path, desiredWidth, displayIntent, ImageDimensionEnum.UseWidth, out _);
            RenderTargetBitmap target = new(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, PixelFormats.Pbgra32);
            DrawingVisual visual = new();

            using (DrawingContext r = visual.RenderOpen())
            {
                float radius = 20;

                // We will draw based on the center of the bitmap
                Point center = new(bmp.Width / 2, bmp.Height / 2);
                PointCollection trianglePoints = GetTriangleVerticesInscribedInCircle(center, radius);

                // Construct the triangle
                StreamGeometry triangle = new();
                using (StreamGeometryContext geometryContext = triangle.Open())
                {
                    geometryContext.BeginFigure(trianglePoints[0], true, true);
                    PointCollection points =
                    [
                        trianglePoints[1],
                        trianglePoints[2]
                    ];
                    geometryContext.PolyLineTo(points, true, true);
                }

                // Define the translucent bruches for the triangle an circle
                SolidColorBrush triangleBrush = new(Colors.LightBlue)
                {
                    Opacity = 0.5
                };

                SolidColorBrush circleBrush = new(Colors.White)
                {
                    Opacity = 0.5
                };

                // Draw everything
                r.DrawImage(bmp, new(0, 0, bmp.Width, bmp.Height));
                r.DrawGeometry(triangleBrush, null, triangle);
                r.DrawEllipse(circleBrush, null, center, radius + 5, radius + 5);
            }
            target.Render(visual);
            return target;
        }

        // Return  3 points (vertices) that inscribe a triangle into the circle defined by a center point and a radius, 
        public static PointCollection GetTriangleVerticesInscribedInCircle(Point center, float radius)
        {
            PointCollection points = [];
            for (int i = 0; i < 3; i++)
            {
                Point v = new()
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
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = 1; // We try to generate a trivial thumbnail, as that suffices to know if this is a valid jpg;
                bitmap.DecodePixelHeight = 1; // We try to generate a trivial thumbnail, as that suffices to know if this is a valid jpg;
                // I changed the bitmap cache options from Default to OnLoad to ensure file deletions would work
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new(path);
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
            if (!File.Exists(filePath))
            {
                return ImageValues.FileNoLongerAvailable.Value.Width / ImageValues.FileNoLongerAvailable.Value.Height;
            }
            try
            {
                // Timing tests: this is fast i.e., 0 - 10 msecs by using the options below
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = 0;
                bitmap.CacheOption = BitmapCacheOption.None;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.UriSource = new(filePath);
                bitmap.EndInit();
                bitmap.Freeze();
                return (bitmap.Width / bitmap.Height);
            }
            catch
            {
                return ImageValues.Corrupt.Value.Width / ImageValues.Corrupt.Value.Height;
            }
        }
        #endregion

        #region Manage the Exif Orientation flag in images
        // Check if the orientaton flag is set in the metadata and, if so,
        // rotate the image as needed and return it. Otherwise just return the image that was passed in
        public static BitmapSource RotateBitmapIfNeeded(BitmapFrame frame)
        {
            string _orientationQuery = "System.Photo.Orientation";
            if (frame.Metadata is BitmapMetadata bitmapMetadata && bitmapMetadata.ContainsQuery(_orientationQuery))
            {
                object o = bitmapMetadata.GetQuery(_orientationQuery);
                if (o != null)
                {
                    int angle;
                    switch ((ushort)o)
                    {
                        case 6:
                            angle = 90;
                            break;
                        case 3:
                            angle = 180;
                            break;
                        case 8:
                            angle = 270;
                            break;
                        default:
                            angle = 0;
                            break;
                    }

                    if (angle != 0)
                    {
                        // rotate the bitmap to the desired angle
                        TransformedBitmap tb = new();
                        tb.BeginInit();
                        tb.Source = frame;
                        RotateTransform transform = new(angle);
                        tb.Transform = transform;
                        tb.EndInit();
                        return tb;
                    }
                }
            }
            return frame;
        }

        // Unused but keep for now in case it becomes useful at some point
        //public static int RotateBitmapGetAngle(BitmapFrame frame)
        //{
        //    string _orientationQuery = "System.Photo.Orientation";
        //    int angle = 0;
        //    if (frame.Metadata is BitmapMetadata bitmapMetadata && bitmapMetadata.ContainsQuery(_orientationQuery))
        //    {
        //        object o = bitmapMetadata.GetQuery(_orientationQuery);

        //        if (o != null)
        //        {
        //            switch ((ushort)o)
        //            {
        //                case 6:
        //                    angle = 90;
        //                    break;
        //                case 3:
        //                    angle = 180;
        //                    break;
        //                case 8:
        //                    angle = 270;
        //                    break;
        //                default:
        //                    angle = 0;
        //                    break;
        //            }
        //        }
        //    }
        //    return angle;
        //}

        // Given a file, return the orientation angle via the arguments in various formats, with 0 as the default (and false) if problems happen.
        public static bool MetadataExtractorGetOrientation(string filePath, out int angle, out Rotation rotation, out RotateFlipType rotateFlip)
        {
            // Default values
            rotateFlip = RotateFlipType.RotateNoneFlipNone;
            rotation = Rotation.Rotate0;
            angle = 0;

            // Use metadata extractor to get the orientation metadata from the file, if it exists.
            if (false == File.Exists(filePath))
            {
                return false;
            }

            try
            {
                // Use metadataextractor to read the orientation flag
                IEnumerable<Directory> directories = ImageMetadataReader.ReadMetadata(filePath);
                ExifIfd0Directory ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0Directory == null)
                {
                    return false;
                }

                int orientation = ifd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int value) ? value : -1;
                switch (orientation)
                {
                    case 6:
                        rotateFlip = RotateFlipType.Rotate90FlipNone;
                        rotation = Rotation.Rotate90;
                        angle = 90;
                        return true;
                    case 3:
                        rotateFlip = RotateFlipType.Rotate180FlipNone;
                        rotation = Rotation.Rotate180;
                        angle = 180;
                        return true;
                    case 8:
                        rotateFlip = RotateFlipType.Rotate270FlipNone;
                        rotation = Rotation.Rotate270;
                        angle = 270;
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
