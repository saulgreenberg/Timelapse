using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Timelapse.Enums;
using Timelapse.ExifTool;
using Timelapse.Util;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for MetadataGrid.xaml
    /// </summary>
    public partial class MetadataGrid : UserControl
    {
        #region Private Variables
        // Collects the various metadata attributes from the file. The Key is the complete metadata name 
        private Dictionary<string, ImageMetadata> metadataDictionary;

        private bool HideMetadataKindColumn
        {
            set
            {
                this.AvailableMetadataDataGrid.Columns[1].Visibility = (value == true)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }
        #endregion

        #region Public properties
        // Whether the metadataExtractor tool is selected (false means the ExifTool)
        public MetadataToolEnum MetadataToolSelected
        {
            get
            {
                return this.MetadataExtractorRB.IsChecked == true
                    ? MetadataToolEnum.MetadataExtractor
                    : MetadataToolEnum.ExifTool;
            }
        }

        // A handle to the ExifTool Manager
        public ExifToolManager ExifToolManager
        {
            get
            {
                return Util.GlobalReferences.TimelapseState.ExifToolManager;
            }
        }

        // A dictionary derived from the Note fields, where the key is a data field's DataLabel and its value is the Label
        // And empty slot is included
        private Dictionary<string, string> _dictDataLabel_Label;
        public Dictionary<string, string> DictDataLabel_Label
        {
            get => _dictDataLabel_Label;
            set
            {
                _dictDataLabel_Label = value;
                // Note labels are a list of labels, with an Empty slot in the beginning to allow labels to be deselected
                this.viewModel.NoteLabels = new ObservableCollection<string>(_dictDataLabel_Label.Values);
                this.viewModel.NoteLabels.Insert(0, String.Empty);
            }
        }

        // Show or hide the DataLabel Column. If we are just inspecting the metadata, we don't need to show that column
        public bool HideDataLabelColumn
        {
            set
            {
                this.AvailableMetadataDataGrid.Columns[4].Visibility = (value == true)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }


        // If UseDateMetadata only is true, then only show metadata fields whose values are parseable as dates.
        // Otherwise all found metadata fields will be displayed
        public bool UseDateMetadataOnly { get; set; } = false;

        // A collection of selectedMetadata and Tags
        public ObservableCollection<KeyValuePair<string, string>> SelectedMetadata { get; set; }

        // Returns a list of selected metadata tags
        public string[] SelectedTags
        {
            get
            {
                List<string> tagList = new List<string>();
                foreach (KeyValuePair<string, string> kvp in this.SelectedMetadata)
                {
                    tagList.Add(kvp.Key);
                }
                return tagList.ToArray();
            }
        }

        // The ViewModel, used to populate the grid and to reflect any changed values
#pragma warning disable IDE1006 // Naming Styles
        public ViewModel viewModel { get; set; } = new ViewModel();
#pragma warning restore IDE1006 // Naming Styles
        #endregion

        #region Initialization, Loaded
        public MetadataGrid()
        {
            this.SelectedMetadata = this.GetSelectedFromMetadataList(this.viewModel.MetadataList, this.SelectedMetadata);
            DataContext = viewModel;
            InitializeComponent();

            // Initializations...
            this.DictDataLabel_Label = new Dictionary<string, string>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Show the metadata of the current image, depending on the kind of tool selected
            this.MetadataToolType_Checked(null, null);

            // Add callbacks to the radio buttons. We do it here so they are not invoked when the window is loaded.
            this.MetadataExtractorRB.Checked += this.MetadataToolType_Checked;
            this.ExifToolRB.Checked += this.MetadataToolType_Checked;

            // Set the tooltip to the cell's contents
            Style CellStyle_ToolTip = new Style();
            var CellSetter = new Setter(DataGridCell.ToolTipProperty, new Binding() { RelativeSource = new RelativeSource(RelativeSourceMode.Self), Path = new PropertyPath("Content.Text") });
            CellStyle_ToolTip.Setters.Add(CellSetter);
            this.AvailableMetadataDataGrid.CellStyle = CellStyle_ToolTip;
        }
        #endregion

        #region Refresh the grid
        public void Refresh()
        {

            if (this.MetadataToolSelected == MetadataToolEnum.MetadataExtractor)
            {
                this.MetadataExtractorShowImageMetadata();
            }
            else
            {
                this.ExifToolShowImageMetadata();
            }
        }
        #endregion

        #region MetadataExtractor-specific methods
        // Retrieve and show a single image's metadata in the datagrid
        private void MetadataExtractorShowImageMetadata()
        {
            // Get the metadata
            this.metadataDictionary = ImageMetadataDictionary.LoadMetadata(this.viewModel.FilePath);


            // If there is no metadata, this is an easy way to inform the user
            if (this.metadataDictionary.Count == 0)
            {
                this.metadataDictionary.Add("Empty", new Timelapse.Util.ImageMetadata("Empty", "No metadata found in the currently displayed image", "Navigate to a displayable image"));
            }

            ObservableCollection<DataContents> temp = new ObservableCollection<DataContents>();

            // In order to populate the datagrid, we have to unpack the dictionary as a list containing four values, plus a fifth item that represents the empty datalabel as ComboBox
            foreach (KeyValuePair<string, ImageMetadata> metadata in this.metadataDictionary)
            {
                // Reconyx cameras, for some reason, do not include the "Exif IFD0.Date/Time" tag, which should be there.
                // Instead, they include a Reconyx HyperFire Makernote.Date/Time Original flag.
                // Both should contain the valid date. Nothing needs to be done here, but just thought I would mention it.
                // So we need an extra check to see if

                // If UseDateMetadata only is true, then only show metadata fields whose values are parseable as dates.
                if (false == this.UseDateMetadataOnly
                    || (metadata.Value?.Value != null && DateTimeHandler.TryParseMetadataDateTaken(metadata.Value.Value.ToString(), out DateTime _)))
                {
                    temp.Add(new DataContents(metadata.Key, metadata.Value.Directory, metadata.Value.Name, metadata.Value.Value, String.Empty));
                }
            }
            this.viewModel.MetadataList = temp;
            this.AvailableMetadataDataGrid.SortByColumnAscending(2);
        }
        #endregion

        #region ExifTool-specific methods
        private void ExifToolShowImageMetadata()
        {
            // Clear the data structures so we get fresh contents
            this.metadataDictionary.Clear();

            // Start the exifTool process if its not already started
            this.ExifToolManager.StartIfNotAlreadyStarted();

            // Fetch the exif data using ExifTool
            Dictionary<string, string> exifDictionary = this.ExifToolManager.FetchExifFrom(this.viewModel.FilePath);

            // If there is no metadata, inform the user by setting bogus dictionary values which will appear on the grid
            if (exifDictionary.Count == 0)
            {
                this.metadataDictionary.Add("Empty", new Timelapse.Util.ImageMetadata("Empty", "No metadata found in the currently displayed image", "Navigate to a displayable image"));
            }

            // In order to populate the metadataDictionary and datagrid , we have to unpack the ExifTool dictionary, recreate the dictionary, and create a list containing four values
            ObservableCollection<DataContents> temp = new ObservableCollection<DataContents>();
            foreach (KeyValuePair<string, string> metadata in exifDictionary)
            {
                // If UseDateMetadata only is true, then only show metadata fields whose values are parseable as dates.
                if (false == this.UseDateMetadataOnly
                    || DateTimeHandler.TryParseMetadataDateTaken(metadata.Value.ToString(), out DateTime _))
                {
                    temp.Add(new DataContents(metadata.Key, String.Empty, metadata.Key, metadata.Value, ""));
                }
            }
            this.viewModel.MetadataList = temp;
            this.AvailableMetadataDataGrid.SortByColumnAscending(2);
        }
        #endregion

        #region Checkbox callbacks
        // Checkbox callback sets which metadata tool should be used
        private void MetadataToolType_Checked(object sender, RoutedEventArgs e)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            if (this.MetadataExtractorRB.IsChecked == true)
            {
                this.MetadataExtractorShowImageMetadata();
            }
            else
            {
                this.ExifToolShowImageMetadata();
            }
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region Combobox callback (in DataGrid) 
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                // Clear other combobox fields whose selected value matches the current comboBox selection, 
                // which guarantees thatmetadatafields will be assigned to unique labels
                DataGridClearComboBoxesWithMatchingSelectedItem(this.AvailableMetadataDataGrid, cb, "Data field");


                // Update SelectedMetadata against the new contents, which in turn may trigger a CollectionChanged event
                this.SelectedMetadata = GetSelectedFromMetadataList(this.viewModel.MetadataList, this.SelectedMetadata);
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
            foreach (KeyValuePair<string, string> kvp in this.DictDataLabel_Label)
            {
                if (kvp.Value == label)
                {
                    return kvp.Key;
                }
            }
            return String.Empty;
        }
        // Return a collection of keyvalue pairs comprised only of matching metadata fields and a non-empty data label
        private ObservableCollection<KeyValuePair<string, string>> GetSelectedFromMetadataList(ObservableCollection<DataContents> metadataList, ObservableCollection<KeyValuePair<string, string>> selectedMetadata)
        {
            if (selectedMetadata == null)
            {
                selectedMetadata = new ObservableCollection<KeyValuePair<string, string>>();
            }
            selectedMetadata.Clear();
            foreach (DataContents dc in metadataList)
            {
                if (false == String.IsNullOrWhiteSpace(dc.AssignedLabel))
                {
                    // We have a non-empty data label, so add it.
                    selectedMetadata.Add(new KeyValuePair<string, string>(dc.MetadataKey, this.GetDataLabelFromLabel(dc.AssignedLabel)));
                }
            }
            return selectedMetadata;
        }

        // Purpose: Clear all comboboxes with the same data label as the currently selected one.
        // This ensures that all metadata fields will be assigned (if at all) to unique data labels.
        // Check all the comboboxes in the grid againste the currently selected combobox.
        // If its value is the same as the currently selected one, clear it.
        private static void DataGridClearComboBoxesWithMatchingSelectedItem(DataGrid dg, ComboBox selectedComboBox, string dataLabelColumnHeader)
        {
            int datalabelColumnIndex = dg.Columns.IndexOf(dg.Columns.FirstOrDefault(c => (string)c.Header == dataLabelColumnHeader));

            for (int rowIndex = 0; rowIndex < dg.Items.Count; rowIndex++)
            {
                // In order for ItemContainerGenerator to work, we need to set the DataGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                DataGridRow row = (DataGridRow)dg.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    continue;
                }

                // Get the two grid cells
                DataGridCellsPresenter presenter = Util.VisualChildren.GetVisualChild<DataGridCellsPresenter>(row);
                DataGridCell datalabelCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(datalabelColumnIndex);
                if (datalabelCell.Content is ContentPresenter presenter1)
                {
                    ComboBox cb = (ComboBox)System.Windows.Media.VisualTreeHelper.GetChild(presenter1, 0);
                    //System.Diagnostics.Debug.Print(cb.Text + "|" + (string)chosenComboBox.SelectedValue);
                    if (cb != selectedComboBox && cb.Text == (string)selectedComboBox.SelectedValue)
                    {
                        cb.Text = String.Empty;
                    }
                }
            }
        }
        #endregion

        #region Class ViewModel
        public class ViewModel : Util.ViewModelBase
        {
            // The full path of the file
            private string _filePath;
            public string FilePath
            {
                get => this._filePath;
                set
                {
                    SetProperty(ref _filePath, value);
                    this.FileName = Path.GetFileName(_filePath);
                }
            }

            // Only the file name (i.e., strip off the path, if any) 
            private string _fileName;
            public string FileName
            {
                get => _fileName;
                set => SetProperty(ref _fileName, value);
            }

            private ObservableCollection<string> _noteLabels = new ObservableCollection<string>();
            public ObservableCollection<string> NoteLabels
            {
                get => _noteLabels;
                set => SetProperty(ref _noteLabels, value);
            }

            private ObservableCollection<DataContents> _metadataList = new ObservableCollection<DataContents>();
            public ObservableCollection<DataContents> MetadataList
            {
                get => _metadataList;
                set
                {
                    SetProperty(ref _metadataList, value);

                }
            }
        }
        #endregion

        #region Class: DataContents: A class defining the data model behind each row in the AvailableMetadataDataGrid 
        public class DataContents
        {
            public string MetadataKey { get; set; } = String.Empty;
            public string MetadataKind { get; set; } = String.Empty;
            public string MetadataName { get; set; } = String.Empty;
            public string MetadataValue { get; set; } = String.Empty;
            public string AssignedLabel { get; set; } = String.Empty;
            public DataContents(string metadataKey, string metadataKind, string metadataName, string metadataValue, string assignedDataLabel)
            {
                this.MetadataKey = metadataKey;
                this.MetadataKind = metadataKind;
                this.MetadataName = metadataName;
                this.MetadataValue = metadataValue;
                this.AssignedLabel = assignedDataLabel;
            }
        }
        #endregion

        private void ShowMetadataDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                this.HideMetadataKindColumn = cb.IsChecked == false;
            }
        }
    }
}