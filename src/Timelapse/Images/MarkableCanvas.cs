using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using ThumbnailGrid = Timelapse.Controls.ThumbnailGrid;

namespace Timelapse.Images
{
    /// <summary>
    /// MarkableCanvas is a canvas that
    /// - contains an image that can be scaled and translated by the user with the mouse 
    /// - can draw and track markers atop the image
    /// - can show a magnified portion of the image in a magnifying glass
    /// - can save and restore a zoom+pan setting
    /// - can display a video 
    /// </summary>
    public partial class MarkableCanvas : Canvas
    {
        #region Public Properties

        /// <summary>
        /// Bounding boxes for detection. Whenever one is set, it is redrawn
        /// </summary>
        public BoundingBoxes BoundingBoxes
        {
            get;
            set
            {
                // update bounding boxes
                field = value;
                // render new bounding boxes and update display image
                RefreshBoundingBoxes();
            }
        }

        /// <summary>
        /// Gets the grid containing a multitude of zoomed out images
        /// </summary>
        public ThumbnailGrid ThumbnailGrid { get; }

        public DataEntryControls DataEntryControls
        {
            get;
            set
            {
                ThumbnailGrid.DataEntryControls = value;
                field = value;
            }
        }

        /// <summary>
        /// Gets the image displayed across the MarkableCanvas for image files
        /// </summary>
        public Image ImageToDisplay { get; set; }

        /// <summary>
        /// Gets the image displayed in the magnifying glass
        /// </summary>
        public Image ImageToMagnify { get; }

        /// <summary>
        /// Whether the thumbnail grid is visible or not
        /// </summary>
        public bool IsThumbnailGridVisible => ThumbnailGrid.Visibility == Visibility.Visible;

        /// <summary>
        /// Gets or sets a value indicating whether the magnifying glass is generally visible or hidden, and returns its state
        /// </summary>
        public bool MagnifiersEnabled
        {
            get =>
                // both the Offset Lens and the Magnifying Lens share the same enable state
                magnifyingGlass.IsEnabled;
            set
            {
                magnifyingGlass.IsEnabled = value;
                OffsetLens.IsEnabled = value;
                SetMagnifiersAccordingToCurrentState(value, value);
            }
        }

        /// <summary>
        /// Gets or sets the markers on the image
        /// </summary>
        public List<Marker> Markers
        {
            get => markers;
            set
            {
                // update markers
                markers = value;
                // render new markers and update display image
                RedrawMarkers();
            }
        }

        /// <summary>
        /// The VideoPlayer displayed by the markable canvasewhen a video is selected
        /// </summary>
        public VideoPlayer VideoPlayer { get; }

        /// <summary>
        /// Gets or sets the maximum zoom of the display image
        /// </summary>
        public double ZoomMaximum { get; set; }

        public bool isZooming => IsThumbnailGridVisible || Math.Abs(imageToDisplayScale.ScaleX) - 1 > 1e-5;

        #endregion

        #region Private variables
        private static readonly SolidColorBrush MarkerFillBrush = new(Color.FromArgb(2, 0, 0, 0));

        // A bookmark that saves the pan and zoom setting
        private readonly ZoomBookmark bookmark;

        // the canvas to magnify contains both an image and markers so the magnifying glass view matches the display image
        private readonly Canvas canvasToMagnify;

        // a Popup to show episode information, regardless of the selection or sorting criteria
        private EpisodePopup episodePopup;

        // A canvas used to display the bounding boxes
        private readonly Canvas bboxCanvas = new();

        // render transforms
        private readonly ScaleTransform imageToDisplayScale;
        private readonly TransformGroup transformGroup;
        private readonly TranslateTransform imageToDisplayTranslation;

        // magnifying glass, including increment for increasing or decreasing magnifying glass zoom
        private readonly MagnifyingGlass magnifyingGlass;
        private double magnifyingGlassZoomStep;

        // Time of the last mousewheel event
        private DateTime lastMouseWheelDateTime = DateTime.Now;

        // Timer for resizing the ThumbnailGrid only after resizing is (likely) completed
        private readonly DispatcherTimer timerResize = new();

        // Timer for delaying updates in the midst of rapid navigation with the slider
        private readonly DispatcherTimer timerSlider = new();

        // markers
        private List<Marker> markers;

        // bounding boxes for detection

        // mouse and position states used to discriminate clicks from drags
        private UIElement mouseDownSender;
        private Point mouseDownLocation;
        private Point previousMousePosition;

        // mouse click timing and state used to determine  double from single clicks
        private DateTime mouseDoubleClickTime;
        private bool isDoubleClick;
        private bool isPanning;
        private bool displayingImage;

        private readonly OffsetLens OffsetLens = new();
        #endregion

        #region Events
        public event EventHandler<MarkerEventArgs> MarkerEvent;
        public event Action SwitchedToThumbnailGridViewEventAction;
        public event Action SwitchedToSingleImageViewEventAction;

        private void SendMarkerEvent(MarkerEventArgs e)
        {
            MarkerEvent?.Invoke(this, e);
        }
        #endregion

        #region Initialization and Loading
        public MarkableCanvas()
        {
            // configure self
            Background = Brushes.Black;
            ClipToBounds = true;
            Focusable = true;
            ResetMaximumZoom();
            SizeChanged += MarkableImageCanvas_SizeChanged;

            markers = [];
            BoundingBoxes = new();

            // initialize render transforms
            // scale transform's center is set during layout once the image size is known
            // default bookmark is default zoomed out, normal pan state
            bookmark = new();
            imageToDisplayScale = new(bookmark.Scale.X, bookmark.Scale.Y);
            imageToDisplayTranslation = new(bookmark.Translation.X, bookmark.Translation.Y);
            transformGroup = new();
            transformGroup.Children.Add(imageToDisplayScale);
            transformGroup.Children.Add(imageToDisplayTranslation);

            // set up the canvas
            MouseWheel += ImageOrCanvas_MouseWheel;

            // set up display image
            ImageToDisplay = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left
            };

            ImageToDisplay.MouseDown += ImageVideoOrCanvas_MouseDown;
            ImageToDisplay.MouseLeftButtonUp += ImageVideoOrCanvas_MouseUp;
            ImageToDisplay.RenderTransform = transformGroup;
            ImageToDisplay.SizeChanged += ImageToDisplay_SizeChanged;
            ImageToDisplay.VerticalAlignment = VerticalAlignment.Top;
            SetLeft(ImageToDisplay, 0);
            SetTop(ImageToDisplay, 0);
            Children.Add(ImageToDisplay);

            // set up display video
            VideoPlayer = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            VideoPlayer.SizeChanged += VideoToDisplay_SizeChanged;
            VideoPlayer.MouseWheel += ImageOrCanvas_MouseWheel;
            VideoPlayer.MouseDown += ImageVideoOrCanvas_MouseDown;
            VideoPlayer.MouseLeftButtonUp += ImageVideoOrCanvas_MouseUp;
            SetLeft(VideoPlayer, 0);
            SetTop(VideoPlayer, 0);
            Children.Add(VideoPlayer);

            // Set up zoomed out grid showing multitude of images
            ThumbnailGrid = new()
            {
                Visibility = Visibility.Collapsed
            };

