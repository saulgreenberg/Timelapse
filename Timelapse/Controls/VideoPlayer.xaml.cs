using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Microsoft.WindowsAPICodePack.Shell;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Util;
using File = System.IO.File;
using Timelapse.DataStructures;
using Timelapse.Images;
using RadioButton = System.Windows.Controls.RadioButton;
using Timelapse.DebuggingSupport;

namespace Timelapse.Controls
{
    public partial class VideoPlayer
    {
        #region Public properties

        /// <summary>
        /// True if the video is unscaled, false if it is zoomed in
        /// </summary>        
        public bool IsUnScaled => Math.Abs(VideoScale.ScaleX - 1) < 0.0001;

        public bool StateShowBestBoundingBox = false;
        /// <summary>
        ///  We do VideoPosition as a property so we can manipulate and/or check things whenever the video position is changed.
        /// </summary>
        private TimeSpan MediaElementCurrentPosition
        {
            get => this.MediaElement.Position;
            set
            {
                this.MediaElement.Position = value;
                // TracePrint.StackTraceToOutput(this.MediaElement.Position.ToString());
            }
        }

        #endregion

        #region Private variables
        private long FileIndex; // A pointer to the ImageRow
        private Uri SourceUri;  // The currently loaded video

        private bool isProgrammaticUpdate; // To control callback execution

        // Tracks whether the player was previously loaded
        private bool IsVideoPlayerLoaded = false;

        // Timers
        private DispatcherTimer TimerUpdatePosition;
        private DispatcherTimer TimerAutoPlayDelay;
        private DispatcherTimer TimerSetSourceAfterBriefDelay;
        private DispatcherTimer TimerPlayBriefly;

        // Render transforms
        private readonly ScaleTransform VideoScale;
        private readonly TranslateTransform VideoTranslation;
        private TransformGroup TransformGroup;

        // Bounding box related variables, so we can track where we are and what we need to show
        private float? FrameRate = -1;
        private int FrameToShow;
        private double VideoDurationSeconds = -1;
        private TimeSpan VideoDurationTimeSpan;

        private BoundingBoxes BoxesForFile = null; // The bounding boxes for the current video

        #endregion

        #region Constructor, Loading, Unloading

        public VideoPlayer()
        {
            InitializeComponent();

            // TracePrint.StackTrace("--->", 5);
            this.isProgrammaticUpdate = false;

            // Initialize various
            this.DoTimersInitialize();

            // Rendering initialization
            this.VideoScale = new ScaleTransform(1.0, 1.0);
            this.VideoTranslation = new TranslateTransform(1.0, 1.0);

            // Start with the video controls disabled
            this.IsEnabled = false;
        }

        private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            if (false == GlobalReferences.MainWindow.IsFileDatabaseAvailable() || this.IsVideoPlayerLoaded)
            {
                // - No need to load things until a file database is actually available.
                // - For an unknown reason, this may be re-invoked twice. loadedAlready ensures we ignore the 2nd invocation.
                return;
            }

            this.IsVideoPlayerLoaded = true;

            // TracePrint.StackTrace("--->", 5);

            //  Initial scaling and transforms
            //this.DoScalingAndTransforms();

            // Initialize Timer callbacks
            this.DoTimersSetCallbacks();

            // For some reason, MediaOpened is invoked twice if we put it here, but not if its in the XAML.
            this.MediaElement.MediaEnded += MediaElementMediaEnded;
            this.MediaElement.Unloaded += MediaElementUnloaded;

            this.SliderScrubbing.ValueChanged += SliderScrubbingValueChanged;
            this.SliderScrubbing.PreviewMouseDown += SliderScrubbingPreviewMouseDown;

            this.PlayOrPause.Click += PlayOrPause_Click;

            this.RBSlow.Checked += SetSpeed_Checked;
            this.RBNormal.Checked += SetSpeed_Checked;
            this.RBFast.Checked += SetSpeed_Checked;

            this.CBAutoPlay.Checked += CBAutoPlay_CheckChanged;
            this.CBAutoPlay.Unchecked += CBAutoPlay_CheckChanged;

            this.OpenExternalPlayer.Click += OpenExternalPlayer_Click;
            this.CBMute.Checked += CBMute_CheckedChanged;
            this.CBMute.Unchecked += CBMute_CheckedChanged; ;
        }
        
        private void CBMute_CheckedChanged(object sender, RoutedEventArgs e)
        {
            MediaElement.Volume = CBMute.IsChecked == true && null != this.MediaElement
                ? 0
                : 0.5;
        }

