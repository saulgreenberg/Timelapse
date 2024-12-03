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
using System.Reflection;
using System.Windows.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.State;

namespace Timelapse.Controls
{
    public partial class VideoPlayer
    {
        #region Public properties
        /// <summary>
        /// True if the video is unscaled, false if it is zoomed in
        /// </summary>        
        public bool IsUnScaled => Math.Abs(videoScale.ScaleX - 1) < 0.0001;
        public Uri VideoFileUri { get; set; }

        /// <summary>
        ///  We do VideoPosition as a property so we can manipulate and/or check things whenever the video position is changed.
        /// </summary>
        private TimeSpan CurrentVideoPosition
        {
            get => this.Video.Position;
            set
            {
                this.Video.Position = value;
                // DebuggingSupport.TracePrint.StackTraceToOutput(this.Video.Position.ToString());
            }
        }
        #endregion

        #region Private variables
        private bool isProgrammaticUpdate;

        // Timers
        private readonly DispatcherTimer positionUpdateTimer;
        private readonly DispatcherTimer autoPlayDelayTimer;

        // render transforms
        private readonly ScaleTransform videoScale;
        private readonly TranslateTransform videoTranslation;
        private TransformGroup transformGroup;

        private long FileIndex = -1;

        // Bounding box related variables, so we can track where we are and what we need to show
        private int frameToShow = -1;
        private float? frameRate = -1;

        private BoundingBoxes BoxesForFile = null;
        #endregion

        #region Constructor, Loading, Unloading
        public VideoPlayer()
        {
            InitializeComponent();
            isProgrammaticUpdate = false;

            // Timer used to provide feedback of the current video position
            positionUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250.0)
            };

