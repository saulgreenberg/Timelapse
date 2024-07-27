using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Timelapse.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// A dialog allowing a used to edit the CamptrapDP contributor fields.
    /// Initialized by a json Contributors object
    /// Returns a modified json Contributors object
    /// </summary>
    public partial class CamptrapDPContributors : Window
    {
        #region Properties and Variables: JsonContributorsList, Fields
        // The initial and final tring of contributors, in json format
        public string JsonContributorsList { get; set; }

        // The main list of contributors. It is
        // - initially populated by the initial jsonContributorList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<Contributors> ContributorsList { get; set; }

        // The fields used to construct the EditList
        public Fields TitleField { get; set; } =
            new Fields("Title*", 
            $"Title of this contributor (the name for a person or organization).{Environment.NewLine}" +
            "• e.g., \"Joe Bloggs\"");

        public Fields EmailField { get; set; } =
            new Fields("Email", 
                $"An email address for this contributor.{Environment.NewLine}" +
                "• e.g., \"bloggs@gmail.com\"");

        public Fields PathField { get; set; } =
            new Fields("Path", 
                $"A fully qualified http URL pointing to a relevant location online for this contributor.{Environment.NewLine}" +
                "• e.g., \"http://www.bloggs.com\"");

        public Fields RoleField { get; set; } =
            new Fields("Role", 
                $"The role of this contributor. Select one from the drop-down menu.{Environment.NewLine}" +
                "• e.g., \"contributor\"");

        public Fields OrganizationField { get; set; } =
            new Fields("Organization", 
                $"The organization this contributor is affiliated to.{Environment.NewLine}" +
                "• e.g., \"Wildlife Eco Organization\"");
        #endregion

        #region Private variables
        private bool DontUpdate = false;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamptrapDPContributors(Window owner, string jsonContributorsList)
        {
            InitializeComponent();
            this.Owner = owner;
            this.JsonContributorsList = jsonContributorsList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonContributorsList))
            {
                // Make sure its a valid json array
                JsonContributorsList = "[]";
            }

            try
            {
                this.ContributorsList = new ObservableCollection<Contributors>(JsonConvert.DeserializeObject<List<Contributors>>(JsonContributorsList));
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
        // Refresh the data grid to show the current itmes in the contributors list
        private void DataGrid_Refresh()
        {
            this.DontUpdate = true;
            this.dataGrid.ItemsSource = null;
            this.dataGrid.Items.Clear();
            this.dataGrid.ItemsSource = this.ContributorsList;
            this.DontUpdate = false;
            dataGrid.SelectedIndex = this.dataGridSelectedRow < this.dataGrid.Items.Count ? this.dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the DeleteContributors button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            this.dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.ContributorsList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                Contributors contributor = this.ContributorsList[dataGrid.SelectedIndex];
                this.DataFieldTitle.Text = contributor.title;
                this.DataFieldEmail.Text = contributor.email;
                this.DataFieldPath.Text = contributor.path;
                this.DataFieldRole.Text = contributor.role;
                this.DataFieldOrganization.Text = contributor.organization;
                this.DontUpdate = false;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;
                this.DontUpdate = true;
                this.DataFieldTitle.Text = string.Empty;
                this.DataFieldEmail.Text = string.Empty;
                this.DataFieldPath.Text = string.Empty;
                this.DataFieldRole.Text = string.Empty;
                this.DataFieldOrganization.Text = string.Empty;
            }
            this.DontUpdate = false;
            if (this.SetFocus)
            {
                this.DataFieldTitle.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.ContributorsList.Add(new Contributors());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldTitle.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.ContributorsList.Count)
            {
                this.ContributorsList.RemoveAt(dataGrid.SelectedIndex);
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

        #region Callbacks: DataField edits
        // When a data field is edited, update the contributors list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.ContributorsList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldTitle":
                        this.ContributorsList[dataGrid.SelectedIndex].title = tb.Text;
                        break;
                    case "DataFieldEmail":
                        this.ContributorsList[dataGrid.SelectedIndex].email = tb.Text;
                        break;
                    case "DataFieldPath":
                        this.ContributorsList[dataGrid.SelectedIndex].path = tb.Text;
                        break;
                    case "DataFieldOrganization":
                        this.ContributorsList[dataGrid.SelectedIndex].organization = tb.Text;
                        break;
                }
            }

            this.SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.SetFocus = true;
        }

        private void DataFieldRole_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.ContributorsList.Count && cb.SelectedValue != null)
            {
                this.ContributorsList[dataGrid.SelectedIndex].role = (string)((ComboBoxItem)cb.SelectedValue).Content;
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            List<Contributors>contributorsListForExport = new List<Contributors>();
            foreach (Contributors taxonomic in this.ContributorsList)
            {
                PropertyInfo[] properties = typeof(Contributors).GetProperties();
                bool allNull = true;
                Contributors newTaxonomic = new Contributors();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(taxonomic) != null && !string.IsNullOrWhiteSpace(property.GetValue(taxonomic).ToString()))
                    {
                        allNull = false;
                        property.SetValue(newTaxonomic, property.GetValue(taxonomic));
                    }
                    else 
                    {
                        property.SetValue(newTaxonomic, null);
                    }
                }
                if (!allNull)
                {
                    contributorsListForExport.Add(newTaxonomic);
                }
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonContributorsList = JsonConvert.SerializeObject(contributorsListForExport, settings);
        }
        #endregion

        #region Class: Contributors 
        // A contributor has these fields, as defined in the CamtrapDP specification
        public class Contributors
        {
            public string title { get; set; } = string.Empty;
            public string email { get; set; } = string.Empty;
            public string path { get; set; } = string.Empty;
            public string role { get; set; } = string.Empty;
            public string organization { get; set; } = string.Empty;
        }
        #endregion

        #region Class: Fields
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
