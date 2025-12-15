using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DebuggingSupport;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Abstract base class for metadata population dialogs.
    /// Provides common UI workflow for preview/populate operations.
    /// </summary>
    public abstract class FileMetadataPopulateBase(Window owner, FileDatabase fileDatabase, DataEntryHandler dataHandler, string filePath)
        : FileMetadataDialogBase(owner, fileDatabase, dataHandler, filePath)
    {
        #region Protected fields
        protected ObservableCollection<FileMetadataFeedbackRow> allFeedbackData;
        #endregion

        #region Abstract methods - must be implemented by derived classes

        /// <summary>
        /// Performs the actual metadata population operation.
        /// </summary>
        protected abstract Task<ObservableCollection<FileMetadataFeedbackRow>> PopulateMetadataAsync(Enums.MetadataToolEnum metadataToolSelected, bool previewOnly);

        /// <summary>
        /// Gets the feedback grid control from the derived dialog.
        /// </summary>
        protected abstract DataGrid GetFeedbackGrid();

        /// <summary>
        /// Gets the feedback panel from the derived dialog.
        /// </summary>
        protected abstract Panel GetFeedbackPanel();

        /// <summary>
        /// Gets the metadata grid control from the derived dialog.
        /// </summary>
        protected abstract Controls.FileMetadataGrid GetMetadataGrid();

        /// <summary>
        /// Gets the busy cancel indicator from the derived dialog.
        /// </summary>
        protected abstract BusyCancelIndicator GetBusyCancelIndicator();

        /// <summary>
        /// Gets the populating message TextBlock from the derived dialog.
        /// </summary>
        protected abstract TextBlock GetPopulatingMessage();

        /// <summary>
        /// Gets the Start/Done button from the derived dialog.
        /// </summary>
        protected abstract Button GetStartDoneButton();

        /// <summary>
        /// Gets the Preview button from the derived dialog.
        /// </summary>
        protected abstract Button GetPreviewButton();

        /// <summary>
        /// Gets the Back button from the derived dialog.
        /// </summary>
        protected abstract Button GetBackButton();

        /// <summary>
        /// Gets the Cancel button from the derived dialog.
        /// </summary>
        protected abstract Button GetCancelButton();

        /// <summary>
        /// Gets the ShowEverything radio button from the derived dialog.
        /// </summary>
        protected abstract RadioButton GetShowEverythingRadioButton();

        /// <summary>
        /// Shows/hides additional UI elements specific to each dialog during mode transitions.
        /// </summary>
        protected abstract void ShowHideDialogSpecificElements(bool showMetadataGrid);

        #endregion

        #region Common button callbacks

        /// <summary>
        /// Handles the Preview button click event.
        /// </summary>
        protected async void Preview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Preview_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        /// <summary>
        /// Handles the Start button click event.
        /// </summary>
        protected async void Start_Click(object sender, RoutedEventArgs e)
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

        /// <summary>
        /// Handles the Back button click event.
        /// </summary>
        protected void Back_Click(object sender, RoutedEventArgs e)
        {
            // Restore the UI to the initial state (before preview was clicked)
            GetBackButton().Visibility = Visibility.Collapsed;
            GetBackButton().IsEnabled = false;
            GetCancelButton().IsEnabled = true;
            GetCancelButton().Visibility = Visibility.Visible;
            GetPreviewButton().IsEnabled = true;
            GetPreviewButton().Visibility = Visibility.Visible;
            GetStartDoneButton().IsEnabled = true;
            GetStartDoneButton().Visibility = Visibility.Visible;
            WindowCloseButtonIsEnabled(true);

            GetFeedbackPanel().Visibility = Visibility.Collapsed;
            GetMetadataGrid().Visibility = Visibility.Visible;
            ShowHideDialogSpecificElements(showMetadataGrid: true);
        }

        /// <summary>
        /// Handles the Done button click event.
        /// </summary>
        protected void Done_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = IsCancellationRequested() || isAnyDataUpdated;
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        protected void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = IsCancellationRequested() || isAnyDataUpdated;
        }

        /// <summary>
        /// Handles radio button changes for feedback display filtering.
        /// </summary>
        protected void FeedbackDisplayRadioButton_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFeedbackGridDisplayInternal();
        }

        #endregion

        #region Common window event handlers

        /// <summary>
        /// Handles the window closing event.
        /// </summary>
        protected void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = IsCancellationRequested() || isAnyDataUpdated;
        }

        /// <summary>
        /// Handles the DataGrid auto-generated columns event.
        /// </summary>
        protected void FeedbackDatagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            ConfigureFeedbackDataGridColumns(GetFeedbackGrid());
        }

        #endregion

        #region Private implementation methods

        private async Task Preview_ClickAsync()
        {
            // Update the UI to show the feedback datagrid in preview mode
            GetPopulatingMessage().Text = "Generating preview";
            GetCancelButton().IsEnabled = false;
            GetCancelButton().Visibility = Visibility.Hidden;
            GetPreviewButton().IsEnabled = false;
            GetPreviewButton().Visibility = Visibility.Collapsed;
            GetStartDoneButton().IsEnabled = false;
            GetStartDoneButton().Visibility = Visibility.Collapsed;
            GetBackButton().Visibility = Visibility.Visible;
            GetBusyCancelIndicator().IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            GetMetadataGrid().Visibility = Visibility.Collapsed;
            ShowHideDialogSpecificElements(showMetadataGrid: false);
            GetFeedbackPanel().Visibility = Visibility.Visible;

            // This call generates the preview without updating the database
            allFeedbackData = await PopulateMetadataAsync(GetMetadataGrid().MetadataToolSelected, previewOnly: true).ConfigureAwait(true);

            // Update the UI to its final state
            GetBackButton().IsEnabled = true;
            GetBusyCancelIndicator().IsBusy = false;
            WindowCloseButtonIsEnabled(true);
            UpdateFeedbackGridDisplayInternal();
            GetPopulatingMessage().Text = IsCancellationRequested()
                ? "Cancelled - preview generation stopped."
                : GetPreviewCompletedMessage();
        }

        private async Task Start_ClickAsync()
        {
            // Update the UI to show the feedback datagrid
            GetPopulatingMessage().Text = "Populating metadata";
            GetCancelButton().IsEnabled = false;
            GetCancelButton().Visibility = Visibility.Hidden;
            GetPreviewButton().IsEnabled = false;
            GetPreviewButton().Visibility = Visibility.Collapsed;
            GetStartDoneButton().Content = "_Done";
            GetStartDoneButton().Click -= Start_Click;
            GetStartDoneButton().Click += Done_Click;
            GetStartDoneButton().IsEnabled = false;
            GetBusyCancelIndicator().IsBusy = true;
            WindowCloseButtonIsEnabled(false);

            GetMetadataGrid().Visibility = Visibility.Collapsed;
            ShowHideDialogSpecificElements(showMetadataGrid: false);
            GetFeedbackPanel().Visibility = Visibility.Visible;
            WindowCloseButtonIsEnabled(false);

            // This call does all the actual populating...
            allFeedbackData = await PopulateMetadataAsync(GetMetadataGrid().MetadataToolSelected, previewOnly: false).ConfigureAwait(true);

            // Update the UI to its final state
            GetStartDoneButton().IsEnabled = true;
            GetBusyCancelIndicator().IsBusy = false;
            WindowCloseButtonIsEnabled(true);
            UpdateFeedbackGridDisplayInternal();
            GetPopulatingMessage().Text = IsCancellationRequested()
                ? "Cancelled - content is unchanged."
                : GetPopulateCompletedMessage();
        }

        private void UpdateFeedbackGridDisplayInternal()
        {
            bool showEverything = GetShowEverythingRadioButton()?.IsChecked == true;
            UpdateFeedbackGridDisplay(GetFeedbackGrid(), allFeedbackData, showEverything);
        }

        /// <summary>
        /// Gets the message to display when preview is completed.
        /// Can be overridden by derived classes for custom messages.
        /// </summary>
        protected virtual string GetPreviewCompletedMessage()
        {
            return "Preview of metadata population (no changes made to database).";
        }

        /// <summary>
        /// Gets the message to display when populate is completed.
        /// Can be overridden by derived classes for custom messages.
        /// </summary>
        protected virtual string GetPopulateCompletedMessage()
        {
            return "Populated metadata as follows.";
        }

        #endregion
    }
}
