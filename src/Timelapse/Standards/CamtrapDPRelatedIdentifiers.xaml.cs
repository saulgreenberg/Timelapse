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
    /// Interaction logic for CamtrapDPRelatedIdentifiers.xaml
    /// </summary>
    public partial class CamtrapDPRelatedIdentifiers 
    {
        #region Properties and Variables: JsonRelatedIdentifiersList, Fields
        // The initial and final tring of contributors, in json format
        public string JsonRelatedIdentifiersList { get; set; }

        // The main list of contributors. It is
        // - initially populated by the initial jsonContributorList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<relatedIdentifiers> RelatedIdentifiersList { get; set; }

        // The fields used to construct the EditList
        public Fields RelationTypeField { get; set; } =
            new(" Relation type*",
            $"Description of the relationship between the resource (the package) and the related resource.{Environment.NewLine}" +
            "• e.g., \" IsCitedBy\"");

        public Fields RelatedIdentifierField { get; set; } =
            new("Related identifier*",
                $"Unique identifier of the related resource (e.g. a DOI or URL).{Environment.NewLine}" +
                "• e.g., \"https://doi.org/10.1000/100\"");

        public Fields ResourceTypeGeneralField { get; set; } =
            new("Resource type - general",
                $"General type of the related resource..{Environment.NewLine}" +
                "• e.g., \"ConferencePaper\"");

        public Fields RelatedIdentifierTypeField { get; set; } =
            new("Related identifier type*",
                $"Type of the RelatedIdentifier.{Environment.NewLine}" +
                "• e.g., \"DOI\"");
        #endregion

        #region Private variables
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPRelatedIdentifiers(Window owner, string jsonRelatedIdentifiersList)
        {
            InitializeComponent();
            Owner = owner;
            JsonRelatedIdentifiersList = jsonRelatedIdentifiersList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonRelatedIdentifiersList))
            {
                // Make sure its a valid json array
                JsonRelatedIdentifiersList = "[]";
            }

            try
            {
                RelatedIdentifiersList = new(JsonConvert.DeserializeObject<List<relatedIdentifiers>>(JsonRelatedIdentifiersList));
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
        // Refresh the data grid to show the current itmes in the RelatedIdentifiers list
        private void DataGrid_Refresh()
        {
            DontUpdate = true;
            dataGrid.ItemsSource = null;
            dataGrid.Items.Clear();
            dataGrid.ItemsSource = RelatedIdentifiersList;
            DontUpdate = false;
            dataGrid.SelectedIndex = dataGridSelectedRow < dataGrid.Items.Count ? dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < RelatedIdentifiersList.Count)
            {
                DeleteRow.IsEnabled = true;
                relatedIdentifiers relatedIdentifier = RelatedIdentifiersList[dataGrid.SelectedIndex];
                DataFieldRelationType.Text = relatedIdentifier.relationType;
                DataFieldRelatedIdentifier.Text = relatedIdentifier.relatedIdentifier;
                DataFieldResourceTypeGeneral.Text = relatedIdentifier.resourceTypeGeneral;
                DataFieldRelatedIdentifierType.Text = relatedIdentifier.relatedIdentifierType;

                DontUpdate = false;
            }
            else
            {
                DeleteRow.IsEnabled = false;
                DontUpdate = true;
                DataFieldRelationType.Text = string.Empty;
                DataFieldRelatedIdentifier.Text = string.Empty;
                DataFieldResourceTypeGeneral.Text = string.Empty;
                DataFieldRelatedIdentifierType.Text = string.Empty;
            }
            DontUpdate = false;
            if (SetFocus)
            {
                DataFieldRelationType.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            RelatedIdentifiersList.Add(new());
            dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DataFieldRelationType.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < RelatedIdentifiersList.Count)
            {
                RelatedIdentifiersList.RemoveAt(dataGrid.SelectedIndex);
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
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < RelatedIdentifiersList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldRelatedIdentifier":
                        RelatedIdentifiersList[dataGrid.SelectedIndex].relatedIdentifier = tb.Text;
                        break;
                }
            }

            SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
            SetFocus = true;
        }

        private void DataField_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < RelatedIdentifiersList.Count && cb.SelectedValue != null)
            {
                switch (cb.Name)
                {
                    case "DataFieldRelationType":
                        RelatedIdentifiersList[dataGrid.SelectedIndex].relationType = (string)((ComboBoxItem)cb.SelectedValue).Content;
                        break;
                case "DataFieldRelatedIdentifierType":
                        RelatedIdentifiersList[dataGrid.SelectedIndex].relatedIdentifierType = (string)((ComboBoxItem)cb.SelectedValue).Content;
                        break;
                case "DataFieldResourceTypeGeneral":
                        RelatedIdentifiersList[dataGrid.SelectedIndex].resourceTypeGeneral = (string)((ComboBoxItem)cb.SelectedValue).Content;
                        break;
                }
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            List<relatedIdentifiers> relatedIdentifiersListForExport = [];
            foreach (relatedIdentifiers taxonomic in RelatedIdentifiersList)
            {
                PropertyInfo[] properties = typeof(relatedIdentifiers).GetProperties();
                bool allNull = true;
                relatedIdentifiers newTaxonomic = new();
                foreach (PropertyInfo property in properties)
                {
                    if (property.GetValue(taxonomic) != null && !string.IsNullOrWhiteSpace(property.GetValue(taxonomic)?.ToString()))
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
                    relatedIdentifiersListForExport.Add(newTaxonomic);
                }
            }

            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            JsonRelatedIdentifiersList = JsonConvert.SerializeObject(relatedIdentifiersListForExport, settings);
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
