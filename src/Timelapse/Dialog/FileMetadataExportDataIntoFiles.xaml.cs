using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.ExifTool;
using Timelapse.Util;
using ImageRow = Timelapse.DataTables.ImageRow;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for FileMetadataExportDataIntoFiles.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class FileMetadataExportDataIntoFiles
    {
        #region Private Fields

        // Store reference to TimelapseWindow for accessing database
        private TimelapseWindow timelapseWindow;

        // Store the mapping of checkboxes to their associated data labels
        private readonly Dictionary<CheckBox, string> checkBoxDataLabelMap;

        #endregion

        #region Constructor and Initialization

        public FileMetadataExportDataIntoFiles(Window owner) : base(owner)
        {
            InitializeComponent();
            Owner = owner;
            checkBoxDataLabelMap = [];
            FormattedDialogHelper.SetupStaticReferenceResolver(WriteExifDataMessage);
        }

        private void FileMetadataExportDataIntoFiles_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            timelapseWindow = Owner as TimelapseWindow;

            this.WriteExifDataMessage.BuildContentFromProperties();

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Validation: Ensure we have access to the database and not in thumbnail view
            if (timelapseWindow?.DataHandler?.FileDatabase == null)
            {
                // Should never get here as menu is disabled if database isn't available
                MessageBox.Show(
                    "Unable to access the database. Please ensure a valid image set is loaded.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Close();
                return;
            }

            // Initialize FileMetadataGrid for view-only mode showing XMP-TimelapseData
            InitializeMetadataGrid();

            PopulateCheckBoxList();
        }

        private void InitializeMetadataGrid()
        {
            // Hide the tool selection panel (radio buttons)
            if (MetadataGrid.FindName("ToolSelectionPanel") is StackPanel toolPanel)
            {
                toolPanel.Visibility = Visibility.Collapsed;
            }

            // Configure grid for view-only mode
            // Show the metadata group column (Details is always on)
            MetadataGrid.HideMetadataKindColumn = false;

            // Hide the Data field column (not needed for view-only)
            MetadataGrid.HideDataFieldColumn();

            // Hide the AssignXmpTimelapseDataButton (not needed for this dialog)
            MetadataGrid.AssignXmpTimelapseDataButton.Visibility = Visibility.Collapsed;

            // No need to set DictDataLabel_Label since we're in view-only mode
            // Note: We don't set radio buttons here to avoid triggering unwanted refresh events
        }

        #endregion

        #region UI Population

        private void PopulateCheckBoxList()
        {
            // Clear any existing checkboxes
            CheckBoxPanel.Children.Clear();
            checkBoxDataLabelMap.Clear();

            // Enable shared size scope for column alignment across rows
            Grid.SetIsSharedSizeScope(CheckBoxPanel, true);

            // Add column headers
            Grid headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 5),
                Background = Brushes.WhiteSmoke
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // XMP-TimelapseData checkbox column
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "DataLabelColumn" }); // Data label column
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "LabelColumn" }); // Label column
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Example column

            // Create XMP column header with Select all/none links
            StackPanel xmpHeader = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 3, 3, 3)
            };

            TextBlock xmpSelectAll = new TextBlock
            {
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            System.Windows.Documents.Hyperlink xmpSelectAllLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Select all"));
            xmpSelectAllLink.Click += SelectAllButton_Click;
            xmpSelectAll.Inlines.Add(xmpSelectAllLink);
            xmpHeader.Children.Add(xmpSelectAll);

            TextBlock xmpSelectNone = new TextBlock
            {
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            System.Windows.Documents.Hyperlink xmpSelectNoneLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Select none"));
            xmpSelectNoneLink.Click += DeselectAllButton_Click;
            xmpSelectNone.Inlines.Add(xmpSelectNoneLink);
            xmpHeader.Children.Add(xmpSelectNone);

            TextBlock labelHeader = new TextBlock
            {
                Text = "Label",
                FontSize = 11,
                FontWeight = FontWeights.DemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 3, 1, 3)
            };

            TextBlock dataLabelHeader = new TextBlock
            {
                Text = "Data label",
                FontSize = 11,
                FontWeight = FontWeights.DemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 3, 1, 3)
            };

            TextBlock exampleHeader = new TextBlock
            {
                Text = "Example",
                FontSize = 11,
                FontWeight = FontWeights.DemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 3, 3, 3)
            };

            Grid.SetColumn(xmpHeader, 0);
            Grid.SetColumn(labelHeader, 1);
            Grid.SetColumn(dataLabelHeader, 2);
            Grid.SetColumn(exampleHeader, 3);
            headerGrid.Children.Add(xmpHeader);
            headerGrid.Children.Add(labelHeader);
            headerGrid.Children.Add(dataLabelHeader);
            headerGrid.Children.Add(exampleHeader);

            CheckBoxPanel.Children.Add(headerGrid);

            // Get current image for example values
            var currentImage = timelapseWindow.DataHandler.ImageCache.Current;

            // Iterate over controls to get both dataLabel and label
            foreach (var control in timelapseWindow.DataHandler.FileDatabase.Controls)
            {
                // Skip the ID, RelativePath, File, and DeleteFlag column as these are of little value to export
                // File and RelativePath could also interfere with file identification if re-imported back into Timelapse
                // Note that we do keep the standard DateTime field as it could have been corrected by the analyst.
                if (control.DataLabel is DatabaseColumn.ID or DatabaseColumn.File or DatabaseColumn.RelativePath or DatabaseColumn.DeleteFlag)
                {
                    continue;
                }

                string dataLabel = control.DataLabel;
                string label = control.Label;
                string exampleValue = currentImage?.GetValueDatabaseString(dataLabel) ?? string.Empty;

                // Create a grid with four columns for each row
                Grid rowGrid = new Grid
                {
                    Margin = new Thickness(0, 1, 0, 1)
                };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // XMP checkbox column
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "DataLabelColumn" }); // Data label column
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "LabelColumn" }); // Label column
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Example column

                // Create XMP-TimelapseData checkbox (initially unchecked)
                CheckBox xmpCheckBox = new CheckBox
                {
                    FontSize = 11,
                    IsChecked = false,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(3, 2, 3, 2)
                };

                // Attach event handlers for checkbox state changes
                xmpCheckBox.Checked += CheckBox_CheckedChanged;
                xmpCheckBox.Unchecked += CheckBox_CheckedChanged;

                // Store the mapping
                checkBoxDataLabelMap[xmpCheckBox] = dataLabel;

                // Create label text
                TextBlock labelText = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 2, 1, 2)
                };

                // Create data label text
                TextBlock dataLabelText = new TextBlock
                {
                    Text = dataLabel,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 2, 1, 2)
                };

                // Create example value text
                TextBlock exampleText = new TextBlock
                {
                    Text = exampleValue,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 2, 3, 2),
                    Foreground = Brushes.Gray
                };

                // Add all elements to grid
                Grid.SetColumn(xmpCheckBox, 0);
                Grid.SetColumn(labelText, 1);
                Grid.SetColumn(dataLabelText, 2);
                Grid.SetColumn(exampleText, 3);
                rowGrid.Children.Add(xmpCheckBox);
                rowGrid.Children.Add(labelText);
                rowGrid.Children.Add(dataLabelText);
                rowGrid.Children.Add(exampleText);

                // Add grid to panel
                CheckBoxPanel.Children.Add(rowGrid);
            }

            // Show message if no data labels available (only header exists)
            if (CheckBoxPanel.Children.Count == 1)
            {
                TextBlock noDataMessage = new TextBlock
                {
                    Text = "No data labels are available in this template.",
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(10)
                };
                CheckBoxPanel.Children.Add(noDataMessage);
            }

            // Update button state based on initial checkbox selections
            DoUpdateButtonEnableState();
        }

        #endregion

        #region Hyperlink Callbacks and actions
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            DoSetAllCheckBoxes(true);
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            DoSetAllCheckBoxes(false);
        }

        private void DoSetAllCheckBoxes(bool isChecked)
        {
            // Toggle all XMP checkboxes
            foreach (var checkBox in checkBoxDataLabelMap.Keys)
            {
                checkBox.IsChecked = isChecked;
            }

            // Update button state after changing all checkboxes
            DoUpdateButtonEnableState();
        }
        #endregion

        #region Checkbox Callbacks and actions

        private void CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Update button state when any checkbox is checked or unchecked
            DoUpdateButtonEnableState();
        }

        private void DoUpdateButtonEnableState()
        {
            // Enable buttons if at least one checkbox is selected
            bool hasSelection = checkBoxDataLabelMap.Keys.Any(cb => cb.IsChecked == true);
            WriteMetadataButton.IsEnabled = hasSelection;
            WriteMetadataToAllButton.IsEnabled = hasSelection;
        }

        #endregion

        #region Button callbacks        
        private void WriteMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            // Call the common method with currentFileOnly = true
            DoWriteMetadataToFiles(currentFileOnly: true);
        }

        private void WriteMetadataToAll_Click(object sender, RoutedEventArgs e)
        {
            // Call the common method with currentFileOnly = false
            DoWriteMetadataToFiles(currentFileOnly: false);
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Data Collection Helpers

        private List<string> GetSelectedDataLabels()
        {
            List<string> selectedLabels = [];

            foreach (var kvp in checkBoxDataLabelMap)
            {
                if (kvp.Key.IsChecked == true)
                {
                    selectedLabels.Add(kvp.Value);
                }
            }

            return selectedLabels;
        }

        private Dictionary<string, string> BuildDataLabelTypesDictionary()
        {
            Dictionary<string, string> dataLabelTypes = [];

            foreach (var control in timelapseWindow.DataHandler.FileDatabase.Controls)
            {
                dataLabelTypes[control.DataLabel] = control.Type;
            }

            return dataLabelTypes;
        }

        private bool IsNumericType(string controlType)
        {
            // These types should have numeric values (not quoted in JSON)
            return controlType == Constant.Control.IntegerAny ||
                   controlType == Constant.Control.IntegerPositive ||
                   controlType == Constant.Control.DecimalAny ||
                   controlType == Constant.Control.DecimalPositive ||
                   controlType == Constant.Control.Counter;
        }


        #endregion

        #region Metadata Building - JSON Format

        private string BuildJsonMetadata(ImageRow imageRow, List<string> selectedDataLabels)
        {
            // Build type dictionary for all controls
            var dataLabelTypes = BuildDataLabelTypesDictionary();

            StringBuilder jsonBuilder = new StringBuilder();
            jsonBuilder.Append("{");

            bool firstEntry = true;
            foreach (string dataLabel in selectedDataLabels)
            {
                if (!firstEntry)
                {
                    jsonBuilder.Append(",");
                }
                firstEntry = false;

                string value = imageRow.GetValueDatabaseString(dataLabel) ?? string.Empty;
                string controlType = dataLabelTypes.TryGetValue(dataLabel, out var type) ? type : string.Empty;

                // Add the key (always quoted)
                jsonBuilder.Append($"\"{dataLabel}\":");

                // Add the value - either as number, string, or null
                if (IsNumericType(controlType))
                {
                    // Empty numeric field - use null (not quoted)
                    // Numeric value - no quotes
                    jsonBuilder.Append(!string.IsNullOrWhiteSpace(value) ? value : "null");
                }
                else
                {
                    // String value - with quotes, escape any quotes in the value
                    string escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    jsonBuilder.Append($"\"{escapedValue}\"");
                }
            }

            jsonBuilder.Append("}");
            return jsonBuilder.ToString();
        }

        #endregion

        #region ExifTool Configuration File Generation

        private string GenerateExifToolConfigContent(List<string> dataLabels)
        {
            StringBuilder sb = new StringBuilder();

            // File header
            sb.AppendLine("#------------------------------------------------------------------------------");
            sb.AppendLine("# File:         .ExifTool_config");
            sb.AppendLine("#");
            sb.AppendLine("# Description:  Custom ExifTool configuration file for Timelapse data");
            sb.AppendLine("#               Generated by Timelapse on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("#");
            sb.AppendLine("# Usage:        exiftool -config .ExifTool_config [OPTIONS] FILE");
            sb.AppendLine("#------------------------------------------------------------------------------");
            sb.AppendLine();

            // Define the custom namespace in the XMP::Main table
            sb.AppendLine("%Image::ExifTool::UserDefined = (");
            sb.AppendLine("    'Image::ExifTool::XMP::Main' => {");
            sb.AppendLine("        # Define a custom XMP namespace for Timelapse application data");
            sb.AppendLine("        TimelapseData => {");
            sb.AppendLine("            SubDirectory => {");
            sb.AppendLine("                TagTable => 'Image::ExifTool::UserDefined::TimelapseData',");
            sb.AppendLine("            },");
            sb.AppendLine("        },");
            sb.AppendLine("    },");
            sb.AppendLine(");");
            sb.AppendLine();

            // Define the TimelapseData namespace and tags
            sb.AppendLine("# Define the XMP TimelapseData namespace and tags");
            sb.AppendLine("%Image::ExifTool::UserDefined::TimelapseData = (");
            sb.AppendLine("    GROUPS => { 0 => 'XMP', 1 => 'XMP-TimelapseData', 2 => 'Image' },");
            sb.AppendLine("    NAMESPACE => { 'TimelapseData' => 'http://saul.cpsc.ucalgary.ca/timelapse/TimelapseData/1.0/' },");
            sb.AppendLine("    WRITABLE => 'string',");
            sb.AppendLine();
            sb.AppendLine("    # Custom fields from your Timelapse template");

            // Add each selected data label as a field
            foreach (string dataLabel in dataLabels)
            {
                // ExifTool tag names should be valid Perl identifiers
                string sanitizedDataLabel = SanitizeDataLabelForPerl(dataLabel);
                // Use Name property to explicitly preserve case and prevent auto-capitalization
                sb.AppendLine($"    {sanitizedDataLabel} => {{ Name => '{sanitizedDataLabel}' }},");
            }

            // Always add json field for optional JSON metadata
            // Use Name property to explicitly preserve lowercase case
            sb.AppendLine("    json => { Name => 'json' },");

            sb.AppendLine(");");
            sb.AppendLine();
            sb.AppendLine("#------------------------------------------------------------------------------");
            sb.AppendLine("1;  #end");

            return sb.ToString();
        }

        private string SanitizeDataLabelForPerl(string dataLabel)
        {
            // Replace spaces with underscores
            string sanitized = dataLabel.Replace(" ", "_");

            // Remove any characters that aren't alphanumeric or underscore
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                @"[^a-zA-Z0-9_]",
                "");

            // Ensure it doesn't start with a number (Perl requirement)
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }

        #endregion

        #region Metadata Writing Operations

        private async void DoWriteMetadataToFiles(bool currentFileOnly)
        {
            try
            {
                await DoWriteMetadataToFilesAsync(currentFileOnly);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task DoWriteMetadataToFilesAsync(bool currentFileOnly)
        {
            string configFilePath = null;
            int written = 0, skipped = 0;
            int totalCount = 0;
            ExifToolConfigBatchWriter batchWriter = null;
            var startTime = DateTime.Now;
            bool cleanupDone = false;
            try
            {
                // Get selected data labels
                List<string> selectedDataLabels = GetSelectedDataLabels();

                // Capture checkbox state before background thread (to avoid threading issues)
                bool includeJsonMetadata = IncludeJsonMetadataCheckBox.IsChecked == true;

                // Generate and write the exiftool config file in temporary directory
                string configContent = GenerateExifToolConfigContent(selectedDataLabels);
                string tempDir = Path.GetTempPath();
                configFilePath = Path.Combine(tempDir, $".ExifTool_config_{Guid.NewGuid():N}.tmp");
                await System.IO.File.WriteAllTextAsync(configFilePath, configContent);


                // Get file count and starting index based on mode
                int startIndex;
                if (currentFileOnly)
                {
                    // Process only the current file
                    totalCount = 1;
                    startIndex = timelapseWindow.DataHandler.ImageCache.CurrentRow;

                    if (timelapseWindow.DataHandler.ImageCache.Current == null)
                    {
                        MessageBox.Show(
                            "No current image available.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Process all currently selected files
                    totalCount = timelapseWindow.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;
                    startIndex = 0; // Not actually used, but needed for compiler

                    if (totalCount == 0)
                    {
                        MessageBox.Show(
                            "No files are currently selected.",
                            "No Files",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                // Disable buttons and clear progress
                WriteMetadataButton.IsEnabled = false;
                WriteMetadataToAllButton.IsEnabled = false;
                ProgressTextBox.Clear();

                // Show the busy indicator and disable window close button
                BusyCancelIndicator.IsBusy = true;
                WindowCloseButtonIsEnabled(false);

                // Run batch operation on background thread
                await System.Threading.Tasks.Task.Run(() =>
                {

                    var rootPath = timelapseWindow.DataHandler.FileDatabase.RootPathToDatabase;

                    batchWriter = GlobalReferences.TimelapseState.ExifToolManager.CreateBatchWriter(configFilePath);

                    for (int i = 0; i < totalCount; i++)
                    {
                        // Provide feedback if the operation was cancelled during the database update
                        if (Token.IsCancellationRequested)
                        {
                            Debug.Print("Cancelled");
                            return;
                        }

                        // Get the file to process (either current file or sequential from all selected)
                        int fileIndex = currentFileOnly ? startIndex : i;
                        ImageRow imageRow = timelapseWindow.DataHandler.FileDatabase.FileTable[fileIndex];

                        if (ReadyToRefresh())
                        {
                            int percentDone = Convert.ToInt32(i / (double)totalCount * 100.0);
                            Progress.Report(new(percentDone,
                                $"{i + 1}/{totalCount} files. Processing {FilesFolders.TruncateFileNameForDisplay(imageRow.File, imageRow.RelativePath, 50)}", true, false));
                            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                        }

                        // Check that file exists
                        string filePath = Path.Combine(rootPath, imageRow.RelativePath, imageRow.File);
                        if (!System.IO.File.Exists(filePath))
                        {
                            skipped++;
                            continue;
                        }

                        // Skip AVI files as they have very limited metadata support
                        string extension = Path.GetExtension(imageRow.File).ToLowerInvariant();
                        if (extension == ".avi")
                        {
                            skipped++;
                            continue;
                        }

                        // Build metadata list for this file - only XMP-TimelapseData fields
                        var metadata = new List<KeyValuePair<string, string>>();

                        // Add individual XMP-TimelapseData fields
                        foreach (string dataLabel in selectedDataLabels)
                        {
                            string value = imageRow.GetValueDatabaseString(dataLabel);
                            string sanitizedDataLabel = SanitizeDataLabelForPerl(dataLabel);
                            string xmpTag = $"{Constant.XMP.Namespace}:{sanitizedDataLabel}";
                            metadata.Add(new KeyValuePair<string, string>(xmpTag, value ?? string.Empty));
                        }

                        // Add JSON field if checkbox is checked
                        if (includeJsonMetadata && selectedDataLabels.Count > 0)
                        {
                            string jsonValue = BuildJsonMetadata(imageRow, selectedDataLabels);
                            string jsonTag = $"{Constant.XMP.Namespace}:json";
                            metadata.Add(new KeyValuePair<string, string>(jsonTag, jsonValue));
                        }

                        // Write metadata via batch writer
                        var response = batchWriter.WriteFileMetadata(filePath, metadata, overwriteOriginal: true);

                        if (response.IsSuccess)
                        {
                            written++;
                        }
                        else
                        {
                            // For some reason writing failed
                            skipped++;
                        }
                    }
                });

                CleanupAfterWritingMetadata(startTime, written, totalCount, skipped, configFilePath);
                cleanupDone = true;

                if (batchWriter != null)
                {
                    await System.Threading.Tasks.Task.Run(() => batchWriter.Dispose());
                    batchWriter = null; // Mark as disposed to prevent double-disposal in finally
                }
            }
            catch (Exception ex)
            {
                AppendProgressOnUIThread($"\n✗ ERROR: {ex.Message}\n");
                MessageBox.Show(
                    $"Error during batch write:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Ensure ExifTool is disposed even if there was an error
                if (batchWriter != null)
                {
                    try
                    {
                        batchWriter.Dispose();
                    }
                    catch
                    {
                        // Suppress disposal errors
                    }
                }

                if (!cleanupDone)
                {
                    CleanupAfterWritingMetadata(startTime, written, totalCount, skipped, configFilePath);
                }
            }
        }

        #endregion

        #region Progress and Results Display

        private void CleanupAfterWritingMetadata(DateTime startTime, int written, int totalCount, int skipped, string configFilePath)
        {
            // Calculate final statistics
            var totalElapsed = DateTime.Now - startTime;
            var avgTimePerFile = written > 0 ? totalElapsed.TotalMilliseconds / written : 0;
            this.ProgressPanel.Visibility = Visibility.Visible;
            this.AvailableDataLabelsPanel.Visibility = Visibility.Collapsed;

            // Show summary (statistics appear immediately, before disposal delay)
            string operationState = Token.IsCancellationRequested
                ? "⚠ Writing partially done, as cancel was requested mid-opertion"
                : "✅ Writing metadata completed";
            AppendProgressOnUIThread($"{operationState}{Environment.NewLine}");
            AppendProgressOnUIThread($"✓ Written: {written} files{Environment.NewLine}");
            AppendProgressOnUIThread($"⊗ Skipped: {skipped} files e.g., cannot write to missing or video (.avi) files{Environment.NewLine}");
            if (totalCount - written - skipped > 0)
            {
                AppendProgressOnUIThread($"⚠️ Not processed as cancelled: {totalCount - written - skipped} files{Environment.NewLine}");
            }
            AppendProgressOnUIThread($"⏱ Total time: {totalElapsed:mm\\:ss}{Environment.NewLine}");
            AppendProgressOnUIThread($"⚡ Average: {avgTimePerFile:F1}ms per file{Environment.NewLine}");

            // Display selected field summary
            BuildAndDisplaySelectedFieldsSummary(checkBoxDataLabelMap,
                "selected data labels and their values were written as XMP-TimelapseData metadata:");

            // Populate and show the metadata grid with XMP-TimelapseData only
            // Use Dispatcher.BeginInvoke to ensure grid population happens after all other UI updates complete,
            // preventing any automatic refresh from overwriting our filtered data
            Dispatcher.BeginInvoke(new Action(PopulateMetadataGrid),
                System.Windows.Threading.DispatcherPriority.Background);

            // Re-enable UI after completion
            WriteMetadataButton.Visibility = Visibility.Collapsed;
            WriteMetadataToAllButton.Visibility = Visibility.Collapsed;
            TextBlockExportLabel.Visibility = Visibility.Collapsed;
            BusyCancelIndicator.IsBusy = false;
            WindowCloseButtonIsEnabled(true);
            CloseButton.Content = "Close";

            if (!string.IsNullOrEmpty(configFilePath) && System.IO.File.Exists(configFilePath))
            {
                try
                {
                    System.IO.File.Delete(configFilePath);
                }
                catch
                {
                    // Suppress cleanup errors
                }
            }
        }

        private void PopulateMetadataGrid()
        {
            try
            {
                // Get the current file's path
                var currentImage = timelapseWindow?.DataHandler?.ImageCache?.Current;
                if (currentImage == null)
                {
                    return;
                }

                string filePath = currentImage.GetFilePath(timelapseWindow.RootPathToImages);
                if (!System.IO.File.Exists(filePath))
                {
                    return;
                }

                // Start ExifTool if not already started
                MetadataGrid.ExifToolManager.StartIfNotAlreadyStarted();

                // Fetch metadata using ExifTool directly (don't set viewModel.FilePath to avoid triggering auto-refresh)
                Dictionary<string, DataStructures.ImageMetadata> allMetadata = MetadataGrid.ExifToolManager.FetchExifFrom(filePath);

                // Filter to only include XMP-TimelapseData entries
                var filteredMetadata = new System.Collections.ObjectModel.ObservableCollection<Controls.FileMetadataGrid.DataContents>();

                foreach (KeyValuePair<string, DataStructures.ImageMetadata> metadata in allMetadata)
                {
                    // Only include entries from XMP-TimelapseData directory
                    if (metadata.Value.Directory == "XMP-TimelapseData")
                    {
                        filteredMetadata.Add(new Controls.FileMetadataGrid.DataContents(
                            metadata.Key,
                            metadata.Value.Directory,
                            metadata.Value.Name,
                            metadata.Value.Value,
                            string.Empty));
                    }
                }

                // Show the grid and header if we have XMP-TimelapseData entries
                if (filteredMetadata.Count > 0)
                {
                    // Make grid header and grid visible
                    MetadataGridHeader.Visibility = Visibility.Visible;
                    MetadataGrid.Visibility = Visibility.Visible;
                    MetadataGrid.SortByXmpTimelapseDataButton.Visibility = Visibility.Collapsed;
                    MetadataGrid.XmpTimelapseDataLabel.Visibility = Visibility.Collapsed;

                    // Clear existing data and add filtered items to trigger collection change notifications
                    MetadataGrid.viewModel.MetadataList.Clear();
                    foreach (var item in filteredMetadata)
                    {
                        MetadataGrid.viewModel.MetadataList.Add(item);
                    }

                    // Set the file path AFTER populating data to update the "Example file:" label
                    // This must be done after populating to avoid triggering auto-refresh
                    MetadataGrid.viewModel.RootPath = timelapseWindow.RootPathToImages;
                    MetadataGrid.viewModel.FilePath = filePath;

                    // Refresh and sort the grid
                    MetadataGrid.AvailableMetadataDataGrid.Items.Refresh();
                    MetadataGrid.ApplySort(MetadataGrid.AvailableMetadataDataGrid.Columns[2],
                        System.ComponentModel.ListSortDirection.Ascending);
                }
                else
                {
                    // No XMP-TimelapseData found, hide the grid
                    MetadataGridHeader.Visibility = Visibility.Collapsed;
                    MetadataGrid.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the dialog
                System.Diagnostics.Debug.WriteLine($"Error populating metadata grid: {ex.Message}");
            }
        }

        /// <summary>
        /// Append text to progress TextBox from any thread (marshals to UI thread if needed)
        /// </summary>
        private void AppendProgressOnUIThread(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                // Already on UI thread
                ProgressTextBox.AppendText(text);
                ProgressTextBox.ScrollToEnd();
            }
            else
            {
                // Marshal to UI thread
                Dispatcher.Invoke(() =>
                {
                    ProgressTextBox.AppendText(text);
                    ProgressTextBox.ScrollToEnd();
                });
            }
        }

        /// <summary>
        /// Build and display summary of selected fields from a checkbox map
        /// </summary>
        private void BuildAndDisplaySelectedFieldsSummary(Dictionary<CheckBox, string> checkBoxMap, string headerText)
        {
            StringBuilder message = new StringBuilder();
            int count = 0;

            foreach (KeyValuePair<CheckBox, string> kvp in checkBoxMap)
            {
                if (kvp.Key.IsChecked == true)
                {
                    message.Append($"{kvp.Value}{Environment.NewLine}");
                    count++;
                }
            }

            if (count > 0)
            {
                AppendProgressOnUIThread($"{Environment.NewLine}{count} {headerText}{Environment.NewLine}");
                AppendProgressOnUIThread(message.ToString());
            }
        }

        #endregion

    }
}
