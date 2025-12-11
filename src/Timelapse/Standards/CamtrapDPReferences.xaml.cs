using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPReferences.xaml
    /// </summary>
    public partial class CamtrapDPReferences 
    {
        #region Properties and Variables: JsonReferencesList, ReferencesList, and Fields
        public string JsonReferencesList { get; set; }

        // The main list of references. It is
        // - initially populated by the initial jsonReferencesList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<string> ReferencesList { get; set; }

        // The fields used to construct the EditList

        public Fields ReferenceField { get; set; } =
            new("Reference",
                "A free-form reference, ideally including a DOI and following a standard reference formatting style.");
        #endregion

        #region Private variables
        private bool dontUpdate;
        private bool setFocus = true;
        private bool resetSelectedIndexToSavedIndex;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPReferences(Window owner, string jsonReferencesList)
        {
            InitializeComponent();
            Owner = owner;
            JsonReferencesList = jsonReferencesList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonReferencesList))
            {
                // Make sure its a valid json array
                JsonReferencesList = "[]";
            }

            try
            {
                ReferencesList = new(JsonConvert.DeserializeObject<List<string>>(JsonReferencesList));
            }
            catch (Exception)
            {
                // Maybe show an error dialog?
                DialogResult = false;
            }

            DataGrid_Refresh();
            if (dataGrid.Items.Count == 0)
            {
                NewRow_OnClick(null, null);
            }
            if (dataGrid.Items.Count > 0)
            {
                dataGrid.SelectedIndex = 0;
                dataGridSelectedRow = dataGrid.SelectedIndex;
            }
        }
        #endregion

        #region Callbacks and helpers: DataGrid
        // Refresh the data grid to show the current itmes in the references list
        private void DataGrid_Refresh()
        {
            dontUpdate = true;
            dataGrid.ItemsSource = null;
            dataGrid.Items.Clear();
            dataGrid.ItemsSource = ReferencesList;
            dontUpdate = false;
            dataGrid.SelectedIndex = dataGridSelectedRow < dataGrid.Items.Count ? dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dontUpdate)
            {
                return;
            }

            if (resetSelectedIndexToSavedIndex)
            {
                dontUpdate = true;
                dataGrid.SelectedIndex = dataGridSelectedRow;
                dontUpdate = false;
            }
            else
            {
                dataGridSelectedRow = dataGrid.SelectedIndex;
            }

            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            dontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < ReferencesList.Count)
            {
                DeleteRow.IsEnabled = true;
                string references_ = ReferencesList[dataGrid.SelectedIndex];
                DataFieldReference.Text = references_;
            }
            else
            {
                DeleteRow.IsEnabled = false;

                dontUpdate = true;
                DataFieldReference.Text = string.Empty;
            }
            dontUpdate = false;
            if (setFocus)
            {
                DataFieldReference.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            ReferencesList.Add(string.Empty);
            dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DataFieldReference.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < ReferencesList.Count)
            {
                dontUpdate = true;
                ReferencesList.RemoveAt(dataGrid.SelectedIndex);
                dontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    setFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    setFocus = true;
                }
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Done_OnClick(object sender, RoutedEventArgs e)
        {
            JsonSerialize();
            DialogResult = true;
        }
        #endregion

        #region Callbacks: DataField edits
        // When a data field is edited, update the contributors list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (dontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < ReferencesList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldReference":
                        resetSelectedIndexToSavedIndex = true;
                        ReferencesList[dataGrid.SelectedIndex] = tb.Text;
                        resetSelectedIndexToSavedIndex = false;
                        break;
                }
            }
            setFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedIndex = dataGridSelectedRow;
            dataGrid.SelectedItem = dataGridSelectedRow;
            setFocus = true;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, remove it (to make for a cleaner json)
            // Note that we could put in a check for required fields here...
            List<string> referencesListForExport = [];
            foreach (string reference in ReferencesList)
            {
                if (false == string.IsNullOrWhiteSpace(reference))
                {
                    referencesListForExport.Add(reference);
                }
            }

            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            JsonReferencesList = JsonConvert.SerializeObject(referencesListForExport, settings);
        }
        #endregion

        #region Fields class
        //The EditFields
        public class Fields(string label, string tooltip)
        {
            public string Label { get; set; } = label;
            public string Tooltip { get; set; } = tooltip;
        }
        #endregion
    }
}

