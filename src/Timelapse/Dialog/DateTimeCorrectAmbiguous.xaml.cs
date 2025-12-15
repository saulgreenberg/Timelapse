using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Detects and displays ambiguous dates, and allows the user to select which ones (if any) should be swapped.
    /// </summary>
    public partial class DateTimeCorrectAmbiguous
    {
        #region Private Varaibles
        // Remember passed in arguments
        private readonly FileDatabase fileDatabase;

        private readonly List<AmbiguousDateRange> ambiguousDatesList; // Will contain a list of all initial images containing ambiguous dates and their state

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;
        #endregion

        #region Constructor and Loaded
        public DateTimeCorrectAmbiguous(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));

            InitializeComponent();
            this.fileDatabase = fileDatabase;
            ambiguousDatesList = [];
            DateChangeFeedback.RootPathToImages = fileDatabase.RootPathToImages;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            Mouse.OverrideCursor = Cursors.Wait;


            // Find and display the ambiguous dates in the current selected set
            // This is a fast operation, so we don't bother to show a progress bar here
            if (FindAllAmbiguousDatesInSelectedImageSet())
            {
                PopulateDateChangeFeedback();
                StartDoneButton.IsEnabled = DateChangeFeedback.AreAnySelected();
            }
            else
            {
                // Since there are no ambiguous dates, we are pretty well done!
                DoneMessagePanel.Visibility = Visibility.Visible;
                StartDoneButton.Visibility = Visibility.Collapsed;
                CancelButton.Content = "Done";
                Height = MinHeight;
            }
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Populate the Feedback Panel to show date changes 
        private void PopulateDateChangeFeedback()
        {
            DateChangeFeedback.ShowDifferenceColumn = false;
            FeedbackPanel.Visibility = Visibility.Visible;
            foreach (AmbiguousDateRange ambiguousDateRange in ambiguousDatesList)
            {
                ImageRow image = fileDatabase.FileTable[ambiguousDateRange.StartIndex];
                DateTimeHandler.TrySwapDayMonth(image.DateTime, out DateTime swappedDate);
                string newDate = DateTimeHandler.ToStringDisplayDatePortion(swappedDate.Date);
                string numFilesWithThatDate = ambiguousDateRange.Count.ToString();
                DateChangeFeedback.AddFeedbackRow(image.File, DateTimeHandler.ToStringDisplayDatePortion(image.DateTimeIncorporatingOffsetPLAINVERSION.Date), newDate, numFilesWithThatDate, image, ambiguousDateRange);
            }
        }
        #endregion

        #region Create the ambiguous date list
        // Create a list of all initial images containing ambiguous dates.
        // This includes calculating the start and end rows of all images matching an ambiguous date
        private bool FindAllAmbiguousDatesInSelectedImageSet()
        {
            int start = SearchForNextAmbiguousDateInSelectedImageSet(0);
            while (start != -1)
            {
                int end = GetLastImageOnSameDay(start, out int count);
                ambiguousDatesList.Add(new(start, end, count, false));
                start = SearchForNextAmbiguousDateInSelectedImageSet(end + 1);
            }
            return (ambiguousDatesList.Count > 0);
        }

        // Starting from the index, navigate successive image rows until an ambiguous date is found
        // If it can't find an ambiguous date, it will return -1.
        private int SearchForNextAmbiguousDateInSelectedImageSet(int startIndex)
        {
            for (int index = startIndex; index < fileDatabase.CountAllCurrentlySelectedFiles; index++)
            {
                ImageRow image = fileDatabase.FileTable[index];
                DateTime imageDateTime = image.DateTime;
                if (imageDateTime.Day <= Time.MonthsInYear)
                {
                    return index; // If the date is ambiguous, return the row index. 
                }
            }
            return -1; // -1 means all dates are unambiguous
        }

        // Given a starting index, find its date and then go through the successive images until the date differs.
        // Return the final image that is dated the same date as this image
        // Assumption is that the index is valid and is pointing to an image with a valid date.
        // However, it still tests for problems and returns -1 if there was a problem.
        private int GetLastImageOnSameDay(int startIndex, out int count)
        {
            count = 1; // We start at 1 as we have at least one image (the starting image) with this date

            // Check if index is in range
            if (startIndex >= fileDatabase.CountAllCurrentlySelectedFiles || startIndex < 0)
            {
                return -1;   // The index is out of range.
            }

            // Parse the provided starting date. Return -1 if it cannot.
            ImageRow image = fileDatabase.FileTable[startIndex];
            DateTime desiredDateTime = image.DateTime;

            int lastMatchingDate = startIndex;
            for (int index = startIndex + 1; index < fileDatabase.CountAllCurrentlySelectedFiles; index++)
            {
                // Parse the date for the given row.
                image = fileDatabase.FileTable[index];
                DateTime imageDateTime = image.DateTime;

                if (desiredDateTime.Date == imageDateTime.Date)
                {
                    lastMatchingDate = index;
                    count++;
                    continue;
                }
                return lastMatchingDate; // This statement is reached only when the date differs, which means the last valid image is the one before it.
            }
            return lastMatchingDate; // if we got here, it means that we arrived at the end of the records
        }
        #endregion

        #region Update files with the new date time
        // Actually update the dates as needed
        private async Task<int> ApplyDateTimeChangesAsync()
        {
            return await Task.Run(() =>
            {
                int totalFileCount = 0;
                int count = ambiguousDatesList.Count;
                int dateIndex = 0;
                foreach (AmbiguousDateRange ambDate in ambiguousDatesList)
                {
                    // Provide progress bar feedback
                    if (ambDate.SwapDates)
                    {
                        IsAnyDataUpdated = true;
                        fileDatabase.UpdateExchangeDayAndMonthInFileDates(ambDate.StartIndex, ambDate.EndIndex);
                        totalFileCount += ambDate.Count;
                    }
                    // Provide feedback if the operation was cancelled during the database update
                    // Update the progress bar every time interval to indicate what file we are working on
                    if (ReadyToRefresh())
                    {
                        dateIndex++;
                        int percentDone = Convert.ToInt32(dateIndex / Convert.ToDouble(count) * 100.0);
                        Progress.Report(new(percentDone,
                            $"Swapping day with month for {dateIndex} / {count} ambiguous dates", false, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                    // The cancellation pattern is shown but is commented out. We don't do anything with the cancellation CancelToken, as we are actually updating the database at this point
                    // and don't want a partially done update.
                    //if (Token.IsCancellationRequested == true)
                    //{
                    //    return;
                    //}
                }
                return totalFileCount;
            }, Token).ConfigureAwait(continueOnCapturedContext: true); // Set to true as we need to continue in the UI context

        }
        #endregion

        #region Mouse button Callbacks
        private void DateChangeFeedback_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StartDoneButton.IsEnabled = DateChangeFeedback.AreAnySelected();
        }
        #endregion

        #region Button Callbackes
        // Select all / none of the checkboxes in the datechangedfeedback panel.
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            DateChangeFeedback.SelectAll(ButtonSelectAll.IsChecked == true);
            StartDoneButton.IsEnabled = DateChangeFeedback.AreAnySelected();
        }

        // When the start button is clicked,
        // - apply the date change
        // - change the UI so that the start button (and its event handler) becomes a 'Done' button,
        //   temporarily disable the window's close button, and show the progress bar.
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Start_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Start_ClickAsync()
        {
            // We have a valide new time that differs by at least one second.
            // ConfigureFormatForDateTimeCustom the UI's initial state
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            StartDoneButton.Content = "_Done";
            StartDoneButton.Click -= Start_Click;
            StartDoneButton.Click += Done_Click;
            StartDoneButton.IsEnabled = false;
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            int totalFileCount = await ApplyDateTimeChangesAsync().ConfigureAwait(true);

            // Update the UI final state
            BusyCancelIndicator.IsBusy = false;
            StartDoneButton.IsEnabled = true;
            WindowCloseButtonIsEnabled(true);
            // Show the final message
            if (totalFileCount > 0)
            {
                DoneMessagePanel.Content = "Dates for " + totalFileCount + " files were swapped";
            }
            else
            {
                DoneMessagePanel.Content = "Nothing changed as no dates were selected.";
            }
            DoneMessagePanel.Visibility = Visibility.Visible;
            FeedbackPanel.Visibility = Visibility.Collapsed;
            Height = MinHeight;
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {

            // We return true if the database was altered. Returning true will reset the FileTable, as a FileSelectAndShow will be done.
            // Kinda hacky as it expects a certain behaviour of the caller, but it works.
            DialogResult = IsAnyDataUpdated;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
