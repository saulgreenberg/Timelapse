﻿using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;

namespace Timelapse.Controls
{
    /// <summary>
    /// This user control contains and maintains a grid that displays feedback about any ambiguous date changes 
    /// and that allows the user to select which of those dates to change.
    /// Populated by the caller via AddFeedbackRow (...), which will add a row to the grid whose contents reflect the contents of the various parameters.
    /// </summary>
    public partial class DateChangesFeedbackControl
    {
        #region Public Properties and Private variables
        public bool ShowDifferenceColumn { get; set; }
        public string Column0Name { get; set; }
        public string Column1Name { get; set; }
        public string Column2Name { get; set; }
        public string Column3Name { get; set; }
        public string Column4Name { get; set; }
        public string FolderPath { get; set; }

        // This collection will hold tuples, where each tuple contains the contents for a row that will be shown in the datagrid  
        private readonly ObservableCollection<FeedbackRowTuple> feedbackRows;
        #endregion

        #region Constructor and AutoGenerateColumns
        // Bind the collection to the datagrid, where any change in the collection will be displayed in the datagrid
        public DateChangesFeedbackControl()
        {
            InitializeComponent();
            Column0Name = "Select";
            Column1Name = "Sample file";
            Column2Name = "Current date";
            Column3Name = "New date";
            Column4Name = "# files with same date";

            feedbackRows = new ObservableCollection<FeedbackRowTuple>();
            feedbackGrid.ItemsSource = feedbackRows;
            FolderPath = string.Empty;
        }

        // Label the datagrid feedback columns with the appropriate headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            feedbackGrid.Columns[0].Header = Column0Name;
            feedbackGrid.Columns[0].Width = DataGridLength.SizeToHeader;
            feedbackGrid.Columns[1].Header = Column1Name;
            feedbackGrid.Columns[2].Header = Column2Name;
            feedbackGrid.Columns[3].Header = Column3Name;
            feedbackGrid.Columns[4].Header = Column4Name;
            feedbackGrid.SelectedCellsChanged += FeedbackGrid_SelectedCellsChanged;
        }
        #endregion

        #region Externally visible methods
        // Add a row to the tuple, which in turn will update the grid. 
        public void AddFeedbackRow(string sampleFileName, string currentDate, string newDate, string numFilesWithThatDate, ImageRow imageRow, AmbiguousDateRange ambiguousDateRange)
        {
            FeedbackRowTuple row = new FeedbackRowTuple(sampleFileName, currentDate, newDate, numFilesWithThatDate, imageRow, ambiguousDateRange);
            feedbackRows.Add(row);
        }

        // Set all checkboxes in column 0 to the selectedState)
        public void SelectAll(bool selectedState)
        {
            foreach (FeedbackRowTuple tuple in feedbackRows)
            {
                tuple.Select = selectedState;
            }
            feedbackGrid.Items.Refresh();
            Synchronize();
        }

        public bool AreAnySelected()
        {
            foreach (FeedbackRowTuple tuple in feedbackRows)
            {
                if (tuple.Select)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region internal Callbacks
        // Selecting a row toggles its Select checkbox. 
        private void FeedbackGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {

            foreach (DataGridCellInfo cell in e.AddedCells)
            {
                // If the selection is in the Select column, toggle its state
                if (cell.Column.Header.ToString() == Column0Name)
                {
                    if (cell.Item is FeedbackRowTuple tuple)
                    {
                        tuple.Select = !tuple.Select;
                        feedbackGrid.Items.Refresh();
                    }
                }
                if (cell.Column.Header.ToString() == Column3Name)
                {

                }
                // Clear all selections
                feedbackGrid.SelectedCells.Remove(cell);
            }

            // Synchronize the AmbiguousDateRange (from the caller) to the current tuple selection.
            Synchronize();
        }

        // Synchronize the AmbiguousDateRange (from the caller) to the current tuple selection.
        private void Synchronize()
        {
            foreach (FeedbackRowTuple tuple in feedbackGrid.ItemsSource)
            {
                tuple.AmbiguousDateRange.SwapDates = tuple.Select;
            }
        }

        // Set the tooltip to the image, if possible, when we enter the row
        private void Row_MouseEnter(object sender, MouseEventArgs e)
        {
            // Set the ToolTip to the image in the row's tag
            DataGridRow row = e.Source as DataGridRow;
            FeedbackRowTuple tuple = (FeedbackRowTuple)row?.Item;
            ImageRow ir = tuple?.ImageRow;
            if (row == null || tuple == null || ir == null)
            {
                return;
            }

            // Load an image from thie image row (specified in the tuple.Tag)
            Image image = new Image
            {
                Source = ir.LoadBitmap(FolderPath, ImageValues.PreviewWidth480, out bool _)
            };
            row.ToolTip = image;
        }

        // Set the tooltip to null when we leave the row
        private void Row_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.Source is DataGridRow row)
            {
                row.ToolTip = "Image not available";
            }
        }
        #endregion

        #region FeedbackRowTuple
        // This class defines a row tuple containing 5 elements, which is used as a row in the datagrid.
        internal class FeedbackRowTuple
        {
            // Properties in Tuple that are shown on the data table
            public bool Select { get; set; }
            public string SampleFileName { get; set; }
            public string CurrentDate { get; set; }
            public string NewDate { get; set; }
            public string NumFilesWithThatDate { get; set; }

            // Variables that are not shown on the data table
            public ImageRow ImageRow;
            public AmbiguousDateRange AmbiguousDateRange;

            public FeedbackRowTuple(string sampleFileName, string currentDate, string newDate, string numFilesWithThatDate, ImageRow imageRow, AmbiguousDateRange ambiguousDateRange)
            {
                // Properties that will be displayed in the data grid
                Select = false;
                SampleFileName = sampleFileName;
                CurrentDate = currentDate;
                NewDate = newDate;
                NumFilesWithThatDate = numFilesWithThatDate;

                ImageRow = imageRow;
                AmbiguousDateRange = ambiguousDateRange;
            }
        }
        #endregion
    }
}