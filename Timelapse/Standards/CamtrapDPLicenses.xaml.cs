using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Dialog;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPLicenses.xaml
    /// </summary>
    public partial class CamtrapDPLicenses : Window
    {
        #region Public properties
        // The list of licenses, in json format
        public string JsonLicensesList { get; set; }

        // The main list of licenses. It is
        // - initially populated by the initial jsonLicenseList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<Licenses> LicensesList { get; set; }

        // The fields used to construct the EditList

        public Fields NameField { get; set; } =
            new Fields("Name*",
                "An Open Definition license ID. See https://opendefinition.org/licenses/api/{Environment.NewLine}" +
                "• e.g., \"ODC-PDDL-1.0\"");

        public Fields PathField { get; set; } =
            new Fields("Path",
                $"An email to the source.{Environment.NewLine}" +
                 "• e.g., \"bloggs@agouti.com\"");

        public Fields TitleField { get; set; } =
            new Fields("Title",
                $"A human-readable title of the license{Environment.NewLine}" +
                "• e.g., \"Open Data Commons Public Domain Dedication and License v1.0\"");

        public Fields ScopeField { get; set; } =
            new Fields("Scope",
                $"Scope of the license to either or both of: {Environment.NewLine}" +
                $"- data: applies to the content of this package and resources,{Environment.NewLine}" +
                $"- media: applies to the media files referenced in media.{Environment.NewLine}" +
                "• e.g., \"data\"");
       
        public List<string> ScopeItems { get; set; } = new List<string>() {"data", "media"};
        
        #endregion

        #region Private variables
        private bool DontUpdate = false;
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
                this.LicensesList = new ObservableCollection<Licenses>(JsonConvert.DeserializeObject<List<Licenses>>(JsonLicensesList));
            }
            catch (Exception)
            {
                // Maybe show an error dialog?
                DialogResult = false;
            }

            DataGrid_Refresh();
            if (this.dataGrid.Items.Count > 0)
            {
                this.dataGrid.SelectedIndex = 0;
                this.dataGridSelectedRow = this.dataGrid.SelectedIndex;
            }
        }
        #endregion

        #region DataGrid callbacks and helpers
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
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.LicensesList.Count)
            {

                this.DeleteRow.IsEnabled = true;

                this.DontUpdate = true;
                Licenses licenses = this.LicensesList[dataGrid.SelectedIndex];
                this.DataFieldName.Text = licenses.name;
                this.DataFieldPath.Text = licenses.path;
                this.DataFieldTitle.Text = licenses.title;
                this.DataFieldScope.Text = licenses.scope;
                this.DontUpdate = false;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;

                this.DontUpdate = true;
                this.DataFieldName.Text = string.Empty;
                this.DataFieldPath.Text = string.Empty;
                this.DataFieldTitle.Text = string.Empty;
                this.DataFieldScope.Text = string.Empty;
                this.DontUpdate = false;
            }
        }
        #endregion

        #region Button callbacks
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.LicensesList.Add(new Licenses());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.LicensesList.Count)
            {
                this.LicensesList.RemoveAt(dataGrid.SelectedIndex);
                dataGrid.SelectedIndex = dataGridSelectedRow == -1 ? -1 : --dataGridSelectedRow;
                EditGrid.IsEnabled = dataGrid.Items.Count > 0;
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
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
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

        #region Helpers - Json Serializer
        private void JsonSerialize()
        {
            this.JsonLicensesList = JsonConvert.SerializeObject(this.LicensesList);
        }
        #endregion

        #region Licenses class
        // A contributor has these fields, as defined in the CamtrapDP specification
        public class Licenses
        {
            public string name { get; set; } = string.Empty;
            public string path { get; set; } = string.Empty;
            public string title { get; set; } = string.Empty;
            public string scope { get; set; } = string.Empty;
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
