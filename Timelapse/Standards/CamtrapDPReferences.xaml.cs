using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPReferences.xaml
    /// </summary>
    public partial class CamtrapDPReferences : Window
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
            new Fields("Reference",
                $"A free-form reference, ideally including a DOI and following a standard reference formatting style.");
        #endregion

        #region Private variables
        private bool dontUpdate = false;
        private bool setFocus = true;
        private bool resetSelectedIndexToSavedIndex = false;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPReferences(Window owner, string jsonReferencesList)
        {
            InitializeComponent();
            this.Owner = owner;
            this.JsonReferencesList = jsonReferencesList;
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
                this.ReferencesList = new ObservableCollection<string>(JsonConvert.DeserializeObject<List<string>>(JsonReferencesList));
            }
            catch (Exception)
            {
                // Maybe show an error dialog?
                DialogResult = false;
            }

            DataGrid_Refresh();
            if (this.dataGrid.Items.Count == 0)
            {
                this.NewRow_OnClick(null, null);
            }
            if (this.dataGrid.Items.Count > 0)
            {
                this.dataGrid.SelectedIndex = 0;
                this.dataGridSelectedRow = this.dataGrid.SelectedIndex;
            }
        }
        #endregion

        #region Callbacks and helpers: DataGrid
        // Refresh the data grid to show the current itmes in the references list
        private void DataGrid_Refresh()
        {
            this.dontUpdate = true;
            this.dataGrid.ItemsSource = null;
            this.dataGrid.Items.Clear();
            this.dataGrid.ItemsSource = this.ReferencesList;
            this.dontUpdate = false;
            dataGrid.SelectedIndex = this.dataGridSelectedRow < this.dataGrid.Items.Count ? this.dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.dontUpdate)
            {
                return;
            }

            if (resetSelectedIndexToSavedIndex)
            {
                this.dontUpdate = true;
                dataGrid.SelectedIndex = this.dataGridSelectedRow;
                this.dontUpdate = false;
            }
            else
            {
                this.dataGridSelectedRow = dataGrid.SelectedIndex;
            }

            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.dontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.ReferencesList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                string references_ = this.ReferencesList[dataGrid.SelectedIndex];
                this.DataFieldReference.Text = references_;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;

                this.dontUpdate = true;
                this.DataFieldReference.Text = string.Empty;
            }
            this.dontUpdate = false;
            if (this.setFocus)
            {
                this.DataFieldReference.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.ReferencesList.Add(string.Empty);
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldReference.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.ReferencesList.Count)
            {
                this.dontUpdate = true;
                this.ReferencesList.RemoveAt(dataGrid.SelectedIndex);
                this.dontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    this.setFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    this.setFocus = true;
                }
            }
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Done_OnClick(object sender, RoutedEventArgs e)
        {
            this.JsonSerialize();
            DialogResult = true;
        }
        #endregion

        #region Callbacks: DataField edits
        // When a data field is edited, update the contributors list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.dontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.ReferencesList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldReference":
                        resetSelectedIndexToSavedIndex = true;
                        this.ReferencesList[dataGrid.SelectedIndex] = tb.Text;
                        resetSelectedIndexToSavedIndex = false;
                        break;
                }
            }
            this.setFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedIndex = this.dataGridSelectedRow;
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.setFocus = true;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, remove it (to make for a cleaner json)
            // Note that we could put in a check for required fields here...
            List<string> referencesListForExport = new List<string>();
            foreach (string reference in this.ReferencesList)
            {
                if (false == string.IsNullOrWhiteSpace(reference))
                {
                    referencesListForExport.Add(reference);
                }
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonReferencesList = JsonConvert.SerializeObject(referencesListForExport, settings);
        }
        #endregion

        #region Fields class
        //The EditFields
        public class Fields
        {
            public string Label { get; set; }
            public string Tooltip { get; set; }

            public Fields(string label, string tooltip)
            {
                this.Label = label;
                this.Tooltip = tooltip;
            }
        }
        #endregion
    }
}

