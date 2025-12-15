using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Controls
{
    /// <summary>
    /// ThumbnailInCell User Control, which is used to fill each cell in the ThumbnailGrid
    /// </summary>
    public partial class ThumbnailInCell
    {
        #region Public Properties
        // ImageHeight is calculated from the width * the image's aspect ratio, but checks for nulls, etc. 
        // Note: while the image width should always be the cell width, the height depends on the aspect ratio
        public double ImageHeight =>
            (Image == null || Image.Source == null || Image.Source.Width == 0)
                ? 0
                : Image.Width * Image.Source.Height / Image.Source.Width;

        public int Row { get; set; }
        public int Column { get; set; }
        public int GridIndex { get; set; }
        public int FileTableIndex { get; set; }
        public ImageRow ImageRow { get; set; }
        public double CellHeight { get; }
        public double CellWidth { get; }
        public DateTime DateTimeLastBitmapWasSet { get; set; }
        public bool IsBitmapSet { get; private set; }

        // bounding boxes for detection
        private BoundingBoxes boundingBoxes;
        // Bounding boxes for detection. Whenever one is set, it is redrawn
        public BoundingBoxes BoundingBoxes
        {
            get => boundingBoxes;
            set
            {
                // update and render bounding boxes
                boundingBoxes = value;
                RefreshBoundingBoxes(true);
            }
        }

        // Whether the Checkbox is checked i.e., the ThumbnailInCell is selected
        public bool IsSelected
        {
            get;
            set
            {
                field = value;
                // Show or hide the checkmark 
                if (field)
                {
                    Cell.Background = selectedBrush;
                    SelectionTextBlock.Text = "\u2713"; // Checkmark in unicode
                    SelectionTextBlock.Background.Opacity = 0.7;
                }
                else
                {
                    Cell.Background = unselectedBrush;
                    SelectionTextBlock.Text = "   ";
                    SelectionTextBlock.Background.Opacity = 0.35;
                }
            }
        }

        // Path is the RelativePath/FileName of the image file
        public string Path => (ImageRow == null) ? string.Empty : System.IO.Path.Combine(ImageRow.RelativePath, ImageRow.File);

        public string RootPathToImages { get; set; }
        #endregion

        #region Private Variables
        // A canvas used to display the bounding boxes
        private readonly Canvas bboxCanvas = new();

        private readonly Brush unselectedBrush = Brushes.Black;
        private readonly Brush selectedBrush = Brushes.LightBlue;
        private readonly Color selectedColor = Colors.LightBlue;
        #endregion

        #region Constructor: Width / height is the desired size of the image
        public ThumbnailInCell(double cellWidth, double cellHeight)
        {
            InitializeComponent();

            CellHeight = cellHeight;
            CellWidth = cellWidth;

            Image.Width = cellWidth;
            Image.MinWidth = cellWidth;
            Image.MaxWidth = cellWidth;

            this.RootPathToImages = string.Empty;
        }

        private void ThumbnailInCell_Loaded(object sender, RoutedEventArgs e)
        {
            // Heuristic for setting font sizes
            SetTextFontSize();
            AdjustMargin();
            if (ImageRow.IsVideo)
            {
                InitializePlayButton();
                PlayButton.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Public: Get/Set Thumbnail bitmaps
        // Get the bitmap, scaled to fit the cellWidth/Height, from the image row's image or video 
        public BitmapSource GetThumbnail(double cellWidth, double cellHeight)
        {
            if (ImageRow.IsVideo == false)
            {
                // Calculate scale factor to ensure that images of different aspect ratios completely fit in the cell
                double desiredHeight = cellWidth / ImageRow.GetBitmapAspectRatioFromFile(this.RootPathToImages);
                double scale = Math.Min(cellWidth / cellWidth, cellHeight / desiredHeight); // 1st term is ScaleWidth, 2nd term is ScaleHeight
                double finalDesiredWidth = (cellWidth * scale - 8); // Subtract another 2 pixels for the grid border (I think)

                return ImageRow.LoadBitmap(this.RootPathToImages, Convert.ToInt32(finalDesiredWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out _);
            }

            // Its a video. If recognitions are used, we need to indicate where in the video timeline we should grab the frame to show
            // Otherwise the time will be 0.

            // Calculate the time of that initial video to show using the frame number from the initial video frame and the video frame rate
            // If there are no bounding boxes or the recognition file is missing those values, time will resolve to 0 i.e., the beginning of the video.
            float? time = this.BoundingBoxes.InitialVideoFrame / this.BoundingBoxes.FrameRate;

            // Get the bitmap frame at that time from the video 
            // Also, for some reason the scale adjustment doesn't seem to be needed, not sure why.
            // Note that the nonVideo case belwow should never happen, but just in case...
            return ImageRow is VideoRow videoRow
                ? videoRow.LoadVideoBitmap(this.RootPathToImages, Convert.ToInt32(cellWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, time, out _)
                : ImageRow.LoadBitmap(this.RootPathToImages, Convert.ToInt32(cellWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out _);
        }

        public void SetThumbnail(BitmapSource bitmapSource)
        {
            try
            {
                Image.Source = bitmapSource;
                IsBitmapSet = true;
            }
            catch // (Exception e)
            {
                // Uncomment for debugging
                //Debug.Print("SetSource: Could not set the bitmapSource: " + e.Message);
            }
        }
        #endregion

        #region Episodes and Bounding Boxes and Duplicates
        public void RefreshBoundingBoxesDuplicatesAndEpisodeInfo(FileTable fileTable, int fileIndex)
        {
            RefreshEpisodeInfo(fileTable, fileIndex);
            RefreshBoundingBoxes(true);
            RefreshDuplicateInfo(fileTable, fileIndex);
        }

        /// <summary>
        /// Redraw  or clear the bounding boxes depending on the visibility state
        /// </summary>
        /// 
        public void RefreshBoundingBoxes(bool visibility)
        {
            if (visibility && Image?.Source != null)
            {
                // Remove existing bounding boxes, if any. Then try to redraw them
                // Note that we do this even if detections may not exist, as we need to clear things if the user had just toggled detections off
                bboxCanvas.Children.Clear();
                Cell.Children.Remove(bboxCanvas);
                try
                {
                    // Checking if this thread has access to the object.
                    // This check sometimes fails (but not on my machine) with a
                    //   System.InvalidOperationException: The calling thread cannot access this object because a different thread owns it.
                    //   If that happens, we just try again in the else statement using Dispatcher.Invoke...
                    if (Dispatcher.CheckAccess())
                    {
                        DoRefreshBoundingBoxes();
                    }
                    else
                    {
                        TracePrint.PrintMessage("In RefreshBoundingBoxes: using the displatcher to avoid the 'calling thread cannot access this object' exception.");
                        Dispatcher.Invoke(DoRefreshBoundingBoxes);
                    }
                }
                catch
                {
                    return;
                }

                Cell.Children.Add(bboxCanvas);
            }
            else
            {
                // There is no image visible, so remove the bounding boxes
                bboxCanvas.Children.Clear();
                Cell.Children.Remove(bboxCanvas);
            }
        }

        private void DoRefreshBoundingBoxes()
        {
            TransformGroup tg = GetTransformGroupToApplyToBoundingBoxes();
            // This thread has access to the object.
            if (this.ImageRow.IsVideo)
            {
                // Its a video. Get the Transform Group to apply to the bounding boxes
                if (false == this.OkayToDrawAndResetInitialVideoFrameIfNeeded(this.ImageRow, boundingBoxes))
                {
                    // For some reason, its not ok to display or draw the initial video frame because some information (such as frame rate) is missing
                    // So we just reset the bounding box to appear over the first (0th) frame. 
                    boundingBoxes.InitialVideoFrame = 0;
                }
                boundingBoxes.DrawBoundingBoxesInCanvas(bboxCanvas, Image.Width, ImageHeight, 0, tg, boundingBoxes.InitialVideoFrame);
            }
            else
            {
                // Its an image
                BoundingBoxes.DrawBoundingBoxesInCanvas(bboxCanvas, Image.Width, ImageHeight, 0, tg);
            }
        }

        // Calculate a transform group that will be applied to bounding boxes.
        // We do this by looknig at the aspect ratio, which in terms determines
        // which scaling and transform parameters need to be applied
        private TransformGroup GetTransformGroupToApplyToBoundingBoxes()
        {
            ScaleTransform sc = new(1.0, 1.0);
            TranslateTransform tt = new(0, 0);
            double imageAspectRatio = this.Image.Width / this.ImageHeight;
            double cellAspectRatio = this.CellWidth / this.CellHeight;

            if (imageAspectRatio > cellAspectRatio)
            {
                // Image aspect ratio is wider than its containing cell
                // This means we have to scall the X direction to make it fit,
                // but we don't have to do anything to the Y direction as the image Y is always less than the cell Y
                // We also don't have to transform things as the image is top-aligned
                sc.ScaleX = this.CellWidth / this.Image.Width;
            }
            else
            {
                // Image aspect ratio is narrower (or equal to) its containing cell
                // This means we have to scale the X direction (as its narrower)
                // and the Y direction (as we shrunk it)
                // We also have to transform it as the image is positioned in the center of the cell
                sc.ScaleX = imageAspectRatio / cellAspectRatio;
                sc.ScaleY = this.CellHeight / this.ImageHeight;
                tt.X = (this.CellWidth - Image.Width * sc.ScaleX) * .5;
            }
            TransformGroup tg = new();
            tg.Children.Add(sc);
            tg.Children.Add(tt);
            return tg;
        }

        // Only invoke this if the ImageRow is a video
        // Essentially, it checks various parameters, including whether the the desired frame is within the time frame of the video.
        // If things fail, it changes the BoundingBox's initial frame to 0, otherwise leaves it untouched.
        private bool OkayToDrawAndResetInitialVideoFrameIfNeeded(ImageRow imageRow, BoundingBoxes bBoxes)
        {
            float? actualDuration = null;

            if (imageRow.IsVideo)
            {
                string filePath = Util.FilesFolders.GetFullPath(GlobalReferences.MainWindow.DataHandler.FileDatabase, imageRow);
                actualDuration = FilesFolders.GetVideoDuration(filePath);
            }

            // Does the bounding box structure have a frame rate set?
            // If not, we can't do anything with it.
            if (false == bBoxes.FrameRate.HasValue || bBoxes.FrameRate <= 0)
            {
                //bBoxes.InitialVideoFrame = 0;
                return false;
            }

            // We can't get the video's duration for whatever reason, e.g., its corrupted or missing. 
            if (false == actualDuration.HasValue || actualDuration < 0)
            {
                bBoxes.InitialVideoFrame = 0;
                return false;
            }

            // If the initial frame is outside the actual video duration, then its of no use to us
            float? frameTime = bBoxes.InitialVideoFrame / bBoxes.FrameRate;
            if (frameTime > actualDuration)
            {
                bBoxes.InitialVideoFrame = 0;
                return false;
            }
            // If we get here, we didn't have to reset the initial video frame.
            return true;
        }

        public void InitializePlayButton()
        {
            // Already initialized
            if (PlayButton.Children.Count > 0)
            {
                return;
            }
            double canvasHeight = CellHeight / 5;
            double canvasWidth = CellWidth;
            double ellipseDiameter = canvasHeight * .7;
            double ellipseRadius = ellipseDiameter / 2;
            PlayButton.Height = canvasHeight;

            Ellipse ellipse = new()
            {
                Width = ellipseDiameter,
                Height = ellipseDiameter,
                Fill = new SolidColorBrush
                {
                    Color = selectedColor,
                    Opacity = 0.5
                }
            };
            Canvas.SetLeft(ellipse, canvasWidth / 2 - ellipseDiameter / 2);

            Point center = new(ellipseRadius, ellipseRadius);
            PointCollection trianglePoints = BitmapUtilities.GetTriangleVerticesInscribedInCircle(center, (float)ellipseDiameter * .7f / 2);

            // Construct the triangle
            Polygon triangle = new()
            {
                Points = trianglePoints,
                Fill = new SolidColorBrush
                {
                    Color = Colors.Blue,
                    Opacity = 0.5
                },
            };
            Canvas.SetLeft(triangle, canvasWidth / 2 - ellipseDiameter / 2);

            PlayButton.Children.Add(ellipse);
            PlayButton.Children.Add(triangle);
        }

        // Get and display the episode text if various conditions are met
        public void RefreshEpisodeInfo(FileTable fileTable, int fileIndex)
        {
            if (Keyboard.IsKeyDown(Key.H))
            {
                EpisodeTextBlock.Visibility = Visibility.Hidden;
                FileNameTextBlock.Visibility = Visibility.Hidden;
                TimeTextBlock.Visibility = Visibility.Hidden;
                return;
            }
            if (Episodes.Episodes.ShowEpisodes)
            {
                // Episode number
                if (Episodes.Episodes.EpisodesDictionary.TryGetValue(fileIndex, out var _) == false)
                {
                    Episodes.Episodes.EpisodeGetEpisodesInRange(fileTable, fileIndex);
                }

                Tuple<int, int> episode = Episodes.Episodes.EpisodesDictionary.TryGetValue(fileIndex, out var value1)
                ? value1
                : new(1, 1); // This is the (rare) error case that happened once ot a user - if for some reason the fileIndex is not in range. Could probably indicate this in the UI (which currently just marks it as single) but not sure why this error happens, so what to put there is unclear

                if (episode.Item1 == int.MaxValue)
                {
                    EpisodeTextBlock.Text = "\u221E";
                }
                else
                {
                    EpisodeTextBlock.Text = (episode.Item2 == 1) ? "Single" : $"{episode.Item1}/{episode.Item2}";
                }
                EpisodeTextBlock.Foreground = (episode.Item1 == 1) ? Brushes.Red : Brushes.Black;
                EpisodeTextBlock.FontWeight = (episode.Item1 == 1 && episode.Item2 != 1) ? FontWeights.Bold : FontWeights.Normal;

                // Filename without the extention and Time in HH: MM
                // This was on request from a user, who needed to scan for the first/last image in a timelapse capture sequence
                FileNameTextBlock.Text = System.IO.Path.GetFileNameWithoutExtension(ImageRow.File);
                string timeInHHMM = ImageRow.DateTime.ToString("hh:mm");
                TimeTextBlock.Text = " (" + timeInHHMM + ")";
            }
            EpisodeTextBlock.Visibility = Episodes.Episodes.ShowEpisodes ? Visibility.Visible : Visibility.Hidden;
            FileNameTextBlock.Visibility = EpisodeTextBlock.Visibility;
            TimeTextBlock.Visibility = EpisodeTextBlock.Visibility;
        }

        // Get and display the episode text if various conditions are met
        public void RefreshDuplicateInfo(FileTable fileTable, int fileIndex)
        {
            if (Keyboard.IsKeyDown(Key.H))
            {
                DuplicateIndicatorInOverview.Visibility = Visibility.Hidden;
                return;
            }

            if (fileIndex < 0 || fileIndex >= fileTable.RowCount)
            {
                // If the fileIndex is not in the fileTable, just abort.
                return;
            }

            ImageRow imageRow = fileTable[fileIndex];
            Point duplicateSequence = GlobalReferences.MainWindow.DuplicatesCheckIfDuplicateAndGetSequenceNumberIfAny(imageRow, fileIndex);
            if (duplicateSequence.Y > 1)
            {
                DuplicateIndicatorInOverview.Visibility = Visibility.Visible;
                DuplicateIndicatorInOverview.Text = $"Duplicate: {duplicateSequence.X}/{duplicateSequence.Y}";
            }
            else
            {
                DuplicateIndicatorInOverview.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Private: Adjust Fonts and Margins of the Info Panel
        // Set the font size of the text for the info panel's children
        private void SetTextFontSize()
        {
            int fontSize = CellHeight / 10 > 30 ? 30 : (int)CellHeight / 10;
            SelectionTextBlock.FontSize = fontSize;
            FileNameTextBlock.FontSize = fontSize;
            TimeTextBlock.FontSize = fontSize;
            EpisodeTextBlock.FontSize = fontSize;

            // This (more or less) fits in the available space 
            DuplicateIndicatorInOverview.FontSize = fontSize / 2.5;
        }

        // Most images have a black bar at its bottom and top. We want to align 
        // the checkbox / text label to be just outside that black bar. This is guesswork, 
        // but the margin should line up within reason most of the time
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        private void AdjustMargin()
        {
            int margin = (int)Math.Ceiling(CellHeight / 25) + 1;
            InfoPanel.Margin = new(0, margin, margin, 0);
        }
        #endregion
    }
}
