using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;
using TimelapseWpf.Toolkit.Core.Input;
using TimelapseWpf.Toolkit.Zoombox;
using MarkableCanvas = Timelapse.Images.MarkableCanvas;

namespace Timelapse.Controls
{
    // Create a popup that displays images surrounding the current image as long as they belong in the current episode.
    // It alternates between the left / right of the current image (shown as a '^' marker) but stops on the side where the
    // episode limit is reached, then filling the other side. If there are no images in the episode, only the '^' marker will be displayed.
    // Importantly, the images on either side are chosen from the order that images were loaded, thus ignoring select and sort criteria.
    // It is sensitive to:
    // - whether images were initially loaded in time-order - if not, then the left/right images may not be the ones in the episode
    // - whether images to the left/right were deleted, as the subsequent images may have a time difference greater than the threshold.
    public class EpisodePopup : Popup
    {
        #region Public properties and private variables
        // We need to access the FileDatabase to get file tables, do selects, etc.
        // This is normally set in the constructor. However, if the image set is closed and another one loaded,
        // it is reset by an external method
        public FileDatabase FileDatabase { get; set; }

        private double ImageHeight { get; }
        private readonly TimelapseWindow timelapseWindow = GlobalReferences.MainWindow;
        private readonly MarkableCanvas markableCanvas;

        // A popup allowing us to inspect a popup in detail
        private readonly Popup InspectionPopup = new();
        #endregion

        #region Constructor
        public EpisodePopup(MarkableCanvas markableCanvas, FileDatabase fileDatabase, double imageHeight)
        {
            this.markableCanvas = markableCanvas;
            FileDatabase = fileDatabase;
            ImageHeight = imageHeight;

            Placement = PlacementMode.Bottom;
            PlacementTarget = markableCanvas;
            IsOpen = false;
        }
        #endregion

