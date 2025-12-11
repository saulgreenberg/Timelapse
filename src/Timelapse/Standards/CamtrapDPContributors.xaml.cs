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
    /// A dialog allowing a used to edit the CamptrapDP contributor fields.
    /// Initialized by a json Contributors object
    /// Returns a modified json Contributors object
    /// </summary>
    public partial class CamptrapDPContributors 
    {
        #region Properties and Variables: JsonContributorsList, Fields
        // The initial and final tring of contributors, in json format
        public string JsonContributorsList { get; set; }

        // The main list of contributors. It is
        // - initially populated by the initial jsonContributorList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<contributors> ContributorsList { get; set; }

        // The fields used to construct the EditList
        public Fields TitleField { get; set; } =
            new("Title*", 
            $"Title of this contributor (the name for a person or organization).{Environment.NewLine}" +
            "• e.g., \"Joe Bloggs\"");

        public Fields EmailField { get; set; } =
            new("Email", 
                $"An email address for this contributor.{Environment.NewLine}" +
                "• e.g., \"bloggs@gmail.com\"");

        public Fields PathField { get; set; } =
            new("Path", 
                $"A fully qualified http URL pointing to a relevant location online for this contributor.{Environment.NewLine}" +
                "• e.g., \"http://www.bloggs.com\"");

        public Fields RoleField { get; set; } =
            new("Role", 
                $"The role of this contributor. Select one from the drop-down menu.{Environment.NewLine}" +
                "• e.g., \"contributor\"");

        public Fields OrganizationField { get; set; } =
            new("Organization", 
                $"The organization this contributor is affiliated to.{Environment.NewLine}" +
                "• e.g., \"Wildlife Eco Organization\"");
        #endregion

        #region Private variables
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamptrapDPContributors(Window owner, string jsonContributorsList)
        {
            InitializeComponent();
            Owner = owner;
            JsonContributorsList = jsonContributorsList;
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
                ContributorsList = new(JsonConvert.DeserializeObject<List<contributors>>(JsonContributorsList));
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
        // Refresh the data grid to show the current itmes in the contributors list
        private void DataGrid_Refresh()
        {
            DontUpdate = true;
            dataGrid.ItemsSource = null;
            dataGrid.Items.Clear();
            dataGrid.ItemsSource = ContributorsList;
            DontUpdate = false;
            dataGrid.SelectedIndex = dataGridSelectedRow < dataGrid.Items.Count ? dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the DeleteContributors button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < ContributorsList.Count)
            {
                DeleteRow.IsEnabled = true;
                contributors contributor = ContributorsList[dataGrid.SelectedIndex];
                DataFieldTitle.Text = contributor.title ;
                DataFieldEmail.Text = contributor.email ;
                DataFieldPath.Text = contributor.path;
                DataFieldRole.Text = contributor.role ;
                DataFieldOrganization.Text = contributor.organization ;
                DontUpdate = false;
            }
            else
            {
                DeleteRow.IsEnabled = false;
                DontUpdate = true;
                DataFieldTitle.Text = string.Empty;
                DataFieldEmail.Text = string.Empty;
                DataFieldPath.Text = string.Empty;
                DataFieldRole.Text = string.Empty;
                DataFieldOrganization.Text = string.Empty;
            }
            DontUpdate = false;
            if (SetFocus)
            {
                DataFieldTitle.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            ContributorsList.Add(new());
            dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DataFieldTitle.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < ContributorsList.Count)
            {
                ContributorsList.RemoveAt(dataGrid.SelectedIndex);
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

        #region Callbacks: DataField edits
        // When a data field is edited, update the contributors list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < ContributorsList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldTitle":
                        ContributorsList[dataGrid.SelectedIndex].title = tb.Text;
                        break;
                    case "DataFieldEmail":
                        ContributorsList[dataGrid.SelectedIndex].email = tb.Text;
                        break;
                    case "DataFieldPath":
                        ContributorsList[dataGrid.SelectedIndex].path = tb.Text;
                        break;
                    case "DataFieldOrganization":
                        ContributorsList[dataGrid.SelectedIndex].organization = tb.Text;
                        break;
                }
            }

            SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
            SetFocus = true;
        }

        private void DataFieldRole_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < ContributorsList.Count && cb.SelectedValue != null)
            {
                ContributorsList[dataGrid.SelectedIndex].role = (string)((ComboBoxItem)cb.SelectedValue).Content;
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            List<contributors> contributorsListForExport = [];
            foreach (contributors contributor in ContributorsList)
            {
                PropertyInfo[] properties = typeof(contributors).GetProperties();
                bool allNull = true;
                contributors newTaxonomic = new();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(contributor) != null && !string.IsNullOrWhiteSpace(property.GetValue(contributor)?.ToString()))
                    {
                        allNull = false;
                        property.SetValue(newTaxonomic, property.GetValue(contributor));
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

            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            JsonContributorsList = JsonConvert.SerializeObject(contributorsListForExport, settings);
        }
        #endregion

        #region Class: Fields
        //The EditFields
        public class Fields(string label, string tooltip)
        {
            public string Label { get; set; } = label;
            public string Tooltip { get; set; } = tooltip;
        }
        #endregion
    }
}
