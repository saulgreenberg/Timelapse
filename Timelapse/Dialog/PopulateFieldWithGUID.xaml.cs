using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Select a text-compatable field and populate those that are empty with a GUID.
    /// </summary>
    public partial class PopulateFieldWithGUID
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private string dataFieldLabel = string.Empty;
        private long imagesUpdatedWithGUID;
        private long totalImages;
        private bool nothingUpdated = true;
        #endregion

        public PopulateFieldWithGUID(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a progress handler that will update the progress bar
            this.InitalizeProgressHandler(this.BusyCancelIndicator);

            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in this.fileDatabase.Controls)
            {
                if (Util.IsCondition.IsControlType_Note_Multiline_Alphanumeric(control.Type))
                {
                    this.ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }
            if (this.ComboBoxSelectNoteField.Items.Count == 0)
            {
                this.ShowFeedbackPanelOnly();
            }
            else if (this.ComboBoxSelectNoteField.Items.Count == 1)
            {
                this.ComboBoxSelectNoteField.SelectedIndex = 0;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = !nothingUpdated;
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if ((string)this.StartDoneButton.Content == "Done")
            {
                this.DialogResult = !nothingUpdated;
                return;
            }
            this.BusyCancelIndicator.IsBusy = true;
            bool isCompleted = await this.PopulateGUIDAsync(this.fileDatabase, this.dataFieldLabel);
            this.BusyCancelIndicator.IsBusy = false;

            if (isCompleted)
            {
                this.TextBlockFeedbackLine1.Text = totalImages == imagesUpdatedWithGUID
                    ? $"Guids were added to the {dataFieldLabel} field for all {totalImages} selected images."
                    : $"Guids were added to the {dataFieldLabel} field for {imagesUpdatedWithGUID} out of {totalImages} selected images.";
                double totalImagesNotUpdated = totalImages - imagesUpdatedWithGUID;
                this.TextBlockFeedbackLine2.Text = totalImagesNotUpdated > 0
                        ? $"{totalImagesNotUpdated} images were not updated as their {dataFieldLabel} field had non-empty contents."
                        : string.Empty;
                this.nothingUpdated = imagesUpdatedWithGUID == 0;
            }
            else
            {
                this.TextBlockFeedbackLine1.Text = "Operation cancelled.";
                this.TextBlockFeedbackLine2.Text = "No data fields were updated";
                this.nothingUpdated = true;
            }
            this.ShowFeedbackPanelOnly();
            this.CancelButton.Visibility = Visibility.Collapsed;
            this.StartDoneButton.Content = "Done";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ComboBoxSelectDataField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                this.dataFieldLabel = ((string)cb.SelectedValue).Trim();
                this.StartDoneButton.IsEnabled = !string.IsNullOrEmpty(this.dataFieldLabel);
            }
        }

        private void ShowFeedbackPanelOnly()
        {
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
        }

        private async Task<bool> PopulateGUIDAsync(FileDatabase fileDatabase_, string dataLabel)
        {
            bool result = await Task.Run(() =>
            {
                // For each row in the database, check if its empty. If not, generate a GUID and populate it with that GUID
                // Report progress as needed.

                // This tuple list will hold the id, key and value that we will want to update in the database
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                this.totalImages = fileDatabase_.CountAllCurrentlySelectedFiles;
                for (int imageIndex = 0; imageIndex < totalImages; ++imageIndex)
                {
                    // Provide feedback if the operation was cancelled during the database update
                    if (Token.IsCancellationRequested)
                    {
                        return false;
                    }

                    ImageRow image = fileDatabase_.FileTable[imageIndex];
                    if (false == string.IsNullOrWhiteSpace(image.GetValueDisplayString(dataLabel)))
                    {
                        // Skip non-empty fields
                        continue;
                    }
                    ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(new List<ColumnTuple>() { new ColumnTuple(dataLabel, Guid.NewGuid().ToString()) }, image.ID);
                    imagesToUpdate.Add(imageUpdate);
                    imagesUpdatedWithGUID++;

                    if (this.ReadyToRefresh())
                    {
                        // ReSharper disable once PossibleLossOfFraction
                        int percentDone = Convert.ToInt32(imageIndex / totalImages * 100.0);
                        this.Progress.Report(new ProgressBarArguments(percentDone,
                            $"{imageIndex}/{totalImages} images. Processing {image.File}", true, false));
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                }
                this.Progress.Report(new ProgressBarArguments(100,
                    $"Writing metadata for {totalImages} files. Please wait...", false, true));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                fileDatabase_.UpdateFiles(imagesToUpdate);
                return true;
            }, this.Token).ConfigureAwait(true);
            return result;

        }
    }
}

