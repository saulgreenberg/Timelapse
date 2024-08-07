using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Util;
using File = System.IO.File;

namespace Timelapse.Controls
{
    public partial class VideoPlayer
    {
        #region Public properties
        /// <summary>
        /// True if the video is unscaled, false if it is zoomed in
        /// </summary>        
        public bool IsUnScaled => Math.Abs(videoScale.ScaleX - 1) < 0.0001;

        #endregion

        #region Private variables
        private bool isProgrammaticUpdate;
        private readonly DispatcherTimer positionUpdateTimer;
        private readonly DispatcherTimer autoPlayDelayTimer;

        // render transforms
        private readonly ScaleTransform videoScale;
        private readonly TranslateTransform videoTranslation;
        private TransformGroup transformGroup;
        #endregion

        #region Constructor, Loading, Unloading
        public VideoPlayer()
        {
            InitializeComponent();
            isProgrammaticUpdate = false;
            positionUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250.0)
            };
            positionUpdateTimer.Tick += TimerUpdatePosition_Tick;

            // Timer used to automatically start playing the videos after a modest interval
            autoPlayDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300.0)
            };
            autoPlayDelayTimer.Tick += AutoPlayDelayTimer_Tick;
            IsEnabled = false;

            // Rendering setup
            videoScale = new ScaleTransform(1.0, 1.0);
            videoTranslation = new TranslateTransform(1.0, 1.0);
        }

        private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            //  Initial render transforms
            videoScale.CenterX = 0.5 * ActualWidth;
            videoScale.CenterY = 0.5 * ActualHeight;

            transformGroup = new TransformGroup();
            transformGroup.Children.Add(videoScale);
            transformGroup.Children.Add(videoTranslation);
            Video.RenderTransform = transformGroup;
            SizeChanged += VideoPlayer_SizeChanged;
            IsVisibleChanged += VideoPlayer_IsVisibleChanged;
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            positionUpdateTimer.Stop();
            IsEnabled = false;
        }
        // If the Video Player becomes visible, we need to start it playing if autoplay is true
        private void VideoPlayer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
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

        #region Public methods (SetSource, ScaleVideo, TranslateVideo)
        /// <summary>
        /// Play the Source Video provided in the URI
        /// </summary>
        /// <param name="source"></param>
        public void SetSource(Uri source)
        {
            // I think this is needed to release resources, but may be redundant
            Video.Stop();
            Video.Source = null;

            Video.Source = source;
            IsEnabled = true;
            double originalVolume = Video.Volume;
            Video.Volume = 0.0;
            Video.Play();
            Video.Pause();
            PlayOrPause.IsChecked = false;
            Video.Position = TimeSpan.FromMilliseconds(0.0);
            Video.Volume = originalVolume;
            // position updated through the media opened event
        }

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
        }

        /// Translate the video afrom the previous mouse position the the current mouse position
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
        }
        #endregion

        #region Public methods (Play, TryPlayOrPause)
        public void Pause()
        {
            positionUpdateTimer.Stop();
            Video.Pause();
            PlayOrPause.IsChecked = false;
            ShowPosition();
        }

        public bool TryTogglePlayOrPause()
        {
            if (Visibility != Visibility.Visible)
            {
                return false;
            }

            // WPF doesn't offer a way to fire a toggle button's click event programatically (ToggleButtonAutomationPeer -> IToggleProvider -> Toggle()
            // changes the state of the button but fails to trigger the click event) so do the equivalent in code
            PlayOrPause.IsChecked = !PlayOrPause.IsChecked;
            PlayOrPause_Click(this, null);
            return true;
        }
        #endregion

        #region Private methods: Play position
        // Scrub the video to the current slider position
        private void VideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isProgrammaticUpdate)
            {
                return;
            }

            TimeSpan videoPosition = TimeSpan.FromSeconds(VideoPosition.Value);
            Video.Position = videoPosition;
            ShowPosition();
            Pause(); // If a user scrubs, force the video to pause if its playing
        }

        // Show the current play position in the ScrollBar and TextBox, if possible.
        private void ShowPosition()
        {
            isProgrammaticUpdate = true;
            try
            {
                if (Video.NaturalDuration.HasTimeSpan)
                {
                    VideoPosition.Maximum = Video.NaturalDuration.TimeSpan.TotalSeconds;
                    // SAULXX: The line below will show the end time as a delta rather than absolute time. I decided that is undesirable, as the start time already shows the delta
                    // this.TimeDuration.Text = (this.Video.NaturalDuration.TimeSpan - this.Video.Position).ToString(Constant.Time_.VideoPositionFormat);
                    TimeDuration.Text = Video.NaturalDuration.TimeSpan.ToString(Time.VideoPositionFormat);
                    VideoPosition.TickFrequency = VideoPosition.Maximum / 10.0;
                }
                TimeFromStart.Text = Video.Position.ToString(Time.VideoPositionFormat);
                VideoPosition.Value = Video.Position.TotalSeconds;
                isProgrammaticUpdate = false;
            }
            catch
            {
                isProgrammaticUpdate = false;
            }
        }

        private void TimerUpdatePosition_Tick(object sender, EventArgs e)
        {
            if (Video.Source != null)
            {
                ShowPosition();
            }
        }
        #endregion

        #region Private methods and callbacks: Play and AutoPlay
        private void Play()
        {
            try
            {
                PlayOrPause.IsChecked = true;
                // start over from beginning if at end of video
                // Technote: The natural duration default value is Automatic if you query this property before MediaOpened. So we just reset the position if its a new video.
                if (Video.NaturalDuration == Duration.Automatic || (Video.NaturalDuration.HasTimeSpan && Video.Position == Video.NaturalDuration.TimeSpan))
                {
                    Video.Position = TimeSpan.Zero;
                    ShowPosition();
                }
                positionUpdateTimer.Start();
                Video.Play();
            }
            catch (Exception)
            {
                // A user reported a rare crash in the above
                PlayOrPause.IsChecked = false;
                positionUpdateTimer.Stop();
            }
        }

        // Set the video to automatically start playing after a brief delay 
        // This helps when one is navigating across videos, as there is a brief moment before the play starts.
        private void AutoPlayDelayTimer_Tick(object sender, EventArgs e)
        {
            Play();
            autoPlayDelayTimer.Stop();
        }

        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            if (PlayOrPause.IsChecked == true)
            {
                Play();
            }
            else
            {
                Pause();
            }
        }
        #endregion

        #region Private Callbacks (Size Changed, various controls, etc)

        // Scale the video by one increment around the screen location
        private void VideoPlayer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Fit the video into the canvas
            Video.Width = VideoCanvas.ActualWidth;
            Video.Height = VideoCanvas.ActualHeight;
            videoScale.CenterX = 0.5 * ActualWidth;
            videoScale.CenterY = 0.5 * ActualHeight;
        }

        // Set the speed, which also causes the video to play (if currently paused)
        private void SetSpeed_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;

            // We use a tryparse as there was a rare bug when the tag could not be converted to a double
            if (rb?.Tag != null && Double.TryParse((string)rb.Tag, out double newSpeed))
            {
                Video.SpeedRatio = newSpeed;
                Play();
            }
        }

        // Open the currently displayed video in an external player
        private void OpenExternalPlayer_Click(object sender, RoutedEventArgs e)
        {
            // Open the currently displayed video in an external player
            if (File.Exists(Uri.UnescapeDataString(Video.Source.AbsolutePath)))
            {
                Uri uri = new Uri(Uri.UnescapeDataString(Video.Source.AbsolutePath));
                ProcessExecution.TryProcessStart(uri);
            }
        }

        // When the video finishes playing, pause it and automatically return it to the beginning
        // Repeat playing if Auto play is on
        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            Pause();
            VideoPosition.Value = VideoPosition.Minimum; // Go back to the beginning
            if (CBRepeat.IsChecked == true)
            {
                Play();
            }
        }

        // When the video is first opened, Auto play it if Auto play is on
        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            ShowPosition();
            if (CBAutoPlay.IsChecked == true && Visibility == Visibility.Visible)
            {
                autoPlayDelayTimer.Start();
            }
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void VideoPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Pause();
        }

        private void CBAutoPlay_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (CBAutoPlay.IsChecked == true)
            {
                Play();
            }
            else
            {
                Pause();
            }
        }
        #endregion
    }
}