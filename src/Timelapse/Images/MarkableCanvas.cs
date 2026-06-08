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
        /// Gets the virtualized scrollable thumbnail grid
        /// </summary>
        public ThumbnailGridVirtualized ThumbnailGridVirtualized { get; }

        public DataEntryControls DataEntryControls
        {
            get;
            set
            {
                ThumbnailGridVirtualized.DataEntryControls = value;
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
        /// Whether the virtualized thumbnail grid is visible or not
        /// </summary>
        public bool IsThumbnailGridVirtualizedVisible => ThumbnailGridVirtualized.Visibility == Visibility.Visible;

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

        public bool IsZooming => IsThumbnailGridVirtualizedVisible || Math.Abs(imageToDisplayScale.ScaleX) - 1 > 1e-5;

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

        // Whether to force updating on an image with ImageProcessing settings (if that control is visible) whenever a new image is displayed
        private bool forceImageProcessingUpdate;

        // Dedicated lock object for zoom/pan operations.
        // Replaces the previous pattern of lock(ImageToDisplay), lock(VideoPlayer), and lock(ThumbnailGrid),
        // which used WPF UI elements as monitor objects — an anti-pattern since WPF controls have
        // thread affinity and should never be used as lock targets.
        private readonly System.Threading.Lock _zoomLock = new();

        private bool isRefreshBoundingBoxesPending;
        private DateTime ctrlScrollOutAtMinZoomStartTime = DateTime.MinValue;
        private DateTime lastZoomChangeTime = DateTime.MinValue;
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

            // Set up virtualized scrollable thumbnail grid
            ThumbnailGridVirtualized = new()
            {
                Visibility = Visibility.Collapsed
            };

            SetZIndex(ThumbnailGridVirtualized, 1000);
            SetLeft(ThumbnailGridVirtualized, 0);
            SetTop(ThumbnailGridVirtualized, 0);
            Children.Add(ThumbnailGridVirtualized);

            // ScrollViewer inside ThumbnailGridVirtualized blocks all WheelEvents from bubbling,
            // so Ctrl+scroll cannot reach ImageOrCanvas_MouseWheel naturally. The grid raises this
            // event explicitly; we forward it to TryZoomInOrOutVirtualized with the same de-bounce.
            ThumbnailGridVirtualized.CtrlMouseWheelScrolled += (_, wheelArgs) =>
            {
                TimeSpan diff = DateTime.Now - lastMouseWheelDateTime;
                if (diff >= TimeSpan.FromMilliseconds(500))
                {
                    lastMouseWheelDateTime = DateTime.Now;
                    TryZoomInOrOutVirtualized(wheelArgs.Delta > 0);
                }
            };
            ThumbnailGridVirtualized.DeactivateRequested += (_, _) =>
            {
                ThumbnailGridVirtualized.Reset();
                if (displayingImage) SwitchToImageView();
                else SwitchToVideoView();
            };

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
            // FIX: Tunnels before any child (including Slider.Thumb which marks bubble MouseDown as handled),
            // guaranteeing we can reset pan state on every new press regardless of which child the cursor is over.
            PreviewMouseLeftButtonDown += MarkableCanvas_PreviewMouseLeftButtonDown;
            VideoPlayer.MediaElement.MouseLeave += MediaElementMouseLeave;
            PreviewKeyDown += MarkableCanvas_PreviewKeyDown;
            PreviewKeyUp += MarkableCanvas_PreviewKeyUp;
            Loaded += MarkableCanvas_Loaded;

            // When started, refreshes the ThumbnailGrid after 100 msecs (unless the timer is reset or stopped)
            timerResize.Interval = TimeSpan.FromMilliseconds(200);
            timerResize.Tick += TimerResize_Tick;

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

            //ImageToDisplay.Source = bitmapSource;
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

            if (GlobalReferences.MainWindow.ImageAdjuster?.IsVisible == true && GlobalReferences.MainWindow.ImageAdjuster.UpdateAutomatically && GlobalReferences.MainWindow.ImageAdjuster.IsNeutralImageAppearance() == false
                && false == Constant.ImageValues.IsPlaceholderImage(bitmapSource))
            {
                // Update the image as at least one parameter has changed (which will affect the image's appearance)
                // TODO Note that because of the delay, the markers and bounding box may appear before the image appears. Not sure if its easily fixed
                forceImageProcessingUpdate = true;
                timerImageProcessingUpdate.Start();
            }
            else
            {
                ImageToDisplay.Source = bitmapSource;
            }
            ImageToMagnify.Source = bitmapSource;
            displayingImage = true;

            // ensure display image is visible
            if (!IsThumbnailGridVirtualizedVisible)
            {
                SwitchToImageView();
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

            if (!IsThumbnailGridVirtualizedVisible)
            {
                SwitchToVideoView();
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
            lock (_zoomLock)
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
            if (IsThumbnailGridVirtualizedVisible)
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
            string toastMessage;
            NotificationOptions toastOptions = new()
            {
                ShowCloseButton = true,
                CloseAfter = 3000,
            };
            if (Math.Abs(imageToDisplayScale.ScaleX - 1) < .0001 && Math.Abs(imageToDisplayScale.ScaleY - 1) < .0001)
            {
                // If the scale is unzoomed, then don't bother saving it as it may just be the result of an unintended key press.
                toastMessage = "No bookmark saved, as you are not zoomed in.";
            }
            else
            {
                bookmark.Set(imageToDisplayScale, imageToDisplayTranslation);
                toastMessage = $"The current zoom and pan settings have been saved as a bookmark.";
            }
            GlobalReferences.MainWindow.ToastNotifier.ShowSuccess(toastMessage, toastOptions);
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

            if (IsThumbnailGridVirtualizedVisible == false)
            {
                return;
            }
            // These operations are only needed if we weren't in the single image view
            ThumbnailGridVirtualized.Visibility = Visibility.Collapsed;
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

            if (IsThumbnailGridVirtualizedVisible == false)
            {
                return;
            }
            // These operations are only needed if we weren't in the single image view
            ThumbnailGridVirtualized.Visibility = Visibility.Collapsed;
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

        #endregion

        public void SwitchToThumbnailGridVirtualizedView()
        {
            if (IsThumbnailGridVirtualizedVisible) return;
            GenerateImageStateChangeEvent(false);
            ThumbnailGridVirtualized.Visibility = Visibility.Visible;
            ImageToDisplay.Visibility = Visibility.Collapsed;
            SetMagnifiersAccordingToCurrentState(false, false);
            VideoPlayer.Visibility = Visibility.Collapsed;
            VideoPlayer.Pause();
            GlobalReferences.MainWindow.DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;

            Action onSwitched = SwitchedToThumbnailGridViewEventAction;
            onSwitched?.Invoke();
        }

        public ThumbnailGridRefreshStatus RefreshThumbnailGridVirtualized(bool? zoomIn)
        {
            if (ThumbnailGridVirtualized == null)
                return ThumbnailGridRefreshStatus.Aborted;
            return ThumbnailGridVirtualized.Refresh(ThumbnailGridVirtualized.Width, ThumbnailGridVirtualized.Height, zoomIn);
        }

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

        // RefreshBoundingBoxes can be invoked multiple times in rapid succession for the same image (e.g., during the initial image  
        // display and the subsequent resize events). By using a guard flag, it will only execute the actual drawing logic once per      
        // render cycle. This ensures that bounding boxes are still displayed on placeholder images but       
        // without the redundant multiple-invocation overhead.
        private void RefreshBoundingBoxes()
        {
            if (isRefreshBoundingBoxesPending)
            {
                return;
            }

            isRefreshBoundingBoxesPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActuallyRefreshBoundingBoxes();
                isRefreshBoundingBoxesPending = false;
            }), DispatcherPriority.Render);
        }

        private void ActuallyRefreshBoundingBoxes()
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
            if (IsThumbnailGridVirtualizedVisible)
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
            OffsetLens.Show = showOffset & MagnifiersEnabled && displayingImage == false && VideoPlayer.IsUnScaled && IsThumbnailGridVirtualizedVisible == false && IsMouseOverVideo();
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

        // Flip the OffsetLens to whichever quadrant keeps it on-screen.
        private void UpdateOffsetLensDirection(Point mousePosition)
        {
            double width = VideoPlayer.MediaElement.ActualWidth;
            double height = VideoPlayer.MediaElement.ActualHeight;
            const double EdgeThreshold = Constant.MarkableCanvas.MagnifyingGlassDiameter;

            bool nearLeft = mousePosition.X < EdgeThreshold;
            bool nearRight = mousePosition.X > width - EdgeThreshold;
            bool nearTop = mousePosition.Y < EdgeThreshold;
            bool nearBot = mousePosition.Y > height - EdgeThreshold;

            OffsetLensDirection newDirection;
            if (nearTop && nearRight)
                newDirection = OffsetLensDirection.BottomLeft;
            else if (nearTop && nearLeft)
                newDirection = OffsetLensDirection.BottomRight;
            else if (nearBot && nearRight)
                newDirection = OffsetLensDirection.TopLeft;
            else if (nearBot && nearLeft)
                newDirection = OffsetLensDirection.TopRight;
            else if (nearRight)
                // keep vertical side, flip horizontal to Left
                newDirection = (OffsetLens.Direction == OffsetLensDirection.TopRight || OffsetLens.Direction == OffsetLensDirection.TopLeft)
                    ? OffsetLensDirection.TopLeft
                    : OffsetLensDirection.BottomLeft;
            else if (nearLeft)
                // keep vertical side, flip horizontal to Right
                newDirection = (OffsetLens.Direction == OffsetLensDirection.TopRight || OffsetLens.Direction == OffsetLensDirection.TopLeft)
                    ? OffsetLensDirection.TopRight
                    : OffsetLensDirection.BottomRight;
            else if (nearTop)
                // keep horizontal side, flip to Bottom
                newDirection = (OffsetLens.Direction == OffsetLensDirection.TopLeft || OffsetLens.Direction == OffsetLensDirection.BottomLeft)
                    ? OffsetLensDirection.BottomLeft
                    : OffsetLensDirection.BottomRight;
            else if (nearBot)
                // keep horizontal side, flip to Top
                newDirection = (OffsetLens.Direction == OffsetLensDirection.TopLeft || OffsetLens.Direction == OffsetLensDirection.BottomLeft)
                    ? OffsetLensDirection.TopLeft
                    : OffsetLensDirection.TopRight;
            else
                newDirection = OffsetLens.Direction; // centre — no change

            if (newDirection != OffsetLens.Direction)
                OffsetLens.SetDirection(newDirection);
        }
        #endregion

        #region Public / Private methods: ThumbnailGridVirtualized
        // Zoom in or out of single image or video (< > keys and plain mouse-scroll path).
        // TGV zoom is handled separately via TryZoomInOrOutVirtualized (Ctrl+scroll).
        public void TryZoomInOrOut(bool zoomIn, Point imageMousePosition, Point videoMousePosition)
        {
            if (IsThumbnailGridVirtualizedVisible) return;
            if (!displayingImage)
            {
                lock (_zoomLock)
                {
                    if (zoomIn || VideoPlayer.IsUnScaled == false)
                    {
                        VideoPlayer.ScaleVideo(videoMousePosition, zoomIn);
                        SetMagnifiersAccordingToCurrentState(false, true);
                    }
                }
                return;
            }
            // At minimum zoom: apply per-modality behaviour then return without scaling.
            if (!zoomIn && Math.Abs(imageToDisplayScale.ScaleX - Constant.MarkableCanvas.ImageZoomMinimum) < .0001)
            {
                if (NativeMethods.IsCtrlKeyDown())
                {
                    // Guard only applies when the user just arrived at min zoom via a continuous
                    // Ctrl+scroll-out (lastZoomChangeTime is within the 250 ms window).
                    // If the image has been at full size longer than that, activate TGV immediately.
                    if (DateTime.Now - lastZoomChangeTime >= TimeSpan.FromMilliseconds(250))
                    {
                        ctrlScrollOutAtMinZoomStartTime = DateTime.MinValue;
                        TryZoomInOrOutVirtualized(false);
                    }
                    else if (ctrlScrollOutAtMinZoomStartTime == DateTime.MinValue)
                    {
                        ctrlScrollOutAtMinZoomStartTime = DateTime.Now;
                    }
                    else if (DateTime.Now - ctrlScrollOutAtMinZoomStartTime >= TimeSpan.FromMilliseconds(250))
                    {
                        ctrlScrollOutAtMinZoomStartTime = DateTime.MinValue;
                        TryZoomInOrOutVirtualized(false);
                    }
                }
                else if (DateTime.Now - lastZoomChangeTime >= TimeSpan.FromMilliseconds(500))
                {
                    // Show hint after a natural pause at min zoom.
                    // Reset lastZoomChangeTime immediately so rapid follow-up scrolls don't re-trigger
                    // until the user pauses again.
                    lastZoomChangeTime = DateTime.Now;
                    GlobalReferences.MainWindow?.ToastNotifier.ShowInformationByCursor("Use Ctrl-scrollwheel to display the overview at different sizes.");
                }
                return;
            }
            // Image is not at min zoom (or zooming in): reset the hold-off timer and scale.
            ctrlScrollOutAtMinZoomStartTime = DateTime.MinValue;
            lock (_zoomLock)
            {
                if (imageMousePosition.X > ImageToDisplay.ActualWidth)
                    imageMousePosition.X = ImageToDisplay.ActualWidth;
                if (imageMousePosition.Y > ImageToDisplay.ActualHeight)
                    imageMousePosition.Y = ImageToDisplay.ActualHeight;
                ScaleImage(imageMousePosition, zoomIn);
            }
            lastZoomChangeTime = DateTime.Now;
        }

        // Mirrors TryZoomInOrOut but routes to ThumbnailGridVirtualized.
        // zoomIn=false → shrink cells (zoom out); zoomIn=true → grow cells (zoom in) until deactivated.
        private void TryZoomInOrOutVirtualized(bool zoomIn)
        {
            if (zoomIn)
            {
                if (!IsThumbnailGridVirtualizedVisible) return;
                ThumbnailGridRefreshStatus status = RefreshThumbnailGridVirtualized(true);
                if (status == ThumbnailGridRefreshStatus.AtZeroZoomLevel)
                {
                    ThumbnailGridVirtualized.Reset();
                    if (displayingImage) SwitchToImageView();
                    else SwitchToVideoView();
                }
                // AnimatingToHome: reveal the home image behind the sliding grid so the destination
                // is visible as the grid slides away. Full switch happens via DeactivateRequested.
                if (status == ThumbnailGridRefreshStatus.AnimatingToHome)
                    ImageToDisplay.Visibility = Visibility.Visible;
            }
            else
            {
                bool isInitialSwitch = !IsThumbnailGridVirtualizedVisible;
                if (isInitialSwitch)
                {
                    if (ThumbnailGridVirtualized.FileTable == null) return;
                    SwitchToThumbnailGridVirtualizedView();
                }

                ThumbnailGridRefreshStatus status = RefreshThumbnailGridVirtualized(false);
                if (status == ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell)
                {
                    if (isInitialSwitch)
                    {
                        if (displayingImage) SwitchToImageView();
                        else SwitchToVideoView();
                    }
                    return;
                }
                if (isInitialSwitch && status == ThumbnailGridRefreshStatus.Ok)
                {
                    ThumbnailGridVirtualized.SelectInitialCellOnly();
                    DataEntryControls.SetEnableState(ControlsEnableStateEnum.MultipleImageView,
                        ThumbnailGridVirtualized.SelectedCount());
                }
            }
        }
        #endregion

        #region Mouse Event Handlers

        // FIX: Runs before any child processes the press (tunneling phase), so it fires even when
        // Slider.Thumb marks the bubble-phase MouseDown as handled and stops it from reaching
        // ImageVideoOrCanvas_MouseDown. Resets pan state unconditionally on every new press, and
        // clears mouseDownSender when the press is in the VideoPlayer controls panel so that
        // MarkableCanvas_MouseMove never arms panning for that gesture.
        private void MarkableCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isPanning = false;
            if (!displayingImage)
            {
                Point posInVideoPlayer = e.GetPosition(VideoPlayer);
                if (posInVideoPlayer.Y > VideoPlayer.VideoCanvas.ActualHeight)
                {
                    mouseDownSender = null;
                }
            }
        }

        // On Mouse down, record the location, and who sent it.
        // We will use this information on move and up events to discriminate between
        // panning/zooming vs. marking.
        private void ImageVideoOrCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            previousMousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // FIX: When displaying video, MouseDown bubbles up from all VideoPlayer children
                // (e.g. SliderScrubbing) since Slider does not mark the bubble-phase event as handled.
                // If the click is below the VideoCanvas (i.e. in the controls panel), clear mouseDownSender
                // so that MarkableCanvas_MouseMove never arms panning for that press.
                if (!displayingImage)
                {
                    Point posInVideoPlayer = e.GetPosition(VideoPlayer);
                    if (posInVideoPlayer.Y > VideoPlayer.VideoCanvas.ActualHeight)
                    {
                        // Click is in the controls panel — reset state so any lingering isPanning=true
                        // from a previous gesture doesn't continue to pan while the user drags the slider.
                        mouseDownSender = null;
                        isPanning = false;
                        return;
                    }
                }

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

            // If we are not yet in panning mode, switch into it if the user has moved at least the threshold distance from mouse down position.
            // FIX: Also guard on mouseDownSender != null so that a press that originated on a video control
            // (e.g. SliderScrubbing — where ImageVideoOrCanvas_MouseDown clears mouseDownSender) never starts a pan.
            if (e.LeftButton == MouseButtonState.Pressed && isPanning == false && mouseDownSender != null &&
                (mouseDownLocation - mousePosition).Length > Constant.MarkableCanvas.MarkingVsPanningDistanceThreshold)
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
                UpdateOffsetLensDirection(e.GetPosition(VideoPlayer.MediaElement));
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
            bool zoomIn = e.Delta > 0;
            // Use Win32 GetKeyState rather than Keyboard.Modifiers: WM_MOUSEWHEEL can arrive while the
            // window is inactive ("scroll inactive windows" feature), leaving WPF's modifier state stale.
            bool ctrlDown = NativeMethods.IsCtrlKeyDown();

            // Ctrl+scroll in TGV: zoom TGV cells (debounced to avoid overshooting)
            if (ctrlDown && IsThumbnailGridVirtualizedVisible)
            {
                TimeSpan diff = DateTime.Now - lastMouseWheelDateTime;
                if (diff >= TimeSpan.FromMilliseconds(500))
                {
                    lastMouseWheelDateTime = DateTime.Now;
                    TryZoomInOrOutVirtualized(zoomIn);
                }
                e.Handled = true;
                return;
            }

            // Dismiss the hint toast on any scroll that does something (zoom in, or Ctrl+scroll).
            // Plain scroll-out is the only event that triggers the toast, so we leave it alone.
            if (ctrlDown || zoomIn)
                GlobalReferences.MainWindow?.ToastNotifier.Dismiss();

            // Image/video view: no debounce — every scroll event is forwarded directly.
            // Min-zoom behaviour (Ctrl counter, hint toast) is handled inside TryZoomInOrOut.
            Point imageMousePosition = e.GetPosition(ImageToDisplay);
            Point videoMousePosition = e.GetPosition(VideoPlayer.MediaElement);
            TryZoomInOrOut(zoomIn, imageMousePosition, videoMousePosition);
            e.Handled = true;
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

            ThumbnailGridVirtualized.Width = ActualWidth;
            ThumbnailGridVirtualized.Height = ActualHeight;
            if (ThumbnailGridVirtualized.Visibility == Visibility.Visible)
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

        // Refresh the TGV when the timer fires after a resize
        private void TimerResize_Tick(object sender, EventArgs e)
        {
            timerResize.Stop();
            if (IsThumbnailGridVirtualizedVisible)
            {
                if (ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell == RefreshThumbnailGridVirtualized(null))
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
                case Key.OemPeriod:  // . or >  — zoom in
                    if (IsThumbnailGridVirtualizedVisible)
                        TryZoomInOrOutVirtualized(true);
                    else
                    {
                        Point imageMousePosition = Mouse.GetPosition(ImageToDisplay);
                        Point videoMousePosition = Mouse.GetPosition(VideoPlayer.MediaElement);
                        TryZoomInOrOut(true, imageMousePosition, videoMousePosition);
                    }
                    break;
                case Key.OemComma:  // , or <  — zoom out
                    if (IsThumbnailGridVirtualizedVisible)
                        TryZoomInOrOutVirtualized(false);
                    else if (Math.Abs(imageToDisplayScale.ScaleX - Constant.MarkableCanvas.ImageZoomMinimum) < .0001)
                        TryZoomInOrOutVirtualized(false);  // at full size: cross to TGV without requiring Ctrl
                    else
                    {
                        Point imageMousePosition2 = Mouse.GetPosition(ImageToDisplay);
                        Point videoMousePosition2 = Mouse.GetPosition(VideoPlayer.MediaElement);
                        TryZoomInOrOut(false, imageMousePosition2, videoMousePosition2);
                    }
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
                        if (IsThumbnailGridVirtualizedVisible)
                            ThumbnailGridVirtualized.RefreshBoundingBoxesAndEpisodeInfo();
                        else
                        {
                            RefreshBoundingBoxes();
                            GlobalReferences.MainWindow.DuplicateDisplayIndicatorInImageIfWarranted();
                        }
                    }
                    break;
                case Key.P:
                    // Show previous/next image in episode in a popup, regardless of the current selection
                    if (!IsThumbnailGridVirtualizedVisible && !e.IsRepeat)
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
                        if (IsThumbnailGridVirtualizedVisible)
                            ThumbnailGridVirtualized.RefreshBoundingBoxesAndEpisodeInfo();
                        else
                        {
                            RefreshBoundingBoxes();
                            GlobalReferences.MainWindow.DuplicateDisplayIndicatorInImageIfWarranted();
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
