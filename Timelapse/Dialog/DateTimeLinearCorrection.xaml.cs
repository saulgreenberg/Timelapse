﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataCommon;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DialogDateTimeLinearCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of a file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// </summary>
    public partial class DateTimeLinearCorrection
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;

        private DateTime latestImageDateTime;
        private DateTime earliestImageDateTime;

        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;
        #endregion

        #region Constructor, Loaded, Closing, AutoGenerated
        // Create the interface
        public DateTimeLinearCorrection(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            InitializeComponent();
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Set up the initial UI and values
            latestImageDateTime = DateTime.MinValue;
            earliestImageDateTime = DateTime.MaxValue;

            // Search the images for the two images with the earliest and latest data/time date 

            if (fileDatabase.FileTable.RowCount == 0)
            {
                // Shouldn't happen, as the menu should be disabled when there are no images
                TracePrint.UnexpectedException(nameof(fileDatabase.FileTable.RowCount) + " should have had at least one element");
                return;
            }

            ImageRow latestImageRow = null;
            ImageRow earliestImageRow = null;
            foreach (ImageRow image in fileDatabase.FileTable)
            {
                DateTime currentImageDateTime = image.DateTime;

                // If the current image's date is later, then it is a candidate latest image  
                if (currentImageDateTime >= latestImageDateTime)
                {
                    latestImageRow = image;
                    latestImageDateTime = currentImageDateTime;
                }

                // If the current image's date is earlier, then it is a candidate earliest image  
                if (currentImageDateTime <= earliestImageDateTime)
                {
                    earliestImageRow = image;
                    earliestImageDateTime = currentImageDateTime;
                }
            }

            // At this point, we should have succeeded getting the oldest and newest data/time
            if (earliestImageRow == null || latestImageRow == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(earliestImageRow) + "or " + nameof(latestImageRow));
                return;
            }
            // ConfigureFormatForDateTimeCustom the earliest date (in datetime picker) and its image
            earliestImageName.Content = earliestImageRow.File;
            earliestImageDate.Content = DateTimeHandler.ToStringDisplayDateTime(earliestImageDateTime);
            imageEarliest.Source = earliestImageRow.LoadBitmap(fileDatabase.FolderPath, out _);

            // ConfigureFormatForDateTimeCustom the latest date (in datetime picker) and its image
            latestImageName.Content = latestImageRow.File;
            CreateControls.Configure(dateTimePickerLatestDateTime, DateTimeFormatEnum.DateAndTime, latestImageDateTime);
            dateTimePickerLatestDateTime.ValueChanged += DateTimePicker_ValueChanged;
            imageLatest.Source = latestImageRow.LoadBitmap(fileDatabase.FolderPath, out _);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }

        // Label and size the datagrid column headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            FeedbackGrid.Columns[0].Header = "File name (only for files whose date was changed)";
            FeedbackGrid.Columns[0].Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            FeedbackGrid.Columns[1].Header = "Old date  \x2192  New date \x2192 Delta";
            FeedbackGrid.Columns[1].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        }
        #endregion

        #region Calculate times and Update files
        // Set up all the Linear Corrections as an asynchronous task which updates the progress bar as needed
        private async Task<ObservableCollection<DateTimeFeedbackTuple>> TaskLinearCorrectionAsync(TimeSpan newestImageAdjustment, TimeSpan intervalFromOldestToNewestImage)
        {
            // A side effect of running this task is that the FileTable will be updated, which means that,
            // at the very least, the calling function will need to run FilesSelectAndShow to either
            // reload the FileTable with the updated data, or to reset the FileTable back to its original form
            // if the operation was cancelled.
            IsAnyDataUpdated = true;

            // Reread the Date/Times from each file 
            return await Task.Run(() =>
            {
                // Collects feedback to display in a datagrid after the operation is done
                ObservableCollection<DateTimeFeedbackTuple> feedbackRows = new ObservableCollection<DateTimeFeedbackTuple>();

                DatabaseUpdateFileDates(Progress, intervalFromOldestToNewestImage, newestImageAdjustment, feedbackRows);

                // Provide feedback if the operation was cancelled during the database update
                if (Token.IsCancellationRequested)
                {
                    feedbackRows.Clear();
                    feedbackRows.Add(new DateTimeFeedbackTuple("Cancelled", "No changes were made"));
                    IsAnyDataUpdated = false;
                    return feedbackRows;
                }
                return feedbackRows;
            }, Token).ConfigureAwait(continueOnCapturedContext: true); // Set to true as we need to continue in the UI context
        }

        private void DatabaseUpdateFileDates(IProgress<ProgressBarArguments> progress, TimeSpan intervalFromOldestToNewestImage, TimeSpan newestImageAdjustment, ObservableCollection<DateTimeFeedbackTuple> feedbackRows)
        {
            if (intervalFromOldestToNewestImage == TimeSpan.Zero)
            {
                fileDatabase.UpdateAdjustedFileTimes(newestImageAdjustment);
            }
            else
            {
                // Note that this passes a function which is invoked by the fileDatabase method. 
                // This not only calculates the new times, but updates the progress bar as the fileDatabase method iterates through the files.
                fileDatabase.UpdateAdjustedFileTimes(
                   (fileName, fileIndex, count, imageDateTime) =>
                   {
                       double imagePositionInInterval = (imageDateTime - earliestImageDateTime).Ticks / (double)intervalFromOldestToNewestImage.Ticks;
                       Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001),
                           $"Interval position {imagePositionInInterval} is not between 0.0 and 1.0.");
                       TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks)); // Used to have a  .5 increment, I think to force rounding upwards                                                                                                        // TimeSpan.Duration means we do these checks on the absolute value (positive) of the Timespan, as slow clocks will have negative adjustments.
                       Debug.Assert((TimeSpan.Zero <= adjustment.Duration()) && (adjustment.Duration() <= newestImageAdjustment.Duration()),
                           $"Expected adjustment {adjustment} to be within [{TimeSpan.Zero} {newestImageAdjustment}].");

                       if (adjustment.Duration() >= TimeSpan.FromSeconds(1))
                       {
                           // We only add to the feedback row if the change duration is > 1 second, as otherwise we don't change it.
                           string oldDT = DateTimeHandler.ToStringDisplayDateTime(imageDateTime);
                           string newDT = DateTimeHandler.ToStringDisplayDateTime(imageDateTime + adjustment);
                           feedbackRows.Add(new DateTimeFeedbackTuple(fileName, oldDT + " \x2192 " + newDT + " \x2192 " + PrettyPrintTimeAdjustment(adjustment)));
                       }

                       // Update the progress bar every time interval to indicate what file we are working on
                       TimeSpan intervalFromLastRefresh = DateTime.Now - lastRefreshDateTime;
                       if (intervalFromLastRefresh > ThrottleValues.ProgressBarRefreshInterval)
                       {
                           int percentDone = Convert.ToInt32(fileIndex / Convert.ToDouble(count) * 100.0);
                           progress.Report(new ProgressBarArguments(percentDone,
                               $"Pass 1: Calculating new date/times for {fileIndex} / {count} files", true, false));
                           Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                           lastRefreshDateTime = DateTime.Now;
                       }

                       if (fileIndex >= count)
                       {
                           // After all files are processed, the next step would be updating the database. Disable the cancel button too.
                           // This really should be somehow signalled from the invoking method (ideally ExecuteNonQueryWrappedInBeginEnd every update interval), but this is a reasonable workaround.
                           progress.Report(new ProgressBarArguments(100,
                               $"Pass 2: Updating {feedbackRows.Count} files. Please wait...", false, true));
                           Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                       }
                       return imageDateTime + adjustment; // Returns the new time
                   },
                   0,
                   fileDatabase.CountAllCurrentlySelectedFiles - 1,
                   Token);
            }
        }
        #endregion

        #region Button callbacks
        // Set up the UI and invoke the linear correction 
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            // A few checks just to make sure we actually have something to do...
            if (dateTimePickerLatestDateTime.Value.HasValue == false)
            {
                // We don't have a valid date, so nothing really to do.
                // This should not happen
                System.Windows.MessageBox.Show("Could not change the date/time, as it date is not in a format recognized by Timelapse.");
                return;
            }

            // ConfigureFormatForDateTimeCustom the UI's initial state
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            StartDoneButton.Content = "_Done";
            StartDoneButton.Click -= Start_Click;
            StartDoneButton.Click += DoneButton_Click;
            StartDoneButton.IsEnabled = false;
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            TimeSpan newestImageAdjustment = dateTimePickerLatestDateTime.Value.Value - latestImageDateTime;
            TimeSpan intervalFromOldestToNewestImage = latestImageDateTime - earliestImageDateTime;
            if (newestImageAdjustment == TimeSpan.Zero)
            {
                // nothing to do
                DialogResult = false;
                return;
            }

            // This call does all the actual updating...
            ObservableCollection<DateTimeFeedbackTuple> feedbackRows = await TaskLinearCorrectionAsync(newestImageAdjustment, intervalFromOldestToNewestImage).ConfigureAwait(true);

            // Hide the busy indicator and update the UI, e.g., to show which files have changed dates
            // Provide summary feedback 
            if (IsAnyDataUpdated && Token.IsCancellationRequested == false)
            {
                string message =
                    $"Updated {feedbackRows.Count}/{fileDatabase.CountAllCurrentlySelectedFiles} files whose dates have changed.";
                feedbackRows.Insert(0, (new DateTimeFeedbackTuple("---", message)));
            }

            BusyCancelIndicator.IsBusy = false;
            PrimaryPanel.Visibility = Visibility.Collapsed;
            FeedbackPanel.Visibility = Visibility.Visible;
            FeedbackGrid.ItemsSource = feedbackRows;
            StartDoneButton.IsEnabled = true;
            WindowCloseButtonIsEnabled(true);
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            // We return false if the database was not altered, i.e., if this was all a no-op
            DialogResult = IsAnyDataUpdated;
        }

        // Cancel - do nothing
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion

        #region DateTimePicker callbacks
        private void DateTimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Because of the bug in the DateTimePicker, we have to get the changed value from the string
            // as DateTimePicker.Value.Value can have the old date rather than the new one.
            if (DateTimeHandler.TryParseDisplayDateTime(dateTimePickerLatestDateTime.Text, out DateTime newDateTime) == false)
            {
                // If we can't parse the date,  do nothing.
                // Debug.Print("DateTimeLinearCorrection|ValueChanged: Could not parse the date:" + this.dateTimePickerLatestDateTime.Text);
                return;
            }

            // Inform the user if the date picker date goes below the earlest time,  
            if (dateTimePickerLatestDateTime.Value != null && dateTimePickerLatestDateTime.Value.Value <= earliestImageDateTime)
            {
                Dialogs.DateTimeNewTimeShouldBeLaterThanEarlierTimeDialog(this);
            }

            // Enable the Ok button only if the latest time has actually changed from its original version
            TimeSpan newestImageAdjustment = newDateTime - latestImageDateTime;
            StartDoneButton.IsEnabled = newestImageAdjustment != TimeSpan.Zero;
        }

        // Mitigates a bug where ValueChanged is not triggered when the date/time is changed
        private void DateTimePickerLatestDateTime_MouseLeave(object sender, MouseEventArgs e)
        {
            DateTimePicker_ValueChanged(null, null);
        }
        #endregion

        #region Utility methods
        // Given the time adjustment to the date, generate a pretty-printed string taht we can use in our feedback
        private static string PrettyPrintTimeAdjustment(TimeSpan adjustment)
        {
            string sign = (adjustment < TimeSpan.Zero) ? "-" : "+";

            // Pretty print the adjustment time, depending upon how many day(s) were included 
            string format;
            if (adjustment.Days == 0)
            {
                format = "{0:s}{1:D2}:{2:D2}:{3:D2}"; // Don't show the days field
            }
            else
            {
                // includes singular or plural form of days
                format = (adjustment.Duration().Days == 1) ? "{0:s}{1:D2}:{2:D2}:{3:D2} {0:s} {4:D} day" : "{0:s}{1:D2}:{2:D2}:{3:D2} {0:s} {4:D} days";
            }
            return string.Format(format, sign, adjustment.Duration().Hours, adjustment.Duration().Minutes, adjustment.Duration().Seconds, adjustment.Duration().Days);
        }
        #endregion
    }
}