            SetZIndex(ThumbnailGrid, 1000); // High Z-index so that it appears above other objects and magnifier
            SetLeft(ThumbnailGrid, 0);
            SetTop(ThumbnailGrid, 0);
            Children.Add(ThumbnailGrid);

            // set up image to magnify
            ImageToMagnify = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            ImageToMagnify.SizeChanged += ImageToMagnify_SizeChanged;
            SetLeft(ImageToMagnify, 0);
            SetTop(ImageToMagnify, 0);

            canvasToMagnify = new();
            canvasToMagnify.SizeChanged += CanvasToMagnify_SizeChanged;
            canvasToMagnify.Children.Add(ImageToMagnify);

            // set up the magnifying glass
            magnifyingGlass = new(this);


            SetZIndex(magnifyingGlass, 999); // Should always be in front
            Children.Add(magnifyingGlass);

            // Initialize double click timing
            mouseDoubleClickTime = DateTime.Now;

            // event handlers for image/video interaction: keys, mouse handling for markers
            MouseLeave += ImageOrCanvas_MouseLeave;
            MouseMove += MarkableCanvas_MouseMove;
            VideoPlayer.MediaElement.MouseLeave += MediaElementMouseLeave;
            PreviewKeyDown += MarkableCanvas_PreviewKeyDown;
            PreviewKeyUp += MarkableCanvas_PreviewKeyUp;
            Loaded += MarkableCanvas_Loaded;

            // When started, refreshes the ThumbnailGrid after 100 msecs (unless the timer is reset or stopped)
            timerResize.Interval = TimeSpan.FromMilliseconds(200);
            timerResize.Tick += TimerResize_Tick;

            // When started, refreshes the ThumbnailGrid after 100 msecs (unless the timer is reset or stopped)
            timerSlider.Interval = TimeSpan.FromMilliseconds(200);
            timerSlider.Tick += TimerSlider_Tick;

            // Default to the image view, as it will be all black
            ImageToDisplay.Visibility = Visibility.Visible;
            VideoPlayer.Visibility = Visibility.Collapsed;

            // Continue with initializations required by the ImageAdjustment partial class
            InitializeImageAdjustment();
        }

        private void MediaElementMouseLeave(object sender, MouseEventArgs e)
        {
            SetMagnifiersAccordingToCurrentState(false, false);
        }

