using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Timelapse.Util;
using FileDatabase = Timelapse.Database.FileDatabase;

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
    public partial class MissingFoldersLocateAllFolders
    {
        #region Public properties
        public Dictionary<string, string> FinalFolderLocations
        {
            get
            {
                Dictionary<string, string> finalFolderLocations = [];
                int rowIndex = 0;
                foreach (MissingFolderRow row in observableCollection)
                {
                    DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                    if (dataGridRow == null) continue;
                    ComboBox comboBox = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (comboBox == null) continue;

                    if (row.Use && false == string.IsNullOrWhiteSpace((string)comboBox.SelectedItem) && false == String.Equals((string)comboBox.SelectedItem, useLocateButtonText))
                    {
                        finalFolderLocations.Add(row.ExpectedOldLocation, (string)comboBox.SelectedItem);
                    }
                    rowIndex++;
                }
                return finalFolderLocations;
            }
        }

        private ObservableCollection<MissingFolderRow> observableCollection { get; set; } // A tuple defining the contents of the datagrid

        #endregion

        #region Private variables
        private readonly string useLocateButtonText = "0 matches. Try [Locate]";
        private readonly string RootPath;
        private readonly FileDatabase FileDatabase;
        private readonly List<string> MissingRelativePaths;

        private IList<DataGridCellInfo> selectedRowValues; // Will contain the tuple of the row corresponding to the selected cell
        #endregion

        #region Constructor, Loaded 
        public MissingFoldersLocateAllFolders(Window owner, string rootPath, List<string> missingRelativePaths, Dictionary<string, List<string>> missingFoldersAndLikelyLocations, FileDatabase fileDatabase)
        {
            InitializeComponent();

            if (missingFoldersAndLikelyLocations == null || missingFoldersAndLikelyLocations.Count == 0)
            {
                // Nothing to do. Abort
                DialogResult = false;
                return;
            }

            Owner = owner;
            RootPath = rootPath;
            FileDatabase = fileDatabase;
            MissingRelativePaths = missingRelativePaths;
            CreateDataTable(missingFoldersAndLikelyLocations);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Get rid of those ugly empty cell headers atop the Locate/View columns
            DataGrid.Columns[5].HeaderStyle = CreateEmptyHeaderStyle();
            DataGrid.Columns[6].HeaderStyle = CreateEmptyHeaderStyle();

            // Add the missing folders
            PopulateDataGridRow();
        }
        #endregion

        private void CreateDataTable(Dictionary<string, List<string>> missingFoldersAndLikelyLocations)
        {
            observableCollection = [];
            EnsureCheckboxValue();

            foreach (KeyValuePair<string, List<string>> pair in missingFoldersAndLikelyLocations)
            {
                List<string> possibleNewLocations = missingFoldersAndLikelyLocations[pair.Key];
                if (possibleNewLocations.Count == 0)
                {
                    possibleNewLocations.Add(useLocateButtonText);
                }
                MissingFolderRow row = new(Path.GetFileName(pair.Key), pair.Key, possibleNewLocations, false);
                observableCollection.Add(row);
            }

            DataGrid.ItemsSource = null;
            DataGrid.ItemsSource = observableCollection;
        }

        private void PopulateDataGridRow()
        {
            // Bind each combobox, and select the first item in each Combobox
            int rowIndex = 0;
            foreach (MissingFolderRow mfr in observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator
                    .ContainerFromIndex(rowIndex);
                if (dataGridRow == null)
                {
                    DataGrid.UpdateLayout();
                    if (null != DataGrid.Items[rowIndex])
                    {
                        DataGrid.ScrollIntoView(DataGrid.Items[rowIndex]);
                    }
                    dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                }
                ComboBox cb = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                cb.ItemsSource = mfr.PossibleNewLocation;
                cb.SelectedIndex = 0;
                rowIndex++;
            }
            SetInitialCheckboxValue();
        }
        #region Button callbacks
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void UseNewLocations_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        #endregion

        #region View/ Locate / CheckChanged, Combobox callbacks
        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            string possibleLocation = GetPossibleLocationFromSelection();
            if (String.Equals(possibleLocation, useLocateButtonText)) return;
            ProcessExecution.TryProcessStartUsingFileExplorerOnFolder(Path.Combine(RootPath, possibleLocation));
        }

        private void LocateButton_Click(object sender, RoutedEventArgs e)
        {
            string missingFolderName = GetFolderNameFromSelection();

            // We need to update the datagrid with the new value. 
            int rowIndex = 0;
            MissingFolderRow rowValues = (MissingFolderRow)selectedRowValues[0].Item;
            string newLocation = Dialogs.LocateRelativePathUsingOpenFileDialog(RootPath, missingFolderName);
            if (string.IsNullOrWhiteSpace(newLocation)) return;

            // Find the selected row
            foreach (MissingFolderRow row in observableCollection)
            {
                if (row == rowValues)
                {
                    // Rebuild its combobox items so it has the latest user-provided location as the first selected item
                    DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                    if (dataGridRow == null) return;
                    ComboBox comboBox = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (comboBox == null) return;

                    // Rebuild the list with the new position at the beginning and selected.
                    List<string> newList = [newLocation];
                    foreach (string item in row.PossibleNewLocation)
                    {
                        if (String.Equals(item, useLocateButtonText))
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
                        newList.Add(useLocateButtonText);
                    }
                    row.PossibleNewLocation = newList;

                    comboBox.ItemsSource = newList;
                    comboBox.SelectedIndex = 0;
                    EnsureCheckboxValue();
                }
                rowIndex++;
            }
        }

        // Currently a no-op. We don't want to invoke the EnsureCheckboxValue as done previously, as
        // it resets the checkbox even when we uncheck  or check it.
        private void Checkbox_CheckChanged(object sender, RoutedEventArgs e)
        {
            // this.EnsureCheckboxValue();
        }

        // Whenever the selection changes (which only happens if the user actually selects something)
        // try to automatically select its 'Use' box
        private void Part_Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBoxSender)
            {
                int rowIndex = 0;
                foreach (MissingFolderRow unused in observableCollection)
                {
                    DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator
                       .ContainerFromIndex(rowIndex++);
                    if (null == dataGridRow) continue;
                    ComboBox comboBox = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (null == comboBox || comboBox != comboBoxSender) continue;

                    // We found the row.
                    CheckBox checkBox = VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
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
            selectedRowValues = e.AddedCells;
        }
        #endregion

        #region Checkbox Checks 
        private void SetInitialCheckboxValue()
        {
            int rowIndex = 0;
            foreach (MissingFolderRow unused in observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (null == dataGridRow) continue;
                CheckBox checkBox = VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                if (null == checkBox) continue;
                ComboBox comboBox = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                if (null == comboBox) continue;
                if (string.IsNullOrEmpty((string)comboBox.SelectedItem) || String.Equals((string)comboBox.SelectedItem, useLocateButtonText) || comboBox.Items.Count > 1)
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

        // Sets the Use checkbox to the appropriate initial values when its invoked.
        // If there is only one option in the location, Use is checked.
        // If there are two options, Use is unchecked.
        private void EnsureCheckboxValue()
        {
            int rowIndex = 0;
            foreach (MissingFolderRow unused in observableCollection)
            {
                DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator
                   .ContainerFromIndex(rowIndex);
                if (null == dataGridRow) continue;
                CheckBox checkBox = VisualChildren.GetVisualChild<CheckBox>(dataGridRow, "Part_Checkbox");
                if (null == checkBox) continue;
                ComboBox comboBox = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                if (null == comboBox) continue;
                if (string.IsNullOrEmpty((string)comboBox.SelectedItem) || String.Equals((string)comboBox.SelectedItem, useLocateButtonText))
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

        // Toggles a more selective match 
        private void CheckBoxStringentMatch_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb))
            {
                return;
            }
            Dictionary<string, List<string>> missingFoldersAndLikelyLocations = cb.IsChecked == true
                ? FilesFolders.TryGetMissingFoldersStringent(FileDatabase.RootPathToImages, MissingRelativePaths, FileDatabase)
                : FilesFolders.TryGetMissingFolders(FileDatabase.RootPathToImages, MissingRelativePaths);

            CreateDataTable(missingFoldersAndLikelyLocations);
            PopulateDataGridRow();
        }
        #endregion

        #region Helper methods
        private string GetFolderNameFromSelection()
        {
            MissingFolderRow mfr = (MissingFolderRow)selectedRowValues[0].Item;
            return (mfr == null) ? string.Empty : mfr.FolderName;
        }

        private string GetPossibleLocationFromSelection()
        {
            string location = string.Empty;
            MissingFolderRow mfr = (MissingFolderRow)selectedRowValues[0].Item;
            // return (mfr == null || mfr.PossibleNewLocation.Count == 0) ? string.Empty : mfr.PossibleNewLocation[0];
            int rowIndex = 0;
            foreach (MissingFolderRow row in observableCollection)
            {
                if (row == mfr)
                {
                    // We foound the row,
                    DataGridRow dataGridRow = (DataGridRow)DataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                    if (null == dataGridRow) break;
                    ComboBox comboBox = VisualChildren.GetVisualChild<ComboBox>(dataGridRow, "Part_Combo");
                    if (null == comboBox) break;
                    location = (string)comboBox.SelectedItem;
                    break;
                }
                rowIndex++;
            }
            return string.IsNullOrEmpty(location) ? string.Empty : location;
        }
        #endregion

        #region Styles
        // A ColumnHeader style that appears (more or less) empty
        private static Style CreateEmptyHeaderStyle()
        {
            Style headerStyle = new()
            {
                TargetType = typeof(DataGridColumnHeader)//sets target type as DataGrid row
            };

            Setter setterBackground = new()
            {
                Property = BackgroundProperty,
                Value = new SolidColorBrush(Colors.White)
            };

            Setter setterBorder = new()
            {
                Property = BorderThicknessProperty,
                Value = new Thickness(0, 0, 0, 1)
            };

            headerStyle.Setters.Add(setterBackground);
            headerStyle.Setters.Add(setterBorder);
            return headerStyle;
        }
        #endregion

        #region Private Class MissingFolderRow - used only by the above class
        // An internal class used to store the contents of a datagrid row
        private class MissingFolderRow(string folderName, string expectedOldLocation, List<string> possibleNewLocation, bool use)
        {
            public string FolderName { get; } = folderName;
            public string ExpectedOldLocation { get; } = expectedOldLocation;
            public List<string> PossibleNewLocation { get; set; } = possibleNewLocation;

            // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
            public bool Use { get; set; } = use;
        }
        #endregion


    }
}
