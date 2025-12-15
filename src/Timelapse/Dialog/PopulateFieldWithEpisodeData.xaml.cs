using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for PopulateFieldWithEpisodeData.xaml
    /// </summary>
    public partial class PopulateFieldWithEpisodeData
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private readonly Dictionary<string, string> dataLabelByLabel;
        private string dataFieldLabel = string.Empty;
        private double TotalImages;
        private double SingleCount;
        private double EpisodeCount;
        private double EpisodeNoSingletonsCount;

        // Tracks whether any changes to the data or database are made
        private bool IsAnyDataUpdated;
        private bool IncludeEpisodeID;
        private bool IncludeSequenceNumber;
        #endregion
        public PopulateFieldWithEpisodeData(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            dataLabelByLabel = [];
            this.fileDatabase = fileDatabase;
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Set up the initial UI and values
            RadioButtonIncludeBoth.IsChecked = true;
            IncludeEpisodeID = true;
            IncludeSequenceNumber = true;

            RadioButtonIncludeBoth.Checked += RadioButtonCheckChanged;
            RadioButtonIncludeEpisodeNumberOnly.Checked += RadioButtonCheckChanged;
            RadioButtonIncludeSequenceNumberOnly.Checked += RadioButtonCheckChanged;

            //RadioButtonIncludeEpisodeID.Unchecked += RadioButtonIncludeEpisodeIdCheckChanged;

            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (IsCondition.IsControlType_Note_MultiLine(control.Type))
                {
                    dataLabelByLabel.Add(control.Label, control.DataLabel);
                    ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }

            IncludeEpisodeID = RadioButtonIncludeBoth.IsChecked == true;

            // Show the current settings
            RunCurrentSettings.Text = String.Format(Episodes.Episodes.TimeThreshold.ToString("g"));
            ShowExampleFormat();
        }

        #region Closing and Disposing
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }

        #endregion

        #region Button callbacks
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
            // Update the UI before starting the operation,
            CancelButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            StartDoneButton.Content = "_Processing";
            StartDoneButton.Click -= Start_Click;
            StartDoneButton.Click += Done_Click;
            StartDoneButton.IsEnabled = false;
            BusyCancelIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            // This call does all the actual populating...
            bool isCompleted = await PopulateAsync().ConfigureAwait(true);

            // Update the UI to its final state
            StartDoneButton.IsEnabled = true;
            StartDoneButton.Content = "_Done";
            BusyCancelIndicator.IsBusy = false;
            WindowCloseButtonIsEnabled(true);
            TextBlockFeedbackLine1.Text = isCompleted
                ? $"Done, with {TotalImages} files processed."
                : "Operation cancelled.";
            TextBlockFeedbackLine2.Text = isCompleted
                ? $"Found {SingleCount} singleton{(Math.Abs(SingleCount - 1) < .0001 ? string.Empty : "s")}, and {EpisodeNoSingletonsCount} episode{(Math.Abs(EpisodeNoSingletonsCount - 1) < .0001 ? string.Empty : "s")}."
                : "No changes were made";
            PrimaryPanel.Visibility = Visibility.Collapsed;
            FeedbackPanel.Visibility = Visibility.Visible;
        }

        private async Task<bool> PopulateAsync()
        {
            return await Task.Run(() =>
            {
                TotalImages = fileDatabase.CountAllCurrentlySelectedFiles;
                List<ColumnTuplesWithWhere> imagesToUpdate = [];

                int imageIndex = 0;
                while (imageIndex < TotalImages)
                {
                    Episodes.Episodes.Reset();
                    Episodes.Episodes.EpisodeGetEpisodesInRange(fileDatabase.FileTable, imageIndex, Int32.MaxValue);

                    // Provide feedback if the operation was cancelled during the database update
                    if (Token.IsCancellationRequested)
                    {
                        return false;
                    }

                    // Provide feedback to the busy indicator every now and then
                    if (ReadyToRefresh())
                    {
                        int percentDone = Convert.ToInt32(imageIndex / TotalImages * 100.0);
                        Progress.Report(new(percentDone,
                            $"Processing {imageIndex}/{TotalImages} images.  ", true, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }

                    // Distinguish between single files vs and episode of files
                    if (Episodes.Episodes.EpisodesDictionary.Count <= 1)
                    {

                        EpisodeCount++;
                        string singletonData;
                        if (IncludeEpisodeID && IncludeSequenceNumber)
                        {
                            singletonData = $"{EpisodeCount}:1|1";
                        }
                        else if (IncludeEpisodeID)
                        {
                            singletonData = $"{EpisodeCount}";
                        }
                        else
                        {
                            singletonData = "1|1";
                        }

                        List<ColumnTuple> ctl = [new(dataLabelByLabel[dataFieldLabel], singletonData)];
                        imagesToUpdate.Add(new(ctl, fileDatabase.FileTable[imageIndex].ID));
                        SingleCount++;
                        imageIndex++;
                    }
                    else
                    {
                        EpisodeCount++;
                        EpisodeNoSingletonsCount++;
                        foreach (KeyValuePair<int, Tuple<int, int>> episode in Episodes.Episodes.EpisodesDictionary)
                        {
                            string episodeData;
                            if (IncludeEpisodeID && IncludeSequenceNumber)
                            {
                                episodeData = $"{EpisodeCount}:{episode.Value.Item1}|{episode.Value.Item2}";
                            }
                            else if (IncludeEpisodeID)
                            {
                                episodeData = $"{EpisodeCount}";
                            }
                            else
                            {
                                episodeData = $"{episode.Value.Item1}|{episode.Value.Item2}";
                            }

                            List<ColumnTuple> ctl =
                            [
                                new(dataLabelByLabel[dataFieldLabel], episodeData)
                            ];
                            imagesToUpdate.Add(new(ctl, fileDatabase.FileTable[imageIndex].ID));
                            imageIndex++;
                        }
                    }
                }
                IsAnyDataUpdated = true;
                Progress.Report(new(100,
                    $"Writing Episode data for {TotalImages} files. Please wait...", false, true));
                Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                fileDatabase.UpdateFiles(imagesToUpdate);

                return true;
            }, Token).ConfigureAwait(true);
        }

        // Provide an example of what the data will look like as various format options are selected
        private void ShowExampleFormat()
        {
            if (true == RadioButtonIncludeBoth.IsChecked)
            {
                TBEpisode.Text = "Episode:    23:1|7";
                TBSingleton.Text = "Singleton: 37:1|1";
            }
            else if (true == RadioButtonIncludeSequenceNumberOnly.IsChecked)
            {
                TBEpisode.Text = "Episode:    1|7";
                TBSingleton.Text = "Singleton: 1|1";

            }
            else //(true == RadioButtonIncludeEpisodeNumberOnly.IsChecked)
            {
                TBEpisode.Text = "Episode:    23";
                TBSingleton.Text = "Singleton: 37";
            }
        }
        private void Done_Click(object sender, RoutedEventArgs e)
        {

            // We return true if the database was altered but also if there was a cancellation, as a cancelled operation
            // may have changed the FileTable (but not database) date entries. Returning true will reset them, as a FileSelectAndShow will be done.
            // Kinda hacky as it expects a certain behaviour of the caller, but it works.
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }
        #endregion

        private void ComboBoxSelectNoteField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                dataFieldLabel = ((string)cb.SelectedValue).Trim();
                StartDoneButton.IsEnabled = !string.IsNullOrEmpty(dataFieldLabel);
            }
        }

        private void RadioButtonCheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true } rb)
            {
                switch (rb.Name)
                {
                    case "RadioButtonIncludeBoth":
                        IncludeEpisodeID = true;
                        IncludeSequenceNumber = true;
                        break;
                    case "RadioButtonIncludeSequenceNumberOnly":
                        IncludeEpisodeID = false;
                        IncludeSequenceNumber = true;
                        break;
                    case "RadioButtonIncludeEpisodeNumberOnly":
                        IncludeEpisodeID = true;
                        IncludeSequenceNumber = false;
                        break;

                }
                ShowExampleFormat();
            }
        }
    }
}
