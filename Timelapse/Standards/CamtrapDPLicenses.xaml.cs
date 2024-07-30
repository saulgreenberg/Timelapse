using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPLicenses.xaml
    /// </summary>
    public partial class CamtrapDPLicenses : Window
    {
        #region Properties and Variables: JsonLicensesList, and Fields
        // The list of licenses, in json format
        public string JsonLicensesList { get; set; }

        // The main list of licenses. It is
        // - initially populated by the initial jsonLicenseList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<Standards.licenses> LicensesList { get; set; }

        // The fields used to construct the EditList

        public Fields NameField { get; set; } =
            new Fields("Name*",
                "An Open Definition license ID. See https://opendefinition.org/licenses/api/{Environment.NewLine}" +
                "• e.g., \"ODC-PDDL-1.0\"");

        public Fields PathField { get; set; } =
            new Fields("Path*",
                $"An email to the source.{Environment.NewLine}" +
                 "• e.g., \"bloggs@agouti.com\"");

        public Fields TitleField { get; set; } =
            new Fields("Title",
                $"A human-readable title of the license{Environment.NewLine}" +
                "• e.g., \"Open Data Commons Public Domain Dedication and License v1.0\"");

        public Fields ScopeField { get; set; } =
            new Fields("Scope*",
                $"Scope of the license to either or both of: {Environment.NewLine}" +
                $"- data: applies to the content of this package and resources,{Environment.NewLine}" +
                $"- media: applies to the media files referenced in media.{Environment.NewLine}" +
                "• e.g., \"data\"");
       
        public List<string> ScopeItems { get; set; } = new List<string>() {"data", "media"};
        
        #endregion

        #region Private variables
        private bool DontUpdate = false;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPLicenses(Window owner, string jsonLicensesList)
        {
            InitializeComponent();
            this.Owner = owner;
            this.JsonLicensesList = jsonLicensesList;
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
                this.LicensesList = new ObservableCollection<Standards.licenses>(JsonConvert.DeserializeObject<List<Standards.licenses>>(JsonLicensesList));
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
        // Refresh the data grid to show the current itmes in the sources list
        private void DataGrid_Refresh()
        {
            this.DontUpdate = true;
            this.dataGrid.ItemsSource = null;
            this.dataGrid.Items.Clear();
            this.dataGrid.ItemsSource = this.LicensesList;
            this.DontUpdate = false;
            dataGrid.SelectedIndex = this.dataGridSelectedRow < this.dataGrid.Items.Count ? this.dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the DeleteLicenses button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            this.dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.LicensesList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                this.DontUpdate = true;
                Standards.licenses licenses = this.LicensesList[dataGrid.SelectedIndex];
                this.DataFieldName.Text = licenses.name;
                this.DataFieldPath.Text = licenses.path;
                this.DataFieldTitle.Text = licenses.title;
                this.DataFieldScope.Text = licenses.scope;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;
                this.DataFieldName.Text = string.Empty;
                this.DataFieldPath.Text = string.Empty;
                this.DataFieldTitle.Text = string.Empty;
                this.DataFieldScope.Text = string.Empty;
            }
            this.DontUpdate = false;
            if (this.SetFocus)
            {
                this.DataFieldName.Focus();
            }
        }
        #endregion

        #region Button callbacks
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.LicensesList.Add(new Standards.licenses());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldName.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.LicensesList.Count)
            {
                this.DontUpdate = true;
                this.LicensesList.RemoveAt(dataGrid.SelectedIndex);
                this.DontUpdate = false;
                // When a row is deleted, select the last row if there is one.
                dataGrid.SelectedIndex = dataGrid.Items.Count > 0 ? dataGrid.Items.Count - 1 : -1;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
                if (dataGrid.SelectedIndex == -1)
                {
                    // This will clear the edit fields if nothing is selected
                    this.SetFocus = false;
                    DataGrid_OnSelectionChanged(null, null);
                    this.SetFocus = true;
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

        #region DataField callbacks
        // When a data field is edited, update the licenses list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.LicensesList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldName":
                        this.LicensesList[dataGrid.SelectedIndex].name = tb.Text;
                        break;
                    case "DataFieldPath":
                        this.LicensesList[dataGrid.SelectedIndex].path = tb.Text;
                        break;
                    case "DataFieldTitle":
                        this.LicensesList[dataGrid.SelectedIndex].title = tb.Text;
                        break;
                    case "DataFieldScope":
                        this.LicensesList[dataGrid.SelectedIndex].scope = tb.Text;
                        break;
                }
            }
            this.SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.SetFocus = true;
        }

        private void DataFieldScope_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is CheckComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.LicensesList.Count && cb.SelectedValue != null)
            {
                this.LicensesList[dataGrid.SelectedIndex].scope = cb.Text;
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            // If an item is an empty string, set it to null (to make for a cleaner json)
            // If a taxonomic object is all empty, skip it/
            // Note that we could put in a check for required fields here...
            List<Standards.licenses> taxonomicListForExport = new List<Standards.licenses>();
            foreach (Standards.licenses license in this.LicensesList)
            {
                PropertyInfo[] properties = typeof(Standards.licenses).GetProperties();
                bool allNull = true;
                Standards.licenses newLicensesList = new Standards.licenses();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(license) != null && !string.IsNullOrWhiteSpace(property.GetValue(license).ToString()))
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

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonLicensesList = JsonConvert.SerializeObject(taxonomicListForExport, settings);
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
