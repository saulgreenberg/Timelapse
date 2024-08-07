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
        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                // Show or hide the checkmark 
                if (isSelected)
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

        public string RootFolder { get; set; }
        #endregion

        #region Private Variables
        // A canvas used to display the bounding boxes
        private readonly Canvas bboxCanvas = new Canvas();

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

            RootFolder = string.Empty;
        }

        // I tried to create a clone so we can add duplicates, but its commented out for now as it doesn't seem to
        // acheive the correct effect. The problem may not be here...
        //public ThumbnailInCell CloneMe(int fileTableIndex, int gridIndex, double cellWidth, double cellHeight, int row, int column)
        //{
        //    ThumbnailInCell clone = new ThumbnailInCell(cellWidth, cellHeight);
        //    clone.Image = this.Image;
        //    clone.Image.Width = cellWidth;
        //    clone.Image.MinWidth = cellWidth;
        //    clone.Image.MaxWidth = cellWidth;
        //    clone.DateTimeLastBitmapWasSet = this.DateTimeLastBitmapWasSet;
        //    clone.IsBitmapSet = this.IsBitmapSet;
        //    clone.GridIndex = gridIndex;
        //    clone.Row = row;
        //    clone.Column = column;
        //    clone.Image = this.Image;
        //    clone.BoundingBoxes = this.BoundingBoxes;
        //    clone.ImageRow = this.ImageRow;
        //    clone.RootFolder = this.RootFolder;
        //    clone.FileTableIndex = this.FileTableIndex;
        //    return clone;
        //}

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
            BitmapSource bf;
            if (ImageRow.IsVideo == false)
            {
                // Calculate scale factor to ensure that images of different aspect ratios completely fit in the cell
                double desiredHeight = cellWidth / ImageRow.GetBitmapAspectRatioFromFile(RootFolder);
                double scale = Math.Min(cellWidth / cellWidth, cellHeight / desiredHeight); // 1st term is ScaleWidth, 2nd term is ScaleHeight
                double finalDesiredWidth = (cellWidth * scale - 8);  // Subtract another 2 pixels for the grid border (I think)

                bf = ImageRow.LoadBitmap(RootFolder, Convert.ToInt32(finalDesiredWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out _);
            }
            else
            {
                // Get it from the video - for some reason the scale adjustment doesn't seem to be needed, not sure why.
                bf = ImageRow.LoadBitmap(RootFolder, Convert.ToInt32(cellWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out _);
            }
            return bf;
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
                    if (Dispatcher.CheckAccess())
                    {
                        // This sometimes fails (but not on my machine) with a System.InvalidOperationException: The calling thread cannot access this object because a different thread owns it. 
                        BoundingBoxes.DrawBoundingBoxesInCanvas(bboxCanvas, Image.Width,
                            ImageHeight);
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TracePrint.PrintMessage("In RefreshBoundingBoxes: using the displatcher to avoid the 'calling thread cannot access this object' exception.");
                            BoundingBoxes.DrawBoundingBoxesInCanvas(bboxCanvas, Image.Width,
                                ImageHeight);
                        });
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

            Ellipse ellipse = new Ellipse
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

            Point center = new Point(ellipseRadius, ellipseRadius);
            PointCollection trianglePoints = BitmapUtilities.GetTriangleVerticesInscribedInCircle(center, (float)ellipseDiameter * .7f / 2);

            // Construct the triangle
            Polygon triangle = new Polygon
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
                : new Tuple<int, int>(1, 1); // This is the (rare) error case that happened once ot a user - if for some reason the fileIndex is not in range. Could probably indicate this in the UI (which currently just marks it as single) but not sure why this error happens, so what to put there is unclear

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
            InfoPanel.Margin = new Thickness(0, margin, margin, 0);
        }
        #endregion
    }
}
