using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Timelapse.Dialog;
using Timelapse.Util;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPLicenses.xaml
    /// </summary>
    public partial class CamtrapDPLicenses 
    {
        #region Properties and Variables: JsonLicensesList, and Fields
        // The list of licenses, in json format
        public string JsonLicensesList { get; set; }

        // The main list of licenses. It is
        // - initially populated by the initial jsonLicenseList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<licenses> LicensesList { get; set; }

        // The fields used to construct the EditList

        public Fields NameField { get; set; } =
            new("Name*",
                "An Open Definition license ID. See https://opendefinition.org/licenses/api/{Environment.NewLine}" +
                "• e.g., \"ODC-PDDL-1.0\"");

        public Fields PathField { get; set; } =
            new("Path*",
                $"An email to the source.{Environment.NewLine}" +
                 "• e.g., \"bloggs@agouti.com\"");

        public Fields TitleField { get; set; } =
            new("Title",
                $"A human-readable title of the license{Environment.NewLine}" +
                "• e.g., \"Open Data Commons Public Domain Dedication and License v1.0\"");

        public Fields ScopeField { get; set; } =
            new("Scope*",
                $"Scope of the license to either or both of: {Environment.NewLine}" +
                $"- data: applies to the content of this package and resources,{Environment.NewLine}" +
                $"- media: applies to the media files referenced in media.{Environment.NewLine}" +
                "• e.g., \"data\"");
       
        // ReSharper disable once UnusedMember.Global
        public List<string> ScopeItems { get; set; } = ["data", "media"];
        
        #endregion

        #region Private variables
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPLicenses(Window owner, string jsonLicensesList)
        {
            InitializeComponent();
            Owner = owner;
            JsonLicensesList = jsonLicensesList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonLicensesList))
            {
                // Make sure its a valid json array
                JsonLicensesList = "[]";
            }

            try
            {
                LicensesList = new(JsonConvert.DeserializeObject<List<licenses>>(JsonLicensesList));
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
        // Refresh the data grid to show the current itmes in the sources list
        private void DataGrid_Refresh()
        {
            DontUpdate = true;
            dataGrid.ItemsSource = null;
            dataGrid.Items.Clear();
            dataGrid.ItemsSource = LicensesList;
            DontUpdate = false;
            dataGrid.SelectedIndex = dataGridSelectedRow < dataGrid.Items.Count ? dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the DeleteLicenses button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < LicensesList.Count)
            {
                DeleteRow.IsEnabled = true;
                DontUpdate = true;
                licenses licenses = LicensesList[dataGrid.SelectedIndex];
                DataFieldName.Text = licenses.name;
                DataFieldPath.Text = licenses.path;
                DataFieldTitle.Text = licenses.title;
                DataFieldScope.Text = licenses.scope;
            }
            else
            {
                DeleteRow.IsEnabled = false;
                DataFieldName.Text = string.Empty;
                DataFieldPath.Text = string.Empty;
                DataFieldTitle.Text = string.Empty;
                DataFieldScope.Text = string.Empty;
            }
            DontUpdate = false;
            if (SetFocus)
            {
                DataFieldName.Focus();
            }
        }
        #endregion

        #region Button callbacks
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            LicensesList.Add(new());
            dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DataFieldName.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < LicensesList.Count)
            {
                DontUpdate = true;
                LicensesList.RemoveAt(dataGrid.SelectedIndex);
                DontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    SetFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    SetFocus = true;
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

        #region DataField callbacks
        // When a data field is edited, update the licenses list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < LicensesList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldName":
                        LicensesList[dataGrid.SelectedIndex].name = tb.Text;
                        break;
                    case "DataFieldPath":
                        LicensesList[dataGrid.SelectedIndex].path = tb.Text;
                        break;
                    case "DataFieldTitle":
                        LicensesList[dataGrid.SelectedIndex].title = tb.Text;
                        break;
                    case "DataFieldScope":
                        LicensesList[dataGrid.SelectedIndex].scope = tb.Text;
                        break;
                }
            }
            SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
            SetFocus = true;
        }

        private void DataFieldScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < LicensesList.Count && cb.SelectedValue != null)
            {
                LicensesList[dataGrid.SelectedIndex].scope = cb.Text;
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
        }
    
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, set it to null (to make for a cleaner json)
            // If a taxonomic object is all empty, skip it/
            // Note that we could put in a check for required fields here...
            List<licenses> taxonomicListForExport = [];
            foreach (licenses license in LicensesList)
            {
                PropertyInfo[] properties = typeof(licenses).GetProperties();
                bool allNull = true;
                licenses newLicensesList = new();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(license) != null && !string.IsNullOrWhiteSpace(property.GetValue(license)?.ToString()))
                    {
                        allNull = false;
                        property.SetValue(newLicensesList, property.GetValue(license));
                    }
                    else 
                    {
                        property.SetValue(newLicensesList, null);
                    }
                }
                if (!allNull)
                {
                    taxonomicListForExport.Add(newLicensesList);
                }
            }

            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            JsonLicensesList = JsonConvert.SerializeObject(taxonomicListForExport, settings);
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
