using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Abstract base class for image metadata-related dialogs.
    /// Provides common functionality for viewing and populating metadata.
    /// </summary>
    public abstract class FileMetadataDialogBase(Window owner, FileDatabase fileDatabase, DataEntryHandler dataHandler, string filePath)
        : BusyableDialogWindow(owner)
    {
        #region Protected fields - available to subclasses
        protected readonly string FilePath = filePath;
        protected readonly FileDatabase FileDatabase = fileDatabase;
        protected readonly DataEntryHandler DataHandler = dataHandler;
        protected bool isAnyDataUpdated = false;
        #endregion


        #region Protected helper methods for subclasses

        /// <summary>
        /// Initializes the progress handler for the dialog's BusyCancelIndicator.
        /// </summary>
        protected void InitializeProgressHandler(Controls.BusyCancelIndicator busyCancelIndicator)
        {
            InitalizeProgressHandler(busyCancelIndicator);
        }

        /// <summary>
        /// Determines if it's time to refresh the UI based on throttle timing.
        /// </summary>
        protected bool IsReadyToRefresh()
        {
            return ReadyToRefresh();
        }

        /// <summary>
        /// Gets the cancellation token for async operations.
        /// </summary>
        protected CancellationToken GetCancellationToken()
        {
            return Token;
        }

        /// <summary>
        /// Checks if cancellation has been requested.
        /// </summary>
        protected bool IsCancellationRequested()
        {
            return Token.IsCancellationRequested;
        }

        /// <summary>
        /// Reports progress to the UI.
        /// </summary>
        protected void ReportProgress(ProgressBarArguments args)
        {
            Progress.Report(args);
        }

        #endregion

        #region Common feedback grid methods

        /// <summary>
        /// Configures the auto-generated columns for feedback DataGrid.
        /// Sets proper headers for feedback display.
        /// </summary>
        protected void ConfigureFeedbackDataGridColumns(DataGrid feedbackGrid)
        {
            // Find columns by binding path and set headers
            foreach (var column in feedbackGrid.Columns)
            {
                if (column is DataGridBoundColumn boundColumn)
                {
                    var binding = boundColumn.Binding as System.Windows.Data.Binding;
                    if (binding?.Path.Path == "FileName")
                    {
                        column.Header = "File Name";
                    }
                    else if (binding?.Path.Path == "MetadataName")
                    {
                        column.Header = "Metadata name";
                    }
                    else if (binding?.Path.Path == "MetadataValue")
                    {
                        column.Header = "Metadata Value";
                    }
                    else if (binding?.Path.Path == "IsSuccessRow")
                    {
                        // Hide the IsSuccessRow column
                        column.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the feedback grid display based on show-all vs show-errors-only mode.
        /// </summary>
        protected void UpdateFeedbackGridDisplay(
            DataGrid feedbackGrid,
            ObservableCollection<FileMetadataFeedbackRow> allFeedbackData,
            bool showEverything)
        {
            if (allFeedbackData == null)
            {
                return;
            }

            if (showEverything)
            {
                // Show all rows
                feedbackGrid.ItemsSource = allFeedbackData;
            }
            else
            {
                // Show only non-success rows (error/warning rows)
                var filteredData = new ObservableCollection<FileMetadataFeedbackRow>(
                    allFeedbackData.Where(row => !row.IsSuccessRow));

                if (filteredData.Count == 0)
                {
                    filteredData.Insert(0, new FileMetadataFeedbackRow("", "No issues found writing metadata to files", "✓ All data fields updated"));
                }
                else if (allFeedbackData.Count > 0 && filteredData.Count > 0)
                {
                    filteredData.Insert(0, new FileMetadataFeedbackRow("", "Some issues found writing metadata to files", "✓⚠Only some data fields were updated"));
                }
                else if (allFeedbackData.Count > 0 && filteredData.Count == 0)
                {
                    filteredData.Insert(0, new FileMetadataFeedbackRow("", "Issues found in every file", "⚠No data fields were updated"));
                }

                feedbackGrid.ItemsSource = filteredData;
            }
        }

        #endregion
    }
}
