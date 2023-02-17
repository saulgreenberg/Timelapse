using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Extensions;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class DarkImagesThreshold : IDisposable
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private readonly TimelapseUserRegistrySettings state;

        private readonly bool updateDarkClassificationForAllSelectedImagesStarted;
        private readonly DispatcherTimer dispatcherTimer = new DispatcherTimer();
        private readonly FileTableEnumerator imageEnumerator;

        private const int MinimumRectangleWidth = 12;

        private WriteableBitmap bitmap;
        private int darkPixelThreshold;
        private double darkPixelRatio;
        private double darkPixelRatioFound;

        private bool disposed;

        private bool isColor;

        private bool displatcherTimerIsPlaying;

        private readonly Dictionary<string, string> FlagLabelsDataLabels = new Dictionary<string, string>();
        private string ChosenFlagLabel = String.Empty;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;
        #endregion

        #region Initialization
        public DarkImagesThreshold(TimelapseWindow owner, FileDatabase fileDatabase, TimelapseUserRegistrySettings state, int currentImageIndex) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(state, nameof(state));

            this.InitializeComponent();
            this.Owner = owner;
            this.fileDatabase = fileDatabase;
            this.state = state;
            this.imageEnumerator = new FileTableEnumerator(fileDatabase);
            this.imageEnumerator.TryMoveToFile(currentImageIndex);

            this.darkPixelThreshold = state.DarkPixelThreshold;
            this.darkPixelRatio = state.DarkPixelRatioThreshold;
            this.darkPixelRatioFound = 0;
            this.isColor = false;
            this.updateDarkClassificationForAllSelectedImagesStarted = false;

            dispatcherTimer.Tick += this.DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);

            this.disposed = false;
        }

        // Display the image and associated details in the UI
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);

            this.DarkThreshold.Value = this.state.DarkPixelThreshold;
            this.DarkThreshold.ValueChanged += this.DarkThresholdSlider_ValueChanged;

            this.ScrollImages.Minimum = 0;
            this.ScrollImages.Maximum = this.fileDatabase.CountAllCurrentlySelectedFiles - 1;
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;

            // Collect all the flag controls in a dictionary by Label,DataLabel (DeleteFlag would not be collected)
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Flag)
                {
                    if (false == this.FlagLabelsDataLabels.ContainsKey(control.Label))
                    {
                        this.FlagLabelsDataLabels.Add(control.Label, control.DataLabel);
                    }
                }
            }
            // Populate the combobox with the labels of the available flag controls
            this.CBPopulateFlagField.ItemsSource = this.FlagLabelsDataLabels.Keys.ToList();

            // Gie the user some instructions depending on whether any flage are available
            if (this.FlagLabelsDataLabels.Count == 0)
            {
                this.SelectAFlagField.Content = "No flag fields available for populating dark classifications! Create one in the template.";
                this.CBPopulateFlagField.Visibility = Visibility.Collapsed;
                this.LabelWarning1.Content = "No flag fields are available for populating dark classifications.";
                this.LabelWarning2.Content = "Create one in the template.";
            }
            else
            {
                this.LabelWarning1.Content = "";
                this.LabelWarning2.Content = "Select a Flag field to activate the Start button.";
            }

            this.SetPreviousNextPlayButtonStates();
            this.ScrollImages_ValueChanged(null, null);
            this.ScrollImages.ValueChanged += this.ScrollImages_ValueChanged;
            this.Focus();               // necessary for the left/right arrow keys to work.
        }
        #endregion

        #region Closing and Disposing
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested && this.IsAnyDataUpdated;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.imageEnumerator != null)
                {
                    this.imageEnumerator.Dispose();
                }
            }

            this.disposed = true;
        }
        #endregion

        #region UI Updating
        public void Repaint()
        {
            // Color the bar to show the current color given the dark color threshold
            byte greyColor = (byte)Math.Round(255 - (double)255 * this.darkPixelThreshold);
            Brush brush = new SolidColorBrush(Color.FromArgb(255, greyColor, greyColor, greyColor));
            this.RectDarkPixelRatioFound.Fill = brush;
            this.lblGreyColorThreshold.Content = (greyColor + 1).ToString();

            // Size the bar to show how many pixels in the current image are at least as dark as that color
            if (this.isColor)
            {
                // color image
                this.RectDarkPixelRatioFound.Width = MinimumRectangleWidth;
            }
            else
            {
                this.RectDarkPixelRatioFound.Width = this.FeedbackCanvas.ActualWidth * this.darkPixelRatioFound;
                if (this.RectDarkPixelRatioFound.Width < MinimumRectangleWidth)
                {
                    this.RectDarkPixelRatioFound.Width = MinimumRectangleWidth; // Just so something is always visible
                }
            }
            this.RectDarkPixelRatioFound.Height = this.FeedbackCanvas.ActualHeight;

            // Show the location of the %age threshold bar
            this.DarkPixelRatioThumb.Height = this.FeedbackCanvas.ActualHeight;
            this.DarkPixelRatioThumb.Width = MinimumRectangleWidth;
            Canvas.SetLeft(this.DarkPixelRatioThumb, (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth) * this.darkPixelRatio);

            this.UpdateLabels();
        }

        // Update all the labels to show the current old and new classification values
        private void UpdateLabels()
        {
            this.DarkPixelRatio.Content = $"{100 * this.darkPixelRatio,3:##0}%";
            this.RatioFound.Content = $"{100 * this.darkPixelRatioFound,3:##0}";

            // We don't want to update labels if the image is not valid 
            if (false == Boolean.TryParse(this.OriginalClassification.Content.ToString(), out _))
            {
                // We don't have a value, so just set it to blank
                this.OriginalClassification.Content = "----";
            }

            if (this.isColor)
            {
                // color image 
                this.ThresholdMessage.Text = "Color - therefore not dark";
                this.Percent.Visibility = Visibility.Collapsed;
                this.RatioFound.Visibility = Visibility.Collapsed;
                this.RatioFound.Content = String.Empty;
            }
            else
            {
                this.ThresholdMessage.Text = "of the pixels are darker than the threshold";
                this.RatioFound.Visibility = Visibility.Visible;
                this.Percent.Visibility = Visibility.Visible;
            }

            if (this.isColor)
            {
                this.NewClassification.Content = Constant.BooleanValue.False;       // Color image
            }
            else if (this.darkPixelRatio <= this.darkPixelRatioFound)
            {
                this.NewClassification.Content = Constant.BooleanValue.True;  // Dark grey scale image
            }
            else
            {
                this.NewClassification.Content = Constant.BooleanValue.False;   // Light grey scale image
            }
        }

        // Utility routine for calling a typical sequence of UI update actions
        private void DisplayImageAndDetails()
        {
            if (this.imageEnumerator.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(this.imageEnumerator.Current));
                return;
            }
            this.bitmap = this.imageEnumerator.Current.LoadBitmap(this.fileDatabase.FolderPath, out _).AsWriteable();
            this.Image.Source = this.bitmap;
            this.FileName.Content = this.imageEnumerator.Current.File;
            this.FileName.ToolTip = this.imageEnumerator.Current.File;

            if (string.IsNullOrEmpty(this.ChosenFlagLabel) ||
                String.IsNullOrWhiteSpace(this.imageEnumerator?.Current?.GetValueDatabaseString(this.FlagLabelsDataLabels[this.ChosenFlagLabel])))
            {
                this.OriginalClassification.Content = String.Empty;
            }
            else
            {
                this.OriginalClassification.Content = this.imageEnumerator.Current.GetValueDatabaseString(this.FlagLabelsDataLabels[this.ChosenFlagLabel]);
            }
            this.RecalculateDarkClassificationForCurrentImage();
            this.Repaint();
        }
        #endregion

        #region Do the actual updating of image quality
        /// <summary>
        /// Redo image quality calculations with current thresholds and return the ratio of pixels at least as dark as the threshold for the current image.
        /// Does not update the database.
        /// </summary>
        private void RecalculateDarkClassificationForCurrentImage()
        {
            this.bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
        }

        /// <summary>
        /// Redo image quality calculations with current thresholds for all images selected.  Updates the database.
        /// </summary>
        private async Task<string> BeginUpdateDarkClassificationForAllSelectedImagesAsync()
        {
            return await Task.Run(() =>
            {
                // The selected files to check
                List<ImageRow> selectedFiles = this.fileDatabase.FileTable.ToList();
                List<ColumnTuplesWithWhere> filesToUpdate = new List<ColumnTuplesWithWhere>();
                int fileIndex = 0;
                int selectedFilesCount = (selectedFiles == null) ? 0 : selectedFiles.Count;
                string dataLabel = string.IsNullOrEmpty(this.ChosenFlagLabel)
                                    ? String.Empty
                                    : this.FlagLabelsDataLabels[this.ChosenFlagLabel];
                foreach (ImageRow file in selectedFiles)
                {
                    if (Token.IsCancellationRequested)
                    {
                        // A cancel was requested. Clear all pending changes and abort
                        filesToUpdate.Clear();
                        return "Cancelled - no changes made.";
                    }

                    ImageSettingsForDarkClassifications imageQuality = new ImageSettingsForDarkClassifications(file, dataLabel);
                    try
                    {
                        // Get the image, and add it to the list of images to be updated if the imageQuality has changed
                        // Note that if the image can't be created, we will just go to the catch.
                        // We also use a TransientLoading, as the estimate of darkness will work just fine on that
                        imageQuality.Bitmap = file.LoadBitmap(this.fileDatabase.FolderPath, ImageDisplayIntentEnum.Ephemeral, out bool isCorruptOrMissing).AsWriteable();
                        if (isCorruptOrMissing)
                        {
                            // If we can't read the image, just set its darkness to false
                            imageQuality.NewDarkClassification = false;
                        }
                        else
                        {
                            // Set the image quality. Note that videos are always classified as false.
                            imageQuality.NewDarkClassification = !file.IsVideo && imageQuality.Bitmap.IsDark(this.darkPixelThreshold, this.darkPixelRatio, out this.darkPixelRatioFound, out this.isColor);
                        }
                        imageQuality.IsColor = this.isColor;
                        imageQuality.DarkPixelRatioFound = this.darkPixelRatioFound;

                        string newDarkClassificationAsString = imageQuality.NewDarkClassification
                        ? Constant.BooleanValue.True
                        : Constant.BooleanValue.False;

                        if (imageQuality.OldDarkClassification != imageQuality.NewDarkClassification)
                        {
                            file.SetValueFromDatabaseString(dataLabel, newDarkClassificationAsString);
                            filesToUpdate.Add(new ColumnTuplesWithWhere(new List<ColumnTuple> { new ColumnTuple(dataLabel, newDarkClassificationAsString) }, file.ID));
                        }
                    }
                    catch (Exception exception)
                    {
                        // file isn't there?
                        imageQuality.NewDarkClassification = false;
                        Debug.Fail("Exception while assessing image quality.", exception.ToString());
                    }

                    fileIndex++;
                    if (this.ReadyToRefresh())
                    {
                        int percentDone = (int)(100.0 * fileIndex / selectedFilesCount);
                        this.Progress.Report(new ProgressBarArguments(percentDone,
                            $"{fileIndex}/{selectedFilesCount} images. Processing {file.File}", true, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                }

                // Update the database to reflect the changed values
                // Tracks whether any changes to the data or database are made
                this.Progress.Report(new ProgressBarArguments(100,
                    $"Writing changes for {filesToUpdate.Count} files. Please wait...", false, true));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                this.IsAnyDataUpdated = true;
                this.fileDatabase.UpdateFiles(filesToUpdate);
                return filesToUpdate.Count > 0
                ? $"{selectedFilesCount} files examined, with {filesToUpdate.Count} updated to reflect changes."
                : $"{selectedFilesCount} files examined. None were updated as nothing has changed.";
            }, this.Token).ConfigureAwait(true);
        }
        #endregion

        #region UI Callback: Set the populate flag field 
        private void CBPopulateFlagField_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cmb)
            {
                foreach (ControlRow control in this.fileDatabase.Controls)
                {
                    if (control.Label == (string)cmb.SelectedItem)
                    {
                        this.ChosenFlagLabel = control.Label;
                    }
                }
                bool success = false == string.IsNullOrEmpty(this.ChosenFlagLabel);
                this.StartDoneButton.IsEnabled = success;
                if (success)
                {
                    this.LabelWarning1.Content = "Press Start to populate Dark classification data";
                    this.LabelWarning2.Content = $"in the {this.ChosenFlagLabel} data field";
                }
                else
                {
                    // This should never happen, as the user can only select valid flags
                    this.LabelWarning1.Content = "";
                    this.LabelWarning2.Content = "Select a Flag field to activate the Start button.";
                }
                this.DisplayImageAndDetails();
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
            resetButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            resetButton.ContextMenu.IsOpen = true;
        }

        // Reset the thresholds to their initial settings
        private void MenuItemResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            this.darkPixelRatio = this.state.DarkPixelRatioThreshold;
            this.darkPixelThreshold = this.state.DarkPixelThreshold;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            this.DarkThreshold.Value = this.state.DarkPixelThreshold;
            this.RecalculateDarkClassificationForCurrentImage();
            this.Repaint();
        }

        // Reset the thresholds to the Default settings
        private void MenuItemResetDefault_Click(object sender, RoutedEventArgs e)
        {
            // Move the thumb to correspond to the original value
            this.darkPixelRatio = Constant.ImageValues.DarkPixelRatioThresholdDefault;
            Canvas.SetLeft(this.DarkPixelRatioThumb, this.darkPixelRatio * (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth));

            // Move the slider to its original position
            this.DarkThreshold.Value = Constant.ImageValues.DarkPixelThresholdDefault;
            this.RecalculateDarkClassificationForCurrentImage();
            this.Repaint();
        }
        #endregion

        #region UI Callbacks - setting thresholds for what is dark

        // Set a new value for the dark pixel threshold and update the UI
        private void DarkThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.DarkPixelRatio == null)
            {
                return;
            }
            this.darkPixelThreshold = Convert.ToInt32(e.NewValue);

            this.RecalculateDarkClassificationForCurrentImage();
            this.Repaint();
        }

        // Set a new value for the Dark Pixel Ratio and update the UI
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (e.Source is UIElement thumb == false)
            {
                TracePrint.NullException(nameof(thumb));
                return;
            }

            if ((Canvas.GetLeft(thumb) + e.HorizontalChange) >= (this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth))
            {
                Canvas.SetLeft(thumb, this.FeedbackCanvas.ActualWidth - this.DarkPixelRatioThumb.ActualWidth);
                this.darkPixelRatio = 1;
            }
            else if ((Canvas.GetLeft(thumb) + e.HorizontalChange) <= 0)
            {
                Canvas.SetLeft(thumb, 0);
                this.darkPixelRatio = 0;
            }
            else
            {
                Canvas.SetLeft(thumb, Canvas.GetLeft(thumb) + e.HorizontalChange);
                this.darkPixelRatio = (Canvas.GetLeft(thumb) + e.HorizontalChange) / this.FeedbackCanvas.ActualWidth;
            }
            if (this.DarkPixelRatio == null)
            {
                return;
            }

            this.RecalculateDarkClassificationForCurrentImage();
            // We don't repaint, as this will screw up the thumb dragging. So just update the labels instead.
            this.UpdateLabels();
        }
        #endregion

        #region UI callbacks - Navigating through images
        // Scroll to another image
        private void ScrollImages_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.updateDarkClassificationForAllSelectedImagesStarted)
            {
                return;
            }

            this.imageEnumerator.TryMoveToFile(Convert.ToInt32(this.ScrollImages.Value));
            this.DisplayImageAndDetails();
            this.SetPreviousNextPlayButtonStates();
        }

        // If its an arrow key navigate left/right image 
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!this.ReadyToRefresh())
            {
                // only update every now and then, as otherwise it stalls when the arrow key is held down
                return;
            }
            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            switch (e.Key)
            {
                case Key.Right:             // next file
                    this.NextButton_Click(null, null);
                    break;
                case Key.Left:              // previous file
                    this.PreviousButton_Click(null, null);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        // Navigate to the previous image
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            this.imageEnumerator.MovePrevious();
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;
        }

        // Navigate to the next image
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.imageEnumerator.MoveNext();
            this.ScrollImages.Value = this.imageEnumerator.CurrentRow;
        }

        // Helper for the above, where previous/next buttons are enabled/disabled as needed
        private void SetPreviousNextPlayButtonStates()
        {
            this.PreviousFile.IsEnabled = this.imageEnumerator.CurrentRow != 0;
            this.NextFile.IsEnabled = (this.imageEnumerator.CurrentRow < this.fileDatabase.CountAllCurrentlySelectedFiles - 1);
            if (NextFile.IsEnabled == false)
            {
                // We are at the end, so stop playback and disable the play button
                this.PlayButtonSetState(false, false);
            }
            else if (this.displatcherTimerIsPlaying == false && NextFile.IsEnabled)
            {
                // We are at the end, so stop playback and disable the play button
                this.PlayButtonSetState(false, true);
            }
        }

        // Show the next file after every tick
        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            this.NextButton_Click(null, null);
        }

        // Play the images automatically
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            this.displatcherTimerIsPlaying = !this.displatcherTimerIsPlaying;
            PlayButtonSetState(this.displatcherTimerIsPlaying, true);
        }

        // Set the play/pause/enable state of the play button
        private void PlayButtonSetState(bool play, bool enabled)
        {
            if (play)
            {
                // Play
                this.dispatcherTimer.Start();
                this.displatcherTimerIsPlaying = true;
                // Show Pause character
                this.PlayFile.Content = "\u23F8";
            }
            else
            {
                // Pause
                this.dispatcherTimer.Stop();
                this.displatcherTimerIsPlaying = false;
                // Start the playback
                // Show Fast forward character
                this.PlayFile.Content = "\u23E9";
            }
            this.PlayFile.IsEnabled = enabled;
        }
        #endregion

        #region Button callbacks
        // Update the database if the OK button is clicked
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Update state variables to the current settings
            this.state.DarkPixelThreshold = this.darkPixelThreshold;
            this.state.DarkPixelRatioThreshold = this.darkPixelRatio;

            // update the UI
            this.CancelButton.IsEnabled = false;
            this.CancelButton.Visibility = Visibility.Hidden;
            this.StartDoneButton.Content = "_Done";
            this.StartDoneButton.Click -= this.StartButton_Click;
            this.StartDoneButton.Click += this.DoneButton_Click;
            this.StartDoneButton.IsEnabled = false;
            this.BusyCancelIndicator.IsBusy = true;
            this.LabelWarning1.Content = "";
            this.LabelWarning2.Content = "";

            string finalMessage = await this.BeginUpdateDarkClassificationForAllSelectedImagesAsync().ConfigureAwait(true);
            this.BusyCancelIndicator.IsBusy = false;

            // Hide various buttons, the primary panel, and the image
            this.StartDoneButton.IsEnabled = true;
            this.CancelButton.IsEnabled = false;
            this.Primary2.Visibility = Visibility.Collapsed;
            this.Primary3.Visibility = Visibility.Collapsed;
            this.Primary4.Visibility = Visibility.Collapsed;
            this.Image.Visibility = Visibility.Collapsed;
            this.FinalMessage.Visibility = Visibility.Visible;
            this.FinalMessage.Text = finalMessage;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            // We return false if the database was not altered, i.e., if this was all a no-op
            this.DialogResult = this.IsAnyDataUpdated;
        }

        // Cancel or Stop - exit the dialog
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
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

                this.Bitmap = null;
                this.DarkPixelRatioFound = 0;
                this.FileName = image.File;
                this.IsColor = false;
                this.OldDarkClassification = !string.IsNullOrEmpty(dataLabel) && image.GetValueDatabaseString(dataLabel) == Constant.BooleanValue.True;
                this.NewDarkClassification = false;
            }
        }
        #endregion
    }
}