        #region Public Show or hide the popup, where we display up to the maxNumberImagesToDisplay
        public void Show(bool isVisible, int maxNumberImagesToDisplay)
        {
            if (timelapseWindow == null)
            {
                TracePrint.NullException(nameof(timelapseWindow));
                return;
            }
            TimeSpan timeThreshold = timelapseWindow.State.EpisodeTimeThreshold;
            ImageRow currentImageRow = timelapseWindow?.DataHandler?.ImageCache?.Current;
            if (isVisible == false || currentImageRow == null || FileDatabase == null)
            {
                // Hide the popup if asked or if basic data isn't available, including deleting the children
                IsOpen = false;
                Child = null;
                InspectionPopup.IsOpen = false;
                return;
            }

            // Images or placeholders will be contained in a horizontal stack panel, which in turn is the popup's child
            StackPanel sp = new()
            {
                Orientation = Orientation.Horizontal
            };
            Child = sp;


            double width = 0;  // Used to calculate the placement offset of the popup relative to the placement target

            // Add a visual marker to show the position of the label in the image list
            Label label = CreateLabel("^", ImageHeight);
            label.VerticalAlignment = VerticalAlignment.Top;
            width += label.Width;
            double height = ImageHeight;
            sp.Children.Add(label);

            int margin = 2;

            // We will only consider images whose relative path is the same as the current file
            string relativePath = currentImageRow.RelativePath;

            // Calculate the lower and upper extent of the range of dates we should examine
            // The maximum date range we need to consider would be the current date plus/minus the (time threshold * the number of images we could display),
            // While this could produce more hits than we need, it should give us a relatively short table of possible candidates
            DateTime lowerDateTime = currentImageRow.DateTime - TimeSpan.FromTicks(timeThreshold.Ticks * maxNumberImagesToDisplay);
            DateTime upperDateTime = currentImageRow.DateTime + TimeSpan.FromTicks(timeThreshold.Ticks * maxNumberImagesToDisplay);
            string slowerDateTime = DateTimeHandler.ToStringDatabaseDateTime(lowerDateTime);
            string supperDateTime = DateTimeHandler.ToStringDatabaseDateTime(upperDateTime);

            // Get a table of files (sorted by datetime) with that relative path which falls between the lower and upper date range
            DataTable dt = FileDatabase.GetIDandDateWithRelativePathAndBetweenDates(relativePath, slowerDateTime, supperDateTime);

            // Find the current image in that table by its ID
            int rowWithCurrentImageRowID = -1;
            int availableRows = dt.Rows.Count;
            for (int i = 0; i < availableRows; i++)
            {
                if (Convert.ToInt64(dt.Rows[i][0]) == currentImageRow.ID)
                {
                    rowWithCurrentImageRowID = i;
                    break;
                }
            }

            // From that current image, alternate between going to the previous/next row.
            // If the date difference between alternating successive images is less than the time threshold, 
            // display it. 
            int goBackwardsRow = rowWithCurrentImageRowID - 1;
            int goForwardsRow = rowWithCurrentImageRowID + 1;
            int imagesLeftToDisplay = maxNumberImagesToDisplay;
            DateTime lastBackwardsDateTime = currentImageRow.DateTime;
            DateTime lastForwardsDateTime = currentImageRow.DateTime;
            while (goBackwardsRow >= 0 || goForwardsRow < availableRows)
            {
                // Abort when there is no more work to do
                if (imagesLeftToDisplay <= 0)
                {
                    break;
                }

                // Start on the left
                FileTable fileTable; // To hold the results of the database selection as a table of ImageRows
                if (goBackwardsRow >= 0)
                {
                    // Add a popup image to the left of the caret
                    using (fileTable = FileDatabase.SelectFileInDataTableById(dt.Rows[goBackwardsRow][0].ToString()))
                    {
                        if (fileTable.Any())
                        {
                            if ((lastBackwardsDateTime - fileTable[0].DateTime).Duration() <= timeThreshold)
                            {
                                // Get the bounding boxes, if any, and create an image.
                                // If its a video, it will generate the image at the InitialVideoFrame 
                                // Note time will be 0 if there are no bounding boxes, as InitialVideoFrame will default to 0
                                Tuple<Image, BoundingBoxes> tuple = GetBoundingBoxesAndImage(fileTable[0], margin, this.ImageHeight, fileTable[0].IsVideo);
                                BoundingBoxes boundingBoxes = tuple.Item2;
                                Image image = tuple.Item1;
                                width += image.Source.Width;
                                height = Math.Max(height, image.Source.Height);
                               
                                // Create a canvas containing the image and bounding boxes (if detections are on)
                                Canvas canvas = CreateCanvasWithBoundingBoxesAndImage(boundingBoxes, image, height, fileTable[0].IsVideo);
                                canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
                                canvas.Tag = fileTable[0];
                                sp.Width = Double.NaN;
                                sp.Height = Double.NaN;
                                sp.Children.Insert(0, canvas);
                                imagesLeftToDisplay--;
                                lastBackwardsDateTime = fileTable[0].DateTime;
                            }
                            else
                            {
                                // Stop searching backwards
                                goBackwardsRow = -1;
                            }
                        }
                    }
                    goBackwardsRow--;
                }

                // Now try to add a popup image to the right if we still have some more  images left to display
                if (goForwardsRow < availableRows && imagesLeftToDisplay > 0)
                {
                    using (fileTable = FileDatabase.SelectFileInDataTableById(dt.Rows[goForwardsRow][0].ToString()))
                    {
                        if (fileTable.Any())
                        {
                            if ((lastForwardsDateTime - fileTable[0].DateTime).Duration() <= timeThreshold)
                            {
                                // Get the bounding boxes, if any, and create an image.
                                // If its a video, it will generate the image at the InitialVideoFrame 
                                // Note time will be 0 if there are no bounding boxes, as InitialVideoFrame will default to 0
                                Tuple<Image, BoundingBoxes> tuple = GetBoundingBoxesAndImage(fileTable[0], margin, this.ImageHeight, fileTable[0].IsVideo);
                                Image image = tuple.Item1;
                                BoundingBoxes boundingBoxes = tuple.Item2;
                                width += image.Source.Width;
                                height = Math.Max(height, image.Source.Height);

                                // Create a canvas containing the image and bounding box
                                Canvas canvas = CreateCanvasWithBoundingBoxesAndImage(boundingBoxes, image, height, fileTable[0].IsVideo);
                                canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
                                canvas.Tag = fileTable[0];
                                sp.Children.Add(canvas);
                                imagesLeftToDisplay--;
                                lastBackwardsDateTime = fileTable[0].DateTime;
                            }
                            else
                            {
                                // Stop searching forwards
                                goForwardsRow = availableRows;
                            }
                        }
                    }
                    goForwardsRow++;
                }
            }

            label.Height = Math.Max(label.Height, height); // So it extends to the top of the popup
            // Position and open the popup so it appears horizontallhy centered just above the cursor
            HorizontalOffset = markableCanvas.ActualWidth / 2.0 - width / 2.0;
            VerticalOffset = -height - 2 * margin;
            IsOpen = true;

            // Cleanup
            dt.Dispose();
        }

        // Used by the above.
        // Get the bounding boxes, if any, and create an image.
        // If its a video,
        // - it will reset the bounding box's initial video frame to 0 if that frame is outside the actual video duration
        // - it will generate the image at the InitialVideoFrame 
        // If the video file is missing:
        // - it will keep the bounding box at its original frame
        // If the file is not a video
        // - it will reset the initial frame to 0