            // Timer used to automatically start playing the videos after a modest interval
            autoPlayDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300.0)
            };
            IsEnabled = false;

            // Rendering setup
            videoScale = new ScaleTransform(1.0, 1.0);
            videoTranslation = new TranslateTransform(1.0, 1.0);
        }

        private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);

            //  Initial scaling and transforms
            videoScale.CenterX = 0.5 * ActualWidth;
            videoScale.CenterY = 0.5 * ActualHeight;
            transformGroup = new TransformGroup();
            transformGroup.Children.Add(videoScale);
            transformGroup.Children.Add(videoTranslation);
            this.Video.RenderTransform = transformGroup;

            // Set the various callbacks here
            // This avoids initial triggering before the video layer is actually loaded
            this.SizeChanged += VideoPlayer_SizeChanged;
            this.Video.MediaEnded += Video_MediaEnded;
            this.Video.MediaOpened += Video_MediaOpened;
            this.Video.Unloaded += Video_Unloaded;

            this.VideoSlider.ValueChanged += VideoPosition_ValueChanged;
            this.VideoSlider.PreviewMouseDown += VideoPosition_PreviewMouseDown;

            this.PlayOrPause.Click += PlayOrPause_Click;

            this.RBSlow.Checked += SetSpeed_Checked;
            this.RBNormal.Checked += SetSpeed_Checked;
            this.RBFast.Checked += SetSpeed_Checked;

            this.CBAutoPlay.Checked += CBAutoPlay_CheckChanged;
            this.CBAutoPlay.Unchecked += CBAutoPlay_CheckChanged;

            this.OpenExternalPlayer.Click += OpenExternalPlayer_Click;
            this.IsVisibleChanged += VideoPlayer_IsVisibleChanged;

            this.positionUpdateTimer.Tick += TimerUpdatePosition_Tick;
            autoPlayDelayTimer.Tick += AutoPlayDelayTimer_Tick;
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            positionUpdateTimer.Stop();
            IsEnabled = false;
        }

        // If the Video Player becomes visible, we need to start it playing if autoplay is true
        private void VideoPlayer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            if (CBAutoPlay.IsChecked == true && (bool)e.NewValue)
            {
                // Only autoplay if the video is visible (i.e., NewValue is true)
                autoPlayDelayTimer.Start();
            }
            else
            {
                autoPlayDelayTimer.Stop();
            }
        }
        #endregion

        #region SetSource / Media Opened/ Ended
        /// <summary>
        /// Set the Source Video provided in the URI.
        /// Doing so invokes the MediaOpened callback
        /// </summary>
        public void SetSource(Uri source, long fileIndex)
        {
            // Stop the video from running, and reinitialize everything
            positionUpdateTimer.Stop();

            this.FileIndex = fileIndex;
            this.frameToShow = -1;

            // I think this is needed to release resources, but may be redundant
            // When commented out, it still works as before.
            // Video.Stop();
            // Video.Source = null;
            Video.Source = source;
            IsEnabled = true;

            // Not sure if saving/restoring volume is actually needed, but...
            double originalVolume = Video.Volume;
            Video.Volume = 0.0;
            PlayOrPause.IsChecked = false;
            Video.Volume = originalVolume;

            // Debug.Print($"SetSource:{this.CurrentVideoPosition}");
        }

        // When the video is first opened, Auto play it if Auto play is on
        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            //Debug.Print($"Video_MediaOpened1:{this.CurrentVideoPosition}");
            // Set initial state to start of the video
            this.frameToShow = 0;
            this.frameRate = null;
            //this.CurrentVideoPosition = TimeSpan.Zero;

            // Set video player slider and text feedback to the video's length
            if (Video.NaturalDuration.HasTimeSpan)
            {
                this.isProgrammaticUpdate = true;
                VideoSlider.Maximum = Video.NaturalDuration.TimeSpan.TotalSeconds;
                TimeDuration.Text = Video.NaturalDuration.TimeSpan.ToString(Time.VideoPositionFormat);
                this.isProgrammaticUpdate = false;
            }

            // Get the bounding boxes for the entire video (i.e., includes 0 - n frames). 
            this.BoxesForFile = null == GlobalReferences.MainWindow?.DataHandler?.FileDatabase?.FileTable
                ? null
                : GlobalReferences.MainWindow?.GetBoundingBoxesForCurrentFile(GlobalReferences.MainWindow.DataHandler.FileDatabase.FileTable[(int)this.FileIndex].ID);
            if (this.BoxesForFile == null)
            {
                // We start at the beginning of the video
                this.ShowPosition();
                return;
            }

            // Get the frame rate from the bounding box or, failing that, the file
            // Null if we can't.
            float? tmpFrameRate = GetFrameRate(this.BoxesForFile, GlobalReferences.MainWindow);
            if (tmpFrameRate == null)
            {
                this.ShowPosition();
                return;
            }

            // Because we got here should have some valid numbers, so set them all

            // Estimate the video's start time from the given frame and frame rate. If we can't, then just start at 0 
            TimeSpan startTime = GetTimeSpanFromFrameNumber(this.BoxesForFile.InitialVideoFrame, tmpFrameRate);
            if (startTime < TimeSpan.Zero || false == Video.NaturalDuration.HasTimeSpan || startTime > Video.NaturalDuration.TimeSpan)
            {
                this.ShowPosition();
                return;
            }

            // Because we got here should have some valid numbers, so set them all
            this.frameRate = tmpFrameRate;
            this.frameToShow = this.BoxesForFile.InitialVideoFrame;

            // Set the video, slider and feedback text to the desired start time.
            this.CurrentVideoPosition = startTime;
            this.isProgrammaticUpdate = false;
            this.VideoSlider.Value = startTime.TotalSeconds; // This will be set in ShowPosition so no need to do it here.
            this.isProgrammaticUpdate = false;

            //Debug.Print($"Video_MediaOpened2:{this.CurrentVideoPosition}");
            this.ShowPosition();
            if (CBAutoPlay.IsChecked == true && Visibility == Visibility.Visible)
            {
                autoPlayDelayTimer.Start();
            }
        }

        // When the video finishes playing, pause it and automatically return it to the beginning
        // Repeat playing if Auto play is on
        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            this.Pause();
            VideoSlider.Value = VideoSlider.Minimum; // Go back to the beginning
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
            positionUpdateTimer.Stop();

            // For some strange reason, pausing the video resets the video position to 0.
            // So we need to restore it to the paused position
            TimeSpan currentPosition = this.CurrentVideoPosition;
            this.Video.Pause();
            this.PlayOrPause.IsChecked = false;
            this.CurrentVideoPosition = currentPosition;
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
        #endregion

        #region Private ShowPosition : Update everything

        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            if (null == this.Video.Source)
            {
                // We aren't displaying anything, so nothing to show
                return;
            }

            TracePrint.StackTrace("--->", 10);
            //Debug.Print($"ShowPosition:{this.CurrentVideoPosition}");
            isProgrammaticUpdate = true;
            try
            {
                // Feedback: show current position in the video (text and slider)
                TimeFromStart.Text = this.CurrentVideoPosition.ToString(Time.VideoPositionFormat);
                this.isProgrammaticUpdate = true;
                VideoSlider.Value = this.CurrentVideoPosition.TotalSeconds;
                Debug.Print("xxx" + VideoSlider.Value.ToString() + "   " + this.CurrentVideoPosition.TotalSeconds.ToString());
                this.isProgrammaticUpdate = false;
                // Debug.Print($"Show: Slider position: {VideoSlider.Value}");

                // Update the bounding boxes
                this.ClearBoundingBoxes();
                this.frameRate = this.BoxesForFile?.FrameRate;
                if (this.frameToShow < 0 || null == this.frameRate || this.frameRate <= 0)
                {
                    // Can't process any bounding boxes
                    isProgrammaticUpdate = false;
                    return;
                }

                Point vidSize = GetActualVideoSize();

                int fromFrame = 0;
                if (this.frameRate != null)
                {
                    // Start from a frame a second before the current one
                    fromFrame = this.frameToShow - (int)Math.Floor((decimal)this.frameRate);
                    if (fromFrame < 0)
                    {
                        fromFrame = 0;
                    }
                }
                // Debug.Print($"frameToShow:{frameToShow} fromFrame{fromFrame}");
                if (null != this.BoxesForFile )
                {
                    this.BoxesForFile.DrawBoundingBoxesInCanvas(this.VideoCanvas, vidSize.X, vidSize.Y, 0, this.transformGroup, this.frameToShow, fromFrame);
                    if (this.frameRate != null)
                    {
                        this.frameToShow = Convert.ToInt32(Math.Ceiling((double)(VideoSlider.Value * this.frameRate)));
                    }
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
                if (Video.NaturalDuration == Duration.Automatic || (Video.NaturalDuration.HasTimeSpan && this.CurrentVideoPosition == Video.NaturalDuration.TimeSpan))
                {
                    this.CurrentVideoPosition = TimeSpan.Zero;
                    ShowPosition();
                }

                // Implementation Note due to media player bug.
                // This seemingly redundant bit of code is necessary as otherwise the media player will
                // sometimes start at the beginning, even if the this.CurrentVideoPosition is set to a different spot.
                // This essentially forces it to start at its current position. Unfortunately, it doesn't always work
                // as the video sometimes 'freezes'. Not sure why - I think its a mediaElement bug.
                TimeSpan startPosition = this.CurrentVideoPosition;
                this.CurrentVideoPosition = TimeSpan.Zero;
                Video.Play();
                Video.Pause();
                this.CurrentVideoPosition = startPosition;
                Video.Play();
                Debug.Print($"Play:{this.CurrentVideoPosition}");
                positionUpdateTimer.Start();
            }
            catch (Exception)
            {
                // A user reported a rare crash in the above
                PlayOrPause.IsChecked = false;
                positionUpdateTimer.Stop();
            }
        }
        #endregion

        #region Private Video UI control callbacks

        // The round play/pause button was clicked
        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
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
            TracePrint.StackTrace("--->", 2);
            // Debug.Print($"SetSpeed_Checked:{this.CurrentVideoPosition}");
            RadioButton rb = sender as RadioButton;

            // We use a tryparse as there was a rare bug when the tag could not be converted to a double
            if (rb?.Tag != null && Double.TryParse((string)rb.Tag, out double newSpeed))
            {
                Video.SpeedRatio = newSpeed;
                Play();
            }
        }
        private void CBAutoPlay_CheckChanged(object sender, RoutedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
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
            TracePrint.StackTrace("--->", 2);
            // Open the currently displayed video in an external player
            if (File.Exists(Uri.UnescapeDataString(Video.Source.AbsolutePath)))
            {
                Uri uri = new Uri(Uri.UnescapeDataString(Video.Source.AbsolutePath));
                ProcessExecution.TryProcessStart(uri);
            }
        }
        #endregion

        #region Private Timer callbacks
        // Set the video to automatically start playing after a brief delay 
        // This helps when one is navigating across videos, as there is a brief moment before the play starts.
        private void AutoPlayDelayTimer_Tick(object sender, EventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            // Debug.Print($"AutoPlay:{this.CurrentVideoPosition}");
            autoPlayDelayTimer.Stop();
            Play();
        }

        private void TimerUpdatePosition_Tick(object sender, EventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            // Debug.Print($"TimerUpdatePosition_Tick:{this.CurrentVideoPosition}");
            if (Video.Source != null)
            {
                ShowPosition();
            }
        }
        #endregion

        #region Private Slider-related callbacks
        // The Slider's position has changed
        // But we only take action when the change is due to scrubbing actions by the user 
        private void VideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (false == this.IsVisible || isProgrammaticUpdate)
            {
                return;
            }
            TracePrint.StackTrace("--->", 2);
            // Only pay attention to a user's scrubbing actions
            // Debug.Print($"ValueChanged (Slider):{this.CurrentVideoPosition}");
            //if (isProgrammaticUpdate)
            //{
            //    return;
            //}

            TimeSpan videoPosition = TimeSpan.FromSeconds(VideoSlider.Value);
            this.CurrentVideoPosition = videoPosition;
            // Debug.Print($"Slider:{this.CurrentVideoPosition}");
            // Update which frame we should be displaying
            if (this.frameRate != null)
            {
                this.frameToShow = Convert.ToInt32(System.Math.Floor((double)(VideoSlider.Value * this.frameRate)));
                this.ShowPosition();
            }

            this.Pause(); // If a user scrubs, force the video to pause if its playing
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void VideoPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
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
            double VideoZoomStep = 1.1;  // Constant.MarkableCanvas.ImageZoomStep

            // Abort if we are already at our maximum or minimum scaling values 
            if ((zoomIn && videoScale.ScaleX >= VideoZoomMaximum) ||
                (!zoomIn && videoScale.ScaleX <= VideoZoomMinimum))
            {
                return;
            }

            // If we are zooming in around a point off the image, then correct the location to the edge of the image
            if (currentMousePosition.X > Video.ActualWidth)
            {
                currentMousePosition.X = Video.ActualWidth;
            }
            if (currentMousePosition.Y > Video.ActualHeight)
            {
                currentMousePosition.Y = Video.ActualHeight;
            }

            // We will scale around the current point (This may be a no-op, but am not sure.)
            Point beforeZoom = PointFromScreen(Video.PointToScreen(currentMousePosition));

            // Calculate the scaling factor during zoom ins or out. Ensure that we keep within our
            // maximum and minimum scaling bounds. 
            if (zoomIn)
            {
                // We are zooming in
                // Calculate the scaling factor
                videoScale.ScaleX *= VideoZoomStep;   // Calculate the scaling factor
                videoScale.ScaleY *= VideoZoomStep;

                // Make sure we don't scale beyond the maximum scaling factor
                videoScale.ScaleX = Math.Min(VideoZoomMaximum, videoScale.ScaleX);
                videoScale.ScaleY = Math.Min(VideoZoomMaximum, videoScale.ScaleY);
            }
            else
            {
                // We are zooming out. 
                // Calculate the scaling factor
                videoScale.ScaleX /= VideoZoomStep;
                videoScale.ScaleY /= VideoZoomStep;

                // Make sure we don't scale beyond the minimum scaling factor
                videoScale.ScaleX = Math.Max(VideoZoomMinimum, videoScale.ScaleX);
                videoScale.ScaleY = Math.Max(VideoZoomMinimum, videoScale.ScaleY);

                // if there is no scaling, reset translations
                if (Math.Abs(videoScale.ScaleX - 1.0) < .0001 && Math.Abs(videoScale.ScaleY - 1.0) < .0001)
                {
                    videoTranslation.X = 0.0;
                    videoTranslation.Y = 0.0;
                    ShowPosition();
                    return; // I THINK WE CAN DO THIS - CHECK;
                }
            }

            Point afterZoom = PointFromScreen(Video.PointToScreen(currentMousePosition));

            // Scale the video, and at the same time translate it so that the 
            // location in the video (which is the location of the cursor) stays there
            lock (Video)
            {
                double videoWidth = Video.Width * videoScale.ScaleX;
                double videoHeight = Video.Height * videoScale.ScaleY;

                Point center = PointFromScreen(Video.PointToScreen(
                    new Point(Video.Width / 2.0, Video.Height / 2.0)));

                double newX = center.X - (afterZoom.X - beforeZoom.X);
                double newY = center.Y - (afterZoom.Y - beforeZoom.Y);

                if (newX - videoWidth / 2.0 >= 0.0)
                {
                    newX = videoWidth / 2.0;
                }
                else if (newX + videoWidth / 2.0 <= Video.ActualWidth)
                {
                    newX = Video.ActualWidth - videoWidth / 2.0;
                }

                if (newY - videoHeight / 2.0 >= 0.0)
                {
                    newY = videoHeight / 2.0;
                }
                else if (newY + videoHeight / 2.0 <= Video.ActualHeight)
                {
                    newY = Video.ActualHeight - videoHeight / 2.0;
                }
                videoTranslation.X += newX - center.X;
                videoTranslation.Y += newY - center.Y;
            }
            ShowPosition();
        }

        /// Translate the video from the previous mouse position the the current mouse position
        public void TranslateVideo(Point currentMousePosition, Point previousMousePosition)
        {
            // Get the center point on the image
            Point center = PointFromScreen(Video.PointToScreen(new Point(Video.Width / 2.0, Video.Height / 2.0)));

            // Calculate the delta position from the last location relative to the center
            double newX = center.X + currentMousePosition.X - previousMousePosition.X;
            double newY = center.Y + currentMousePosition.Y - previousMousePosition.Y;

            // get the translated image width
            double imageWidth = Video.Width * videoScale.ScaleX;
            double imageHeight = Video.Height * videoScale.ScaleY;

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
            videoTranslation.X += newX - center.X;
            videoTranslation.Y += newY - center.Y;
            ShowPosition();
        }
        #endregion

        #region Private Size changed callbacks
        // Refresh the bounding boxes if the canvas size changes, as otherwise they may be in the wrong place
        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            // Debug.Print("in Canvas_SizeChanged");
            this.ShowPosition();
        }

        // Scale the video by one increment around the screen location
        private void VideoPlayer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TracePrint.StackTrace("--->", 2);
            //Debug.Print($"SizeChanged:{this.CurrentVideoPosition}");
            // Fit the video into the canvas
            Video.Width = VideoCanvas.ActualWidth;
            Video.Height = VideoCanvas.ActualHeight;
            videoScale.CenterX = 0.5 * ActualWidth;
            videoScale.CenterY = 0.5 * ActualHeight;
        }
        #endregion

        #region Private Bounding box helpers
        // Remove all children (i.e., should only be existing bounding boxes) except for the video player
        private void ClearBoundingBoxes()
        {
            GlobalReferences.MainWindow.MarkableCanvas.ClearBoundingBoxes();
            if (this.VideoCanvas.Children.Count == 1)
            {
                // If only one child (i.e., the media player), then there are no bounding boxes
                return;
            }
            for (int i = this.VideoCanvas.Children.Count - 1; i > 0; i--)
            {
                UIElement child = this.VideoCanvas.Children[i];
                if (!child.Equals(this.Video))
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
            double videoWidth = this.Video.NaturalVideoWidth;
            double videoHeight = this.Video.NaturalVideoHeight;
            double playerWidth = this.Video.Width;
            double playerHeight = this.Video.Height;
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
        private static TimeSpan GetTimeSpanFromFrameNumber(int frameNumber, float? frameRate)
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
                return errorTime; ;
            }

            return TimeSpan.FromSeconds(frameNumber / (double)frameRate);
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
    }
}