using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Microsoft.WindowsAPICodePack.Shell;
using System;
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
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Timelapse.Controls
{
    public partial class VideoPlayer
    {
        #region Public properties

        /// <summary>
        /// True if the video is unscaled (or null), false if it is zoomed in
        /// </summary>        
        public bool IsUnScaled => VideoScale == null || Math.Abs(VideoScale.ScaleX - 1) < 0.0001;

        /// <summary>
        ///  We do VideoPosition as a property so we can manipulate and/or check things whenever the video position is changed.
        /// </summary>
        private TimeSpan MediaElementCurrentPosition
        {
            get => this.MediaElement.Position;
            set => this.MediaElement.Position = value;
        }

        #endregion

        #region Private variables
        // Pointers to the video itself
        private long FileIndex; // A pointer to the ImageRow
        private Uri SourceUri;  // The currently loaded video

        // Bounding boxes and variables, so we can track where we are and what we need to show
        public float? FrameRate { get; set; } = -1;
        public int FrameToShow { get; set; }
        private double VideoDurationSeconds = -1;
        private TimeSpan VideoDurationTimeSpan;
        private BoundingBoxes BoxesForFile; // The bounding boxes for the current video
        private bool isProgrammaticUpdate; // To control callback execution

        // Tracks whether the player was previously loaded
        private bool IsVideoPlayerLoaded;

        // Timers
        private readonly DispatcherTimer TimerUpdatePosition;

        // There is a bug in the media player, where pausing and playing it doesn't respect the speed ratio
        // The workaround is to save the ratio here, then reset the ratio to 1 before playing, then reset
        // the ratio back to the saved one whenever the video position changes.
        double SavedSpeedRatio = 1;

        // Render transforms
        private readonly ScaleTransform VideoScale;
        private readonly TranslateTransform VideoTranslation;
        private TransformGroup TransformGroup;
        #endregion

        #region Constructor, Loading, Unloading

        public VideoPlayer()
        {
            InitializeComponent();

            // Set some parameters for the Media Element behavior
            // When the MediaState is Close, it releases all video-related resources, including video memory
            this.MediaElement.LoadedBehavior = MediaState.Manual;
            this.MediaElement.ScrubbingEnabled = true;
            this.MediaElement.UnloadedBehavior = MediaState.Close;

            this.isProgrammaticUpdate = false;

            // Initialize the timer that updates the UI to reflect the current video position 
            this.TimerUpdatePosition = new() { Interval = TimeSpan.FromMilliseconds(125.0) };

            // Rendering initialization
            this.VideoScale = new(1.0, 1.0);
            this.VideoTranslation = new(1.0, 1.0);

            // Start with the video controls disabled
            this.IsEnabled = false;
        }

        private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            if (false == GlobalReferences.MainWindow.IsFileDatabaseAvailable() || this.IsVideoPlayerLoaded)
            {
                // - No need to load things until a file database is actually available.
                // - For an unknown reason, this may be re-invoked twice. IsVideoPlayerLoaded ensures we ignore the 2nd invocation.
                return;
            }
            this.IsVideoPlayerLoaded = true;

            // Initialize Timer callbacks
            this.TimerUpdatePosition.Tick += TimerUpdatePosition_Tick;

            // Set various controls to their previously saved state.
            this.CBAutoPlay.IsChecked = GlobalReferences.TimelapseState.VideoAutoPlay;
            this.CBRepeat.IsChecked = GlobalReferences.TimelapseState.VideoRepeat;
            this.CBMute.IsChecked = GlobalReferences.TimelapseState.VideoMute;
            this.CBMute_CheckedChanged(null, null); // and set the mute state


            if (GlobalReferences.TimelapseState.VideoSpeed == 2)
            {
                this.RBNormal.IsChecked = true;
                this.MediaElement.SpeedRatio = 1;
            }
            else if (GlobalReferences.TimelapseState.VideoSpeed == 1)
            {
                this.RBSlow.IsChecked = true;
                this.MediaElement.SpeedRatio = 0.5;
            }
            else if (GlobalReferences.TimelapseState.VideoSpeed == 3)// if (GlobalReferences.TimelapseState.VideoSpeed == 3)
            {
                this.RBFast.IsChecked = true;
                this.MediaElement.SpeedRatio = 2;
            }
            else if (GlobalReferences.TimelapseState.VideoSpeed == 4)
            {
                this.RBVeryFast.IsChecked = true;
                this.MediaElement.SpeedRatio = 6;
            }
            else // set to normal speed
            {
                this.RBNormal.IsChecked = true;
                this.MediaElement.SpeedRatio = 1;
            }
            // Save the current speed
            this.SavedSpeedRatio = MediaElement.SpeedRatio;

            // For some reason, MediaOpened is invoked twice if we put it here, but not if its in the XAML.
            this.MediaElement.MediaEnded += MediaElementMediaEnded;
            this.MediaElement.Unloaded += MediaElementUnloaded;

            this.SliderScrubbing.ValueChanged += SliderScrubbing_ValueChanged;
            this.SliderScrubbing.PreviewMouseDown += SliderScrubbing_PreviewMouseDown;

            this.PlayOrPause.Click += PlayOrPause_Click;

            this.RBSlow.Checked += SetSpeed_CheckedChanged;
            this.RBNormal.Checked += SetSpeed_CheckedChanged;
            this.RBFast.Checked += SetSpeed_CheckedChanged;
            this.RBVeryFast.Checked += SetSpeed_CheckedChanged;

            this.CBAutoPlay.Checked += CBAutoPlay_CheckChanged;
            this.CBAutoPlay.Unchecked += CBAutoPlay_CheckChanged;

            this.OpenExternalPlayer.Click += OpenExternalPlayer_Click;
            this.CBMute.Checked += CBMute_CheckedChanged;
            this.CBMute.Unchecked += CBMute_CheckedChanged;

            this.CBRepeat.Checked += CBRepeat_CheckChanged;
            this.CBRepeat.Unchecked += CBRepeat_CheckChanged;
        }

        // Set the flag to indicate that the video player is no longer loaed
        private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
        {
            //TracePrint.StackTrace("--->", 5);
            this.IsVideoPlayerLoaded = false;
        }
        #endregion

        #region SetSource
        /// <summary>
        /// Set the Source Video provided in the URI.
        /// Doing so invokes the MediaOpened callback, which actually initializes everything
        /// </summary>
        public void SetSource(Uri source, long fileIndex)
        {
            this.ClearBoundingBoxes();
            this.FileIndex = fileIndex;
            this.SourceUri = source;

            // Get bounding boxes for this video (if any), and enable the  best frame button as needed
            this.BoxesForFile = null == GlobalReferences.MainWindow?.DataHandler?.FileDatabase?.FileTable
                ? null
                : GlobalReferences.MainWindow?.GetBoundingBoxesForCurrentFile(GlobalReferences.MainWindow.DataHandler.FileDatabase.FileTable[(int)fileIndex].ID);
            this.ButtonEnableBestFrameIfNeeded();

            // Stop the timers for now
            this.TimerUpdatePosition.Stop();

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
        #endregion

        #region MediaElement Opened / Ended / Unloaded
        // When the video is first opened, set everything to conform to this video and play it if Auto play is on
        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (this.MediaElement.Source == null)
            {
                this.IsEnabled = false;
                return;
            }

            this.IsEnabled = true;

            // Determine and set the FrameRate, FrameToShow, VideoDuraton and Slider Scrubbing Duration values
            this.DoSetFrameRateAndFrameToShowValues();
            this.DoSetDurationValues();
            this.DoSetSliderAttributesForDuration();

            // We start at the beginning of the video
            TimeSpan startTime = TimeSpan.Zero;
            this.FrameToShow = 0;

            // Set the video, slider and feedback text to the desired start time.
            this.isProgrammaticUpdate = true;
            this.SliderScrubbing.Value = startTime.TotalSeconds; // This will be set in ShowPosition so no need to do it here.
            this.isProgrammaticUpdate = false;

            // Update the slider as needed
            this.DoSetSliderText();

            // Update the bounding boxes (if any) to display at this time interval  (i.e., 0)
            this.UpdateBoundingBoxes();

            // This is needed to prime the video
            this.Play();

            // Pause the video depending on conditions
            if (Visibility != Visibility.Visible || CBAutoPlay.IsChecked == false)
            {
                this.Pause();
            }
        }

        // When the video finishes playing, pause it and automatically return it to the beginning
        // Repeat playing if Auto play is on
        private void MediaElementMediaEnded(object sender, RoutedEventArgs e)
        {
            this.Pause();

            SliderScrubbing.Value = SliderScrubbing.Minimum; // Go back to the beginning
            if (CBRepeat.IsChecked == true)
            {
                this.Play();
            }
        }

        // Stop all timers and diable the video player controls
        private void MediaElementUnloaded(object sender, RoutedEventArgs e)
        {
            this.TimerUpdatePosition.Stop();
            IsEnabled = false;
        }
        #endregion

        #region Play, TryPlayOrPause, Pause - Public ones invoked by MarkableCanvas
        private void Play()
        {
            try
            {
                PlayOrPause.IsChecked = true;
                // start over from beginning if at end of video
                // Technote: The natural duration default value is Automatic if you query this property before MediaOpened. So we just reset the position if its a new video.
                if (this.MediaElementCurrentPosition >= this.VideoDurationTimeSpan)
                {
                    this.MediaElementCurrentPosition = TimeSpan.Zero;
                    ShowPosition();
                }

                // There is a bug in the media player, where pausing and playing it doesn't respect the speed ratio
                // The workaround is to save the ratio here, then reset the ratio to 1 before playing, then reset
                // the ratio back to the saved one whenever the video position changes.
                MediaElement.SpeedRatio = 1; 
                MediaElement.Play();
                TimerUpdatePosition.Start();
            }
            catch (Exception)
            {
                // A user reported a rare crash in the above
                PlayOrPause.IsChecked = false;
                TimerUpdatePosition.Stop();
            }
        }

        // Pause the video.
        // Public as the markable canvas pauses the video when switching to Image or ThumbnailGrid views
        public void Pause()
        {
            TimerUpdatePosition.Stop();
            this.MediaElement.Pause();
            this.PlayOrPause.IsChecked = false;
            if (this.isProgrammaticUpdate == false)
            {
                this.ShowPosition();
            }
        }

        // A programmatic equivalent to clicking the play/pause button
        public bool TryTogglePlayOrPause()
        {
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

        #region Frame by frame navigation
        private void BtnNavigateFrame_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is RepeatButton { Tag: "Next" })
            {
                // Navigate to the next frame
                this.NavigateFrame(true);
            }
            else if (sender is RepeatButton { Tag: "Previous" })
            {
                // Navigate to the previous frame
                this.NavigateFrame(false);
            }
        }

        // Navigate the next or previous frame in the video. Return true if the navigation did anything (i.e., not at beginning or end of video)
        public bool NavigateFrame(bool forward)
        {
            if (this.Visibility != Visibility.Visible || null == this.MediaElement.Source || this.VideoDurationSeconds <= 0)
            {
                // No video to navigate
                return false;
            }

            this.isProgrammaticUpdate = true;
            this.Pause();
            this.isProgrammaticUpdate = false;

            // We should almost always have a valid frame rate.
            // But if we don't, we can't navigate by frames
            // So lets just set it for 15, which may be too fast or too slow for some videos
            float? frameRate = this.FrameRate == null || this.FrameRate == 0
                ? 15.0f
                : this.FrameRate;

            TimeSpan newPosition = forward
            ? TimeSpan.FromMilliseconds(this.MediaElement.Position.TotalMilliseconds + 1000.0 / frameRate.Value)
            : TimeSpan.FromMilliseconds(this.MediaElement.Position.TotalMilliseconds - 1000.0 / frameRate.Value);

            if (newPosition.TotalMilliseconds < 0)
            {
                // We are at the beginning of the video, so don't go back any further
                return false;
            }
            this.MediaElement.Position = newPosition;
            TimerUpdatePosition.Start();
            return true;
        }
        #endregion

        #region ShowPosition : Update everything depending upon the current video position
        private void MediaElementUpdatePosition(TimeSpan videoTime)
        {
            // Update the position in the video to the given time
            if (null == this.MediaElement.Source || this.FrameRate == null || this.VideoDurationSeconds <= 0)
            {
                return;
            }

            this.MediaElementCurrentPosition = videoTime;
        }

        // Show the current play position in the ScrollBar and TextBox, if possible.
        // TODO Performance (very minor, likley not worth doing). Sometimes, when scrubbing, the same video frame is updated several times.
        // To see this, just click on the slider without moving its position.
        private void ShowPosition()
        {
            if (null == this.MediaElement.Source)
            {
                // We aren't displaying anything, so nothing to show
                return;
            }

            isProgrammaticUpdate = true;
            try
            {
                // Feedback: show current position in the video (text and slider)
                TimeFromStart.Text = this.MediaElementCurrentPosition.ToString(Time.VideoPositionFormat);
                this.isProgrammaticUpdate = true;
                SliderScrubbing.Value = this.MediaElementCurrentPosition.TotalSeconds;
                this.isProgrammaticUpdate = false;

                // Update the bounding boxes
                this.ClearBoundingBoxes();
                //this.FrameRate = this.BoxesForFile?.FrameRate; // Already set in DoSetFrameRateAndFrameToShowValues
                if (this.FrameToShow < 0 || null == this.FrameRate || this.FrameRate <= 0)
                {
                    // Can't process any bounding boxes
                    isProgrammaticUpdate = false;
                    return;
                }

                // Start searching from a frame a half second before the desired one
                Point vidSize = GetActualVideoSize();
                int frameWindow = this.FrameRate == null
                    ? 0
                    : (int)Math.Floor((decimal)(this.FrameRate / 2.0));

                if (null != this.BoxesForFile)
                {
                    this.BoxesForFile.DrawBoundingBoxesInCanvas(this.VideoCanvas, vidSize.X, vidSize.Y, 0, this.TransformGroup, this.FrameToShow, frameWindow);
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

        #region Callback: IsVisibleChanged
        // When the visibility changes to visible, play it if Autoplay is checked, otherwise pause it.
        private void VideoPlayer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (CBAutoPlay.IsChecked == true && (bool)e.NewValue)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }
        #endregion

        #region Callbacks: Buttons and Checkbuttons

        // The round play/pause button was clicked
        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            if (this.PlayOrPause.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        // The Mute checkbutton
        private void CBMute_CheckedChanged(object sender, RoutedEventArgs e)
        {
            MediaElement.Volume = CBMute.IsChecked == true && null != this.MediaElement
                ? 0
                : 0.5;
            GlobalReferences.TimelapseState.VideoMute = CBMute.IsChecked == true;
        }

        // The Speed Checkbutton.
        // Note that some videos don't do anything even if the speed ratio is changed.
        private void SetSpeed_CheckedChanged(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            // The speed is hard-wired to the tag. Note: we use tryparse as there was a rare bug when the tag could not be converted to a double
            if (rb?.Tag != null && Double.TryParse((string)rb.Tag, out double newSpeed))
            {
                this.MediaElement.SpeedRatio = newSpeed;
                this.SavedSpeedRatio = newSpeed;

                if (rb.Name == "RBFast")
                {
                    GlobalReferences.TimelapseState.VideoSpeed = 3;
                }
                else if (rb.Name == "RBVeryFast")
                {
                    GlobalReferences.TimelapseState.VideoSpeed = 4;
                }
                else if (rb.Name == "RBSlow")
                {
                    GlobalReferences.TimelapseState.VideoSpeed = 1;
                }
                else // (rb.Name == "RBNormal")
                {
                    GlobalReferences.TimelapseState.VideoSpeed = 2;
                }
                this.Play();
            }
        }

        // The AutoPlay checkbutton
        private void CBAutoPlay_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (CBAutoPlay.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
            GlobalReferences.TimelapseState.VideoAutoPlay = CBAutoPlay.IsChecked == true;
        }

        private void CBRepeat_CheckChanged(object sender, RoutedEventArgs e)
        {
            GlobalReferences.TimelapseState.VideoRepeat = CBRepeat.IsChecked == true;
        }

        // The Open ExternalPlayer button
        private void OpenExternalPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(Uri.UnescapeDataString(MediaElement.Source.AbsolutePath)))
            {
                Uri uri = new(Uri.UnescapeDataString(MediaElement.Source.AbsolutePath));
                ProcessExecution.TryProcessStart(uri);
            }
        }
        #endregion

        #region Callbacks: Timer Ticks

        private void TimerUpdatePosition_Tick(object sender, EventArgs e)
        {
            if (MediaElement.Source != null)
            {
                // There is a bug in the media player, where pausing and playing it doesn't respect the speed ratio
                // The workaround is to save the ratio here, then reset the ratio to 1 before playing, then reset
                // the ratio back to the saved one whenever the video position changes.
                MediaElement.SpeedRatio = SavedSpeedRatio; // Restore the speed ratio
                ShowPosition();
            }
        }
        #endregion

        #region Private Slider-related callbacks
        // The Slider's position has changed
        // But we only take action when the change is due to scrubbing actions by the user 
        private void SliderScrubbing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (false == this.IsVisible || isProgrammaticUpdate)
            {
                return;
            }

            TimeSpan videoPosition = TimeSpan.FromSeconds(SliderScrubbing.Value);
            this.MediaElementCurrentPosition = videoPosition;

            // Update which frame we should be displaying
            if (this.FrameRate != null)
            {
                this.FrameToShow = Convert.ToInt32(System.Math.Floor((double)(SliderScrubbing.Value * this.FrameRate)));
            }
            // Pause also updates the position
            this.Pause(); // If a user scrubs, force the video to pause if its playing
        }

        // When the user starts moving the slider, we want to pause the video so the two actions don't interfere with each other
        private void SliderScrubbing_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Pause();
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
                ? new(playerWidth, playerWidth * videoHeight / videoWidth)
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

                string currentPath = mainWindow.DataHandler.ImageCache.Current.GetFilePath(GlobalReferences.MainWindow.DataHandler.FileDatabase.RootPathToImages);
                frameRate = VideoPlayer.GetFrameRateFromFile(currentPath);
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
                ShellObject obj = ShellObject.FromParsingName(path);
                ShellProperty<uint?> rateProp = obj?.Properties?.GetProperty<uint?>("System.Video.FrameRate");
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
            this.VideoDurationTimeSpan = new(0, 0, 0, 0, Convert.ToInt32(this.VideoDurationSeconds * 1000));
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
            this.VideoScale.CenterX = 0.5 * ActualWidth;
            this.VideoScale.CenterY = 0.5 * ActualHeight;
            this.TransformGroup = new();
            this.TransformGroup.Children.Add(VideoScale);
            this.TransformGroup.Children.Add(VideoTranslation);
            this.MediaElement.RenderTransform = TransformGroup;
        }
        #endregion

        #region UpdateBoundingBoxes
        // This may be invoked several times unecessarily, but in those case the tests below (e.g., for null etc)
        // make those cases a nearly 'noop' operation
        private void UpdateBoundingBoxes()
        {
            try
            {
                this.ClearBoundingBoxes();

                if (null == this.MediaElement.Source || this.FrameRate == null || this.FrameRate <= 0 || this.FrameToShow < 0 || this.VideoDurationSeconds <= 0)
                {
                    // Can't process any bounding boxes or can't display them
                    return;
                }

                isProgrammaticUpdate = true;
                // Start searching from a frame a second before the desired one
                Point vidSize = GetActualVideoSize();
                int frameWindow = this.FrameRate == null
                    ? 0
                    : (int)Math.Floor((decimal)(this.FrameRate / 2.0));

                if (null != this.BoxesForFile)
                {
                    //Debug.Print($"{this.FrameToShow}, {fromFrame}, {span}");
                    this.BoxesForFile.DrawBoundingBoxesInCanvas(this.VideoCanvas, vidSize.X, vidSize.Y, 0, this.TransformGroup, this.FrameToShow, frameWindow);
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

        #region Go to best frame
        // Try going to the best video frame, if possible
        // Usually invoked with a shortcut key
        public void TryGoToBestFrame()
        {
            if (this.Visibility == Visibility.Visible && null != this.MediaElement?.Source)
            {
                // This will go to the best bounding box, but only if there are bounding boxes available
                this.DoGoToBestFrame();
            }
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
            this.isProgrammaticUpdate = false;

            // Show the bounding box for the current frame
            this.UpdateBoundingBoxes();
        }
        #endregion

        #region ButtonBestFrame
        private void ButtonEnableBestFrameIfNeeded()
        {
            // Enable only if there are bounding boxes and the max confidence > the display threshold
            this.BorderButtonBestFrame.Visibility = null != this.BoxesForFile &&
                                                    this.BoxesForFile.Boxes.Count > 0 &&
                                                    this.BoxesForFile.MaxConfidence >= GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold &&
                                                    this.BoxesForFile.FrameRate is > 0
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
                    new(MediaElement.Width / 2.0, MediaElement.Height / 2.0)));

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
            Point center = PointFromScreen(MediaElement.PointToScreen(new(MediaElement.Width / 2.0, MediaElement.Height / 2.0)));

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
            // Minor inefficiency: SizeChanged is invoked twice when Timelapse is first started on a video
            MediaElement.Width = VideoCanvas.ActualWidth;
            MediaElement.Height = VideoCanvas.ActualHeight;
            VideoScale.CenterX = 0.5 * ActualWidth;
            VideoScale.CenterY = 0.5 * ActualHeight;

            this.UpdateBoundingBoxes();
        }
        #endregion


    }
}