        private static Tuple<Image, BoundingBoxes> GetBoundingBoxesAndImage(ImageRow imageRow, int margin, double height, bool isVideo)
        {
            float? actualDuration = null;
            float? frameTime = 0;

            if (isVideo)
            {
                string filePath = FilesFolders.GetFullPath(GlobalReferences.MainWindow.DataHandler.FileDatabase, imageRow);
                actualDuration = FilesFolders.GetVideoDuration(filePath);
            }

            BoundingBoxes boundingBoxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(imageRow.ID, isVideo);
            if (false == actualDuration.HasValue)
            {
                // the file is not a video.
                boundingBoxes.InitialVideoFrame = 0;
            }
            else if (actualDuration < 0)
            {
                // the file is missing
                // NOOP
            }
            else //if (actualDuration.HasValue)
            {
                frameTime = boundingBoxes.InitialVideoFrame / boundingBoxes.FrameRate;
                if (frameTime > actualDuration)
                {
                    // The frame is outside the actual video duration
                    boundingBoxes.InitialVideoFrame = 0;
                }
            }
            
            Image image = CreateImage(imageRow, margin, height, frameTime);
            return new(image, boundingBoxes);
        }
        #endregion

        #region Internal methods
        // Create a canvas containing the image as well as the  bounding boxes defined by the filetable id 
        private static Canvas CreateCanvasWithBoundingBoxesAndImage(BoundingBoxes boundingBoxes, Image image, double height, bool isVideo)
        {
            Canvas canvas = new()
            {
                Width = image.Source.Width,
                Height = height,
                Background = Brushes.Gray
            };
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            canvas.Children.Add(image);

            if (isVideo)
            {
                boundingBoxes.DrawBoundingBoxesInCanvas(canvas, image.Source.Width, image.Source.Height, 0, null, boundingBoxes.InitialVideoFrame);
            }
            else
            {
                boundingBoxes.DrawBoundingBoxesInCanvas(canvas, image.Source.Width, image.Source.Height);
            }
            return (canvas);
        }

        // Create the image
        private static Image CreateImage(ImageRow imageRow, int margin, double imageHeight, float? time)
        {
            float frameTime = time ?? 0;
            Image image = new()
            {
                Source = imageRow.IsVideo && imageRow is VideoRow videoRow
                        ? videoRow.LoadVideoBitmap(GlobalReferences.MainWindow.RootPathToImages, Convert.ToInt32(imageHeight), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseHeight, frameTime, out bool isCorruptOrMissing)
                        : imageRow.LoadBitmap(GlobalReferences.MainWindow.RootPathToImages, Convert.ToInt32(imageHeight), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseHeight, out isCorruptOrMissing)
            };
            // Need to scale the image to the correct height
            if (isCorruptOrMissing)
            {
                if (image.Height <= 0 || ImageValues.FileNoLongerAvailable.Value.Height <= 0)
                {
                    image.Source = null;
                }
                else
                {
                    double scale = imageHeight / ImageValues.FileNoLongerAvailable.Value.Height;
                    image.Source = new TransformedBitmap(ImageValues.FileNoLongerAvailable.Value, new ScaleTransform(scale, scale));
                }
                image.Tag = null;
            }
            else if (image.Source?.Height > 0 && Math.Abs(image.Height - image.Source.Height) > .0001)
            {
                // Need to adjust the image width due to differing dpi settings of the bitmap vs. device independent units used to actually display the bitmap
                // as otherwise it may not be the correct size. It may not be the mose efficient way to do this, but it seems to work.
                double scale = imageHeight / image.Source.Height;
                image.Source = new TransformedBitmap((BitmapSource)image.Source, new ScaleTransform(scale, scale));
            }

            image.Tag = imageRow;
            image.Margin = new(margin);
            return image;
        }

        // Create a new larger zoomable popup so that the user can inspect the image details
        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            int height = 480;
            if (sender is Canvas { Tag: not null } canvas)
            {
                if (!(canvas.Tag is ImageRow imageRow))
                {
                    return;
                }

                Tuple<Image, BoundingBoxes> tuple = GetBoundingBoxesAndImage(imageRow, 0, height, imageRow.IsVideo);
                BoundingBoxes boundingBoxes = tuple.Item2;
                Image image = tuple.Item1;
                Canvas clone = CreateCanvasWithBoundingBoxesAndImage(boundingBoxes, image, height, imageRow.IsVideo);

                Zoombox zoombox = CreateZoombox();
                zoombox.Content = clone;

                InspectionPopup.Child = zoombox;
                InspectionPopup.Placement = PlacementMode.MousePoint;
                InspectionPopup.IsOpen = true;
            }
        }

        static Zoombox CreateZoombox()
        {
            Zoombox zoombox = new()
            {
                DragModifiers = [KeyModifier.None],
                RelativeZoomModifiers = [KeyModifier.None],
                ZoomModifiers = [KeyModifier.None],
                MinScale = 1
            };
            return zoombox;
        }

        private static Label CreateLabel(string content, double height)
        {
            return new()
            {
                Content = content,
                FontSize = 48.0,
                FontWeight = FontWeights.Bold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Bottom,
                Height = height,
                Width = 40,
                Foreground = Brushes.Black,
                Background = Brushes.LightGray
            };
        }
        #endregion
    }
}
