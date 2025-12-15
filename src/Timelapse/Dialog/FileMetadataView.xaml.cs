using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Dialog for viewing image metadata without editing capabilities.
    /// Includes slider navigation to browse through files.
    /// </summary>
    public partial class FileMetadataView
    {
        #region Private variables
        // Timer for debouncing metadata refresh during slider navigation
        private System.Windows.Threading.DispatcherTimer metadataRefreshTimer;
        private int pendingFileIndex = -1;

        // Remember the selected row across file navigation
        private string rememberedMetadataKind;
        private string rememberedMetadataName;
        #endregion

        #region Constructor
        public FileMetadataView(Window owner, FileDatabase fileDatabase, DataEntryHandler dataHandler, string filePath)
            : base(owner, fileDatabase, dataHandler, filePath)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(ViewOnlyMessage);
        }
        #endregion

        #region Window event handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewOnlyMessage.BuildContentFromProperties();

            // Set up progress handler
            InitializeProgressHandler(BusyCancelIndicator);

            // Configure the metadata grid
            MetadataGrid.viewModel.RootPath = FileDatabase.RootPathToDatabase;
            MetadataGrid.viewModel.FilePath = FilePath;
            MetadataGrid.ImageMetadataFilter = ImageMetadataFiltersEnum.AllMetadata;

            // Show the metadata group column (Details is always on)
            // Note: HideMetadataKindColumn = false means show the column
            MetadataGrid.HideMetadataKindColumn = false;

            // Set ExifTool radio button to true
            MetadataGrid.ExifToolRB.IsChecked = true;

            // Hide the Data field column
            MetadataGrid.HideDataFieldColumn();

            // Hide the Assign XMP-TimelapseData button
            MetadataGrid.AssignXmpTimelapseDataButton.Visibility = Visibility.Collapsed;

            // Set up the slider file navigator
            SliderFileNavigator.Minimum = 0;
            SliderFileNavigator.Maximum = FileDatabase.CountAllCurrentlySelectedFiles - 1;
            int rowIndex = DataHandler.ImageCache.CurrentRow;
            SliderFileNavigator.Value = rowIndex;
            ImageRow row = DataHandler.FileDatabase.FileTable[rowIndex];
            FileNameWhileScrolling.Content = Path.Combine(row.RelativePath, row.File);
            SliderFileNavigator.ValueChanged += SliderFileNavigator_OnValueChanged;

            // Initialize the metadata refresh timer for debouncing
            metadataRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            metadataRefreshTimer.Tick += MetadataRefreshTimer_Tick;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // View-only mode never updates data
            DialogResult = false;
        }
        #endregion

        #region Button callbacks
        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion

        #region Slider navigation
        private void SliderFileNavigator_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Update the filename display immediately for responsive UI
            int fileIndex = Convert.ToInt32(e.NewValue);
            ImageRow row = DataHandler.FileDatabase.FileTable[fileIndex];
            FileNameWhileScrolling.Content = Path.Combine(row.RelativePath, row.File);

            // Store the pending file index and restart the timer
            pendingFileIndex = fileIndex;
            metadataRefreshTimer.Stop();
            metadataRefreshTimer.Start();
        }

        private void MetadataRefreshTimer_Tick(object sender, EventArgs e)
        {
            // Stop the timer
            metadataRefreshTimer.Stop();

            // Check if there's a pending refresh
            if (pendingFileIndex < 0)
            {
                return;
            }

            // Get the file information
            ImageRow row = DataHandler.FileDatabase.FileTable[pendingFileIndex];
            string filePath = Path.Combine(FileDatabase.RootPathToDatabase, row.RelativePath, row.File);
            MetadataGrid.viewModel.FilePath = filePath;

            // Capture the MetadataKind and MetadataName of the currently selected row
            // This allows us to restore the selection even after navigating through files that don't have this metadata
            if (MetadataGrid.AvailableMetadataDataGrid.SelectedItem is Controls.FileMetadataGrid.DataContents selectedItem)
            {
                rememberedMetadataKind = selectedItem.MetadataKind;
                rememberedMetadataName = selectedItem.MetadataName;
            }

            // Refresh the metadata display without applying default sort
            MetadataGrid.Refresh(applyDefaultSort: false);

            // Apply the current sort state from FileMetadataGrid
            if (MetadataGrid.CurrentSortColumn != null)
            {
                MetadataGrid.ApplySort(MetadataGrid.CurrentSortColumn, MetadataGrid.CurrentSortDirection);
            }
            else
            {
                MetadataGrid.ApplySort(MetadataGrid.AvailableMetadataDataGrid.Columns[2], System.ComponentModel.ListSortDirection.Ascending);
            }

            // Try to restore selection based on remembered MetadataKind and MetadataName
            if (!string.IsNullOrEmpty(rememberedMetadataKind) && !string.IsNullOrEmpty(rememberedMetadataName))
            {
                var itemToSelect = MetadataGrid.viewModel.MetadataList
                    .FirstOrDefault(item => item.MetadataKind == rememberedMetadataKind &&
                                          item.MetadataName == rememberedMetadataName);

                if (itemToSelect != null)
                {
                    MetadataGrid.AvailableMetadataDataGrid.SelectedItem = itemToSelect;
                    MetadataGrid.AvailableMetadataDataGrid.ScrollIntoView(itemToSelect);
                }
            }
        }
        #endregion
    }
}
