using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.ExifTool;
using Timelapse.Extensions;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;
using DatabaseColumn = Timelapse.Constant.DatabaseColumn;
using ImageMetadata = Timelapse.DataStructures.ImageMetadata;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for FileMetadataGrid.xaml
    /// </summary>
    public partial class FileMetadataGrid
    {
        #region Private Variables
        // Placeholder text shown in dropdown when no compatible data fields are available
        public const string NoCompatibleFieldsPlaceholder = "No compatible data fields available";

        // Collects the various metadata attributes from the file. The Key is the complete metadata name
        private Dictionary<string, ImageMetadata> metadataDictionary;

        public bool HideMetadataKindColumn
        {
            set =>
                AvailableMetadataDataGrid.Columns[1].Visibility = value
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }
        #endregion

        #region Public properties
        // Track the current sort state
        public DataGridColumn CurrentSortColumn { get; private set; }
        public System.ComponentModel.ListSortDirection CurrentSortDirection { get; private set; } = System.ComponentModel.ListSortDirection.Ascending;

        // Whether the metadataExtractor tool is selected (false means the ExifTool)
        public MetadataToolEnum MetadataToolSelected =>
            MetadataExtractorRB.IsChecked == true
                ? MetadataToolEnum.MetadataExtractor
                : MetadataToolEnum.ExifTool;

        // A handle to the ExifTool Manager
        public ExifToolManager ExifToolManager => GlobalReferences.TimelapseState?.ExifToolManager;

        // A dictionary derived from the Note fields, where the key is a data field's DataLabel and its value is the Label
        // And empty slot is included
        public Dictionary<string, string> DictDataLabel_Label
        {
            get;
            set
            {
                field = value;
                // Note labels are a list of labels, with an Empty slot in the beginning to allow labels to be deselected
                viewModel.NoteLabels = new(field.Values);
                viewModel.NoteLabels.Insert(0, string.Empty);
            }
        }

        // A dictionary to map DataLabel to ControlType for filtering
        private Dictionary<string, string> dictDataLabel_ControlType = [];

        // A dictionary to map DataLabel to Choices for FixedChoice/MultiChoice filtering
        private Dictionary<string, Choices> dictDataLabel_Choices = [];

        /// <summary>
        /// Sets the control type mapping for data labels.
        /// This is used by PopulateAvailableLabelsForRow to filter controls based on metadata value type.
        /// Updates the available labels for all existing rows after setting the mapping.
        /// </summary>
        public void SetControlTypeMapping(Dictionary<string, string> dataLabelToControlType)
        {
            dictDataLabel_ControlType = dataLabelToControlType ?? [];

            // Refresh available labels for all existing rows now that we have the control type mapping
            foreach (DataContents row in viewModel.MetadataList)
            {
                PopulateAvailableLabelsForRow(row);
            }
        }

        /// <summary>
        /// Sets the choices mapping for data labels with FixedChoice/MultiChoice controls.
        /// This is used by PopulateAvailableLabelsForRow to filter controls based on whether the metadata value matches the choice list.
        /// </summary>
        public void SetChoicesMapping(Dictionary<string, Choices> dataLabelToChoices)
        {
            dictDataLabel_Choices = dataLabelToChoices ?? [];
        }

        // Show or hide the DataLabel Column. If we are just inspecting the metadata, we don't need to show that column
        public bool HideDataLabelColumn
        {
            set =>
                AvailableMetadataDataGrid.Columns[4].Visibility = value
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        // Method to hide the Data field and Type columns
        public void HideDataFieldColumn()
        {
            AvailableMetadataDataGrid.Columns[4].Visibility = Visibility.Collapsed;  // Data field column
            AvailableMetadataDataGrid.Columns[5].Visibility = Visibility.Collapsed;  // Type column
        }

        // Property to hide just the Type column (keep Data field column visible)
        public bool HideTypeColumn
        {
            set =>
                AvailableMetadataDataGrid.Columns[5].Visibility = value
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }


        // If UseDateMetadata only is true, then only show metadata fields whose values are parseable as dates.
        // Otherwise all found metadata fields will be displayed
        public ImageMetadataFiltersEnum ImageMetadataFilter { get; set; } = ImageMetadataFiltersEnum.AllMetadata;

        // A collection of selectedMetadata and Tags
        public ObservableCollection<SelectedMetadataItem> SelectedMetadata { get; set; }

        // Returns a list of selected metadata tags
        public string[] SelectedTags
        {
            get
            {
                List<string> tagList = [];
                foreach (SelectedMetadataItem item in SelectedMetadata)
                {
                    tagList.Add(item.MetadataTag);
                }
                return tagList.ToArray();
            }
        }

        // The ViewModel, used to populate the grid and to reflect any changed values
#pragma warning disable IDE1006 // Naming Styles
        public ViewModel viewModel { get; set; } = new();
#pragma warning restore IDE1006 // Naming Styles
        #endregion

        #region Initialization, Loaded
        public FileMetadataGrid()
        {
            SelectedMetadata = GetSelectedFromMetadataList(viewModel.MetadataList, null);
            DataContext = viewModel;
            InitializeComponent();

            // Initializations...
            metadataDictionary = new Dictionary<string, ImageMetadata>();
            DictDataLabel_Label = [];
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Don't execute runtime code in the designer
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            // Initialize default sort state (column 2 = Metadata name)
            CurrentSortColumn = AvailableMetadataDataGrid.Columns[2];
            CurrentSortDirection = System.ComponentModel.ListSortDirection.Ascending;

            // Show the metadata of the current image, depending on the kind of tool selected
            MetadataToolType_Checked(null, null);

            // Add callbacks to the radio buttons. We do it here so they are not invoked when the window is loaded.
            MetadataExtractorRB.Checked += MetadataToolType_Checked;
            ExifToolRB.Checked += MetadataToolType_Checked;

            // Set the tooltip to the cell's contents
            Style CellStyle_ToolTip = new();
            var CellSetter = new Setter(ToolTipProperty, new Binding { RelativeSource = new(RelativeSourceMode.Self), Path = new("Content.Text") });
            CellStyle_ToolTip.Setters.Add(CellSetter);
            AvailableMetadataDataGrid.CellStyle = CellStyle_ToolTip;

            // Subscribe to MetadataList changes to update button state
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateAssignXmpButtonState();
        }
        #endregion

        #region Refresh the grid
        public void Refresh(bool applyDefaultSort = true)
        {

            if (MetadataToolSelected == MetadataToolEnum.MetadataExtractor)
            {
                MetadataExtractorShowImageMetadata(applyDefaultSort);
            }
            else
            {
                ExifToolShowImageMetadata(applyDefaultSort);
            }
        }
        #endregion

        #region MetadataExtractor-specific methods
        // Retrieve and show a single image's metadata in the datagrid
        private void MetadataExtractorShowImageMetadata(bool applyDefaultSort = true)
        {
            // Get the metadata
            metadataDictionary = ImageMetadataDictionary.LoadMetadata(viewModel.FilePath);

            ObservableCollection<DataContents> temp = [];

            // In order to populate the datagrid, we have to unpack the dictionary as a list containing four values, plus a fifth item that represents the empty datalabel as ComboBox
            foreach (KeyValuePair<string, ImageMetadata> metadata in metadataDictionary)
            {
                // Reconyx cameras, for some reason, do not include the "Exif IFD0.Date/Time" tag, which should be there.
                // Instead, they include a Reconyx HyperFire Makernote.Date/Time Original flag.
                // Both should contain the valid date. Nothing needs to be done here, but just thought I would mention it.
                // So we need an extra check to see if

                // If ImageMetadataFilter is DatesOnly, then only show metadata fields whose values are parseable as dates.
                if (metadata.Value?.Value != null)
                {
                    if (ImageMetadataFilter == ImageMetadataFiltersEnum.DatesOnly
                        && false == DateTimeHandler.TryParseMetadataDateTaken(metadata.Value.Value, out DateTime _))
                    {
                        // Not a date, but date is required, so skip it
                        continue;
                    }
                    DataContents row = new(metadata.Key, metadata.Value.Directory, metadata.Value.Name, metadata.Value.Value, string.Empty);
                    PopulateAvailableLabelsForRow(row);
                    temp.Add(row);
                }
            }
            viewModel.MetadataList = temp;
            AddNoMetadataPlaceholderRowIfEmpty();
            if (applyDefaultSort)
            {
                AvailableMetadataDataGrid.SortByColumnAscending(2);
            }
        }
        #endregion

        #region ExifTool-specific methods
        private void ExifToolShowImageMetadata(bool applyDefaultSort = true)
        {
            // Don't execute if ExifToolManager is not available (e.g., in designer)
            if (ExifToolManager == null)
            {
                return;
            }

            // Clear the data structures so we get fresh contents
            metadataDictionary.Clear();

            // Start the exifTool process if its not already started
            ExifToolManager.StartIfNotAlreadyStarted();

            // Fetch the exif data using ExifTool (now returns Dictionary<string, ImageMetadata>)
            Dictionary<string, ImageMetadata> exifDictionary = ExifToolManager.FetchExifFrom(viewModel.FilePath);

            // In order to populate the metadataDictionary and datagrid, we have to unpack the ExifTool dictionary, recreate the dictionary, and create a list containing four values
            ObservableCollection<DataContents> temp = [];
            foreach (KeyValuePair<string, ImageMetadata> metadata in exifDictionary)
            {
                // If UseDateMetadata only is true, then only show metadata fields whose values are parseable as dates.
                if (ImageMetadataFilter == ImageMetadataFiltersEnum.DatesOnly
                    && false == DateTimeHandler.TryParseMetadataDateTaken(metadata.Value.Value, out DateTime _))
                {
                    continue;
                }
                DataContents row = new(metadata.Key, metadata.Value.Directory, metadata.Value.Name, metadata.Value.Value, string.Empty);
                PopulateAvailableLabelsForRow(row);
                temp.Add(row);
            }
            viewModel.MetadataList = temp;

            // If there is no metadata, inform the user by setting bogus dictionary values which will appear on the grid
            AddNoMetadataPlaceholderRowIfEmpty();
            if (applyDefaultSort)
            {
                AvailableMetadataDataGrid.SortByColumnAscending(2);
            }
        }
        #endregion

        #region Placeholder for empty metadata list
        /// <summary>
        /// Add a placeholder row if the metadata list is empty telling the user that no metadata was found
        /// </summary>
        private void AddNoMetadataPlaceholderRowIfEmpty()
        {
            if (viewModel.MetadataList.Count == 0)
            {
                viewModel.MetadataList.Add(new DataContents(
                    string.Empty,
                    "Empty",
                    "No metadata found in the currently displayed file",
                    "Navigate to a displayable image",
                    string.Empty));
            }
        }
        #endregion

        #region Checkbox callbacks
        // Checkbox callback sets which metadata tool should be used
        private void MetadataToolType_Checked(object sender, RoutedEventArgs e)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            if (MetadataExtractorRB.IsChecked == true)
            {
                MetadataExtractorShowImageMetadata();
            }
            else
            {
                ExifToolShowImageMetadata();
            }
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region Combobox callback (in DataGrid)
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { DataContext: DataContents currentRow })
            {
                string selectedLabel = currentRow.AssignedLabel;

                // Prevent the placeholder from being selected
                if (selectedLabel == NoCompatibleFieldsPlaceholder)
                {
                    currentRow.AssignedLabel = string.Empty;
                    return;
                }

                // If a label was selected (not empty), set the type and clear it from other rows
                if (!string.IsNullOrEmpty(selectedLabel))
                {
                    // Find the dataLabel that corresponds to this label (reverse lookup)
                    string dataLabel = DictDataLabel_Label.FirstOrDefault(kvp => kvp.Value == selectedLabel).Key;

                    // Get the control type for this dataLabel and set it in AssignedType
                    if (!string.IsNullOrEmpty(dataLabel) && dictDataLabel_ControlType.TryGetValue(dataLabel, out string controlType))
                    {
                        currentRow.AssignedType = controlType;
                    }

                    // Clear the same label from all other rows to ensure uniqueness
                    foreach (DataContents row in viewModel.MetadataList)
                    {
                        // Skip the current row
                        if (row == currentRow)
                            continue;

                        // If another row has the same label selected, clear it
                        if (row.AssignedLabel == selectedLabel)
                        {
                            row.AssignedLabel = string.Empty;
                            row.AssignedType = string.Empty;
                        }
                    }
                }
                else
                {
                    // Label was cleared, also clear the type
                    currentRow.AssignedType = string.Empty;
                }

                // Update SelectedMetadata against the new contents, which in turn may trigger a CollectionChanged event
                SelectedMetadata = GetSelectedFromMetadataList(viewModel.MetadataList, SelectedMetadata);
            }
        }
        #endregion

        #region Static Helpers
        // Get the data label that first matches the label in the DictDataLabel_Label dictionary
        // Note that this is like a reverse dictionary (where we look up the key from its value).
        // This is done because its possible that labels aren't unique
        // (while later versions of Timelapse ensures that templates have unique labels, earlier templates may not)
        private string GetDataLabelFromLabel(string label)
        {
            foreach (KeyValuePair<string, string> kvp in DictDataLabel_Label)
            {
                if (kvp.Value == label)
                {
                    return kvp.Key;
                }
            }
            return string.Empty;
        }
        // Return a collection of metadata items comprised only of matching metadata fields and a non-empty data label
        private ObservableCollection<SelectedMetadataItem> GetSelectedFromMetadataList(ObservableCollection<DataContents> metadataList, ObservableCollection<SelectedMetadataItem> selectedMetadata)
        {
            selectedMetadata ??= [];
            selectedMetadata.Clear();
            foreach (DataContents dc in metadataList)
            {
                if (false == string.IsNullOrWhiteSpace(dc.AssignedLabel))
                {
                    // We have a non-empty data label, so add it with metadata tag, data label, and type
                    selectedMetadata.Add(new(dc.MetadataKey, GetDataLabelFromLabel(dc.AssignedLabel), dc.AssignedType ?? string.Empty));
                }
            }
            return selectedMetadata;
        }
        #endregion

        #region Per-row label filtering based on metadata value type
        /// <summary>
        /// Populate the AvailableLabels for a DataContents row based on the metadata value type.
        /// Filters the available data fields according to whether the value is a decimal, integer, or string.
        /// </summary>
        private void PopulateAvailableLabelsForRow(DataContents row)
        {
            if (row == null)
            {
                return;
            }

            row.AvailableLabels.Clear();

            // Always include empty option first
            row.AvailableLabels.Add(string.Empty);

            // Determine which control types are allowed based on the metadata value
            // Note: Integer and Decimal are mutually exclusive per user requirements
            bool isDecimal = false;
            bool isInteger = false;
            bool isPositive = false;
            bool isBoolean = false;
            bool isDateTime = false;
            bool isAlphaNumeric = false;

            if (row.MetadataValue != null)
            {
                string trimmedValue = row.MetadataValue.Trim();

                // Check if it's a boolean value (true/false, case-insensitive)
                if (trimmedValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    trimmedValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    isBoolean = true;
                }

                // Check if it's a parsable date/time value (independent check)
                if (DateTimeHandler.TryParseMetadataDateTaken(trimmedValue, out DateTime _))
                {
                    isDateTime = true;
                }

                // Check if it's an alphanumeric value (independent check)
                if (IsCondition.IsAlphaNumeric(trimmedValue))
                {
                    isAlphaNumeric = true;
                }

                // Numbers
                if (trimmedValue == string.Empty)
                {
                    // empty numbers are allowed
                    isPositive = true;
                    isInteger = true;
                    isDecimal = true;
                }
                else
                {
                    // Try to parse as a number (independent check) or as an empty string as empty numbers are allowed
                    if (decimal.TryParse(trimmedValue, out var numericValue))
                    {
                        isPositive = numericValue > 0;

                        // Check if it's an integer (no fractional part)
                        if (numericValue == Math.Floor(numericValue))
                        {
                            // It's an integer
                            isInteger = true;
                        }
                        else
                        {
                            // It's a decimal with fractional part
                            isDecimal = true;
                        }
                    }
                }
            }

            // Add labels from DictDataLabel_Label based on control type rules
            foreach (KeyValuePair<string, string> kvp in DictDataLabel_Label)
            {
                string dataLabel = kvp.Key;
                string label = kvp.Value;

                // Get the control type for this data label
                if (!dictDataLabel_ControlType.TryGetValue(dataLabel, out string controlType) ||
                    string.IsNullOrEmpty(controlType))
                {
                    continue;
                }

                // Apply filtering rules based on metadata value type
                bool shouldInclude = false;

                if (isBoolean)
                {
                    // Boolean values (true/false) can go into: Note, AlphaNumeric, MultiLine, Flag
                    if (controlType == Control.Note ||
                        controlType == Control.AlphaNumeric ||
                        controlType == Control.MultiLine ||
                        controlType == Control.Flag)
                    {
                        shouldInclude = true;
                    }
                }
                else if (isDateTime)
                {
                    // DateTime values can go into: Note, MultiLine, DateTime, DateTime_, Date_, Time_
                    if (controlType == Control.Note ||
                        controlType == Control.MultiLine ||
                        controlType == DatabaseColumn.DateTime ||
                        controlType == Control.DateTime_ ||
                        controlType == Control.Date_ ||
                        controlType == Control.Time_)
                    {
                        shouldInclude = true;
                    }
                }
                else if (isInteger)
                {
                    // Integer values can go into: Note, MultiLine, IntegerAny, DecimalAny
                    // If positive: also IntegerPositive, Counter, DecimalPositive
                    // Note: Integers are valid decimals, so they can go into decimal fields too
                    if (controlType == Control.Note ||
                        controlType == Control.MultiLine ||
                        controlType == Control.IntegerAny ||
                        controlType == Control.DecimalAny)
                    {
                        shouldInclude = true;
                    }
                    else if (isPositive &&
                            (controlType == Control.IntegerPositive ||
                             controlType == Control.Counter ||
                             controlType == Control.DecimalPositive))
                    {
                        shouldInclude = true;
                    }
                }
                else if (isDecimal)
                {
                    // Decimal values (with fractional part) can go into: Note, MultiLine, DecimalAny
                    // If positive: also DecimalPositive
                    // Note: Integer fields are NOT included for decimal values
                    if (controlType == Control.Note ||
                        controlType == Control.MultiLine ||
                        controlType == Control.DecimalAny)
                    {
                        shouldInclude = true;
                    }
                    else if (isPositive && controlType == Control.DecimalPositive)
                    {
                        shouldInclude = true;
                    }
                }
                else
                {
                    // Everything else (strings) can only go into Note or MultiLine
                    if (controlType == Control.Note ||
                        controlType == Control.MultiLine)
                    {
                        shouldInclude = true;
                    }
                }

                // Alphanumeric check (independent of other type checks)
                if (isAlphaNumeric && controlType == Control.AlphaNumeric)
                {
                    shouldInclude = true;
                }

                // FixedChoice and MultiChoice check (independent of other type checks)
                if (controlType == Control.FixedChoice || controlType == Control.MultiChoice)
                {
                    // Check if we have choices for this data label
                    if (dictDataLabel_Choices.TryGetValue(dataLabel, out Choices choices))
                    {
                        string trimmedValue = row.MetadataValue?.Trim() ?? string.Empty;

                        if (controlType == Control.FixedChoice)
                        {
                            // For FixedChoice: value must be in choice list or empty (if allowed)
                            if (string.IsNullOrEmpty(trimmedValue) && choices.IncludeEmptyChoice)
                            {
                                shouldInclude = true;
                            }
                            else if (choices.ChoiceList.Contains(trimmedValue))
                            {
                                shouldInclude = true;
                            }
                        }
                        else if (controlType == Control.MultiChoice)
                        {
                            // For MultiChoice: split by comma and check each value
                            bool allValuesValid = true;

                            if (string.IsNullOrEmpty(trimmedValue))
                            {
                                // Empty value is valid if IncludeEmptyChoice is true
                                allValuesValid = choices.IncludeEmptyChoice;
                            }
                            else
                            {
                                string[] values = trimmedValue.Split(',');
                                foreach (string val in values)
                                {
                                    string trimmedVal = val.Trim();
                                    if (!choices.ChoiceList.Contains(trimmedVal))
                                    {
                                        allValuesValid = false;
                                        break;
                                    }
                                }
                            }

                            if (allValuesValid)
                            {
                                shouldInclude = true;
                            }
                        }
                    }
                }

                if (shouldInclude)
                {
                    row.AvailableLabels.Add(label);
                }
            }

            // If only the empty string was added (no compatible fields), add a placeholder
            if (row.AvailableLabels.Count == 1)
            {
                row.AvailableLabels.Add(NoCompatibleFieldsPlaceholder);
            }
        }
        #endregion

        #region Class ViewModel
        public class ViewModel : ViewModelBase
        {
            // The root path to the database (used to trim the FilePath for display)
            public string RootPath
            {
                get;
                set
                {
                    SetProperty(ref field, value);
                    OnPropertyChanged(nameof(FileNameDisplayable));
                }
            }

            // The full path of the file
            public string FilePath
            {
                get;
                set
                {
                    SetProperty(ref field, value);
                    FileName = Path.GetFileName(field);
                    OnPropertyChanged(nameof(FileNameDisplayable));
                }
            }

            // Only the file name (i.e., strip off the path, if any)
            public string FileName
            {
                get;
                set
                {
                    SetProperty(ref field, value);
                    OnPropertyChanged(nameof(FileNameDisplayable));
                }
            }

            // A Path/Filename value, truncated if needed
            // Trims the RootPath from FilePath before displaying
            public string FileNameDisplayable
            {
                get
                {
                    string pathToDisplay = FilePath;
                    if (!string.IsNullOrEmpty(RootPath) && !string.IsNullOrEmpty(FilePath))
                    {
                        // Remove the root path from the beginning if it matches
                        if (FilePath.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
                        {
                            pathToDisplay = FilePath.Substring(RootPath.Length);
                            // Remove leading path separator if present
                            if (pathToDisplay.StartsWith(Path.DirectorySeparatorChar.ToString()) ||
                                pathToDisplay.StartsWith(Path.AltDirectorySeparatorChar.ToString()))
                            {
                                pathToDisplay = pathToDisplay.Substring(1);
                            }
                        }
                    }

                    // Extract just the directory path (without the filename)
                    string directoryPath = Path.GetDirectoryName(pathToDisplay) ?? string.Empty;

                    // TruncateFileNameForDisplay expects: (fileName, path, length)
                    return Util.FilesFolders.TruncateFileNameForDisplay(FileName, directoryPath, 80);
                }
            }

            public ObservableCollection<string> NoteLabels
            {
                get;
                set => SetProperty(ref field, value);
            } = [];

            public ObservableCollection<DataContents> MetadataList
            {
                get;
                set => SetProperty(ref field, value);
            } = [];
        }
        #endregion

        #region Class: DataContents: A class defining the data model behind each row in the AvailableMetadataDataGrid
        public class DataContents(string metadataKey, string metadataKind, string metadataName, string metadataValue, string assignedDataLabel)
            : ViewModelBase
        {
            public string MetadataKey { get; set; } = metadataKey;
            public string MetadataKind { get; set; } = metadataKind;
            public string MetadataName { get; set; } = metadataName;
            public string MetadataValue { get; set; } = metadataValue;

            public string AssignedLabel
            {
                get;
                set => SetProperty(ref field, value);
            } = assignedDataLabel;

            public string AssignedType
            {
                get;
                set => SetProperty(ref field, value);
            }

            // Per-row list of available labels for the Data field dropdown
            // This list is filtered based on the metadata value type
            public ObservableCollection<string> AvailableLabels { get; set; } = [];
        }
        #endregion

        #region DataGrid Sorting

        /// <summary>
        /// Apply sort to a specific column with a specific direction
        /// </summary>
        public void ApplySort(DataGridColumn column, System.ComponentModel.ListSortDirection direction)
        {
            if (column == null) return;

            // Get the sort member path
            var sortBy = column.SortMemberPath;

            // Perform the sort with secondary sorting
            IEnumerable<DataContents> sortedList;

            if (sortBy == "MetadataKind")
            {
                // Primary sort by MetadataKind (directory), secondary by MetadataName
                // Use ordinal string comparison for consistent alphabetical sorting
                sortedList = direction == System.ComponentModel.ListSortDirection.Ascending
                    ? viewModel.MetadataList.OrderBy(x => x.MetadataKind, StringComparer.Ordinal).ThenBy(x => x.MetadataName, StringComparer.Ordinal)
                    : viewModel.MetadataList.OrderByDescending(x => x.MetadataKind, StringComparer.Ordinal).ThenByDescending(x => x.MetadataName, StringComparer.Ordinal);
            }
            else if (sortBy == "MetadataName")
            {
                // Sort by MetadataName only
                // Use ordinal string comparison for consistent alphabetical sorting
                sortedList = direction == System.ComponentModel.ListSortDirection.Ascending
                    ? viewModel.MetadataList.OrderBy(x => x.MetadataName, StringComparer.Ordinal)
                    : viewModel.MetadataList.OrderByDescending(x => x.MetadataName, StringComparer.Ordinal);
            }
            else
            {
                // No sorting - column is not sortable
                return;
            }

            // Update the collection
            viewModel.MetadataList = new ObservableCollection<DataContents>(sortedList);

            // Update tracked sort state (needed for preserving sort during navigation)
            CurrentSortColumn = column;
            CurrentSortDirection = direction;

            // Set the sort direction indicators AFTER updating the collection
            // (updating the collection can clear the sort indicators)
            foreach (var col in AvailableMetadataDataGrid.Columns)
            {
                col.SortDirection = null;
            }
            column.SortDirection = direction;
        }

        private void AvailableMetadataDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Prevent default sorting
            e.Handled = true;

            var column = e.Column;
            System.ComponentModel.ListSortDirection direction;

            // Determine sort direction based on our tracked state (not column.SortDirection)
            if (CurrentSortColumn == column)
            {
                // Same column clicked - toggle direction
                direction = CurrentSortDirection == System.ComponentModel.ListSortDirection.Ascending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending;
            }
            else
            {
                // Different column clicked - start with ascending
                direction = System.ComponentModel.ListSortDirection.Ascending;
            }

            // Update tracked state and column indicator
            CurrentSortColumn = column;
            CurrentSortDirection = direction;
            column.SortDirection = direction;

            // Get the sort member path
            var sortBy = column.SortMemberPath;

            // Perform the sort with secondary sorting
            IEnumerable<DataContents> sortedList;

            if (sortBy == "MetadataKind")
            {
                // Primary sort by MetadataKind (directory), secondary by MetadataName
                // Use ordinal string comparison for consistent alphabetical sorting
                sortedList = direction == System.ComponentModel.ListSortDirection.Ascending
                    ? viewModel.MetadataList.OrderBy(x => x.MetadataKind, StringComparer.Ordinal).ThenBy(x => x.MetadataName, StringComparer.Ordinal)
                    : viewModel.MetadataList.OrderByDescending(x => x.MetadataKind, StringComparer.Ordinal).ThenByDescending(x => x.MetadataName, StringComparer.Ordinal);
            }
            else if (sortBy == "MetadataName")
            {
                // Sort by MetadataName only
                // Use ordinal string comparison for consistent alphabetical sorting
                sortedList = direction == System.ComponentModel.ListSortDirection.Ascending
                    ? viewModel.MetadataList.OrderBy(x => x.MetadataName, StringComparer.Ordinal)
                    : viewModel.MetadataList.OrderByDescending(x => x.MetadataName, StringComparer.Ordinal);
            }
            else
            {
                // Shouldn't happen, but handle gracefully
                return;
            }

            // Update the collection
            var temp = new ObservableCollection<DataContents>(sortedList);
            viewModel.MetadataList = temp;
        }
        #endregion

        #region Assign XMP-TimelapseData Button
        /// <summary>
        /// Handle property changes in the ViewModel to update button state
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(viewModel.MetadataList))
            {
                UpdateAssignXmpButtonState();
            }
        }

        /// <summary>
        /// Update the enabled state of the XMP-TimelapseData buttons
        /// Enable only if there's at least one XMP-TimelapseData entry in the metadata group column
        /// </summary>
        private void UpdateAssignXmpButtonState()
        {
            bool hasXmpTimelapseData = viewModel.MetadataList.Any(item =>
                item.MetadataKind != null &&
                item.MetadataKind.Contains("XMP-TimelapseData", StringComparison.OrdinalIgnoreCase));

            AssignXmpTimelapseDataButton.IsEnabled = hasXmpTimelapseData;
            SortByXmpTimelapseDataButton.IsEnabled = hasXmpTimelapseData;

            // Update label color to match button state
            XmpTimelapseDataLabel.Foreground = hasXmpTimelapseData
                ? Brushes.Black
                : SystemColors.GrayTextBrush;
        }

        /// <summary>
        /// Button click handler for Assign XMP-TimelapseData
        /// Automatically assigns data fields to metadata entries where:
        /// - Metadata group contains "XMP-TimelapseData"
        /// - A data field exists with a label matching the metadata name (case-insensitive)
        /// </summary>
        private void AssignXmpTimelapseDataButton_Click(object sender, RoutedEventArgs e)
        {
            // For each row in the metadata list
            foreach (DataContents item in viewModel.MetadataList)
            {
                // Check if the Metadata group contains "XMP-TimelapseData"
                if (item.MetadataKind == null ||
                    !item.MetadataKind.Contains("XMP-TimelapseData", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if there's a data field that matches the metadata name (case-insensitive)
                string matchingDataLabel = FindMatchingDataLabel(item.MetadataName);

                if (!string.IsNullOrEmpty(matchingDataLabel))
                {
                    // Get the label for this data label
                    if (DictDataLabel_Label.TryGetValue(matchingDataLabel, out string label))
                    {
                        // Assign the label to this item
                        item.AssignedLabel = label;
                    }
                }
            }

            // Refresh the selected metadata collection
            SelectedMetadata = GetSelectedFromMetadataList(viewModel.MetadataList, SelectedMetadata);

            // Notify that the metadata list has changed to trigger UI update
            // Note: When creating a new collection, AvailableLabels are preserved since they're part of each DataContents object
            viewModel.MetadataList = new ObservableCollection<DataContents>(viewModel.MetadataList);

            // Sort and scroll to XMP-TimelapseData entries
            SortAndScrollToXmpTimelapseData();
        }

        /// <summary>
        /// Button click handler for Sort by XMP-TimelapseData
        /// Sorts the metadata grid by directory and scrolls to the first XMP-TimelapseData entry
        /// </summary>
        private void SortByXmpTimelapseDataButton_Click(object sender, RoutedEventArgs e)
        {
            SortAndScrollToXmpTimelapseData();
        }

        /// <summary>
        /// Sort the metadata grid by directory (ascending) and scroll to position
        /// the first XMP-TimelapseData entry about 2 rows below the top
        /// </summary>
        private void SortAndScrollToXmpTimelapseData()
        {
            // Sort by Metadata group (column 1) in ascending order
            DataGridColumn metadataKindColumn = AvailableMetadataDataGrid.Columns[1];
            ApplySort(metadataKindColumn, System.ComponentModel.ListSortDirection.Ascending);

            // Scroll to position XMP-TimelapseData entries near the top of the visible area
            int firstXmpIndex = -1;
            for (int i = 0; i < viewModel.MetadataList.Count; i++)
            {
                var item = viewModel.MetadataList[i];
                if (item.MetadataKind != null &&
                    item.MetadataKind.Contains("XMP-TimelapseData", StringComparison.OrdinalIgnoreCase))
                {
                    firstXmpIndex = i;
                    break;
                }
            }

            if (firstXmpIndex >= 0)
            {
                // Calculate approximate number of visible rows in the DataGrid
                // Using ActualHeight and estimating row height of ~28 pixels (typical for DataGrid rows)
                int estimatedVisibleRows = Math.Max(5, (int)(AvailableMetadataDataGrid.ActualHeight / 28));

                // Scroll to position XMP entries near the top (about 2-3 rows from top)
                // First scroll further down to establish viewport position
                int scrollToIndex = Math.Min(viewModel.MetadataList.Count - 1, firstXmpIndex + estimatedVisibleRows - 3);
                AvailableMetadataDataGrid.ScrollIntoView(viewModel.MetadataList[scrollToIndex]);
                AvailableMetadataDataGrid.UpdateLayout();

                // Then scroll back to the first XMP item to position it near the top
                AvailableMetadataDataGrid.ScrollIntoView(viewModel.MetadataList[firstXmpIndex]);
            }
        }

        /// <summary>
        /// Find a data label that matches the given metadata name (case-insensitive)
        /// </summary>
        /// <param name="metadataName">The metadata name to match</param>
        /// <returns>The matching data label, or empty string if no match found</returns>
        private string FindMatchingDataLabel(string metadataName)
        {
            if (string.IsNullOrEmpty(metadataName))
            {
                return string.Empty;
            }

            // Search through all data labels for a case-insensitive match
            foreach (KeyValuePair<string, string> kvp in DictDataLabel_Label)
            {
                if (string.Equals(kvp.Key, metadataName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return string.Empty;
        }
        #endregion
    }
}