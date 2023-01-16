using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Database;
using Timelapse.DataStructures;
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
            (this.Image == null || this.Image.Source == null || this.Image.Source.Width == 0)
                ? 0
                : this.Image.Width * this.Image.Source.Height / this.Image.Source.Width;

        public int Row { get; set; }
        public int Column { get; set; }
        public int GridIndex { get; set; }
        public int FileTableIndex { get; set; }
        public ImageRow ImageRow { get; set; }
        public double CellHeight { get; private set; }
        public double CellWidth { get; private set; }
        public DateTime DateTimeLastBitmapWasSet { get; set; }
        public bool IsBitmapSet { get; private set; }

        // bounding boxes for detection
        private BoundingBoxes boundingBoxes;
        // Bounding boxes for detection. Whenever one is set, it is redrawn
        public BoundingBoxes BoundingBoxes
        {
            get => this.boundingBoxes;
            set
            {
                // update and render bounding boxes
                this.boundingBoxes = value;
                this.RefreshBoundingBoxes(true);
            }
        }

        // Whether the Checkbox is checked i.e., the ThumbnailInCell is selected
        private bool isSelected;
        public bool IsSelected
        {
            get => this.isSelected;
            set
            {
                this.isSelected = value;
                // Show or hide the checkmark 
                if (this.isSelected)
                {
                    this.Cell.Background = this.selectedBrush;
                    this.SelectionTextBlock.Text = "\u2713"; // Checkmark in unicode
                    this.SelectionTextBlock.Background.Opacity = 0.7;
                }
                else
                {
                    this.Cell.Background = this.unselectedBrush;
                    this.SelectionTextBlock.Text = "   ";
                    this.SelectionTextBlock.Background.Opacity = 0.35;
                }
            }
        }

        // Path is the RelativePath/FileName of the image file
        public string Path => (this.ImageRow == null) ? String.Empty : System.IO.Path.Combine(this.ImageRow.RelativePath, this.ImageRow.File);

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
            this.InitializeComponent();

            this.CellHeight = cellHeight;
            this.CellWidth = cellWidth;

            this.Image.Width = cellWidth;
            this.Image.MinWidth = cellWidth;
            this.Image.MaxWidth = cellWidth;

            this.RootFolder = String.Empty;
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
            this.SetTextFontSize();
            this.AdjustMargin();
            if (this.ImageRow.IsVideo)
            {
                this.InitializePlayButton();
                this.PlayButton.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Public: Get/Set Thumbnail bitmaps
        // Get the bitmap, scaled to fit the cellWidth/Height, from the image row's image or video 
        public BitmapSource GetThumbnail(double cellWidth, double cellHeight)
        {
            BitmapSource bf;
            double finalDesiredWidth;
            if (this.ImageRow.IsVideo == false)
            {
                // Calculate scale factor to ensure that images of different aspect ratios completely fit in the cell
                double desiredHeight = cellWidth / this.ImageRow.GetBitmapAspectRatioFromFile(this.RootFolder);
                double scale = Math.Min(cellWidth / cellWidth, cellHeight / desiredHeight); // 1st term is ScaleWidth, 2nd term is ScaleHeight
                finalDesiredWidth = (cellWidth * scale - 8);  // Subtract another 2 pixels for the grid border (I think)

                bf = this.ImageRow.LoadBitmap(this.RootFolder, Convert.ToInt32(finalDesiredWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out _);
            }
            else
            {
                // Get it from the video - for some reason the scale adjustment doesn't seem to be needed, not sure why.
                bf = this.ImageRow.LoadBitmap(this.RootFolder, Convert.ToInt32(cellWidth), ImageDisplayIntentEnum.Persistent, ImageDimensionEnum.UseWidth, out _);
            }
            return bf;
        }

        public void SetThumbnail(BitmapSource bitmapSource)
        {
            try
            {
                this.Image.Source = bitmapSource;
                this.IsBitmapSet = true;
            }
            catch // (Exception e)
            {
                // Uncomment for debugging
                //System.Diagnostics.Debug.Print("SetSource: Could not set the bitmapSource: " + e.Message);
            }
        }
        #endregion

        #region Episodes and Bounding Boxes and Duplicates
        public void RefreshBoundingBoxesDuplicatesAndEpisodeInfo(FileTable fileTable, int fileIndex)
        {
            this.RefreshEpisodeInfo(fileTable, fileIndex);
            this.RefreshBoundingBoxes(true);
            this.RefreshDuplicateInfo(fileTable, fileIndex);
        }

        /// <summary>
        /// Redraw  or clear the bounding boxes depending on the visibility state
        /// </summary>
        /// 
        public void RefreshBoundingBoxes(bool visibility)
        {
            if (visibility && this.Image?.Source != null)
            {
                // Remove existing bounding boxes, if any. Then try to redraw them
                // Note that we do this even if detections may not exist, as we need to clear things if the user had just toggled detections off
                this.bboxCanvas.Children.Clear();
                this.Cell.Children.Remove(this.bboxCanvas);
                this.BoundingBoxes.DrawBoundingBoxesInCanvas(this.bboxCanvas, this.Image.Width, this.ImageHeight);
                this.Cell.Children.Add(this.bboxCanvas);
            }
            else
            {
                // There is no image visible, so remove the bounding boxes
                this.bboxCanvas.Children.Clear();
                this.Cell.Children.Remove(this.bboxCanvas);
            }
        }

        public void InitializePlayButton()
        {
            // Already initialized
            if (PlayButton.Children.Count > 0)
            {
                return;
            }
            double canvasHeight = this.CellHeight / 5;
            double canvasWidth = this.CellWidth;
            double ellipseDiameter = canvasHeight * .7;
            double ellipseRadius = ellipseDiameter / 2;
            this.PlayButton.Height = canvasHeight;

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

            this.PlayButton.Children.Add(ellipse);
            this.PlayButton.Children.Add(triangle);
        }

        // Get and display the episode text if various conditions are met
        public void RefreshEpisodeInfo(FileTable fileTable, int fileIndex)
        {
            if (Keyboard.IsKeyDown(Key.H))
            {
                this.EpisodeTextBlock.Visibility = Visibility.Hidden;
                this.FileNameTextBlock.Visibility = Visibility.Hidden;
                this.TimeTextBlock.Visibility = Visibility.Hidden;
                return;
            }
            if (Episodes.ShowEpisodes)
            {
                // Episode number
                if (Episodes.EpisodesDictionary.ContainsKey(fileIndex) == false)
                {
                    Episodes.EpisodeGetEpisodesInRange(fileTable, fileIndex);
                }

                Tuple<int, int> episode = Episodes.EpisodesDictionary.ContainsKey(fileIndex)
                ? Episodes.EpisodesDictionary[fileIndex]
                : new Tuple<int, int>(1, 1); // This is the (rare) error case that happened once ot a user - if for some reason the fileIndex is not in range. Could probably indicate this in the UI (which currently just marks it as single) but not sure why this error happens, so what to put there is unclear

                if (episode.Item1 == int.MaxValue)
                {
                    this.EpisodeTextBlock.Text = "\u221E";
                }
                else
                {
                    this.EpisodeTextBlock.Text = (episode.Item2 == 1) ? "Single" : String.Format("{0}/{1}", episode.Item1, episode.Item2);
                }
                this.EpisodeTextBlock.Foreground = (episode.Item1 == 1) ? Brushes.Red : Brushes.Black;
                this.EpisodeTextBlock.FontWeight = (episode.Item1 == 1 && episode.Item2 != 1) ? FontWeights.Bold : FontWeights.Normal;

                // Filename without the extention and Time in HH: MM
                // This was on request from a user, who needed to scan for the first/last image in a timelapse capture sequence
                this.FileNameTextBlock.Text = System.IO.Path.GetFileNameWithoutExtension(this.ImageRow.File);
                string timeInHHMM = this.ImageRow.DateTime.ToString("hh:mm");
                //string timeInHHMM = (this.ImageRow.Time.Length > 3) ? this.ImageRow.Time.Remove(this.ImageRow.Time.Length - 3) : String.Empty;
                this.TimeTextBlock.Text = " (" + timeInHHMM + ")";
            }
            this.EpisodeTextBlock.Visibility = Episodes.ShowEpisodes ? Visibility.Visible : Visibility.Hidden;
            this.FileNameTextBlock.Visibility = this.EpisodeTextBlock.Visibility;
            this.TimeTextBlock.Visibility = this.EpisodeTextBlock.Visibility;
        }

        // Get and display the episode text if various conditions are met
        public void RefreshDuplicateInfo(FileTable fileTable, int fileIndex)
        {
            if (Keyboard.IsKeyDown(Key.H))
            {
                this.DuplicateIndicatorInOverview.Visibility = Visibility.Hidden;
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
                this.DuplicateIndicatorInOverview.Visibility = Visibility.Visible;
                this.DuplicateIndicatorInOverview.Text = String.Format("Duplicate: {0}/{1}", duplicateSequence.X, duplicateSequence.Y);
            }
            else
            {
                this.DuplicateIndicatorInOverview.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Private: Adjust Fonts and Margins of the Info Panel
        // Set the font size of the text for the info panel's children
        private void SetTextFontSize()
        {
            int fontSize = this.CellHeight / 10 > 30 ? 30 : (int)this.CellHeight / 10;
            this.SelectionTextBlock.FontSize = fontSize;
            this.FileNameTextBlock.FontSize = fontSize;
            this.TimeTextBlock.FontSize = fontSize;
            this.EpisodeTextBlock.FontSize = fontSize;

            // This (more or less) fits in the available space 
            this.DuplicateIndicatorInOverview.FontSize = fontSize / 2.5;
        }

        // Most images have a black bar at its bottom and top. We want to align 
        // the checkbox / text label to be just outside that black bar. This is guesswork, 
        // but the margin should line up within reason most of the time
        // Also, values are hard-coded vs. dynamic. Ok until we change the standard width or layout of the display space.
        private void AdjustMargin()
        {
            int margin = (int)Math.Ceiling(this.CellHeight / 25) + 1;
            this.InfoPanel.Margin = new Thickness(0, margin, margin, 0);
        }
        #endregion
    }
}