        // Set the flag to indicate that the video player is no longer loaed
        private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 5);
            this.IsVideoPlayerLoaded = false;
        }

        // Stop all timers and diable the video player controls
        private void MediaElementUnloaded(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            DoTimersStopAll();
            IsEnabled = false;
        }

        private void VideoPlayer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 4);
            if (CBAutoPlay.IsChecked == true && (bool)e.NewValue)
            {
                // Only autoplay if the video is visible (i.e., NewValue is true)
                TimerAutoPlayDelay.Start();
            }
            else
            {
                TimerAutoPlayDelay.Stop();
            }
        }
        #endregion

        #region SetSource / Media Opened/ Ended

        /// <summary>
        /// Set the Source Video provided in the URI.
        /// Doing so invokes the MediaOpened callback, which actually initializes everything
        /// </summary>
        public void SetSource(Uri source, long fileIndex)
        {
            this.ClearBoundingBoxes();
            this.FileIndex = fileIndex;
            this.SourceUri = source;
            // We do this here as we have the fileIndex handy
            this.BoxesForFile = null == GlobalReferences.MainWindow?.DataHandler?.FileDatabase?.FileTable
                ? null
                : GlobalReferences.MainWindow?.GetBoundingBoxesForCurrentFile(GlobalReferences.MainWindow.DataHandler.FileDatabase.FileTable[(int)fileIndex].ID);
            this.ButtonEnableBestFrameIfNeeded();
            this.TimerSetSourceAfterBriefDelay.Start();
        }


        // When the video is first opened, Auto play it if Auto play is on
        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = true;
            if (this.MediaElement.Source == null)
            {
                return;
            }
            // TracePrint.StackTrace($"--->{this.MediaElement.Source}", 3);
            // Bounding box canvas will have been cleared before this is invoked
            // Determine and set the FrameRate, FrameToShow, VideoDuraton and Slider Scrubbing Duration values
            this.DoSetFrameRateAndFrameToShowValues();
            this.DoSetDurationValues();
            this.DoSetSliderAttributesForDuration();
            //this.DoSaveRestoreVolume(); // Likely not needed

            if (this.BoxesForFile == null || this.FrameRate == null || this.VideoDurationSeconds == 0)
            {
                // Start at the beginning of the video as we can't do anything else.
                this.DoSetSliderText();
                if (this.CBAutoPlay.IsChecked == true && this.Visibility == Visibility.Visible)
                {
                    TimerAutoPlayDelay.Start();
                }
                return;
            }

            // Estimate the video's start time from the given frame and frame rate. If we can't, then just start at 0 
            TimeSpan startTime = this.StateShowBestBoundingBox
                ? GetTimeSpanFromFrameNumber(this.BoxesForFile.InitialVideoFrame, this.FrameRate, this.VideoDurationTimeSpan)
                : TimeSpan.Zero;

            // Because we got here should have some valid numbers, so set them all

            this.FrameToShow = this.StateShowBestBoundingBox
            ? this.BoxesForFile.InitialVideoFrame
            : 0;

            // Set the video, slider and feedback text to the desired start time.
            this.isProgrammaticUpdate = true;
            this.SliderScrubbing.Value = startTime.TotalSeconds; // This will be set in ShowPosition so no need to do it here.
            this.isProgrammaticUpdate = false;

            // Update the slider as needed
            this.DoSetSliderText();

            // Set the video position to the initial position

            if (StateShowBestBoundingBox)
            {
                this.isProgrammaticUpdate = true;
                this.MediaElementUpdatePosition(startTime);
                InitialStartingTime = startTime;
                this.isProgrammaticUpdate = false;
            }
            // Update the bounding boxes to display at this time interval, if any 
            this.UpdateBoundingBoxes();

            // Don't play if the control isn't visible
            if (Visibility != Visibility.Visible)
            {
                this.Pause();
                return;
            }

            if (CBAutoPlay.IsChecked == false)
            {
                if (InitialStartingTime == TimeSpan.Zero && this.StateShowBestBoundingBox)
                {
                    this.TimerPlayBriefly.Start();
                }
                else
                {
                    this.Pause();
                }
            }
            else
            {
                if (this.StateShowBestBoundingBox)
                {
                    DoGoToBestFrame();
                }
                this.TimerAutoPlayDelay.Start();
            }

            //InitialStartingTime != TimeSpan.Zero)
            //{
            //    //this.Play();
            //    if (this.StateShowBestBoundingBox)
            //    {
            //        this.TimerPlayBriefly.Start();
            //    }
            //}
            //else
            //{
            //    if (this.StateShowBestBoundingBox)
            //    {
            //        this.PlayBrieflyIfNeeded();
            //    }
            //}
        }

        // When the video finishes playing, pause it and automatically return it to the beginning
        // Repeat playing if Auto play is on
        private void MediaElementMediaEnded(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            this.Pause();
            SliderScrubbing.Value = SliderScrubbing.Minimum; // Go back to the beginning
            if (CBRepeat.IsChecked == true)
            {
                this.Play();
            }
        }
        #endregion

        #region Public methods (Play, TryPlayOrPause)

        // Pause the video.
        // Public as the markable canvas pauses the video when switching to Image or ThumbnailGrid views
        public void Pause()
        {
            TimerUpdatePosition.Stop();

            // For some strange reason, pausing the video resets the video position to 0.
            // So we need to restore it to the paused position
            TimeSpan currentPosition = this.MediaElementCurrentPosition;
            this.MediaElement.Pause();
            this.PlayOrPause.IsChecked = false;
            this.MediaElementCurrentPosition = currentPosition;
            //// Debug.Print($"Pause:{this.CurrentVideoPosition}");
            this.ShowPosition();
        }

        public bool TryTogglePlayOrPause()
        {
            // Debug.Print($"TryTogglePlayOrPause:{this.CurrentVideoPosition}");

            if (this.Visibility != Visibility.Visible)
            {
                // Don't bother if the video player isn't being displayed
                return false;
            }

            // WPF doesn't offer a way to fire a toggle button's click event programatically (ToggleButtonAutomationPeer -> IToggleProvider -> Toggle()
            // changes the state of the button but fails to trigger the click event) so do the equivalent in code
            PlayOrPause.IsChecked = !PlayOrPause.IsChecked;
            PlayOrPause_Click(this, null);
            return true;
        }

        // Try going to the best video frame, if possible
        public void TryGoToBestFrame()
        {
            if (this.Visibility == Visibility.Visible && null != this.MediaElement?.Source)
            {
                // This will do something only if there are bounding boxes available
                this.DoGoToBestFrame();
            }
        }

        // Try refreshing the source video
        public void TryRefreshSource()
        {
            if (this.Visibility == Visibility.Visible && null != this.MediaElement?.Source)
            {
                // This will do something only if there are bounding boxes available
                this.SetSource(this.SourceUri, this.FileIndex);
            }
        }


        #endregion

        #region Private ShowPosition : Update everything
        // This may be invoked several times unecessarily, but in those case the tests below (e.g., for null etc)
        // make those cases a nearly 'noop' operation
        private void UpdateBoundingBoxes()
        {
            try
            {
                this.ClearBoundingBoxes();

                // Update the bounding boxes
                if (null == this.MediaElement.Source || this.FrameRate == null || this.VideoDurationSeconds <= 0)
                {
                    return;
                }

                // TracePrint.StackTrace("--->", 5);

                isProgrammaticUpdate = true;
                if (this.FrameToShow < 0 || null == this.FrameRate || this.FrameRate <= 0)
                {
                    // Can't process any bounding boxes
                    isProgrammaticUpdate = false;
                    return;
                }

                // Start searching from a frame a second before the desired one
                Point vidSize = GetActualVideoSize();
                int fromFrame = 0;
                if (this.FrameRate != null)
                {

                    fromFrame = this.FrameToShow - (int)Math.Floor((decimal)this.FrameRate);
                    if (fromFrame < 0)
                    {
                        fromFrame = 0;
                    }
                }

                if (null != this.BoxesForFile)
                {
                    this.BoxesForFile.DrawBoundingBoxesInCanvas(this.VideoCanvas, vidSize.X, vidSize.Y, 0, this.TransformGroup, this.FrameToShow, fromFrame);
                    if (this.FrameRate != null)
                    {
                        this.FrameToShow = Convert.ToInt32(Math.Ceiling((double)(SliderScrubbing.Value * this.FrameRate)));
                    }

                    this.DoButtonBestFrameSetBackgroundColor();
                }

                isProgrammaticUpdate = false;
            }
            catch
            {
                isProgrammaticUpdate = false;
            }
        }

        private void MediaElementUpdatePosition(TimeSpan videoTime)
        {
            // Update the position in the video to the given time
            if (null == this.MediaElement.Source || this.FrameRate == null || this.VideoDurationSeconds <= 0)
            {
                return;
            }

            //TracePrint.StackTrace("--->", 4);
            this.MediaElementCurrentPosition = videoTime;
        }

        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            if (null == this.MediaElement.Source)
            {
                // We aren't displaying anything, so nothing to show
                return;
            }

            // TracePrint.StackTrace("--->", 5);
            //Debug.Print($"ShowPosition:{this.CurrentVideoPosition}");
            isProgrammaticUpdate = true;
            try
            {
                // Feedback: show current position in the video (text and slider)
                TimeFromStart.Text = this.MediaElementCurrentPosition.ToString(Time.VideoPositionFormat);
                this.isProgrammaticUpdate = true;
                SliderScrubbing.Value = this.MediaElementCurrentPosition.TotalSeconds;
                // Debug.Print("xxx" + SliderScrubbing.Value.ToString() + "   " + this.MediaElementCurrentPosition.TotalSeconds.ToString());
                this.isProgrammaticUpdate = false;
                // Debug.Print($"Show: Slider position: {VideoSlider.Value}");

                // Update the bounding boxes
                this.ClearBoundingBoxes();
                this.FrameRate = this.BoxesForFile?.FrameRate;
                if (this.FrameToShow < 0 || null == this.FrameRate || this.FrameRate <= 0)
                {
                    // Can't process any bounding boxes
                    isProgrammaticUpdate = false;
                    return;
                }

                Point vidSize = GetActualVideoSize();

                int fromFrame = 0;
                if (this.FrameRate != null)
                {
                    // Start from a frame a second before the current one
                    fromFrame = this.FrameToShow - (int)Math.Floor((decimal)this.FrameRate);
                    if (fromFrame < 0)
                    {
                        fromFrame = 0;
                    }
                }

                // Debug.Print($"frameToShow:{frameToShow} fromFrame{fromFrame}");
                if (null != this.BoxesForFile)
                {
                    this.BoxesForFile.DrawBoundingBoxesInCanvas(this.VideoCanvas, vidSize.X, vidSize.Y, 0, this.TransformGroup, this.FrameToShow, fromFrame);
                    if (this.FrameRate != null)
                    {
                        this.FrameToShow = Convert.ToInt32(Math.Ceiling((double)(SliderScrubbing.Value * this.FrameRate)));
                    }
                    this.DoButtonBestFrameSetBackgroundColor();
                }
                isProgrammaticUpdate = false;
            }
            catch
            {
                isProgrammaticUpdate = false;
            }
        }

        #endregion

        #region Private methods: Play

        private void Play()
        {
            try
            {
                //this.ClearBoundingBoxes();
                PlayOrPause.IsChecked = true;
                // start over from beginning if at end of video
                // Technote: The natural duration default value is Automatic if you query this property before MediaOpened. So we just reset the position if its a new video.
                if (this.MediaElementCurrentPosition >= this.VideoDurationTimeSpan)
                {
                    this.MediaElementCurrentPosition = TimeSpan.Zero;
                    ShowPosition();
                }

                // Implementation Note due to media player bug.
                // This seemingly redundant bit of code is necessary as otherwise the media player will
                // sometimes start at the beginning, even if the this.CurrentVideoPosition is set to a different spot.
                // This essentially forces it to start at its current position. Unfortunately, it doesn't always work
                // as the video sometimes 'freezes'. Not sure why - I think its a mediaElement bug.
                TimeSpan startPosition = this.MediaElementCurrentPosition;
                this.MediaElementCurrentPosition = TimeSpan.Zero;
                MediaElement.Play();
                MediaElement.Pause();
                this.MediaElementCurrentPosition = startPosition;
                MediaElement.Play();
                // Debug.Print($"Play:{this.MediaElementCurrentPosition}");
                TimerUpdatePosition.Start();
            }
            catch (Exception)
            {
                // A user reported a rare crash in the above
                PlayOrPause.IsChecked = false;
                TimerUpdatePosition.Stop();
            }
        }

        #endregion

        #region Private Video UI control callbacks

        // The round play/pause button was clicked
        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            // Debug.Print($"PlayOrPause_Click:{this.CurrentVideoPosition}");
            if (this.PlayOrPause.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        // The speed setting was changed. Set the speed, which also causes the video to play (if currently paused)
        private void SetSpeed_Checked(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            // Debug.Print($"SetSpeed_Checked:{this.CurrentVideoPosition}");
            RadioButton rb = sender as RadioButton;

            // We use a tryparse as there was a rare bug when the tag could not be converted to a double
            if (rb?.Tag != null && Double.TryParse((string)rb.Tag, out double newSpeed))
            {
                MediaElement.SpeedRatio = newSpeed;
                Play();
            }
        }

        private void CBAutoPlay_CheckChanged(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            if (CBAutoPlay.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        // Open the currently displayed video in an external player
        private void OpenExternalPlayer_Click(object sender, RoutedEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            // Open the currently displayed video in an external player
            if (File.Exists(Uri.UnescapeDataString(MediaElement.Source.AbsolutePath)))
            {
                Uri uri = new Uri(Uri.UnescapeDataString(MediaElement.Source.AbsolutePath));
                ProcessExecution.TryProcessStart(uri);
            }
        }

        #endregion

        #region Private Timer initializations and callbacks

        private void DoTimersInitialize()
        {
            // Provide feedback of the current video position
            this.TimerUpdatePosition = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250.0) };

            // Automatically start playing the videos after a modest interval
            this.TimerAutoPlayDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300.0) };

            // Automatically loads the new source video after a modest interval
            // We do this to avoid the black flickering plus initial crappy video load
            // if a user is navigating quickly through their videos.
            this.TimerSetSourceAfterBriefDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

            this.TimerPlayBriefly = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };

        }

        // Set the various Timer callbacks here
        private void DoTimersSetCallbacks()
        {
            this.TimerUpdatePosition.Tick += TimerUpdatePosition_Tick;
            this.TimerAutoPlayDelay.Tick += TimerAutoPlayDelay_Tick;
            this.TimerSetSourceAfterBriefDelay.Tick += TimerSetSourceAfterBriefDelay_Tick;
            this.TimerPlayBriefly.Tick += TimerPlayBriefly_Tick;
        }

        private void DoTimersStopAll()
        {
            this.TimerUpdatePosition.Stop();
            this.TimerAutoPlayDelay.Stop();
            this.TimerSetSourceAfterBriefDelay.Stop();
            this.TimerPlayBriefly.Stop();
        }

        // Set the video to automatically start playing after a brief delay 
        // This helps when one is navigating across videos, as there is a brief moment before the play starts.
        // Note that this is normally only started if the AutoPlay button is checked
        private void TimerAutoPlayDelay_Tick(object sender, EventArgs e)
        {
            //TracePrint.StackTrace("--->", 2);
            // Debug.Print($"AutoPlay:{this.CurrentVideoPosition}");
            this.TimerAutoPlayDelay.Stop();
            this.Play();
        }

        private void TimerUpdatePosition_Tick(object sender, EventArgs e)
        {
            //TracePrint.StackTrace("--->", 2);
            // Debug.Print($"TimerUpdatePosition_Tick:{this.CurrentVideoPosition}");
            if (MediaElement.Source != null)
            {
                ShowPosition();
            }
        }

        // Instead of setting the source immediately, we do it after a brief delay
        // This allows a user to rapidly navigate through videos without incurring the
        // cost of trying to display a video that wouldn't actually be seen anyways.
        // If they slow down, then the video will be shown

        private static TimeSpan InitialStartingTime;

        private void TimerSetSourceAfterBriefDelay_Tick(object sender, EventArgs e)
        {
            this.TimerSetSourceAfterBriefDelay.Stop();
            this.DoSetSource(this.SourceUri, this.FileIndex);

        }

        private void PlayBrieflyIfNeeded()
        {
            if (this.CBAutoPlay.IsChecked == true)
            {
                return;
            }
            if (InitialStartingTime <= TimeSpan.Zero)
            {
                return;
            }
            TimeSpan earlierTime = InitialStartingTime - new TimeSpan(0, 0, 0, 500);
            if (earlierTime < TimeSpan.Zero)
            {
                return;
            }
            this.Play();
            this.TimerPlayBriefly.Start();
        }
        private void TimerPlayBriefly_Tick(object sender, EventArgs e)
        {
            TracePrint.StackTrace("--<", 5);
            this.TimerPlayBriefly.Stop();
            this.MediaElementCurrentPosition = InitialStartingTime;
            this.Pause();
        }

        #endregion

        #region Private Slider-related callbacks

        // The Slider's position has changed
        // But we only take action when the change is due to scrubbing actions by the user 
        private void SliderScrubbingValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (false == this.IsVisible || isProgrammaticUpdate)
            {
                return;
            }

            // TracePrint.StackTrace("--->", 2);
            // Only pay attention to a user's scrubbing actions
            // Debug.Print($"ValueChanged (Slider):{this.CurrentVideoPosition}");
            //if (isProgrammaticUpdate)
            //{
            //    return;
            //}

            TimeSpan videoPosition = TimeSpan.FromSeconds(SliderScrubbing.Value);
            this.MediaElementCurrentPosition = videoPosition;
            // Debug.Print($"Slider:{this.CurrentVideoPosition}");
            // Update which frame we should be displaying
            if (this.FrameRate != null)
            {
                this.FrameToShow = Convert.ToInt32(System.Math.Floor((double)(SliderScrubbing.Value * this.FrameRate)));
                this.ShowPosition();
            }

            this.Pause(); // If a user scrubs, force the video to pause if its playing
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void SliderScrubbingPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // TracePrint.StackTrace("--->", 2);
            this.Pause();
        }

        #endregion

        #region Public methods (ScaleVideo, TranslateVideo) invoked only by Markable Canvas

        /// <summary>
        /// Scale (Zoom) the video in or out around the provided location, which should be the cursor location in video coordinates
        /// </summary>
        public void ScaleVideo(Point currentMousePosition, bool zoomIn)
        {
            double VideoZoomMaximum = 4.0;
            double VideoZoomMinimum = 1; // Unscaled
            double VideoZoomStep = 1.1; // Constant.MarkableCanvas.ImageZoomStep

            // Abort if we are already at our maximum or minimum scaling values 
            if ((zoomIn && VideoScale.ScaleX >= VideoZoomMaximum) ||
                (!zoomIn && VideoScale.ScaleX <= VideoZoomMinimum))
            {
                return;
            }

            // If we are zooming in around a point off the image, then correct the location to the edge of the image
            if (currentMousePosition.X > MediaElement.ActualWidth)
            {
                currentMousePosition.X = MediaElement.ActualWidth;
            }

            if (currentMousePosition.Y > MediaElement.ActualHeight)
            {
                currentMousePosition.Y = MediaElement.ActualHeight;
            }

            // We will scale around the current point (This may be a no-op, but am not sure.)
            Point beforeZoom = PointFromScreen(MediaElement.PointToScreen(currentMousePosition));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn)
            {
                // We are zooming in
                // Calculate the scaling factor
                VideoScale.ScaleX *= VideoZoomStep; // Calculate the scaling factor
                VideoScale.ScaleY *= VideoZoomStep;

                // Make sure we don't scale beyond the maximum scaling factor
                VideoScale.ScaleX = Math.Min(VideoZoomMaximum, VideoScale.ScaleX);
                VideoScale.ScaleY = Math.Min(VideoZoomMaximum, VideoScale.ScaleY);
            }
            else
            {
                // We are zooming out. 
                // Calculate the scaling factor
                VideoScale.ScaleX /= VideoZoomStep;
                VideoScale.ScaleY /= VideoZoomStep;

                // Make sure we don't scale beyond the minimum scaling factor
                VideoScale.ScaleX = Math.Max(VideoZoomMinimum, VideoScale.ScaleX);
                VideoScale.ScaleY = Math.Max(VideoZoomMinimum, VideoScale.ScaleY);

                // if there is no scaling, reset translations
                if (Math.Abs(VideoScale.ScaleX - 1.0) < .0001 && Math.Abs(VideoScale.ScaleY - 1.0) < .0001)
                {
                    VideoTranslation.X = 0.0;
                    VideoTranslation.Y = 0.0;
                    ShowPosition();
                    return; // I THINK WE CAN DO THIS - CHECK;
                }
            }

            Point afterZoom = PointFromScreen(MediaElement.PointToScreen(currentMousePosition));

            // Scale the video, and at the same time translate it so that the 
            // location in the video (which is the location of the cursor) stays there
            lock (MediaElement)
            {
                double videoWidth = MediaElement.Width * VideoScale.ScaleX;
                double videoHeight = MediaElement.Height * VideoScale.ScaleY;

                Point center = PointFromScreen(MediaElement.PointToScreen(
                    new Point(MediaElement.Width / 2.0, MediaElement.Height / 2.0)));

                double newX = center.X - (afterZoom.X - beforeZoom.X);
                double newY = center.Y - (afterZoom.Y - beforeZoom.Y);

                if (newX - videoWidth / 2.0 >= 0.0)
                {
                    newX = videoWidth / 2.0;
                }
                else if (newX + videoWidth / 2.0 <= MediaElement.ActualWidth)
                {
                    newX = MediaElement.ActualWidth - videoWidth / 2.0;
                }

                if (newY - videoHeight / 2.0 >= 0.0)
                {
                    newY = videoHeight / 2.0;
                }
                else if (newY + videoHeight / 2.0 <= MediaElement.ActualHeight)
                {
                    newY = MediaElement.ActualHeight - videoHeight / 2.0;
                }

                VideoTranslation.X += newX - center.X;
                VideoTranslation.Y += newY - center.Y;
            }

            ShowPosition();
        }

        /// Translate the video from the previous mouse position the the current mouse position
        public void TranslateVideo(Point currentMousePosition, Point previousMousePosition)
        {
            // Get the center point on the image
            Point center = PointFromScreen(MediaElement.PointToScreen(new Point(MediaElement.Width / 2.0, MediaElement.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + currentMousePosition.X - previousMousePosition.X;
            double newY = center.Y + currentMousePosition.Y - previousMousePosition.Y;

            // get the translated image width
            double imageWidth = MediaElement.Width * VideoScale.ScaleX;
            double imageHeight = MediaElement.Height * VideoScale.ScaleY;

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
            VideoTranslation.X += newX - center.X;
            VideoTranslation.Y += newY - center.Y;
            ShowPosition();
        }

        #endregion

        #region Private Size changed callbacks

        // Scale the video by one increment around the screen location
        // Minor problem: This is invoked twice when Timelapse is first started on a video,
        // Likley due to an OnIsVisibleChange event
        private void VideoPlayer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width == 0 && e.PreviousSize.Height == 0)
            {
                // Minor problem: SizeChanged is invoked twice when Timelapse is first started on a video,
                // This perhaps ignores the first invocation.
                return;
            }

            // TracePrint.StackTrace($"--->${e.NewSize}, {e.PreviousSize}", 4);
            // Fit the video into the canvas
            MediaElement.Width = VideoCanvas.ActualWidth;
            MediaElement.Height = VideoCanvas.ActualHeight;
            VideoScale.CenterX = 0.5 * ActualWidth;
            VideoScale.CenterY = 0.5 * ActualHeight;

            this.UpdateBoundingBoxes();
        }

        #endregion

        #region Private Bounding box helpers

        // Remove all children (i.e., should only be existing bounding boxes) except for the video player
        private void ClearBoundingBoxes()
        {
            if (this.IsVisible)
            {
                // not sure if we should only do this if isVisible
                GlobalReferences.MainWindow.MarkableCanvas.ClearBoundingBoxes();
            }
            if (this.VideoCanvas.Children.Count == 1)
            {
                // If only one child (i.e., the media player), then there are no bounding boxes
                return;
            }

            for (int i = this.VideoCanvas.Children.Count - 1; i > 0; i--)
            {
                UIElement child = this.VideoCanvas.Children[i];
                if (!child.Equals(this.MediaElement))
                {
                    this.VideoCanvas.Children.Remove(child);
                }
            }
        }

        #endregion

        #region Private Video helpers

        // The video running in the player may be smaller than the the player (i.e. when the player shows black borders)
        // This calculates the actual width/height of the video within that player. It does this by determining the actual video width/height resolution,
        // Since either the width or the height of the video will completely fit within the video player, we determine which is which, where we calculate
        // the other dimension based on the current width (or height) of the player.
        private Point GetActualVideoSize()
        {
            double videoWidth = this.MediaElement.NaturalVideoWidth;
            double videoHeight = this.MediaElement.NaturalVideoHeight;
            double playerWidth = this.MediaElement.Width;
            double playerHeight = this.MediaElement.Height;
            // Compare the aspect ratio of the video player to the video aspect ratio 
            // Depending on the results, we are either filling the video's width or its height.
            // We can then use the aspect ratio to determine the other dimension.
            return playerWidth / playerHeight <= videoWidth / videoHeight
                ? new Point(playerWidth, playerWidth * videoHeight / videoWidth)
                : new Point(videoWidth * playerHeight / videoHeight, playerHeight);
        }

        #endregion

        #region Static methods:Frame rate / number helpers

        // Given a frame number and frame rate, get the approximate matching time in the video 
        // If we can't, return a negative timespan
        private static TimeSpan GetTimeSpanFromFrameNumber(int frameNumber, float? frameRate, TimeSpan duration)
        {
            TimeSpan errorTime = TimeSpan.FromSeconds(-1);

            if (frameNumber == 0)
            {
                // Simplest case
                return TimeSpan.FromSeconds(0);
            }

            if (frameNumber < 0 || null == frameRate || frameRate <= 0)
            {
                // Error
                return errorTime;
            }

            TimeSpan startTime = TimeSpan.FromSeconds(frameNumber / (double)frameRate);
            return startTime < TimeSpan.Zero || startTime > duration
                ? TimeSpan.Zero
                : startTime;
        }

        private static float? GetFrameRate(BoundingBoxes boxesForFile, TimelapseWindow mainWindow)
        {
            float? frameRate = boxesForFile?.FrameRate;

            // We couln't get the frameRate from the bounding boxes, so try getting it from the file
            if (frameRate == null || frameRate <= 0)
            {
                if (null == mainWindow?.DataHandler?.ImageCache.Current)
                {
                    return null;
                }

                string currentPath = mainWindow.DataHandler.ImageCache.Current.GetFilePath(GlobalReferences.MainWindow.DataHandler.FileDatabase.FolderPath);
                frameRate = VideoPlayer.GetFrameRateFromFile(currentPath) ?? null;
                if (frameRate <= 0)
                {
                    frameRate = null;
                }
            }

            return frameRate;
        }

        // Given a path to a file, return the frame rate property attached to that file
        // On failure, return -1.
        private static float? GetFrameRateFromFile(string path)
        {
            if (false == File.Exists(path))
            {
                return null;
            }

            try
            {
                // The frame rate will be 
                ShellObject obj = ShellObject.FromParsingName(@path);
                ShellProperty<uint?> rateProp = obj.Properties.GetProperty<uint?>("System.Video.FrameRate");
                return rateProp?.Value == null
                    ? null
                    : (float?)(rateProp.Value / 1000.0); // converts it from milliseconds to seconds
            }
            catch
            {
                // Error. Prehapse because the file is not accessible or property isn't there?
                return null;
            }
        }

        #endregion

        #region Do actions

        // Initialize things to a new source video file 
        public void DoSetSource(Uri source, long fileIndex)
        {
            // Stop everything from running
            this.TimerUpdatePosition.Stop();
            this.TimerAutoPlayDelay.Stop();
            this.TimerSetSourceAfterBriefDelay.Stop();
            this.TimerPlayBriefly.Stop();

            // We redo this as sometimes it doesn't seem to 'catch' in the VideoPlayer_Loaded event
            this.DoScalingAndTransforms();

            // Change to a new source file, which invokes the Media_Opened callback that does the actual work
            if (this.MediaElement.Source == source)
            {
                // Forces the source (if its the same) to refresh, although it will briefly show a black frame
                this.MediaElement.Source = null;
            }
            this.MediaElement.Source = source;
        }

        // Update the slider to the current values


        private void DoSetFrameRateAndFrameToShowValues()
        {
            // Set the frame rate and frame to show (default is frame 0) for this video from the bounding box structure
            // Default is frame 0
            if (this.BoxesForFile == null)
            {
                this.FrameRate = null;
                this.FrameToShow = 0;
            }
            else
            {
                this.FrameRate = GetFrameRate(this.BoxesForFile, GlobalReferences.MainWindow);
                this.FrameToShow = null == FrameRate
                    ? 0
                    : this.BoxesForFile.InitialVideoFrame;
            }
        }

        // Detremine the video duration
        private void DoSetDurationValues()
        {
            // Set video player slider and text feedback to the video's length
            this.VideoDurationSeconds = MediaElement.NaturalDuration.HasTimeSpan && MediaElement.NaturalDuration != Duration.Automatic
                ? MediaElement.NaturalDuration.TimeSpan.TotalSeconds
                : 0;
            this.VideoDurationTimeSpan = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(this.VideoDurationSeconds * 1000));
        }

        private void DoSetSliderAttributesForDuration()
        {
            this.isProgrammaticUpdate = true;
            this.SliderScrubbing.Maximum = this.VideoDurationSeconds;
            this.TimeDuration.Text = this.VideoDurationSeconds.ToString(Time.VideoPositionFormat);
            this.isProgrammaticUpdate = false;
        }

        // Set the various text values in the slider
        private void DoSetSliderText()
        {
            if (null == this.MediaElement.Source || this.FrameRate == null || this.VideoDurationSeconds <= 0)
            {
                DoSetSliderTextToUnknown();
                return;
            }
            // TracePrint.StackTrace("--->", 5);
            TimeFromStart.Text = this.MediaElementCurrentPosition.ToString(Time.VideoPositionFormat);
            TimeDuration.Text = this.VideoDurationTimeSpan.ToString(Time.VideoPositionFormat);
        }
        private void DoSetSliderTextToUnknown()
        {
            TimeFromStart.Text = "--";
            TimeDuration.Text = "--";

            this.isProgrammaticUpdate = true;
            SliderScrubbing.Value = 0;
            SliderScrubbing.Maximum = 0;
            this.isProgrammaticUpdate = false;
        }


        private void DoScalingAndTransforms()
        {
            // TracePrint.StackTrace("-->", 5);
            this.VideoScale.CenterX = 0.5 * ActualWidth;
            this.VideoScale.CenterY = 0.5 * ActualHeight;
            this.TransformGroup = new TransformGroup();
            this.TransformGroup.Children.Add(VideoScale);
            this.TransformGroup.Children.Add(VideoTranslation);
            this.MediaElement.RenderTransform = TransformGroup;
        }

        private void DoGoToBestFrame()
        {
            if (null == this.BoxesForFile || this.BoxesForFile.Boxes.Count == 0)
            {
                return;
            }

            // Estimate the video's start time from the given frame and frame rate. If we can't, then just start at 0 
            TimeSpan startTime = GetTimeSpanFromFrameNumber(this.BoxesForFile.InitialVideoFrame, this.FrameRate, this.VideoDurationTimeSpan);

            // Because we got here should have some valid numbers, so set them all
            this.FrameToShow = this.BoxesForFile.InitialVideoFrame;

            // Set the video, slider and feedback text to the desired start time.
            this.isProgrammaticUpdate = true;
            this.SliderScrubbing.Value = startTime.TotalSeconds; // This will be set in ShowPosition so no need to do it here.
            this.isProgrammaticUpdate = false;

            // Update the slider as needed
            this.DoSetSliderText();

            // Set the video position to the initial position
            this.isProgrammaticUpdate = true;
            this.MediaElementUpdatePosition(startTime);
            InitialStartingTime = startTime;
            this.isProgrammaticUpdate = false;

            // Show the bounding box for the current frame
            this.UpdateBoundingBoxes();
        }


        // Not used
        private void DoSaveRestoreVolume()
        {
            // Not sure if saving/restoring volume is actually needed, but..
            double originalVolume = MediaElement.Volume;
            this.MediaElement.Volume = 0.0;
            this.PlayOrPause.IsChecked = false;
            this.MediaElement.Volume = originalVolume;
        }
        #endregion

        #region ButtonBestFrame
        private void ButtonEnableBestFrameIfNeeded()
        {
            // Enable only if there are bounding boxes and the max confidence > the display threshold
            this.BorderButtonBestFrame.Visibility = null != this.BoxesForFile &&
                                                    this.BoxesForFile.Boxes.Count > 0 &&
                                                    this.BoxesForFile.MaxConfidence >= GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        private void ButtonBestFrame_OnClick(object sender, RoutedEventArgs e)
        {
            DoGoToBestFrame();
        }

        private void DoButtonBestFrameSetBackgroundColor()
        {
            if (this.BoxesForFile.InitialVideoFrame >= this.FrameToShow - 1 && this.BoxesForFile.InitialVideoFrame <= this.FrameToShow + 1)
            {
                this.ButtonBestFrame.Background = Brushes.Coral;
            }
            else
            {
                this.ButtonBestFrame.Background = Brushes.Red;
            }
        }
        #endregion


    }
}