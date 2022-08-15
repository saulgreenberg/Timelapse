using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog deals with missing folders, i.e. folders that are not found at their relative paths.
    /// It is given a data structure of old and possibly matching folder names, and based on that it displays a table of rows, each showing 
    /// - a folder's name, 
    /// - a relative path to an expected old location of that folder,
    /// - a relative path to a possible new location of that folder
    /// - a checkbox indicating whether that new location should be used
    /// - A Locate button
    /// - a View button
    /// The user can then check each location (via View) and find a new location (via Locate) if needed
    /// - return true: the new locations with the 'Use' checkbox checked will be returned
    /// - return false: cancel all attempts to find the locaton of missing folders.
    /// </summary>
    public partial class MissingFoldersLocateAllFolders : Window
    {
        #region Public properties
        public Dictionary<string, string> FinalFolderLocations
        {
            get
            {
                Dictionary<string, string> finalFolderLocations = new Dictionary<string, string>();
                int rowIndex = 0;
                foreach (MissingFolderRow row in observableCollection)
                {
                    DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                    if (dataGridRow == null) continue;
                    ComboBox comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (comboBox == null) continue;

                    if (row.Use == true && false == String.IsNullOrWhiteSpace((string)comboBox.SelectedItem) && false == String.Equals((string)comboBox.SelectedItem, this.useLocateButtonText))
                    {
                        finalFolderLocations.Add(row.ExpectedOldLocation, (string)comboBox.SelectedItem);
                    }
                    rowIndex++;
                }
                return finalFolderLocations;
            }
        }
        #endregion

        #region Private variables
        private readonly string useLocateButtonText = "0 matches. Try [Locate]";
        private readonly string RootPath;
        private readonly ObservableCollection<MissingFolderRow> observableCollection; // A tuple defining the contents of the datagrid
        private IList<DataGridCellInfo> selectedRowValues; // Will contain the tuple of the row corresponding to the selected cell
        #endregion

        #region Constructor, Loaded 
        public MissingFoldersLocateAllFolders(Window owner, string rootPath, Dictionary<string, List<string>> missingFoldersAndLikelyLocations)
        {
            InitializeComponent();

            if (missingFoldersAndLikelyLocations == null || missingFoldersAndLikelyLocations.Count == 0)
            {
                // Nothing to do. Abort
                this.DialogResult = false;
                return;
            }

            this.Owner = owner;
            this.RootPath = rootPath;
            this.observableCollection = new ObservableCollection<MissingFolderRow>();
            this.EnsureCheckboxValue();
            foreach (KeyValuePair<string, List<string>> pair in missingFoldersAndLikelyLocations)
            {
                List<string> possibleNewLocations = missingFoldersAndLikelyLocations[pair.Key];
                if (possibleNewLocations.Count == 0)
                {
                    possibleNewLocations.Add(this.useLocateButtonText);
                }
                MissingFolderRow row = new MissingFolderRow(Path.GetFileName(pair.Key), pair.Key, possibleNewLocations, false);
                this.observableCollection.Add(row);
            }
            this.DataGrid.ItemsSource = this.observableCollection;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Get rid of those ugly empty cell headers atop the Locate/View columns
            this.DataGrid.Columns[4].HeaderStyle = CreateEmptyHeaderStyle();
            this.DataGrid.Columns[5].HeaderStyle = CreateEmptyHeaderStyle();

            // Bind each combobox, and select the first item in each Combobox
            int rowIndex = 0;
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                                               .ContainerFromIndex(rowIndex);
                ComboBox cb = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                cb.ItemsSource = mfr.PossibleNewLocation;
                cb.SelectedIndex = 0;
                rowIndex++;
            }
            this.SetInitialCheckboxValue();
        }
        #endregion

        #region Button callbacks
        private void Cancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;

        private void UseNewLocations_Click(object sender, RoutedEventArgs e) => this.DialogResult = true;
        #endregion

        #region View/ Locate / CheckChanged, Combobox callbacks
        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            string possibleLocation = GetPossibleLocationFromSelection();
            if (String.Equals(possibleLocation, this.useLocateButtonText)) return;
            Util.ProcessExecution.TryProcessStartUsingFileExplorer(Path.Combine(this.RootPath, possibleLocation));
        }

        private void LocateButton_Click(object sender, RoutedEventArgs e)
        {
            string missingFolderName = this.GetFolderNameFromSelection();

            // We need to update the datagrid with the new value. 
            MissingFolderRow rowValues;
            int rowIndex = 0;
            rowValues = (MissingFolderRow)this.selectedRowValues[0].Item;
            string newLocation = Dialogs.LocateRelativePathUsingOpenFileDialog(this.RootPath, missingFolderName);
            if (String.IsNullOrWhiteSpace(newLocation)) return;

            // Find the selected row
            foreach (MissingFolderRow row in this.observableCollection)
            {
                if (row == rowValues)
                {
                    // Rebuild its combobox items so it has the latest user-provided location as the first selected item
                    DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                    if (dataGridRow == null) return;
                    ComboBox comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (comboBox == null) return;

                    // Rebuild the list with the new position at the beginning and selected.
                    List<string> newList = new List<string>
                    {
                        newLocation
                    };
                    foreach (string item in row.PossibleNewLocation)
                    {
                        if (String.Equals(item, this.useLocateButtonText))
                        {
                            continue;
                        }
                        if (false == String.Equals(item, newLocation))
                        {
                            newList.Add(item);
                        }
                    }
                    if (newList.Count == 0)
                    {
                        newList.Add(this.useLocateButtonText);
                    }
                    row.PossibleNewLocation = newList;

                    comboBox.ItemsSource = newList;
                    comboBox.SelectedIndex = 0;
                    this.EnsureCheckboxValue();
                }
                rowIndex++;
            }
        }

        private void Checkbox_CheckChanged(object sender, RoutedEventArgs e)
        {
            this.EnsureCheckboxValue();
        }

        // Whenever the selection changes (which only happens if the user actually selects something)
        // try to automatically select its 'Use' box
        private void Part_Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBoxSender)
            {
                int rowIndex = 0;
                CheckBox checkBox;
                ComboBox comboBox;

                foreach (MissingFolderRow mfr in this.observableCollection)
                {
                    DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                       .ContainerFromIndex(rowIndex++);
                    if (null == dataGridRow) continue;
                    comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (null == comboBox || comboBox != comboBoxSender) continue;

                    // We found the row.
                    checkBox = Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                    if (null == checkBox) break;

                    if (comboBox.Items.Count > 1)
                    {
                        checkBox.IsChecked = true;
                    }

                }
            }
        }
        #endregion

        #region DataGrid callbacks
        // Remember the data for the selected row
        private void MatchDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            this.selectedRowValues = e.AddedCells;
        }
        #endregion

        #region Checkbox Checks 
        private void SetInitialCheckboxValue()
        {
            int rowIndex = 0;
            CheckBox checkBox;
            ComboBox comboBox;
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (null == dataGridRow) continue;
                checkBox = Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                if (null == checkBox) continue;
                comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                if (null == comboBox) continue;
                if (String.IsNullOrEmpty((string)comboBox.SelectedItem) || String.Equals((string)comboBox.SelectedItem, this.useLocateButtonText) || comboBox.Items.Count > 1)
                {
                    checkBox.IsChecked = false;
                }
                else
                {
                    checkBox.IsChecked = true;
                }
                rowIndex++;
            }
        }

        private void EnsureCheckboxValue()
        {
            int rowIndex = 0;
            CheckBox checkBox;
            ComboBox comboBox;
            foreach (MissingFolderRow mfr in this.observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (null == dataGridRow) continue;
                checkBox = Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                if (null == checkBox) continue;
                comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                if (null == comboBox) continue;
                if (String.IsNullOrEmpty((string)comboBox.SelectedItem) || String.Equals((string)comboBox.SelectedItem, this.useLocateButtonText))
                {
                    checkBox.IsChecked = false;
                }
                else if (comboBox.Items.Count == 1)
                {
                    // We have a single valid item
                    checkBox.IsChecked = true;
                }
                // else leave it unchanged
                rowIndex++;
            }
        }

        //private void SetCheckboxValue(CheckBox checkBox)
        //{
        //    int rowIndex = 0;
        //    ComboBox comboBox;
        //    foreach (MissingFolderRow mfr in this.observableCollection)
        //    {
        //        DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator
        //           .ContainerFromIndex(rowIndex);
        //        if (Util.VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox") == checkBox)
        //        {
        //            comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
        //            if (String.IsNullOrEmpty((string)comboBox.SelectedItem))
        //            {
        //                checkBox.IsChecked = false;
        //            }
        //        }
        //        rowIndex++;
        //    }
        //}
        #endregion

        #region Helper methods
        private string GetFolderNameFromSelection()
        {
            MissingFolderRow mfr = (MissingFolderRow)this.selectedRowValues[0].Item;
            return (mfr == null) ? String.Empty : mfr.FolderName;
        }

        private string GetPossibleLocationFromSelection()
        {
            string location = String.Empty;
            MissingFolderRow mfr = (MissingFolderRow)this.selectedRowValues[0].Item;
            // return (mfr == null || mfr.PossibleNewLocation.Count == 0) ? String.Empty : mfr.PossibleNewLocation[0];
            int rowIndex = 0;
            ComboBox comboBox;
            foreach (MissingFolderRow row in this.observableCollection)
            {
                if (row == mfr)
                {
                    // We foound the row,
                    DataGridRow dataGridRow = (DataGridRow)this.DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                    if (null == dataGridRow) break;
                    comboBox = Util.VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (null == comboBox) break;
                    location = (string)comboBox.SelectedItem;
                    break;
                }
                rowIndex++;
            }
            return String.IsNullOrEmpty(location) ? String.Empty : location;
        }
        #endregion

        #region Styles
        // A ColumnHeader style that appears (more or less) empty
        private static Style CreateEmptyHeaderStyle()
        {
            Style headerStyle = new Style
            {
                TargetType = typeof(DataGridColumnHeader)//sets target type as DataGrid row
            };

            Setter setterBackground = new Setter
            {
                Property = DataGridColumnHeader.BackgroundProperty,
                Value = new SolidColorBrush(Colors.White)
            };

            Setter setterBorder = new Setter
            {
                Property = DataGridColumnHeader.BorderThicknessProperty,
                Value = new Thickness(0, 0, 0, 1)
            };

            headerStyle.Setters.Add(setterBackground);
            headerStyle.Setters.Add(setterBorder);
            return headerStyle;
        }
        #endregion

        #region Private Class MissingFolderRow - used only by the above class
        // An internal class used to store the contents of a datagrid row
        private class MissingFolderRow
        {
            public string FolderName { get; set; }
            public string ExpectedOldLocation { get; set; }
            public List<string> PossibleNewLocation { get; set; }
            public bool Use { get; set; }

            public MissingFolderRow(string folderName, string expectedOldLocation, List<string> possibleNewLocation, bool use)
            {
                this.FolderName = folderName;
                this.ExpectedOldLocation = expectedOldLocation;
                this.PossibleNewLocation = possibleNewLocation;
                this.Use = use;
            }
        }
        #endregion
    }
}