        // Set the various magnifier / offset lens states.
        // Hide the magnifiers initially, as the mouse pointer may not be atop the canvas
        private void MarkableCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                // Prevents a Xaml design mode error 
                return;
            }
            MagnifierManager.SetMagnifier(VideoPlayer.MediaElement, OffsetLens);
            magnifyingGlass.ZoomFactor = GlobalReferences.TimelapseState.MagnifyingGlassZoomFactor;
            magnifyingGlassZoomStep = Constant.MarkableCanvas.MagnifyingGlassZoomIncrement;
            OffsetLens.ZoomFactor = GlobalReferences.TimelapseState.OffsetLensZoomFactor;

            // Hide the magnifiers initially:
            // the mouse pointer may not be atop the canvas and it would appear in an odd place
            SetMagnifiersAccordingToCurrentState(false, false);
        }

        #endregion

        #region Public methods - Set Display Image or Video
        /// <summary>
        /// Sets only the display image and leaves markers and the magnifier image unchanged.  Used by the differencing routines to set the difference image.
        /// </summary>
        public void SetDisplayImage(BitmapSource bitmapSource)
        {
            // If its a differenced image, generate an event saying so.
            ImageCache imageCache = GlobalReferences.MainWindow?.DataHandler?.ImageCache;
            if (imageCache != null)
            {
                bool isImageView = imageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered;
                GenerateImageStateChangeEvent(isImageView); //  Signal change in image state (consumed by ImageAdjuster)
            }
            ImageToDisplay.Source = bitmapSource;
            SetMagnifiersAccordingToCurrentState(true, true);
        }

        /// <summary>
        /// Set a wholly new image.  Clears existing markers and syncs the magnifier image to the display image.
        /// </summary>
        public void SetNewImage(BitmapSource bitmapSource, List<Marker> markersList)
        {
            // change to new markers
            markers = markersList;

            ImageToDisplay.Source = bitmapSource;
            // initiate render of magnified image
            // The asynchronous chain behind this is not entirely trivial.  The links are
            //   1) ImageToMagnify_SizeChanged fires and updates canvasToMagnify's size to match
            //   2) CanvasToMagnify_SizeChanged fires and redraws the magnified markers since the cavas size is now known and marker positions can update
            //   3) CanvasToMagnify_SizeChanged initiates a render on the magnifying glass to show the new image and marker positions
            //   4) if it's visible the magnifying glass content updates
            // This synchronization to WPF render opertations is necessary as, despite their appearance, properties like Source, Width, and Height are 
            // asynchronous.  Other approaches therefore tend to be subject to race conditions in render order which hide or misplace markers in the 
            // magnified view and also have a proclivity towards leaving incorrect or stale magnifying glass content on screen.
            // 
            // Another race exists as this.Markers can be set during the above rendering, initiating a second, concurrent marker render.  This is unavoidable
            // due to the need to expose a marker property but is mitigated by accepting new markers through this API and performing the set above as 
            // this.markers rather than this.Markers.
            ImageToMagnify.Source = bitmapSource;
            displayingImage = true;

            // ensure display image is visible
            if (ThumbnailGrid.IsGridActive == false)
            {
                SwitchToImageView();
            }
            else
            {
                SwitchToThumbnailGridView();
            }
        }

        public bool SetNewVideo(FileInfo videoFile, List<Marker> markersList, long fileIndex)
        {
            this.ClearBoundingBoxes();
            // Check the arguments for null 
            if (videoFile == null || videoFile.Exists == false)
            {
                SetNewImage(ImageValues.FileNoLongerAvailable.Value, markers);
                displayingImage = true;
                return false;
            }

            markers = markersList;
            VideoPlayer.SetSource(new(videoFile.FullName), fileIndex);
            displayingImage = false;

            if (ThumbnailGrid.IsGridActive == false)
            {
                SwitchToVideoView();
            }
            else
            {
                SwitchToThumbnailGridView();
            }

            return true;
        }
        #endregion

        #region Public methods: Scaling and Zooming
        public void ResetMaximumZoom()
        {
            ZoomMaximum = Constant.MarkableCanvas.ImageZoomMaximum;
        }

        // Scale the image around the given image location point, where we are zooming in if
        // zoomIn is true, and zooming out if zoomIn is false
        private void ScaleImage(Point location, bool zoomIn)
        {

            // Get out of here if we are already at our maximum or minimum scaling values 
            // while zooming in or out respectively 
            if ((zoomIn && imageToDisplayScale.ScaleX >= ZoomMaximum) ||
                (!zoomIn && imageToDisplayScale.ScaleX <= Constant.MarkableCanvas.ImageZoomMinimum))
            {
                return;
            }

            // We will scale around the current point
            Point beforeZoom = PointFromScreen(ImageToDisplay.PointToScreen(location));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn)
            {
                // We are zooming in
                // Calculate the scaling factor
                imageToDisplayScale.ScaleX *= Constant.MarkableCanvas.ImageZoomStep;   // Calculate the scaling factor
                imageToDisplayScale.ScaleY *= Constant.MarkableCanvas.ImageZoomStep;

                // Make sure we don't scale beyond the maximum scaling factor
                imageToDisplayScale.ScaleX = Math.Min(ZoomMaximum, imageToDisplayScale.ScaleX);
                imageToDisplayScale.ScaleY = Math.Min(ZoomMaximum, imageToDisplayScale.ScaleY);
            }
            else
            {
                // We are zooming out. 
                // Calculate the scaling factor
                imageToDisplayScale.ScaleX /= Constant.MarkableCanvas.ImageZoomStep;
                imageToDisplayScale.ScaleY /= Constant.MarkableCanvas.ImageZoomStep;

                // Make sure we don't scale beyond the minimum scaling factor
                imageToDisplayScale.ScaleX = Math.Max(Constant.MarkableCanvas.ImageZoomMinimum, imageToDisplayScale.ScaleX);
                imageToDisplayScale.ScaleY = Math.Max(Constant.MarkableCanvas.ImageZoomMinimum, imageToDisplayScale.ScaleY);

                // if there is no scaling, reset translations
                if (Math.Abs(imageToDisplayScale.ScaleX - 1.0) < .0001 && Math.Abs(imageToDisplayScale.ScaleY - 1.0) < .0001)
                {
                    imageToDisplayTranslation.X = 0.0;
                    imageToDisplayTranslation.Y = 0.0;
                }
            }

            Point afterZoom = PointFromScreen(ImageToDisplay.PointToScreen(location));

            // Scale the image, and at the same time translate it so that the 
            // point in the image under the cursor stays there
            lock (ImageToDisplay)
            {
                double imageWidth = ImageToDisplay.Width * imageToDisplayScale.ScaleX;
                double imageHeight = ImageToDisplay.Height * imageToDisplayScale.ScaleY;

                Point center = PointFromScreen(ImageToDisplay.PointToScreen(
                    new(ImageToDisplay.Width / 2.0, ImageToDisplay.Height / 2.0)));

                double newX = center.X - (afterZoom.X - beforeZoom.X);
                double newY = center.Y - (afterZoom.Y - beforeZoom.Y);

                if (newX - imageWidth / 2.0 >= 0.0)
                {
                    newX = imageWidth / 2.0;
                }
                else if (newX + imageWidth / 2.0 <= ActualWidth)
                {
                    newX = ActualWidth - imageWidth / 2.0;
                }

                if (newY - imageHeight / 2.0 >= 0.0)
                {
                    newY = imageHeight / 2.0;
                }
                else if (newY + imageHeight / 2.0 <= ActualHeight)
                {
                    newY = ActualHeight - imageHeight / 2.0;
                }

                imageToDisplayTranslation.X += newX - center.X;
                imageToDisplayTranslation.Y += newY - center.Y;
            }
            RedrawMarkers();
            RefreshBoundingBoxes();
        }


        // Return to the zoomed out level, with no panning
        public void ZoomOutAllTheWay()
        {
            imageToDisplayScale.ScaleX = 1.0;
            imageToDisplayScale.ScaleY = 1.0;
            imageToDisplayTranslation.X = 0.0;
            imageToDisplayTranslation.Y = 0.0;
            RedrawMarkers();
            RefreshBoundingBoxes();
            if (ThumbnailGrid.Visibility == Visibility.Visible)
            {
                SwitchToImageView();
            }
        }
        #endregion

        #region Public methods: Bookmarks
        // Save the current zoom / pan levels as a bookmark
        public void SetBookmark()
        {
            // a user may want to flip between completely zoomed out / normal pan settings and a saved zoom / pan setting that focuses in on a particular region
            // To do this, we save / restore the zoom pan settings of a particular view, or return to the default zoom/pan.
            if (Math.Abs(imageToDisplayScale.ScaleX - 1) < .0001 && Math.Abs(imageToDisplayScale.ScaleY - 1) < .0001)
            {
                // If the scale is unzoomed, then don't bother saving it as it may just be the result of an unintended key press. 
                return;
            }
            bookmark.Set(imageToDisplayScale, imageToDisplayTranslation);
        }

        // This version sets the bookmark with the provided points (retrieved from the registry) indicating scale and translation saved from a previous session
        public void SetBookmark(Point scale, Point translation)
        {
            bookmark.Set(scale, translation);
        }

        // return the current Bookmark scale point
        public Point GetBookmarkScale()
        {
            return bookmark.GetScale();
        }

        // return the current Bookmark Translation as a point
        public Point GetBookmarkTranslation()
        {
            return bookmark.GetTranslation();
        }

        // Return to the zoom / pan levels saved as a bookmark
        public void ApplyBookmark()
        {
            bookmark.Apply(imageToDisplayScale, imageToDisplayTranslation);
            RedrawMarkers();
            RefreshBoundingBoxes();
        }
        #endregion

        #region Public methods: Window shuffling
        public void SwitchToImageView()
        {
            // Just to make sure we are displaying the correct things
            ImageToDisplay.Visibility = Visibility.Visible;
            VideoPlayer.Visibility = Visibility.Collapsed;
            VideoPlayer.Pause();
            SetMagnifiersAccordingToCurrentState(false, true);

            // Signal change in image state (consumed by ImageAdjuster. We check to make sure that its an actual image vs. a placeholder)
            GenerateImageStateChangeEvent(ImageToDisplay.Source != ImageValues.Corrupt.Value && ImageToDisplay.Source != ImageValues.FileNoLongerAvailable.Value);

            if (IsThumbnailGridVisible == false)
            {
                return;
            }
            // These operations are only needed if we weren't in the single image view
            ThumbnailGrid.Visibility = Visibility.Collapsed;
            Action OnSwitchedToSingleImageViewEventAction = SwitchedToSingleImageViewEventAction;
            if (OnSwitchedToSingleImageViewEventAction == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(OnSwitchedToSingleImageViewEventAction));
                return;
            }
            OnSwitchedToSingleImageViewEventAction();

            DataEntryControls.SetEnableState(ControlsEnableStateEnum.SingleImageView, -1);

            // Show the DuplicateIndicator for the main window, if needed
            GlobalReferences.MainWindow.DuplicateDisplayIndicatorInImageIfWarranted();
        }
        public void SwitchToVideoView()
        {
            ImageToDisplay.Visibility = Visibility.Collapsed;
            SetMagnifiersAccordingToCurrentState(false, true);
            //this.OffsetLens.Show = this.MagnifiersEnabled && this.VideoToDisplay.IsUnScaled;
            VideoPlayer.Visibility = Visibility.Visible;
            RedrawMarkers(); // Clears the markers as none should be associated with the video

            GenerateImageStateChangeEvent(false); //  Signal change in image state (consumed by ImageAdjuster)

            if (IsThumbnailGridVisible == false)
            {
                return;
            }
            // These operations are only needed if we weren't in the single image view
            ThumbnailGrid.Visibility = Visibility.Collapsed;
            Action OnSwitchedToSingleImageViewEventAction = SwitchedToSingleImageViewEventAction;
            if (OnSwitchedToSingleImageViewEventAction == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(OnSwitchedToSingleImageViewEventAction));
                return;
            }
            OnSwitchedToSingleImageViewEventAction();

            DataEntryControls.SetEnableState(ControlsEnableStateEnum.SingleImageView, -1);

            // Show the DuplicateIndicator for the main window, if needed
            GlobalReferences.MainWindow.DuplicateDisplayIndicatorInImageIfWarranted();
        }

        public void SwitchToThumbnailGridView()
        {
            // No need to switch as we are already in it
            if (IsThumbnailGridVisible)
            {
                return;
            }
            GenerateImageStateChangeEvent(false); //  Signal change in image state (consumed by ImageAdjuster, if it is visible)

            ThumbnailGrid.Visibility = Visibility.Visible;
            Action OnSwitchedToThumbnailGridViewEventAction = SwitchedToThumbnailGridViewEventAction;
            if (OnSwitchedToThumbnailGridViewEventAction == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(OnSwitchedToThumbnailGridViewEventAction));
                return;
            }
            OnSwitchedToThumbnailGridViewEventAction();
            

            ImageToDisplay.Visibility = Visibility.Collapsed;
            SetMagnifiersAccordingToCurrentState(false, false);
            VideoPlayer.Visibility = Visibility.Collapsed;
            VideoPlayer.Pause();

            // Hide the DuplicateIndicator for the main window
            GlobalReferences.MainWindow.DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Public / Private methods: Draw Bounding Box
        // 
        /// <summary>
        /// Draw bounding boxes into a boundingbox canvas that overlays the MarkableCanvas  
        /// </summary>
        public void ClearBoundingBoxes()
        {
            bboxCanvas.Children.Clear();
            if (Children.Contains(bboxCanvas))
            {
                Children.Remove(bboxCanvas);
            }
            bboxCanvas.Children.Clear();
        }

        // A public version of RefreshBoundingBoxes
        public void RefreshBoundingBoxesIfNeeded()
        {
            RefreshBoundingBoxes();
        }

        private void RefreshBoundingBoxes()
        {
            if (ImageToDisplay != null)
            {
                try // Handle as a no-op for rare bug that occurs when the calling thread cannot access the  object
                {
                    // Remove all prior bounding boxes and then redraw them
                    bboxCanvas.Children.Clear();
                    if (Children.Contains(bboxCanvas))
                    {
                        Children.Remove(bboxCanvas);
                    }
                    bboxCanvas.Children.Clear();

                    // Set the new heights
                    bboxCanvas.Width = ImageToDisplay.RenderSize.Width;
                    bboxCanvas.Height = ImageToDisplay.RenderSize.Height;
                    bool boundingBoxesDrawn = BoundingBoxes.DrawBoundingBoxesInCanvas(bboxCanvas, ImageToDisplay.RenderSize.Width, ImageToDisplay.RenderSize.Height, 0, transformGroup);
                    if (boundingBoxesDrawn)
                    {
                        Children.Add(bboxCanvas);
                    }
                }
                catch
                {
                    TracePrint.Noop();
                }
            }
        }
        #endregion

        #region Public / Private methods: Magnifier Drawing and Zooming
        /// <summary>
        /// Zoom in/out of the magnifying glass / offset lens image (whichever is currently visible) by the zoom step
        /// </summary>
        public void MagnifierOrOffsetChangeZoomLevel(ZoomDirection zoomDirection)
        {
            // Process zoom requests only if the magnifiers are visible, and only when the particular image/video magnifier is being displayed
            if (IsThumbnailGridVisible)
            {
                return;
            }
            if (magnifyingGlass.IsVisible)
            {
                double zoomStep = (zoomDirection == ZoomDirection.ZoomIn) ? -magnifyingGlassZoomStep : magnifyingGlassZoomStep;
                SetMagnifyingGlassZoom(GetMagnifyingGlassZoomFactor() + zoomStep);
            }
            else if (OffsetLens.Show)
            {
                // Adjust the new zoom level for the offset lens, making sure its not below the minimum
                double zoomStep = (zoomDirection == ZoomDirection.ZoomIn) ? -Constant.MarkableCanvas.OffsetLensZoomIncrement : Constant.MarkableCanvas.OffsetLensZoomIncrement;
                double newZoomFactor = OffsetLens.ZoomFactor + zoomStep;

                // Make sure the zoom factor is within bounds
                if (newZoomFactor <= Constant.MarkableCanvas.OffsetLensMinimumZoom)
                {
                    newZoomFactor = Constant.MarkableCanvas.OffsetLensMinimumZoom;
                }
                else if (newZoomFactor > Constant.MarkableCanvas.OffsetLensMaximumZoom)
                {
                    newZoomFactor = Constant.MarkableCanvas.OffsetLensMaximumZoom;
                }
                OffsetLens.ZoomFactor = newZoomFactor;
            }
        }

        /// <summary>
        /// Gets or sets the amount we should zoom (scale) the image in the magnifying glass
        /// </summary>
        private void SetMagnifyingGlassZoom(double value)
        {
            // clamp the value
            if (value < Constant.MarkableCanvas.MagnifyingGlassMaximumZoom)
            {
                value = Constant.MarkableCanvas.MagnifyingGlassMaximumZoom;
            }
            else if (value > Constant.MarkableCanvas.MagnifyingGlassMinimumZoom)
            {
                value = Constant.MarkableCanvas.MagnifyingGlassMinimumZoom;
            }
            magnifyingGlass.ZoomFactor = value;
            GlobalReferences.TimelapseState.MagnifyingGlassZoomFactor = value;

            // update magnifier content if there is something to magnify
            if (ImageToMagnify.Source != null && ImageToDisplay.ActualWidth > 0)
            {
                RedrawMagnifyingGlassIfVisible();
            }
        }

        /// <summary>
        /// Gets or sets the amount we should zoom (scale) the image in the magnifying glass
        /// </summary>
        private double GetMagnifyingGlassZoomFactor()
        {
            return magnifyingGlass.ZoomFactor;
        }

        public void RedrawMagnifyingGlassIfVisible()
        {
            magnifyingGlass.RedrawIfVisible(NativeMethods.GetCursorPos(this), canvasToMagnify);
        }

        public void SetMagnifiersAccordingToCurrentState(bool showMagnifier, bool showOffset)
        {
            magnifyingGlass.Show = showMagnifier && MagnifiersEnabled && displayingImage && IsMouseOverImage();
            // We can't show the offset lens on the scaled video, as scaling the video also scales the offset lens (at least, not until we fix it)!
            OffsetLens.Show = showOffset & MagnifiersEnabled && displayingImage == false && VideoPlayer.IsUnScaled && IsThumbnailGridVisible == false && IsMouseOverVideo();
        }


        // Return true if the mouse cursor is over the image, otherwise false
        private bool IsMouseOverImage()
        {
            Point mousePosition = Mouse.GetPosition(ImageToDisplay);
            return mousePosition.X >= 0 && mousePosition.X <= ImageToDisplay.ActualWidth &&
                    mousePosition.Y >= 0 && mousePosition.Y <= ImageToDisplay.ActualHeight;
        }

        private bool IsMouseOverVideo()
        {
            Point mousePosition = Mouse.GetPosition(VideoPlayer.MediaElement);
            return mousePosition.X >= 0 && mousePosition.X <= VideoPlayer.MediaElement.ActualWidth &&
                   mousePosition.Y >= 0 && mousePosition.Y <= VideoPlayer.MediaElement.ActualHeight;
        }
        #endregion

        #region Public / Private methods: ThumbnailGrid
        // Zoom in (or out) of single image and/or overview 
        public void TryZoomInOrOut(bool zoomIn, Point imageMousePosition, Point videoMousePosition)
        {
            // Manage videos first
            if (IsThumbnailGridVisible == false && ImageToDisplay.IsVisible == false)
            {
                lock (VideoPlayer)
                {
                    // Request Zoom out on a zoomed-in Video
                    if (zoomIn || VideoPlayer.IsUnScaled == false)
                    {
                        VideoPlayer.ScaleVideo(videoMousePosition, zoomIn);
                        SetMagnifiersAccordingToCurrentState(false, true);
                        return;
                    }
                }
            }
            lock (ThumbnailGrid)
            {
                // Request Zoom out on either an unscaled image or the thumbnail grid. 
                // Note on why this is ambiguous: if the thumbnail grid is visible, it means the (hidden) image is also unscaled
                if (zoomIn == false && Math.Abs(imageToDisplayScale.ScaleX - Constant.MarkableCanvas.ImageZoomMinimum) < .0001)
                {
                    // Option 1. Request zoom out on Thumbnail Grid,
                    //           Aborted as we are already at the maximum allowable steps on ThumbnailGrid
                    //if (this.ThumbnailGridState >= Constant.ThumbnailGrid.MaxRows)
                    //{
                    //    return;
                    //}

                    // Option 2. Request zoom out on either the ThumbnailGrid an unscaled image. 
                    bool isInitialSwitchToThumbnailGrid = ThumbnailGrid.IsGridActive;
                    SwitchToThumbnailGridView();

                    // Option 2a. We tried to refresh, but there isn't enough space available on the thumbnail grid.
                    //            Thus try to zoom out again at the next zoom-out level
                    ThumbnailGridRefreshStatus status = RefreshThumbnailGrid(false);
                    if (status == ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell)
                    {
                        TryZoomInOrOut(false, imageMousePosition, videoMousePosition); // STOPPING CONDITION AT MINIMUM???
                        return;
                    }
                    // Option 2b: Zoom out request denied.

                    if (status == ThumbnailGridRefreshStatus.Aborted || status == ThumbnailGridRefreshStatus.AtMaximumZoomLevel)
                    {
                        return;
                    }

                    // Option 2c. We've gone from the single image to the multi-image view.
                    // By default, select the first item (as we want the data for the first item to remain displayed)
                    if (isInitialSwitchToThumbnailGrid)
                    {
                        ThumbnailGrid.SelectInitialCellOnly();
                        DataEntryControls.SetEnableState(ControlsEnableStateEnum.MultipleImageView, ThumbnailGrid.SelectedCount());
                    }
                }
                else if (IsThumbnailGridVisible)
                {
                    // State: currently zoomed in on ThumbnailGrid, but not at the minimum step
                    // Zoom in another step
                    //this.ThumbnailGridState--;
                    ThumbnailGridRefreshStatus status = RefreshThumbnailGrid(zoomIn);
                    if (status == ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell)
                    {
                        // we couldn't refresh the grid, likely because there is not enough space available to show even a single image at this image state
                        // So try again by zooming in another step
                        TryZoomInOrOut(zoomIn, imageMousePosition, videoMousePosition);
                    }
                    else if (status == ThumbnailGridRefreshStatus.AtMaximumZoomLevel
                        || status == ThumbnailGridRefreshStatus.Aborted)

                    {
                        // return;
                    }
                    else if (status == ThumbnailGridRefreshStatus.AtZeroZoomLevel)
                    {
                        if (displayingImage)
                        {
                            SwitchToImageView();
                        }
                        else
                        {
                            SwitchToVideoView();
                        }
                    }
                }
                else if (IsThumbnailGridVisible)
                {
                    // State: zoomed in on ThumbnailGrid, but at the minimum step
                    // Switch to the image or video, depending on what was last displayed
                    // update the magnifying glass

                    if (displayingImage)
                    {
                        SwitchToImageView();
                    }
                    else
                    {
                        SwitchToVideoView();
                    }
                }
                else
                {
                    if (displayingImage)
                    {
                        // If we are zooming in off the image, then correct the mouse position to the edge of the image
                        if (imageMousePosition.X > ImageToDisplay.ActualWidth)
                        {
                            imageMousePosition.X = ImageToDisplay.ActualWidth;
                        }
                        if (imageMousePosition.Y > ImageToDisplay.ActualHeight)
                        {
                            imageMousePosition.Y = ImageToDisplay.ActualHeight;
                        }
                        ScaleImage(imageMousePosition, zoomIn);
                    }
                }
            }
        }

        // Refresh only the episode information in the thumbnail grid
        public void DisplayEpisodeTextInThumbnailGridIfWarranted()
        {
            ThumbnailGrid.RefreshEpisodeTextIfWarranted();
        }

        // If the ThumbnailGrid is displayed, refresh it. Use a timer if the we are navigating via a slider (to avoid excessive refreshes)
        public void RefreshIfMultipleImagesAreDisplayed(bool isInSliderNavigation)
        {
            if (IsThumbnailGridVisible)
            {
                // State: zoomed in on ThumbnailGrid.
                // Updating it ensures that the correct image is shown as the first cell
                // However, if we are navigating with the slider, delay update as otherwise it can't keep up
                if (isInSliderNavigation)
                {
                    // Refresh the ThumbnailGrid only via the timer, where it will 
                    // try to refresh only when the user pauses (or ends) navigation via the slider
                    timerSlider.Stop();
                    timerSlider.Start();
                }
                else
                {
                    RefreshThumbnailGrid(null); // null signals a refresh at the current zoom level
                }
            }
        }

        // Refresh the ThumbnailGrid
        public ThumbnailGridRefreshStatus RefreshThumbnailGrid(bool? zoomIn)
        {
            if (ThumbnailGrid == null)
            {
                return ThumbnailGridRefreshStatus.Aborted;
            }
            // Find the current height of the available space and split it the number of rows defined by the state. i.e. state 1 is 2 rows, 2 is 3 rows, etc.
            // However, if the resulting image is less than a minimum height, then ignore it.
            //if (!resizing && cellHeight < Constant.ThumbnailGrid.MinumumThumbnailHeight) return ThumbnailGridRefreshStatus.AtMaximumZoomLevel;

                return ThumbnailGrid.Refresh(ThumbnailGrid.Width, ThumbnailGrid.Height, zoomIn);
        }

        private void TimerSlider_Tick(object sender, EventArgs e)
        {
            timerSlider.Stop();
            RefreshThumbnailGrid(null); // null signals a refresh at the current zoom level
        }
        #endregion

        #region Mouse Event Handlers
        // On Mouse down, record the location, and who sent it.
        // We will use this information on move and up events to discriminate between 
        // panning/zooming vs. marking. 
        private void ImageVideoOrCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            previousMousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                mouseDownLocation = (displayingImage)
                    ? e.GetPosition(ImageToDisplay)
                    : e.GetPosition(VideoPlayer.MediaElement);
                mouseDownSender = (UIElement)sender;
                mouseDownLocation = transformGroup.Transform(mouseDownLocation); // In case we are panning
                // If its more than the given time interval since the last click, then we are on the 2nd click of a double click
                // If we aren't then we are on the first click and thus we want to reset the time.
                TimeSpan timeSinceLastClick = DateTime.Now - mouseDoubleClickTime;
                if (timeSinceLastClick.TotalMilliseconds < Constant.MarkableCanvas.DoubleClickTimeThreshold.TotalMilliseconds)
                {
                    isDoubleClick = true;
                }
                else
                {
                    isDoubleClick = false;
                    mouseDoubleClickTime = DateTime.Now;
                }
                // Panning: ensure we are reset to false at the beginning of a mouse down
                isPanning = false;
            }
        }

        // Unused. Trigger a mouse move event. This is used to keep the emagnifying glass in view when switching files.
        // ReSharper disable once UnusedMember.Global
        public void TriggerMouseMoveEvent()
        {
            MouseEventArgs e = new(Mouse.PrimaryDevice, 0)
            {
                RoutedEvent = Mouse.MouseMoveEvent
            };
            RaiseEvent(e);
        }

        // If we move the mouse with the left mouse button press, translate the image
        private void MarkableCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePosition = (displayingImage)
                    ? e.GetPosition(ImageToDisplay)
                    : e.GetPosition(VideoPlayer.MediaElement);

            // If we are not yet in panning mode, switch into it if the user has moved at least the threshold distance from mouse down position
            if (e.LeftButton == MouseButtonState.Pressed && isPanning == false && (mouseDownLocation - mousePosition).Length > Constant.MarkableCanvas.MarkingVsPanningDistanceThreshold)
            {
                isPanning = true;
            }

            // The magnifying glass is visible only if the current mouse position is over the image. 
            // Note that it uses the actual (transformed) bounds of the image            
            if (magnifyingGlass.IsEnabled && displayingImage)
            {

                SetMagnifiersAccordingToCurrentState(true, false);
            }
            else if (OffsetLens.IsEnabled && displayingImage == false)
            {
                SetMagnifiersAccordingToCurrentState(false, true);
            }

            if (isPanning)
            {
                // If the left button is pressed, translate (pan) across the scaled image or video
                // We hide the magnifying glass during panning so it won't be distracting.
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    // Don't show magnifiers when panning
                    SetMagnifiersAccordingToCurrentState(false, false);
                    if (displayingImage)
                    {
                        // Translation is possible only if the image isn't already scaled
                        if (Math.Abs(imageToDisplayScale.ScaleX - 1.0) > .0001 || Math.Abs(imageToDisplayScale.ScaleY - 1.0) > .0001)
                        {
                            Cursor = Cursors.ScrollAll;    // Change the cursor to a panning cursor
                            mousePosition = transformGroup.Transform(mousePosition);
                            TranslateImage(mousePosition);
                        }
                    }
                    else
                    {
                        // Translation is possible only if the video isn't already scaled
                        if (VideoPlayer.IsUnScaled == false)
                        {
                            Cursor = Cursors.ScrollAll;    // Change the cursor to a panning cursor
                            VideoPlayer.TranslateVideo(mousePosition, previousMousePosition);
                        }
                    }
                }
            }
            else
            {
                // Ensure the cursor is a normal arrow cursor
                Cursor = Cursors.Arrow;
            }
            canvasToMagnify.Width = ImageToMagnify.ActualWidth;      // Make sure that the canvas is the same size as the image
            canvasToMagnify.Height = ImageToMagnify.ActualHeight;

            // update the magnifying glass
            RedrawMagnifyingGlassIfVisible();
            previousMousePosition = mousePosition;
        }

        private void ImageVideoOrCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Make sure the cursor reverts to the normal arrow cursor
            Cursor = Cursors.Arrow;
            mouseDoubleClickTime = DateTime.Now;

            // Is this the end of a translate operation, or of placing a marker?
            // We decide by checking if the left button has been released, the mouse location is
            // smaller than a given threshold, and less than 200 ms have passed since the original
            // mouse down. i.e., the use has done a rapid click and release on a small location
            if ((e.LeftButton == MouseButtonState.Released) &&
                (Equals(sender, mouseDownSender)) &&
                isPanning == false &&
                isDoubleClick == false)
            {
                if (displayingImage && GlobalReferences.TimelapseState.IsViewOnly == false)
                {
                    // Note that the test above is to ensure that we don't create markers in view-only mode.
                    // Get the current point, and create a marker on it.
                    Point position = e.GetPosition(ImageToDisplay);
                    position = Marker.ConvertPointToRatio(position, ImageToDisplay.ActualWidth, ImageToDisplay.ActualHeight);
                    if (Marker.IsPointValidRatio(position))
                    {
                        // Add the marker if its between 0,0 and 1,1. This should always be the case, but there was one case
                        // where it was recorded in the database as Ininity, INfinity, so this should guard against that.
                        Marker marker = new(null, position);

                        // don't add marker to the marker list
                        // Main window is responsible for filling in remaining properties and adding it.
                        SendMarkerEvent(new(marker, true));
                        e.Handled = true;
                    }
                }
                else
                {
                    // The video player is displayed and we are not panning)
                    // Toggle Play or Pause 
                    VideoPlayer.TryTogglePlayOrPause();
                }
            }
            // Show the magnifying glass if its enables, as it may have been hidden during other mouseDown operations
            // this.ShowMagnifierIfEnabledOtherwiseHide();
            SetMagnifiersAccordingToCurrentState(true, true);
            RedrawMagnifyingGlassIfVisible();
        }

        // Remove a marker on a right mouse button up event
        private void Marker_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                // We don't delete markers in view-only mode.
                return;
            }

            Canvas canvas = (Canvas)sender;
            Marker marker = (Marker)canvas.Tag;
            Markers.Remove(marker);
            SendMarkerEvent(new(marker, false));
            RedrawMarkers();
        }


        private void ImageOrCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool zoomIn = e.Delta > 0; // Zooming in if delta is positive, else zooming out

            // Eliminate overly exuberant mouse wheel events
            // Check the time interval between mouse wheel events. If below a threshold, ignore the event.
            // 1. This manages rapid turns of the wheel that would otherwise cause over-shooting of desired zoom.
            // 2. It introduces a longer time threshold to switch from the image to the ThumbnailGrid, in order to give a natural 'break point' between the two.
            // 3. A windows 10 bug (so it seems) generates 2 mouse wheel events for every mouse wheel click
            //    This tries to catch that and eliminate the second click. 

            TimeSpan timeDifference = DateTime.Now - lastMouseWheelDateTime;
            if (timeDifference < TimeSpan.FromMilliseconds(500)) // At least a 500 msecs delay in use of the scroll wheel is needed between transitions
            {
                if (zoomIn &&
                    ((ImageToDisplay.Visibility == Visibility.Visible && Math.Abs(imageToDisplayScale.ScaleX - Constant.MarkableCanvas.ImageZoomMinimum) < .0001)
                     || (VideoPlayer.Visibility == Visibility.Visible && VideoPlayer.IsUnScaled)))
                {
                    // Pause on the transition from unzoomed image/video to zoomed image/video
                    return;
                }

                if (zoomIn == false &&
                    ((ImageToDisplay.Visibility == Visibility.Visible && Math.Abs(imageToDisplayScale.ScaleX - Constant.MarkableCanvas.ImageZoomMinimum) < .0001)
                      || (VideoPlayer.Visibility == Visibility.Visible && VideoPlayer.IsUnScaled)))
                {
                    // Pause on the transition from unscaled image/video to thumbnail Grid
                    return;
                }
            }
            lastMouseWheelDateTime = DateTime.Now;

            // Zoom in or out
            Point imageMousePosition = e.GetPosition(ImageToDisplay);
            Point videoMousePosition = e.GetPosition(VideoPlayer.MediaElement);
            TryZoomInOrOut(zoomIn, imageMousePosition, videoMousePosition);
            e.Handled = true; // As otherwise it may be invoked twice by both the marker and markable canvase
        }

        // Hide the magnifying glass when the mouse cursor leaves the image
        private void ImageOrCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            SetMagnifiersAccordingToCurrentState(false, false);
        }
        #endregion

        #region SizeChanged Event Handlers
        private void ImageToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // keep the magnifying glass canvas in sync with the magnified image size
            // this update triggers a call to CanvasToMagnify_SizeChanged
            canvasToMagnify.Width = ImageToMagnify.ActualWidth;
            canvasToMagnify.Height = ImageToMagnify.ActualHeight;
        }

        // resize content and update transforms when canvas size changes
        private void MarkableImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ImageToDisplay.Width = ActualWidth;
            ImageToDisplay.Height = ActualHeight;

            VideoPlayer.Width = ActualWidth;
            VideoPlayer.Height = ActualHeight;

            ThumbnailGrid.Width = ActualWidth;
            ThumbnailGrid.Height = ActualHeight;
            if (ThumbnailGrid.Visibility == Visibility.Visible)
            {
                // Refresh the ThumbnailGrid only via the timer, where it will 
                // try to refresh only if the SizeChanged event doesn't refire after the given interval i.e.,
                // when the user pauses or completes the manual resizing action
                timerResize.Stop();
                timerResize.Start();
            }

            imageToDisplayScale.CenterX = 0.5 * ActualWidth;
            imageToDisplayScale.CenterY = 0.5 * ActualHeight;

            // clear the bookmark (if any) as it will no longer be correct
            // if needed, the bookmark could be rescaled instead
            // this.bookmark.Reset();
        }

        // Refresh the ThumbnailGrid when the timer fires 
        private void TimerResize_Tick(object sender, EventArgs e)
        {
            timerResize.Stop();
            if (ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell == RefreshThumbnailGrid(null)) // null signals a refresh at the current zoom level
            {
                // We couldn't show at least one image in the overview, so go back to the normal view
                SwitchToImageView();
            }
        }

        private void CanvasToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // redraw markers so they're in the right place to appear in the magnifying glass
            RedrawMarkers();
            RefreshBoundingBoxes();
            // update the magnifying glass's contents
            RedrawMagnifyingGlassIfVisible();
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void ImageToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawMarkers();
            RefreshBoundingBoxes();
        }

        // Whenever the image size changes, refresh the markers so they appear in the correct place
        private void VideoToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawMarkers();
            RefreshBoundingBoxes();
        }
        #endregion

        #region Key Event Handlers
        // if it's < or > key zoom out or in around the mouse point
        // If its an H, RedrawBoundingBoxes will hide ow the detection boxes
        private void MarkableCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.OemPeriod:  // Key.>
                    // zoom in
                    Point imageMousePosition = Mouse.GetPosition(ImageToDisplay);
                    Point videoMousePosition = Mouse.GetPosition(VideoPlayer.MediaElement);
                    TryZoomInOrOut(true, imageMousePosition, videoMousePosition);
                    break;
                // zoom out // Key,>
                case Key.OemComma:
                    // zoom out
                    Point imageMousePosition2 = Mouse.GetPosition(ImageToDisplay);
                    Point videoMousePosition2 = Mouse.GetPosition(VideoPlayer.MediaElement);
                    TryZoomInOrOut(false, imageMousePosition2, videoMousePosition2);
                    break;
                // if the current file's a video allow the user to hit the space bar to start or stop playing the video
                case Key.Space:
                    // This is desirable as the play or pause button doesn't necessarily have focus and it saves the user having to click the button with
                    // the mouse.
                    if (VideoPlayer.TryTogglePlayOrPause() == false)
                    {
                        return;
                    }
                    break;
                case Key.R:
                    // Try going to the best frame, if there is one
                    if (VideoPlayer.IsVisible && null != VideoPlayer?.MediaElement?.Source)
                    {
                        VideoPlayer.TryGoToBestFrame();
                    }
                    break;
                //case Key.F5:
                // TODO: This may no longer be needed
                //    // Refresh the video, if one is showing
                //    if (VideoPlayer.IsVisible && null != VideoPlayer?.MediaElement?.Source)
                //    {
                //        VideoPlayer.TryRefreshSource();
                //    }
                //    break;
                case Key.H:
                    // Will hide detection boxes, if any
                    if (!e.IsRepeat)
                    {
                        if (IsThumbnailGridVisible == false)
                        {
                            RefreshBoundingBoxes();
                            GlobalReferences.MainWindow.DuplicateDisplayIndicatorInImageIfWarranted();
                        }
                        else
                        {
                            ThumbnailGrid.RefreshBoundingBoxesAndEpisodeInfo();
                        }
                    }
                    break;
                case Key.P:
                    // Show previous/next image in episode in a popup, regardless of the current selection
                    if (!IsThumbnailGridVisible && !e.IsRepeat)
                    {
                        EpisodePopupIsVisible(true);
                    }
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        // If its an H, RedrawBoundingBoxes will show the detection boxes
        private void MarkableCanvas_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.H:
                    // Will show detection boxes, if any
                    if (!e.IsRepeat)
                    {
                        if (IsThumbnailGridVisible == false)
                        {
                            RefreshBoundingBoxes();
                            GlobalReferences.MainWindow.DuplicateDisplayIndicatorInImageIfWarranted();
                        }
                        else
                        {
                            ThumbnailGrid.RefreshBoundingBoxesAndEpisodeInfo();
                        }
                    }
                    break;
                case Key.P:
                    // Show previous/next image regardless of selection
                    if (!e.IsRepeat)
                    {
                        EpisodePopupIsVisible(false);
                    }
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }
        #endregion

        #region Private methods: Translate Image
        // Given the mouse location on the image, translate the image
        // This is normally called from a left mouse move event
        private void TranslateImage(Point mousePosition)
        {
            // Get the center point on the image
            Point center = PointFromScreen(ImageToDisplay.PointToScreen(new(ImageToDisplay.Width / 2.0, ImageToDisplay.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + mousePosition.X - previousMousePosition.X;
            double newY = center.Y + mousePosition.Y - previousMousePosition.Y;

            // get the translated image width
            double imageWidth = ImageToDisplay.Width * imageToDisplayScale.ScaleX;
            double imageHeight = ImageToDisplay.Height * imageToDisplayScale.ScaleY;

            // Limit the delta position so that the image stays on the screen
            if (newX - imageWidth / 2.0 >= 0.0)
            {
                newX = imageWidth / 2.0;
            }
            else if (newX + imageWidth / 2.0 <= ActualWidth)
            {
                newX = ActualWidth - imageWidth / 2.0;
            }

            if (newY - imageHeight / 2.0 >= 0.0)
            {
                newY = imageHeight / 2.0;
            }
            else if (newY + imageHeight / 2.0 <= ActualHeight)
            {
                newY = ActualHeight - imageHeight / 2.0;
            }

            // Translate the canvas and redraw the markers
            imageToDisplayTranslation.X += newX - center.X;
            imageToDisplayTranslation.Y += newY - center.Y;

            RedrawMarkers();
            RefreshBoundingBoxes();
        }
        #endregion

        #region Private methods: Episodes
        // Display or hide the episode popup
        private void EpisodePopupIsVisible(bool isVisible)
        {
            FileDatabase fileDatabase = GlobalReferences.MainWindow?.DataHandler?.FileDatabase;
            if (fileDatabase == null)
            {
                return;
            }
            if (episodePopup == null)
            {
                episodePopup = new(this, fileDatabase, 160);
            }
            else
            {
                // reset the filedatabase just in case it has been reloaded
                // to a new image set since the last time we used it
                episodePopup.FileDatabase = fileDatabase;
            }
            episodePopup.Show(isVisible, 6);
        }
        #endregion

        #region Private methods: Draw Marker Methods
        private Canvas DrawMarker(Marker marker, Size canvasRenderSize, bool doTransform)
        {
            Canvas markerCanvas = new();
            markerCanvas.MouseRightButtonUp += Marker_MouseRightButtonUp;
            markerCanvas.MouseWheel += ImageOrCanvas_MouseWheel; // Make the mouse wheel work over marks as well as the image

            markerCanvas.ToolTip = string.IsNullOrEmpty(marker.Tooltip.Trim()) 
                ? null 
                : marker.Tooltip;
            markerCanvas.Tag = marker;

            // Create a marker
            Ellipse mark = new()
            {
                Width = Constant.MarkableCanvas.MarkerDiameter,
                Height = Constant.MarkableCanvas.MarkerDiameter,
                Stroke = marker.Brush,
                StrokeThickness = Constant.MarkableCanvas.MarkerStrokeThickness,
                Fill = MarkerFillBrush
            };
            markerCanvas.Children.Add(mark);

            // Draw another Ellipse as a black outline around it
            Ellipse blackOutline = new()
            {
                Stroke = Brushes.Black,
                Width = mark.Width + 1,
                Height = mark.Height + 1,
                StrokeThickness = 1
            };
            markerCanvas.Children.Add(blackOutline);

            // And another Ellipse as a white outline around it
            Ellipse whiteOutline = new()
            {
                Stroke = Brushes.White,
                Width = blackOutline.Width + 1,
                Height = blackOutline.Height + 1,
                StrokeThickness = 1
            };
            markerCanvas.Children.Add(whiteOutline);

            // maybe add emphasis
            double outerDiameter = whiteOutline.Width;
            Ellipse glow = null;
            if (marker.Emphasise)
            {
                glow = new()
                {
                    Width = whiteOutline.Width + Constant.MarkableCanvas.MarkerGlowDiameterIncrease,
                    Height = whiteOutline.Height + Constant.MarkableCanvas.MarkerGlowDiameterIncrease,
                    StrokeThickness = Constant.MarkableCanvas.MarkerGlowStrokeThickness,
                    Stroke = mark.Stroke,
                    Opacity = Constant.MarkableCanvas.MarkerGlowOpacity
                };
                markerCanvas.Children.Add(glow);

                outerDiameter = glow.Width;
            }

            markerCanvas.Width = outerDiameter;
            markerCanvas.Height = outerDiameter;

            double position = (markerCanvas.Width - mark.Width) / 2.0;
            SetLeft(mark, position);
            SetTop(mark, position);

            position = (markerCanvas.Width - blackOutline.Width) / 2.0;
            SetLeft(blackOutline, position);
            SetTop(blackOutline, position);

            position = (markerCanvas.Width - whiteOutline.Width) / 2.0;
            SetLeft(whiteOutline, position);
            SetTop(whiteOutline, position);

            if (marker.Emphasise && glow != null)
            {
                position = (markerCanvas.Width - glow.Width) / 2.0;
                SetLeft(glow, position);
                SetTop(glow, position);
            }

            if (marker.ShowLabel)
            {
                TextBlock label = new()
                {
                    Text = marker.Tooltip,
                    IsHitTestVisible = false,
                    Opacity = 0.6,
                    Background = Brushes.White,
                    Padding = new(0, 0, 0, 0),
                    Margin = new(0, 0, 0, 0)
                };
                markerCanvas.Children.Add(label);

                position = (markerCanvas.Width / 2.0) + (whiteOutline.Width / 2.0);
                SetLeft(label, position);
                SetTop(label, markerCanvas.Height / 2);
            }

            // Get the point from the marker, and convert it so that the marker will be in the right place
            if (Marker.IsPointValidRatio(marker.Position) == false)
            {
                // We had one case where the marker point was recorded as Infinity,Infinity. Not sure why.
                // As a workaround, we just make sure the marker is a valid ration. If it isn't we just put the marker in the middle
                // Yup, a hack, but its a very rare bug and thus this is good enough. 
                // While we can instead repair the database, its not really worth the bother of coding that.
                marker.Position = new(.5, .5);
            }
            Point screenPosition = Marker.ConvertRatioToPoint(marker.Position, canvasRenderSize.Width, canvasRenderSize.Height);
            if (doTransform)
            {
                screenPosition = transformGroup.Transform(screenPosition);
            }

            SetLeft(markerCanvas, screenPosition.X - markerCanvas.Width / 2.0);
            SetTop(markerCanvas, screenPosition.Y - markerCanvas.Height / 2.0);
            SetZIndex(markerCanvas, 0);
            markerCanvas.MouseDown += ImageVideoOrCanvas_MouseDown;
            markerCanvas.MouseMove += MarkableCanvas_MouseMove;
            markerCanvas.MouseLeftButtonUp += ImageVideoOrCanvas_MouseUp;
            return markerCanvas;
        }

        private void DrawMarkers(Canvas canvas, Size canvasRenderSize, bool doTransform)
        {
            if (Markers != null)
            {
                foreach (Marker marker in Markers)
                {
                    Canvas markerCanvas = DrawMarker(marker, canvasRenderSize, doTransform);
                    canvas.Children.Add(markerCanvas);
                }
            }
        }

        /// <summary>
        /// Remove all and then draw all the markers
        /// </summary>
        private void RedrawMarkers()
        {
            RemoveMarkers(this);
            RemoveMarkers(canvasToMagnify);
            if (ImageToDisplay != null)
            {
                DrawMarkers(this, ImageToDisplay.RenderSize, true);
                DrawMarkers(canvasToMagnify, canvasToMagnify.RenderSize, false);
            }
        }

        // remove all markers from the canvas
        private void RemoveMarkers(Canvas canvas)
        {
            for (int index = canvas.Children.Count - 1; index >= 0; index--)
            {
                if (canvas.Children[index] is Canvas && canvas.Children[index] != magnifyingGlass)
                {
                    // Its either a marker or a bounding box, so we have to figure out which one.
                    if (canvas.Children[index] is Canvas { Tag: not null } tempCanvas && tempCanvas.Tag.ToString() != Constant.MarkableCanvas.BoundingBoxCanvasTag)
                    {
                        canvas.Children.RemoveAt(index);
                    }
                }
            }
        }
        #endregion
    }
}
