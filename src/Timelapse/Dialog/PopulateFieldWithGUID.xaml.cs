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
    /// Select a text-compatable field and populate those that are empty with a GUID.
    /// </summary>
    public partial class PopulateFieldWithGUID
    {
        #region Private Variables
        private readonly FileDatabase fileDatabase;
        private string selectedFieldDataLabel = string.Empty;
        private string selectedFieldLabel = string.Empty;

        private long imagesUpdatedWithGUID;
        private long totalImages;
        private bool nothingUpdated = true;
        private readonly List<KeyValuePair<string, string>> labelDataLabel = [];
        #endregion

        public PopulateFieldWithGUID(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            this.fileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Construct a list showing the available note fields in the combobox
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (IsCondition.IsControlType_Note_Multiline_Alphanumeric(control.Type))
                {
                    this.labelDataLabel.Add(new(control.Label, control.DataLabel));
                    ComboBoxSelectNoteField.Items.Add(control.Label);
                }
            }
            if (ComboBoxSelectNoteField.Items.Count == 0)
            {
                ShowFeedbackPanelOnly();
            }
            else if (ComboBoxSelectNoteField.Items.Count == 1)
            {
                ComboBoxSelectNoteField.SelectedIndex = 0;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = !nothingUpdated;
        }

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
            if ((string)StartDoneButton.Content == "Done")
            {
                DialogResult = !nothingUpdated;
                return;
            }
            BusyCancelIndicator.IsBusy = true;
            bool isCompleted = await PopulateGUIDAsync(fileDatabase, selectedFieldDataLabel);
            BusyCancelIndicator.IsBusy = false;

            if (isCompleted)
            {
                TextBlockFeedbackLine1.Text = totalImages == imagesUpdatedWithGUID
                    ? $"Guids were added to the {selectedFieldLabel} field for all {totalImages} selected images."
                    : $"Guids were added to the {selectedFieldLabel} field for {imagesUpdatedWithGUID} out of {totalImages} selected images.";
                double totalImagesNotUpdated = totalImages - imagesUpdatedWithGUID;
                TextBlockFeedbackLine2.Text = totalImagesNotUpdated > 0
                        ? $"{totalImagesNotUpdated} images were not updated as their {selectedFieldLabel} field had non-empty contents."
                        : string.Empty;
                nothingUpdated = imagesUpdatedWithGUID == 0;
            }
            else
            {
                TextBlockFeedbackLine1.Text = "Operation cancelled.";
                TextBlockFeedbackLine2.Text = "No data fields were updated";
                nothingUpdated = true;
            }
            ShowFeedbackPanelOnly();
            CancelButton.Visibility = Visibility.Collapsed;
            StartDoneButton.Content = "Done";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ComboBoxSelectDataField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                //selectedFieldDataLabel = ((string)cb.SelectedValue).Trim();
                if (cb.SelectedIndex < labelDataLabel.Count)
                {
                    selectedFieldDataLabel = labelDataLabel[cb.SelectedIndex].Value;
                    selectedFieldLabel = labelDataLabel[cb.SelectedIndex].Key;
                    StartDoneButton.IsEnabled = !string.IsNullOrEmpty(selectedFieldDataLabel);
                }
            }
        }

        private void ShowFeedbackPanelOnly()
        {
            PrimaryPanel.Visibility = Visibility.Collapsed;
            FeedbackPanel.Visibility = Visibility.Visible;
        }

        private async Task<bool> PopulateGUIDAsync(FileDatabase fileDatabase_, string dataLabel)
        {
            bool result = await Task.Run(() =>
            {
                // For each row in the database, check if its empty. If not, generate a GUID and populate it with that GUID
                // Report progress as needed.

                // This tuple list will hold the id, key and value that we will want to update in the database
                List<ColumnTuplesWithWhere> imagesToUpdate = [];
                totalImages = fileDatabase_.CountAllCurrentlySelectedFiles;
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
                    ColumnTuplesWithWhere imageUpdate = new([new(dataLabel, Guid.NewGuid().ToString())], image.ID);
                    imagesToUpdate.Add(imageUpdate);
                    imagesUpdatedWithGUID++;

                    if (ReadyToRefresh())
                    {
                        // ReSharper disable once PossibleLossOfFraction
                        int percentDone = Convert.ToInt32(imageIndex / totalImages * 100.0);
                        Progress.Report(new(percentDone,
                            $"{imageIndex}/{totalImages} images. Processing {image.File}", true, false));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    }
                }
                Progress.Report(new(100,
                    $"Writing metadata for {totalImages} files. Please wait...", false, true));
                Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                fileDatabase_.UpdateFiles(imagesToUpdate);
                return true;
            }, Token).ConfigureAwait(true);
            return result;

        }
    }
}

