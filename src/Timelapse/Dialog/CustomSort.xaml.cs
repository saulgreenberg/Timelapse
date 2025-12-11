using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;

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
            InitializeComponent();
            this.database = database;
            SortTerm1 = new();
            SortTerm2 = new();
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();

            // Adjust this dialog window position
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Get the sort terms. 
            sortTermList = SortTerms.GetSortTerms(database.CustomSelection.SearchTerms);

            // We need the labels of the File and Date datalabels, as we will check to see if they are selected in the combo box
            foreach (SortTerm sortTerm in sortTermList)
            {
                if (sortTerm.DataLabel == DatabaseColumn.File)
                {
                    fileDisplayLabel = sortTerm.DisplayLabel;
                }
                else if (sortTerm.DataLabel == DatabaseColumn.DateTime)
                {
                    dateDisplayLabel = sortTerm.DisplayLabel;
                }
                else if (sortTerm.DataLabel == DatabaseColumn.RelativePath)
                {
                    relativePathDisplayLabel = sortTerm.DisplayLabel;
                }
            }

            // Create the combo box entries showing the sort terms
            // As a side effect, PopulatePrimaryComboBox() invokes PrimaryComboBox_SelectionChanged, which then populates the secondary combo bo
            PopulatePrimaryUIElements();
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
            PrimaryComboBox.SelectedIndex = 0;
            SortTerm sortTermDB = database.ImageSet.GetSortTerm(0); // Get the 1st sort term from the database
            bool dateTimeAlreadyProcessed = false; 
            foreach (SortTerm sortTerm in sortTermList)
            {
                // As there are two datetimes in the sort terms, just use the first one.
                if (sortTerm.DisplayLabel == DatabaseColumn.DateTime && dateTimeAlreadyProcessed == false)
                {
                    dateTimeAlreadyProcessed = true;
                    continue;
                }

                if (sortTerm.DataLabel == DatabaseColumn.File && sortTerm.DisplayLabel == DatabaseColumn.File)
                {
                    // There are two files in the sort terms, one where it sorts by full path and the other by just the file name
                    // Skip the one with just the file name, as the sql sort method is tuned to look at the full path on sort by file name
                    continue;
                }
                PrimaryComboBox.Items.Add(sortTerm.DisplayLabel);

                // If the current PrimarySort sort term matches the current item, then set it as selected
                if (sortTerm.DataLabel == sortTermDB.DataLabel)
                {
                    PrimaryComboBox.SelectedIndex = PrimaryComboBox.Items.Count - 1;
                }
            }

            // Set the radio buttons to the default values
            PrimaryAscending.IsChecked = sortTermDB.IsAscending == BooleanValue.True;
            PrimaryDescending.IsChecked = sortTermDB.IsAscending == BooleanValue.False;
        }
        private void PopulateSecondaryUIElements()
        {
            // Populate the Secondary combo box with choices
            // By default, we select "None' unless its over-ridden
            SecondaryComboBox.Items.Clear();
            // Add a 'None' entry, as sorting on a second term is optional
            SecondaryComboBox.Items.Add(SortTermValues.NoneDisplayLabel);
            SecondaryComboBox.SelectedIndex = 0;

            SortTerm sortTermDB = database.ImageSet.GetSortTerm(1); // Get the 2nd sort term from the database
            bool dateTimeAlreadyProcessed = false;
            foreach (SortTerm sortTerm in sortTermList)
            {
                // As there are two datetimes in the sort terms, just use the first one.
                if (sortTerm.DisplayLabel == DatabaseColumn.DateTime && dateTimeAlreadyProcessed == false)
                {
                    dateTimeAlreadyProcessed = true;
                    continue;
                }

                // If the current sort term is the one already selected in the primary combo box, skip it
                // as it doesn't make sense to sort again on the same term
                // Additionally, There are two files in the sort terms, one where it sorts by full path and the other by just the file name
                // Skip the one with just the file name, as the sql sort method is tuned to look at the full path on sort by file name
                if (sortTerm.DisplayLabel == (string)PrimaryComboBox.SelectedItem
                   || (sortTerm.DataLabel == DatabaseColumn.File && sortTerm.DisplayLabel == DatabaseColumn.File))
                {
                    continue;
                }
                SecondaryComboBox.Items.Add(sortTerm.DisplayLabel);

                // If the current SecondarySort sort term matches the current item, then set it as selected.
                // Note that we check both terms for it, as File would be the 2nd term vs. the 1st term
                if (sortTermDB.DataLabel == sortTerm.DataLabel)
                {
                    SecondaryComboBox.SelectedIndex = SecondaryComboBox.Items.Count - 1;
                }
            }
            // Set the radio buttons to the default values
            SecondaryAscending.IsChecked = sortTermDB.IsAscending == BooleanValue.True;
            SecondaryDescending.IsChecked = sortTermDB.IsAscending == BooleanValue.False;
        }
        #endregion

        #region Callbacks - ComboBoxes
        // Whenever the primary combobox changes, repopulated the secondary combo box to make sure it excludes the currently selected item
        private void PrimaryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateSecondaryUIElements();
        }
        #endregion

        #region Ok/Cancel buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            string selectedPrimaryItem = (string)PrimaryComboBox.SelectedItem;
            string selectedSecondaryItem = (string)SecondaryComboBox.SelectedItem;

            foreach (SortTerm sortTerm in sortTermList)
            {
                if (selectedPrimaryItem == fileDisplayLabel)
                {
                    SortTerm1.DataLabel = DatabaseColumn.File;
                    SortTerm1.DisplayLabel = fileDisplayLabel;
                    SortTerm1.ControlType = string.Empty;
                }
                else if (selectedPrimaryItem == dateDisplayLabel)
                {
                    SortTerm1.DataLabel = DatabaseColumn.DateTime;
                    SortTerm1.DisplayLabel = dateDisplayLabel;
                    SortTerm1.ControlType = string.Empty;
                }
                else if (selectedPrimaryItem == relativePathDisplayLabel)
                {
                    SortTerm1.DataLabel = DatabaseColumn.RelativePath;
                    SortTerm1.DisplayLabel = relativePathDisplayLabel;
                    SortTerm1.ControlType = string.Empty;
                }
                else if (selectedPrimaryItem == sortTerm.DisplayLabel)
                {
                    SortTerm1.DataLabel = sortTerm.DataLabel;
                    SortTerm1.DisplayLabel = sortTerm.DisplayLabel;
                    SortTerm1.ControlType = sortTerm.ControlType;
                }
                SortTerm1.IsAscending = (PrimaryAscending.IsChecked == true) ? BooleanValue.True : BooleanValue.False;
            }

            if (selectedSecondaryItem != SortTermValues.NoneDisplayLabel)
            {
                foreach (SortTerm sortTerm in sortTermList)
                {
                    if (selectedSecondaryItem == fileDisplayLabel)
                    {
                        SortTerm2.DataLabel = DatabaseColumn.File;
                        SortTerm2.DisplayLabel = fileDisplayLabel;
                        SortTerm2.ControlType = string.Empty;
                        SortTerm2.IsAscending = (SecondaryAscending.IsChecked == true) ? BooleanValue.True : BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == dateDisplayLabel)
                    {
                        SortTerm2.DataLabel = DatabaseColumn.DateTime;
                        SortTerm2.DisplayLabel = dateDisplayLabel;
                        SortTerm2.ControlType = string.Empty;
                        SortTerm2.IsAscending = (SecondaryAscending.IsChecked == true) ? BooleanValue.True : BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == dateDisplayLabel)
                    {
                        SortTerm2.DataLabel = DatabaseColumn.DateTime;
                        SortTerm2.DisplayLabel = dateDisplayLabel;
                        SortTerm2.ControlType = string.Empty;
                        SortTerm2.IsAscending = (SecondaryAscending.IsChecked == true) ? BooleanValue.True : BooleanValue.False;
                    }
                    else if (selectedSecondaryItem == sortTerm.DisplayLabel)
                    {
                        SortTerm2.DataLabel = sortTerm.DataLabel;
                        SortTerm2.DisplayLabel = sortTerm.DisplayLabel;
                        SortTerm2.ControlType = sortTerm.ControlType;
                        SortTerm2.IsAscending = (SecondaryAscending.IsChecked == true) ? BooleanValue.True : BooleanValue.False;
                    }
                }
            }
            DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
