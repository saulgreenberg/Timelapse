using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.SearchingAndSorting;

namespace Timelapse.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom sort by choosing primary and secondary data fields.
    /// </summary>
    public partial class CustomSort
    {
        #region Public Properties
        public SortTerm SortTerm1 { get; set; }
        public SortTerm SortTerm2 { get; set; }
        #endregion

        #region Private Variables
        private List<SortTerm> sortTermList;
        private string fileDisplayLabel = string.Empty;
        private string dateDisplayLabel = string.Empty;
        private string relativePathDisplayLabel = string.Empty;
        private readonly FileDatabase database;
        #endregion

        #region Constructor and Loaded
        public CustomSort(FileDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
            this.SortTerm1 = new SortTerm();
            this.SortTerm2 = new SortTerm();
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Get the sort terms. 
            this.sortTermList = SortTerms.GetSortTerms(this.database.CustomSelection.SearchTerms);

            // We need the labels of the File and Date datalabels, as we will check to see if they are selected in the combo box
            foreach (SortTerm sortTerm in this.sortTermList)
            {
                if (sortTerm.DataLabel == Constant.DatabaseColumn.File)
                {
                    this.fileDisplayLabel = sortTerm.DisplayLabel;
                }
                else if (sortTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    this.dateDisplayLabel = sortTerm.DisplayLabel;
                }
                else if (sortTerm.DataLabel == Constant.DatabaseColumn.RelativePath)
                {
                    this.relativePathDisplayLabel = sortTerm.DisplayLabel;
                }
            }

            // Create the combo box entries showing the sort terms
            // As a side effect, PopulatePrimaryComboBox() invokes PrimaryComboBox_SelectionChanged, which then populates the secondary combo bo
            this.PopulatePrimaryUIElements();
        }
        #endregion

        #region Populate ComboBoxes
        // Populate the two combo boxes  with potential sort terms
        // We use the custom selection to get the field we need, but note that: 
        // - we add a None entry to the secondary combo box, allowing the user to clear the selection
        private void PopulatePrimaryUIElements()
        {
            // Populate the Primary combo box with choices
            // By default, we select sort by ID unless its over-ridden
            this.PrimaryComboBox.SelectedIndex = 0;
            SortTerm sortTermDB = this.database.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            bool dateTimeAlreadyProcessed = false; 
            foreach (SortTerm sortTerm in this.sortTermList)
            {
                // As there are two datetimes in the sort terms, just use the first one.
                if (sortTerm.DisplayLabel == Constant.DatabaseColumn.DateTime && dateTimeAlreadyProcessed == false)
                {
                    dateTimeAlreadyProcessed = true;
                    continue;
                }

                if (sortTerm.DataLabel == Constant.DatabaseColumn.File && sortTerm.DisplayLabel == Constant.DatabaseColumn.File)
                {
                    // There are two files in the sort terms, one where it sorts by full path and the other by just the file name
                    // Skip the one with just the file name, as the sql sort method is tuned to look at the full path on sort by file name
                    continue;
                }
                this.PrimaryComboBox.Items.Add(sortTerm.DisplayLabel);

                // If the current PrimarySort sort term matches the current item, then set it as selected
                if (sortTerm.DataLabel == sortTermDB.DataLabel)
                {
                    this.PrimaryComboBox.SelectedIndex = this.PrimaryComboBox.Items.Count - 1;
                }
            }

            // Set the radio buttons to the default values
            this.PrimaryAscending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.True;
            this.PrimaryDescending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.False;
        }
        private void PopulateSecondaryUIElements()
        {
            // Populate the Secondary combo box with choices
            // By default, we select "None' unless its over-ridden
            this.SecondaryComboBox.Items.Clear();
            // Add a 'None' entry, as sorting on a second term is optional
            this.SecondaryComboBox.Items.Add(Constant.SortTermValues.NoneDisplayLabel);
            this.SecondaryComboBox.SelectedIndex = 0;

            SortTerm sortTermDB = this.database.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            bool dateTimeAlreadyProcessed = false;
            foreach (SortTerm sortTerm in this.sortTermList)
            {
                // As there are two datetimes in the sort terms, just use the first one.
                if (sortTerm.DisplayLabel == Constant.DatabaseColumn.DateTime && dateTimeAlreadyProcessed == false)
                {
                    dateTimeAlreadyProcessed = true;
                    continue;
                }

                // If the current sort term is the one already selected in the primary combo box, skip it
                // as it doesn't make sense to sort again on the same term
                // Additionally, There are two files in the sort terms, one where it sorts by full path and the other by just the file name
                // Skip the one with just the file name, as the sql sort method is tuned to look at the full path on sort by file name
                if (sortTerm.DisplayLabel == (string)this.PrimaryComboBox.SelectedItem
                   || (sortTerm.DataLabel == Constant.DatabaseColumn.File && sortTerm.DisplayLabel == Constant.DatabaseColumn.File))
                {
                    continue;
                }
                this.SecondaryComboBox.Items.Add(sortTerm.DisplayLabel);

                // If the current SecondarySort sort term matches the current item, then set it as selected.
                // Note that we check both terms for it, as File would be the 2nd term vs. the 1st term
                if (sortTermDB.DataLabel == sortTerm.DataLabel)
                {
                    this.SecondaryComboBox.SelectedIndex = this.SecondaryComboBox.Items.Count - 1;
                }
            }
            // Set the radio buttons to the default values
            this.SecondaryAscending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.True;
            this.SecondaryDescending.IsChecked = sortTermDB.IsAscending == Constant.BooleanValue.False;
        }
        #endregion

        #region Callbacks - ComboBoxes
        // Whenever the primary combobox changes, repopulated the secondary combo box to make sure it excludes the currently selected item
        private void PrimaryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.PopulateSecondaryUIElements();
        }
        #endregion

        #region Ok/Cancel buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            string selectedPrimaryItem = (string)this.PrimaryComboBox.SelectedItem;
            string selectedSecondaryItem = (string)this.SecondaryComboBox.SelectedItem;

            foreach (SortTerm sortTerm in this.sortTermList)
            {
                if (selectedPrimaryItem == this.fileDisplayLabel)
                {
                    this.SortTerm1.DataLabel = Constant.DatabaseColumn.File;
                    this.SortTerm1.DisplayLabel = this.fileDisplayLabel;
                    this.SortTerm1.ControlType = string.Empty;
                }
                else if (selectedPrimaryItem == this.dateDisplayLabel)
                {
                    this.SortTerm1.DataLabel = Constant.DatabaseColumn.DateTime;
                    this.SortTerm1.DisplayLabel = this.dateDisplayLabel;
                    this.SortTerm1.ControlType = string.Empty;
                }
                else if (selectedPrimaryItem == this.relativePathDisplayLabel)
                {
                    this.SortTerm1.DataLabel = Constant.DatabaseColumn.RelativePath;
                    this.SortTerm1.DisplayLabel = this.relativePathDisplayLabel;
                    this.SortTerm1.ControlType = string.Empty;
                }
                else if (selectedPrimaryItem == sortTerm.DisplayLabel)
                {
                    this.SortTerm1.DataLabel = sortTerm.DataLabel;
                    this.SortTerm1.DisplayLabel = sortTerm.DisplayLabel;
                    this.SortTerm1.ControlType = sortTerm.ControlType;
                }
                this.SortTerm1.IsAscending = (this.PrimaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
            }

            if (selectedSecondaryItem != Constant.SortTermValues.NoneDisplayLabel)
            {
                foreach (SortTerm sortTerm in this.sortTermList)
                {
                    if (selectedSecondaryItem == this.fileDisplayLabel)
                    {
                        this.SortTerm2.DataLabel = Constant.DatabaseColumn.File;
                        this.SortTerm2.DisplayLabel = this.fileDisplayLabel;
                        this.SortTerm2.ControlType = string.Empty;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == this.dateDisplayLabel)
                    {
                        this.SortTerm2.DataLabel = Constant.DatabaseColumn.DateTime;
                        this.SortTerm2.DisplayLabel = this.dateDisplayLabel;
                        this.SortTerm2.ControlType = string.Empty;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == this.dateDisplayLabel)
                    {
                        this.SortTerm2.DataLabel = Constant.DatabaseColumn.DateTime;
                        this.SortTerm2.DisplayLabel = this.dateDisplayLabel;
                        this.SortTerm2.ControlType = string.Empty;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == sortTerm.DisplayLabel)
                    {
                        this.SortTerm2.DataLabel = sortTerm.DataLabel;
                        this.SortTerm2.DisplayLabel = sortTerm.DisplayLabel;
                        this.SortTerm2.ControlType = sortTerm.ControlType;
                        this.SortTerm2.IsAscending = (this.SecondaryAscending.IsChecked == true) ? Constant.BooleanValue.True : Constant.BooleanValue.False;
                    }
                }
            }
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
