﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.Images;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for FindMissingImageFolder.xaml
    /// </summary>
    public partial class MissingImageLocateRelativePaths
    {
        #region Public Properties
        public Tuple<string, string> LocatedMissingFile
        {
            get
            {
                foreach (Tuple<string, string, bool> tuple in observableCollection)
                {
                    if (tuple.Item3)
                    {
                        return new Tuple<string, string>(Path.GetDirectoryName(tuple.Item1), Path.GetFileName(tuple.Item1));
                    }
                }
                // Should never happen as at least one 'Use' checkmark is checked
                return new Tuple<string, string>(string.Empty, string.Empty);
            }
        }
        #endregion

        #region Private Variables
        private readonly string FolderPath;
        private ObservableCollection<Tuple<string, string, bool>> observableCollection; // A tuple defining the contents of the datagrid
        private IList<DataGridCellInfo> selectedRowTuple; // Will contain the tuple of the row corresponding to the selected cell
        #endregion

        #region Constructor, Loaded and AutoGeneratedColumns
        public MissingImageLocateRelativePaths(Window owner, FileDatabase fileDatabase, string relativePath, string fileName, Dictionary<string, List<string>> candidates)
        {

            // Keeps the tooltip open until the user moves off the data row
            //ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));

            InitializeComponent();
            if (fileDatabase == null || candidates == null || candidates.Count == 0)
            {
                // Nothing to do. Abort
                this.DialogResult = false;
                return;
            }

            this.Owner = owner;
            this.FolderPath = fileDatabase.FolderPath;

            // Show the file name and its full relative path in the UI
            this.RunImageName.Text = fileName;
            this.RunRelativePath.Text = relativePath;

            // Create a collection comprising: relative path to the found image, number of other missing images found in that relative path, and whether or not that relative path should be used.
            // Then display it by binding it to the data grid 
            this.observableCollection = new ObservableCollection<Tuple<string, string, bool>>();
            int i = 0;
            foreach (KeyValuePair<string, List<string>> candidate in candidates)
            {
                this.observableCollection.Add(new Tuple<string, string, bool>(Path.Combine(candidate.Key, fileName), candidate.Value.Count.ToString(), i++ == 0));
            }
            this.DataGrid.ItemsSource = observableCollection;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // Get rid of those ugly empty cell headers atop the Locate/View columns
            this.DataGrid.Columns[0].HeaderStyle = CreateEmptyHeaderStyle();

        }

        // Create the datagrid column headers
        private void MatchDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.DataGrid.Columns[1].Header = "Possible new location";
            this.DataGrid.Columns[1].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            this.DataGrid.Columns[2].Header = "# Matching files";

            this.DataGrid.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            this.DataGrid.Columns[3].Header = "Use?";
            this.DataGrid.Columns[3].Width = new DataGridLength(2, DataGridLengthUnitType.Auto);
        }
        #endregion

        #region Button callbacks
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion

        #region Datagridrow callbacks to display file image in thumbnails
        // When the user enters a listbox item, show the image
        private void Row_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!(sender is DataGridRow dgr))
            {
                return;
            }
            if (dgr.Item == null)
            {
                return;
            }

            Tuple<string, string, bool> tuple = (Tuple<string, string, bool>)dgr.Item;
            string path = Path.Combine(this.FolderPath, tuple.Item1);
            Image image = new Image()
            {
                Source = Util.FilesFolders.GetFileTypeByItsExtension(path) == FileExtensionEnum.IsImage
                ? BitmapUtilities.GetBitmapFromImageFile(path, Constant.ImageValues.PreviewWidth480, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth, out _)
                : BitmapUtilities.GetBitmapFromVideoFile(path, Constant.ImageValues.PreviewWidth480, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth, out _),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            dgr.ToolTip = image;
        }

        // When the user leaves the row, remove the image
        private void Row_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!(sender is DataGridRow dgr))
            {
                return;
            }
            dgr.ToolTip = null;
        }
        #endregion

        #region DataGrid callbacks
        // Remember the tuple of the selected row
        private void MatchDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            this.selectedRowTuple = e.AddedCells;
        }

        // Determine if the user clicked the View or Checkmark cell, and take the appropriate action
        private void MatchDataGrid_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.selectedRowTuple == null || this.selectedRowTuple.Count == 0 || this.selectedRowTuple[0].Item == null)
            {
                return;
            }
            int selectedColumn = this.selectedRowTuple[0].Column.DisplayIndex;
            string possibleFolderLocation = Path.GetDirectoryName(GetPossibleLocationFromSelection()) ?? string.Empty;
            switch (selectedColumn)
            {
                case 0:
                    // Show the folder in the file explorer
                    Util.ProcessExecution.TryProcessStartUsingFileExplorerOnFolder(Path.Combine(this.FolderPath, possibleFolderLocation));
                    break;
                case 3:
                    // Use checkmark has been selected. 
                    // We need to update the datagrid with the new value. 
                    // To keep it simple,  just rebuild the observable collection and rebind it
                    Tuple<string, string, bool>  rowValues = (Tuple<string, string, bool>)this.selectedRowTuple[0].Item;

                    ObservableCollection<Tuple<string, string, bool>>  obsCollection = new ObservableCollection<Tuple<string, string, bool>>();
                    foreach (Tuple<string, string, bool> row in this.observableCollection)
                    {
                       
                        obsCollection.Add(!Equals(row, rowValues)
                            // To make it work as a radio butotn, toggle all other rows to be what the selected row is not
                            ? new Tuple<string, string, bool>(row.Item1, row.Item2, rowValues.Item3)
                            // Toggle the selected row
                            : new Tuple<string, string, bool>(rowValues.Item1, rowValues.Item2, !rowValues.Item3));
                    }
                    this.observableCollection = obsCollection;
                    this.DataGrid.ItemsSource = this.observableCollection;
                    break;
                default:
                    return;
            }
        }
        #endregion

        #region Helper methods
        private string GetPossibleLocationFromSelection()
        {
            Tuple<string, string, bool> tuple = (Tuple<string, string, bool>)this.selectedRowTuple[0].Item;
            return (tuple != null)
                ? tuple.Item1
                : string.Empty;
        }
        #endregion

        #region Styles
        // A ColumnHeader style that appears (more or less) empty
        private static Style CreateEmptyHeaderStyle()
        {
            // Its way more compact to use the xaml approach rather than to declare styles, setters, etc.
            // But we have to ensure the expression is well formed.
            string xaml = "<Style xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "TargetType=\"DataGridColumnHeader\">" +
                "<Setter Property=\"Background\" Value=\"White\"/>" +
                 "<Setter Property=\"BorderThickness\" Value=\"0, 0, 0, 1\"/>" +
                "</Style>";
            return System.Windows.Markup.XamlReader.Parse(xaml) as Style;
        }
        #endregion
    }
}
