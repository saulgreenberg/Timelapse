using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Timelapse.Dialog;
using Timelapse.Util;
using TextBox = System.Windows.Controls.TextBox;
#pragma warning disable IDE1006

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPTaxonomic.xaml
    /// </summary>
    public partial class CamtrapDPTaxonomic 
    {
        #region Properties and Variables: JsonTaxonomicList, TaxonomicList, and Fields
        // The json list will eventally contain a serialized taxonomic object
        public string JsonTaxonomicList { get; set; }

        // The main list of taxonomic fields. It is
        // - initially populated by the initial jsonTaxonomicList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is available as a json string when done.
        // - VernacualNames is itself a list of keyvalue pairs that has to be handled separately
        public ObservableCollection<taxonomic> TaxonomicList { get; set; }

        // Fields are used to bind a field label and tooltip info in the xaml
        public Fields ScientificNameField { get; set; } =
            new("Scientific name*",
                $"Scientific name of the taxon.{Environment.NewLine}" +
                $"see http://rs.tdwg.org/dwc/terms/scientificName.{Environment.NewLine}" +
                "• e.g., \"Canis lupus\"");

        public Fields TaxonIDField { get; set; } =
            new("Taxon id",
                "Unique identifier of the taxon. " +
                $"Preferably a global unique identifier issued by an authoritative checklist.{Environment.NewLine}" +
                 "• e.g., \"https://www.checklistbank.org/dataset/COL2023/taxon/QLXL\"");

        public Fields TaxonRankField { get; set; } =
            new("Taxonomic rank",
                $"Taxonomic rank of the scientific name.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/taxonRank {Environment.NewLine}" +
                 "• e.g., \"species\"");

        public Fields KingdomField { get; set; } =
            new("Kingdom",
                $"Kingdom in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/kingdom {Environment.NewLine}" +
                "• e.g., \"Animalia\"");

        public Fields PhylumField { get; set; } =
            new("Phylum",
                $"Phylum or division in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/phylum {Environment.NewLine}" +
                "• e.g., \"Chordata\"");

        public Fields ClassField { get; set; } =
            new("Class",
                $"Class in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/class {Environment.NewLine}" +
                "• e.g., \"Mammalia\"");

        public Fields OrderField { get; set; } =
            new("Order",
                $"Order in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/order {Environment.NewLine}" +
                "• e.g., \"Carnivora\"");

        public Fields FamilyField { get; set; } =
            new("Family",
                $"Family in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/family {Environment.NewLine}" +
                "• e.g., \"Canidae\"");

        public Fields GenusField { get; set; } =
            new("Genus",
                $"Genus in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/genus {Environment.NewLine}" +
                "• e.g., \"Canis\"");

        public Fields VernacularNamesField { get; set; } =
            new("Vernacular names",
                $"Common or vernacular names of the taxon, as languageCode: vernacular name pairs.{Environment.NewLine}" +
                $"Language codes should follow ISO 693-3 (e.g. eng for English).{Environment.NewLine}" +
                $"See https://iso639-3.sil.org/code_tables/639/data {Environment.NewLine}" +
                "• e.g., \"{'eng': 'wolf', 'fra': 'loup gris'}\"");
        #endregion

        #region Private variables
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPTaxonomic(Window owner, string jsonTaxonomicList)
        {
            InitializeComponent();
            DataContext = this;
            Owner = owner;
            JsonTaxonomicList = jsonTaxonomicList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            DataContext = this;
            if (string.IsNullOrWhiteSpace(JsonTaxonomicList))
            {
                // Make sure its a valid json array
                JsonTaxonomicList = "[]";
            }

            try
            {
                TaxonomicList = new(JsonConvert.DeserializeObject<List<taxonomic>>(JsonTaxonomicList));
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
            dataGrid.ItemsSource = TaxonomicList;
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
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count)
            {
                DeleteRow.IsEnabled = true;
                taxonomic taxonomic = TaxonomicList[dataGrid.SelectedIndex];
                DataFieldScientificName.Text = taxonomic.scientificName;
                DataFieldTaxonID.Text = taxonomic.taxonID;
                DataFieldTaxonRank.Text = taxonomic.taxonRank;
                DataFieldKingdom.Text = taxonomic.kingdom;
                DataFieldPhylum.Text = taxonomic.phylum;
                DataFieldClass.Text = taxonomic.class_;
                DataFieldOrder.Text = taxonomic.order;
                DataFieldFamily.Text = taxonomic.family;
                DataFieldGenus.Text = taxonomic.genus;
            }
            else
            {
                DeleteRow.IsEnabled = false;
                DataFieldScientificName.Text = string.Empty;
                DataFieldTaxonID.Text = string.Empty;
                DataFieldTaxonRank.Text = string.Empty;
                DataFieldKingdom.Text = string.Empty;
                DataFieldPhylum.Text = string.Empty;
                DataFieldClass.Text = string.Empty;
                DataFieldOrder.Text = string.Empty;
                DataFieldFamily.Text = string.Empty;
                DataFieldGenus.Text = string.Empty;
            }
            DontUpdate = false;
            if (SetFocus)
            {
                DataFieldScientificName.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            TaxonomicList.Add(new());
            dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            DataFieldScientificName.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count)
            {
                DontUpdate = true;
                TaxonomicList.RemoveAt(dataGrid.SelectedIndex);
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
        // When a data field is edited, update the taxonomic list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldScientificName":
                        TaxonomicList[dataGrid.SelectedIndex].scientificName = tb.Text;
                        break;
                    case "DataFieldTaxonID":
                        TaxonomicList[dataGrid.SelectedIndex].taxonID = tb.Text;
                        break;
                    case "DataFieldKingdom":
                        TaxonomicList[dataGrid.SelectedIndex].kingdom = tb.Text;
                        break;
                    case "DataFieldPhylum":
                        TaxonomicList[dataGrid.SelectedIndex].phylum = tb.Text;
                        break; 
                    case "DataFieldClass":
                        TaxonomicList[dataGrid.SelectedIndex].class_ = tb.Text;
                        break; 
                    case "DataFieldOrder":
                        TaxonomicList[dataGrid.SelectedIndex].order = tb.Text;
                        break; 
                    case "DataFieldFamily":
                        TaxonomicList[dataGrid.SelectedIndex].family = tb.Text;
                        break; 
                    case "DataFieldGenus":
                        TaxonomicList[dataGrid.SelectedIndex].genus = tb.Text;
                        break;
                }
            }
            SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = dataGridSelectedRow;
            SetFocus = true;
        }

        // Rank updates are via combobox changes
        private void DataFieldTaxonRank_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count && cb.SelectedValue != null)
            {
                TaxonomicList[dataGrid.SelectedIndex].taxonRank = (string)((ComboBoxItem)cb.SelectedValue).Content;
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
            List<taxonomic> taxonomicListForExport = [];
            foreach (taxonomic taxonomic in TaxonomicList)
            {
                PropertyInfo[] properties = typeof(taxonomic).GetProperties();
                bool allNull = true;
                taxonomic newTaxonomic = new();
                foreach (PropertyInfo property in properties)
                {
                    // Ignore vernacularCounts, as that is just used for display purposes internally
                    if (property.Name != "vernacularCount" && property.GetValue(taxonomic) != null && !string.IsNullOrWhiteSpace(property.GetValue(taxonomic)?.ToString()))
                    {
                        allNull = false;
                        property.SetValue(newTaxonomic, property.GetValue(taxonomic));
                    }
                    else if (property.Name != "vernacularCount")
                    {
                        property.SetValue(newTaxonomic, null);
                    }
                }
                if (!allNull)
                {
                    taxonomicListForExport.Add(newTaxonomic);
                }
            }
 
            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            JsonTaxonomicList = JsonConvert.SerializeObject(taxonomicListForExport, settings);
        }
        #endregion

        #region Class: Fields 
        //The EditFields
        public class Fields(string label, string tooltip)
        {
            public string Label { get; set; } = label;
            public string Tooltip { get; set; } = tooltip;
        }

        #region VernacularNames editor callbacks

        private void VernacularButton_OnOpened(object sender, RoutedEventArgs e)
        {
            if (dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count)
            {
                TBVernacularItemsEditor.Text = string.Empty;
                if (null != TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
                {
                    foreach (KeyValuePair<string, string> vItem in TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
                    {
                        TBVernacularItemsEditor.Text += $"{vItem.Key}:{vItem.Value}{Environment.NewLine}";
                    }
                }
                TBVernacularItemsEditor.Text = TBVernacularItemsEditor.Text.TrimEnd('\r', '\n');
            }
        }

        private void VernacularButton_Click(object sender, RoutedEventArgs e)
        {
            // This will trigger the closed event, caught below
            DataFieldVernacularNames.IsOpen = false;
        }
        private void VernacularButton_Closed(object sender, RoutedEventArgs e)
        {
            SetVernacularItemsListFromStringList(
                TBVernacularItemsEditor.Text.Split([Environment.NewLine],
                StringSplitOptions.None).ToList());
            DataGrid_Refresh();
        }
        #endregion

        #region VernacularItems Helpers
        private void SetVernacularItemsListFromStringList(List<string> stringList)
        {

            if (dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count)
            {

                if (null == TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
                {
                    TaxonomicList[dataGrid.SelectedIndex].vernacularNames = [];
                }
                else
                {
                    TaxonomicList[dataGrid.SelectedIndex].vernacularNames.Clear();
                }

                // Populate the list
                foreach (string str in stringList)
                {
                    KeyValuePair<string, string> vItem = VernacularItemFromString(str);
                    if (vItem.Key != string.Empty)
                    {
                        TaxonomicList[dataGrid.SelectedIndex].vernacularNames.Add(vItem.Key, vItem.Value);
                    }
                }
            }
        }

        public static KeyValuePair<string, string> VernacularItemFromString(string itemAsString)
        {
            string[] items = itemAsString.Split(':');
            if (items.Length != 2)
            {
                // Not in correct format!
                return new(string.Empty, string.Empty);
            }

            return new(items[0].Trim(), items[1].Trim());
        }
        #endregion
    }
    #endregion
}