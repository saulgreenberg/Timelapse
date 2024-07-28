using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPRelatedIdentifiers.xaml
    /// </summary>
    public partial class CamtrapDPRelatedIdentifiers : Window
    {
        #region Properties and Variables: JsonRelatedIdentifiersList, Fields
        // The initial and final tring of contributors, in json format
        public string JsonRelatedIdentifiersList { get; set; }

        // The main list of contributors. It is
        // - initially populated by the initial jsonContributorList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<RelatedIdentifier> RelatedIdentifiersList { get; set; }

        // The fields used to construct the EditList
        public Fields RelationTypeField { get; set; } =
            new Fields(" Relation type*",
            $"Description of the relationship between the resource (the package) and the related resource.{Environment.NewLine}" +
            "• e.g., \" IsCitedBy\"");

        public Fields RelatedIdentifierField { get; set; } =
            new Fields("Related identifier*",
                $"Unique identifier of the related resource (e.g. a DOI or URL).{Environment.NewLine}" +
                "• e.g., \"https://doi.org/10.1000/100\"");

        public Fields ResourceTypeGeneralField { get; set; } =
            new Fields("Resource type - general",
                $"General type of the related resource..{Environment.NewLine}" +
                "• e.g., \"ConferencePaper\"");

        public Fields RelatedIdentifierTypeField { get; set; } =
            new Fields("Related identifier type*",
                $"Type of the RelatedIdentifier.{Environment.NewLine}" +
                "• e.g., \"DOI\"");
        #endregion

        #region Private variables
        private bool DontUpdate = false;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPRelatedIdentifiers(Window owner, string jsonRelatedIdentifiersList)
        {
            InitializeComponent();
            this.Owner = owner;
            this.JsonRelatedIdentifiersList = jsonRelatedIdentifiersList;
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
                this.RelatedIdentifiersList = new ObservableCollection<RelatedIdentifier>(JsonConvert.DeserializeObject<List<RelatedIdentifier>>(JsonRelatedIdentifiersList));
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
        // Refresh the data grid to show the current itmes in the RelatedIdentifiers list
        private void DataGrid_Refresh()
        {
            this.DontUpdate = true;
            this.dataGrid.ItemsSource = null;
            this.dataGrid.Items.Clear();
            this.dataGrid.ItemsSource = this.RelatedIdentifiersList;
            this.DontUpdate = false;
            dataGrid.SelectedIndex = this.dataGridSelectedRow < this.dataGrid.Items.Count ? this.dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        // When a data grid row is selected, populate the data fields with that row's contents
        // The enable state of the Delete button should also reflect whether there is a row to delete
        private void DataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            this.dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DontUpdate = true;
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.RelatedIdentifiersList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                RelatedIdentifier relatedIdentifier = this.RelatedIdentifiersList[dataGrid.SelectedIndex];
                this.DataFieldRelationType.Text = relatedIdentifier.relationType;
                this.DataFieldRelatedIdentifier.Text = relatedIdentifier.relatedIdentifier;
                this.DataFieldResourceTypeGeneral.Text = relatedIdentifier.resourceTypeGeneral;
                this.DataFieldRelatedIdentifierType.Text = relatedIdentifier.relatedIdentifierType;

                this.DontUpdate = false;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;
                this.DontUpdate = true;
                this.DataFieldRelationType.Text = string.Empty;
                this.DataFieldRelatedIdentifier.Text = string.Empty;
                this.DataFieldResourceTypeGeneral.Text = string.Empty;
                this.DataFieldRelatedIdentifierType.Text = string.Empty;
            }
            this.DontUpdate = false;
            if (this.SetFocus)
            {
                this.DataFieldRelationType.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.RelatedIdentifiersList.Add(new RelatedIdentifier());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldRelationType.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.RelatedIdentifiersList.Count)
            {
                this.RelatedIdentifiersList.RemoveAt(dataGrid.SelectedIndex);
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
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.RelatedIdentifiersList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldRelatedIdentifier":
                        this.RelatedIdentifiersList[dataGrid.SelectedIndex].relatedIdentifier = tb.Text;
                        break;
                }
            }

            this.SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.SetFocus = true;
        }

        private void DataField_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.RelatedIdentifiersList.Count && cb.SelectedValue != null)
            {
                switch (cb.Name)
                {
                    case "DataFieldRelationType":
                        this.RelatedIdentifiersList[dataGrid.SelectedIndex].relationType = (string)((ComboBoxItem)cb.SelectedValue).Content;
                        break;
                case "DataFieldRelatedIdentifierType":
                        this.RelatedIdentifiersList[dataGrid.SelectedIndex].relatedIdentifierType = (string)((ComboBoxItem)cb.SelectedValue).Content;
                        break;
                case "DataFieldResourceTypeGeneral":
                        this.RelatedIdentifiersList[dataGrid.SelectedIndex].resourceTypeGeneral = (string)((ComboBoxItem)cb.SelectedValue).Content;
                        break;
                }
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
        }
        #endregion

        #region Json Serializer
        private void JsonSerialize()
        {
            List<RelatedIdentifier> relatedIdentifiersListForExport = new List<RelatedIdentifier>();
            foreach (RelatedIdentifier taxonomic in this.RelatedIdentifiersList)
            {
                PropertyInfo[] properties = typeof(RelatedIdentifier).GetProperties();
                bool allNull = true;
                RelatedIdentifier newTaxonomic = new RelatedIdentifier();
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
                    relatedIdentifiersListForExport.Add(newTaxonomic);
                }
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonRelatedIdentifiersList = JsonConvert.SerializeObject(relatedIdentifiersListForExport, settings);
        }
        #endregion

        #region Class: RelatedIdentifier 
        // A contributor has these fields, as defined in the CamtrapDP specification
        public class RelatedIdentifier
        {
            public string relationType { get; set; } 
            public string relatedIdentifier { get; set; } 
            public string resourceTypeGeneral { get; set; } 
            public string relatedIdentifierType { get; set; } 
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
