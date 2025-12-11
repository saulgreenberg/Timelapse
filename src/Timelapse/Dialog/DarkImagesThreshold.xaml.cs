using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.State;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace Timelapse.Dialog
{
    public partial class DarkImagesThreshold : IDisposable
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private readonly TimelapseUserRegistrySettings state;

        private readonly bool updateDarkClassificationForAllSelectedImagesStarted;
        private readonly DispatcherTimer dispatcherTimer = new();
        private readonly FileTableEnumerator imageEnumerator;

        private const int MinimumRectangleWidth = 12;

        private WriteableBitmap bitmap;
        private int darkPixelThreshold;
        private double darkPixelRatio;
        private double darkPixelRatioFound;

        private bool disposed;

        private bool isColor;

        private bool displatcherTimerIsPlaying;

        private readonly Dictionary<string, string> FlagLabelsDataLabels = [];
        private string ChosenFlagLabel = string.Empty;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;
        #endregion

        #region Initialization
        public DarkImagesThreshold(TimelapseWindow owner, FileDatabase fileDatabase, TimelapseUserRegistrySettings state, int currentImageIndex) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(state, nameof(state));

            InitializeComponent();
            Owner = owner;
            this.fileDatabase = fileDatabase;
            this.state = state;
            imageEnumerator = new(fileDatabase);
            imageEnumerator.TryMoveToFile(currentImageIndex);

            darkPixelThreshold = state.DarkPixelThreshold;
            darkPixelRatio = state.DarkPixelRatioThreshold;
            darkPixelRatioFound = 0;
            isColor = false;
            updateDarkClassificationForAllSelectedImagesStarted = false;

            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new(0, 0, 0, 0, 100);

            disposed = false;
        }

        // Display the image and associated details in the UI
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            DarkThreshold.Value = state.DarkPixelThreshold;
            DarkThreshold.ValueChanged += DarkThresholdSlider_ValueChanged;

            ScrollImages.Minimum = 0;
            ScrollImages.Maximum = fileDatabase.CountAllCurrentlySelectedFiles - 1;
            ScrollImages.Value = imageEnumerator.CurrentRow;

            // Collect all the flag controls in a dictionary by Label,DataLabel (DeleteFlag would not be collected)
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type == Control.Flag)
                {
                    if (false == FlagLabelsDataLabels.ContainsKey(control.Label))
                    {
                        FlagLabelsDataLabels.Add(control.Label, control.DataLabel);
                    }
                }
            }
            // Populate the combobox with the labels of the available flag controls
            CBPopulateFlagField.ItemsSource = FlagLabelsDataLabels.Keys.ToList();

            // Gie the user some instructions depending on whether any flage are available
            if (FlagLabelsDataLabels.Count == 0)
            {
                SelectAFlagField.Content = "No flag fields available for populating dark classifications! Create one in the template.";
                CBPopulateFlagField.Visibility = Visibility.Collapsed;
                LabelWarning1.Content = "No flag fields are available for populating dark classifications.";
                LabelWarning2.Content = "Create one in the template.";
            }
            else
            {
                LabelWarning1.Content = "";
                LabelWarning2.Content = "Select a Flag field to activate the Start button.";
            }

            SetPreviousNextPlayButtonStates();
            ScrollImages_ValueChanged(null, null);
            ScrollImages.ValueChanged += ScrollImages_ValueChanged;
            Focus();               // necessary for the left/right arrow keys to work.
        }
        #endregion

        #region Closing and Disposing
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested && IsAnyDataUpdated;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                imageEnumerator?.Dispose();
            }

            disposed = true;
        }
        #endregion

        #region UI Updating
        public void Repaint()
        {
            // Color the bar to show the current color given the dark color threshold
            byte greyColor = (byte)Math.Round(255 - (double)255 * darkPixelThreshold);
            Brush brush = new SolidColorBrush(Color.FromArgb(255, greyColor, greyColor, greyColor));
            RectDarkPixelRatioFound.Fill = brush;
            lblGreyColorThreshold.Content = (greyColor + 1).ToString();

            // Size the bar to show how many pixels in the current image are at least as dark as that color
            if (isColor)
            {
                // color image
                RectDarkPixelRatioFound.Width = MinimumRectangleWidth;
            }
            else
            {
                RectDarkPixelRatioFound.Width = FeedbackCanvas.ActualWidth * darkPixelRatioFound;
                if (RectDarkPixelRatioFound.Width < MinimumRectangleWidth)
                {
                    RectDarkPixelRatioFound.Width = MinimumRectangleWidth; // Just so something is always visible
                }
            }
            RectDarkPixelRatioFound.Height = FeedbackCanvas.ActualHeight;

            // Show the location of the %age threshold bar
            DarkPixelRatioThumb.Height = FeedbackCanvas.ActualHeight;
            DarkPixelRatioThumb.Width = MinimumRectangleWidth;
            Canvas.SetLeft(DarkPixelRatioThumb, (FeedbackCanvas.ActualWidth - DarkPixelRatioThumb.ActualWidth) * darkPixelRatio);

            UpdateLabels();
        }

        // Update all the labels to show the current old and new classification values
        private void UpdateLabels()
        {
            DarkPixelRatio.Content = $"{100 * darkPixelRatio,3:##0}%";
            RatioFound.Content = $"{100 * darkPixelRatioFound,3:##0}";

            // We don't want to update labels if the image is not valid 
            if (false == Boolean.TryParse(OriginalClassification.Content.ToString(), out _))
            {
                // We don't have a value, so just set it to blank
                OriginalClassification.Content = "----";
            }

            if (isColor)
            {
                // color image 
                ThresholdMessage.Text = "Color - therefore not dark";
                Percent.Visibility = Visibility.Collapsed;
                RatioFound.Visibility = Visibility.Collapsed;
                RatioFound.Content = string.Empty;
            }
            else
            {
                ThresholdMessage.Text = "of the pixels are darker than the threshold";
                RatioFound.Visibility = Visibility.Visible;
                Percent.Visibility = Visibility.Visible;
            }

            if (isColor)
            {
                NewClassification.Content = BooleanValue.False;       // Color image
            }
            else if (darkPixelRatio <= darkPixelRatioFound)
            {
                NewClassification.Content = BooleanValue.True;  // Dark grey scale image
            }
            else
            {
                NewClassification.Content = BooleanValue.False;   // Light grey scale image
            }
        }

        // Utility routine for calling a typical sequence of UI update actions
        private void DisplayImageAndDetails()
        {
            if (imageEnumerator.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(imageEnumerator.Current));
                return;
            }
            bitmap = imageEnumerator.Current.LoadBitmap(fileDatabase.RootPathToImages, out _).AsWriteable();
            Image.Source = bitmap;
            FileName.Content = imageEnumerator.Current.File;
            FileName.ToolTip = imageEnumerator.Current.File;

            if (string.IsNullOrEmpty(ChosenFlagLabel) ||
                string.IsNullOrWhiteSpace(imageEnumerator?.Current?.GetValueDatabaseString(FlagLabelsDataLabels[ChosenFlagLabel])))
            {
                OriginalClassification.Content = string.Empty;
            }
            else
            {
                OriginalClassification.Content = imageEnumerator.Current.GetValueDatabaseString(FlagLabelsDataLabels[ChosenFlagLabel]);
            }
            RecalculateDarkClassificationForCurrentImage();
            Repaint();
        }
        #endregion

        #region Do the actual updating of image quality
        /// <summary>
        /// Redo image quality calculations with current thresholds and return the ratio of pixels at least as dark as the threshold for the current image.
        /// Does not update the database.
        /// </summary>
        private void RecalculateDarkClassificationForCurrentImage()
        {
            bitmap.IsDark(darkPixelThreshold, darkPixelRatio, out darkPixelRatioFound, out isColor);
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds for all images selected.  Updates the database.
        /// </summary>
        private async Task<string> BeginUpdateDarkClassificationForAllSelectedImagesAsync()
        {
            return await Task.Run(() =>
            {
                // The selected files to check
                List<ImageRow> selectedFiles = fileDatabase.FileTable.ToList();
                List<ColumnTuplesWithWhere> filesToUpdate = [];
                int fileIndex = 0;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                int selectedFilesCount = selectedFiles.Count;
                string dataLabel = string.IsNullOrEmpty(ChosenFlagLabel)
                                    ? string.Empty
                                    : FlagLabelsDataLabels[ChosenFlagLabel];
                foreach (ImageRow file in selectedFiles)
                {
                    if (Token.IsCancellationRequested)
                    {
                        // A cancel was requested. Clear all pending changes and abort
                        filesToUpdate.Clear();
                        return "Cancelled - no changes made.";
                    }

                    ImageSettingsForDarkClassifications imageQuality = new(file, dataLabel);
                    try
                    {
                        // Get the image, and add it to the list of images to be updated if the imageQuality has changed
                        // Note that if the image can't be created, we will just go to the catch.
                        // We also use a TransientLoading, as the estimate of darkness will work just fine on that
                        imageQuality.Bitmap = file.LoadBitmap(fileDatabase.RootPathToImages, ImageDisplayIntentEnum.Ephemeral, out bool isCorruptOrMissing).AsWriteable();
                        if (isCorruptOrMissing)
                        {
                            // If we can't read the image, just set its darkness to false
                            imageQuality.NewDarkClassification = false;
                        }
                        else
                        {
                            // Set the image quality. Note that videos are always classified as false.
                            imageQuality.NewDarkClassification = !file.IsVideo && imageQuality.Bitmap.IsDark(darkPixelThreshold, darkPixelRatio, out darkPixelRatioFound, out isColor);
                        }
                        imageQuality.IsColor = isColor;
                        imageQuality.DarkPixelRatioFound = darkPixelRatioFound;

                        string newDarkClassificationAsString = imageQuality.NewDarkClassification
                        ? BooleanValue.True
                        : BooleanValue.False;

                        if (imageQuality.OldDarkClassification != imageQuality.NewDarkClassification)
                        {
                            file.SetValueFromDatabaseString(dataLabel, newDarkClassificationAsString);
                            filesToUpdate.Add(new([new(dataLabel, newDarkClassificationAsString)], file.ID));
                        }
                    }
                    catch (Exception exception)
                    {
                        // file isn't there?
                        imageQuality.NewDarkClassification = false;
                        Debug.Fail("Exception while assessing image quality.", exception.ToString());
                    }

                    fileIndex++;
                    if (ReadyToRefresh())
                    {
                        int percentDone = (int)(100.0 * fileIndex / selectedFilesCount);
                        Progress.Report(new(percentDone,
                            $"{fileIndex}/{selectedFilesCount} images. Processing {file.File}", true, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                }

                // Update the database to reflect the changed values
                // Tracks whether any changes to the data or database are made
                Progress.Report(new(100,
                    $"Writing changes for {filesToUpdate.Count} files. Please wait...", false, true));
                Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                IsAnyDataUpdated = true;
                fileDatabase.UpdateFiles(filesToUpdate);
                return filesToUpdate.Count > 0
                ? $"{selectedFilesCount} files examined, with {filesToUpdate.Count} updated to reflect changes."
                : $"{selectedFilesCount} files examined. None were updated as nothing has changed.";
            }, Token).ConfigureAwait(true);
        }
        #endregion

        #region UI Callback: Set the populate flag field 
        private void CBPopulateFlagField_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cmb)
            {
                foreach (ControlRow control in fileDatabase.Controls)
                {
                    if (control.Label == (string)cmb.SelectedItem)
                    {
                        ChosenFlagLabel = control.Label;
                    }
                }
                bool success = false == string.IsNullOrEmpty(ChosenFlagLabel);
                StartDoneButton.IsEnabled = success;
                if (success)
                {
                    LabelWarning1.Content = "Press Start to populate Dark classification data";
                    LabelWarning2.Content = $"in the {ChosenFlagLabel} data field";
                }
                else
                {
                    // This should never happen, as the user can only select valid flags
                    LabelWarning1.Content = "";
                    LabelWarning2.Content = "Select a Flag field to activate the Start button.";
                }
                DisplayImageAndDetails();
            }
        }
        #endregion

        #region UI Menu Callbacks for resetting thresholds
        // A drop-down menu providing the user with two ways to reset thresholds
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button resetButton == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }

            if (resetButton.ContextMenu == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(resetButton.ContextMenu));
                return;
            }
            resetButton.ContextMenu.IsEnabled = true;
            resetButton.ContextMenu.PlacementTarget = (Button)sender;
            resetButton.ContextMenu.Placement = PlacementMode.Bottom;
            resetButton.ContextMenu.IsOpen = true;
        }

        // Reset the thresholds to their initial settings
        private void MenuItemResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            darkPixelRatio = state.DarkPixelRatioThreshold;
            darkPixelThreshold = state.DarkPixelThreshold;
            Canvas.SetLeft(DarkPixelRatioThumb, darkPixelRatio * (FeedbackCanvas.ActualWidth - DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            DarkThreshold.Value = state.DarkPixelThreshold;
            RecalculateDarkClassificationForCurrentImage();
            Repaint();
        }

        // Reset the thresholds to the Default settings
        private void MenuItemResetDefault_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            darkPixelRatio = ImageValues.DarkPixelRatioThresholdDefault;
            Canvas.SetLeft(DarkPixelRatioThumb, darkPixelRatio * (FeedbackCanvas.ActualWidth - DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            DarkThreshold.Value = ImageValues.DarkPixelThresholdDefault;
            RecalculateDarkClassificationForCurrentImage();
            Repaint();
        }
        #endregion

        #region UI Callbacks - setting thresholds for what is dark

        // Set a new value for the dark pixel threshold and update the UI
        private void DarkThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DarkPixelRatio == null)
            {
                return;
            }
            darkPixelThreshold = Convert.ToInt32(e.NewValue);

            RecalculateDarkClassificationForCurrentImage();
            Repaint();
        }

        // Set a new value for the Dark Pixel Ratio and update the UI
        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (e.Source is UIElement thumb == false)
            {
                TracePrint.NullException(nameof(thumb));
                return;
            }

            if ((Canvas.GetLeft(thumb) + e.HorizontalChange) >= (FeedbackCanvas.ActualWidth - DarkPixelRatioThumb.ActualWidth))
            {
                Canvas.SetLeft(thumb, FeedbackCanvas.ActualWidth - DarkPixelRatioThumb.ActualWidth);
                darkPixelRatio = 1;
            }
            else if ((Canvas.GetLeft(thumb) + e.HorizontalChange) <= 0)
            {
                Canvas.SetLeft(thumb, 0);
                darkPixelRatio = 0;
            }
            else
            {
                Canvas.SetLeft(thumb, Canvas.GetLeft(thumb) + e.HorizontalChange);
                darkPixelRatio = (Canvas.GetLeft(thumb) + e.HorizontalChange) / FeedbackCanvas.ActualWidth;
            }
            if (DarkPixelRatio == null)
            {
                return;
            }

            RecalculateDarkClassificationForCurrentImage();
            // We don't repaint, as this will screw up the thumb dragging. So just update the labels instead.
            UpdateLabels();
        }
        #endregion

        #region UI callbacks - Navigating through images
        // Scroll to another image
        private void ScrollImages_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updateDarkClassificationForAllSelectedImagesStarted)
            {
                return;
            }

            imageEnumerator.TryMoveToFile(Convert.ToInt32(ScrollImages.Value));
            DisplayImageAndDetails();
            SetPreviousNextPlayButtonStates();
        }

        // If its an arrow key navigate left/right image 
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!ReadyToRefresh())
            {
                // only update every now and then, as otherwise it stalls when the arrow key is held down
                return;
            }
            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            switch (e.Key)
            {
                case Key.Right:             // next file
                    NextButton_Click(null, null);
                    break;
                case Key.Left:              // previous file
                    PreviousButton_Click(null, null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        // Navigate to the previous image
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            imageEnumerator.MovePrevious();
            ScrollImages.Value = imageEnumerator.CurrentRow;
        }

        // Navigate to the next image
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            imageEnumerator.MoveNext();
            ScrollImages.Value = imageEnumerator.CurrentRow;
        }

        // Helper for the above, where previous/next buttons are enabled/disabled as needed
        private void SetPreviousNextPlayButtonStates()
        {
            PreviousFile.IsEnabled = imageEnumerator.CurrentRow != 0;
            NextFile.IsEnabled = (imageEnumerator.CurrentRow < fileDatabase.CountAllCurrentlySelectedFiles - 1);
            if (NextFile.IsEnabled == false)
            {
                // We are at the end, so stop playback and disable the play button
                PlayButtonSetState(false, false);
            }
            else if (displatcherTimerIsPlaying == false && NextFile.IsEnabled)
            {
                // We are at the end, so stop playback and disable the play button
                PlayButtonSetState(false, true);
            }
        }

        // Show the next file after every tick
        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            NextButton_Click(null, null);
        }

        // Play the images automatically
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            displatcherTimerIsPlaying = !displatcherTimerIsPlaying;
            PlayButtonSetState(displatcherTimerIsPlaying, true);
        }

        // Set the play/pause/enable state of the play button
        private void PlayButtonSetState(bool play, bool enabled)
        {
            if (play)
            {
                // Play
                dispatcherTimer.Start();
                displatcherTimerIsPlaying = true;
                // Show Pause character
                PlayFile.Content = "\u23F8";
            }
            else
            {
                // Pause
                dispatcherTimer.Stop();
                displatcherTimerIsPlaying = false;
                // Start the playback
                // Show Fast forward character
                PlayFile.Content = "\u23E9";
            }
            PlayFile.IsEnabled = enabled;
        }
        #endregion

        #region Button callbacks
        // Update the database if the OK button is clicked
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Update state variables to the current settings
            state.DarkPixelThreshold = darkPixelThreshold;
            state.DarkPixelRatioThreshold = darkPixelRatio;

            // update the UI
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            StartDoneButton.Content = "_Done";
            StartDoneButton.Click -= StartButton_Click;
            StartDoneButton.Click += DoneButton_Click;
            StartDoneButton.IsEnabled = false;
            BusyCancelIndicator.IsBusy = true;
            LabelWarning1.Content = "";
            LabelWarning2.Content = "";

            string finalMessage = await BeginUpdateDarkClassificationForAllSelectedImagesAsync().ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;

            // Hide various buttons, the primary panel, and the image
            StartDoneButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            Primary2.Visibility = Visibility.Collapsed;
            Primary3.Visibility = Visibility.Collapsed;
            Primary4.Visibility = Visibility.Collapsed;
            Image.Visibility = Visibility.Collapsed;
            FinalMessage.Visibility = Visibility.Visible;
            FinalMessage.Text = finalMessage;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            // We return false if the database was not altered, i.e., if this was all a no-op
            DialogResult = IsAnyDataUpdated;
        }

        // Cancel or Stop - exit the dialog
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }
        #endregion

        #region Class ImageSettings
        /// <summary>
        /// ImageSettings defines aspects of the image as set and used only by DarkImagesThreshold
        /// </summary>
        protected class ImageSettingsForDarkClassifications
        {
            public WriteableBitmap Bitmap { get; set; }
            public double DarkPixelRatioFound { get; set; }
            public string FileName { get; set; }
            public bool IsColor { get; set; }
            public bool NewDarkClassification { get; set; }
            public bool OldDarkClassification { get; set; }

            public bool Dark { get; set; }
            public ImageSettingsForDarkClassifications(ImageRow image, string dataLabel)
            {
                // Check the arguments for null 
                ThrowIf.IsNullArgument(image, nameof(image));

                Bitmap = null;
                DarkPixelRatioFound = 0;
                FileName = image.File;
                IsColor = false;
                OldDarkClassification = !string.IsNullOrEmpty(dataLabel) && image.GetValueDatabaseString(dataLabel) == BooleanValue.True;
                NewDarkClassification = false;
            }
        }
        #endregion
    }
}
