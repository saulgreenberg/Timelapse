using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using TextBox = System.Windows.Controls.TextBox;
#pragma warning disable IDE1006

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPTaxonomic.xaml
    /// </summary>
    public partial class CamtrapDPTaxonomic : Window
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
        public ObservableCollection<Standards.taxonomic> TaxonomicList { get; set; }

        // Fields are used to bind a field label and tooltip info in the xaml
        public Fields ScientificNameField { get; set; } =
            new Fields("Scientific name*",
                $"Scientific name of the taxon.{Environment.NewLine}" +
                $"see http://rs.tdwg.org/dwc/terms/scientificName.{Environment.NewLine}" +
                "• e.g., \"Canis lupus\"");

        public Fields TaxonIDField { get; set; } =
            new Fields("Taxon id",
                $"Unique identifier of the taxon. " +
                $"Preferably a global unique identifier issued by an authoritative checklist.{Environment.NewLine}" +
                 "• e.g., \"https://www.checklistbank.org/dataset/COL2023/taxon/QLXL\"");

        public Fields TaxonRankField { get; set; } =
            new Fields("Taxonomic rank",
                $"Taxonomic rank of the scientific name.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/taxonRank {Environment.NewLine}" +
                 "• e.g., \"species\"");

        public Fields KingdomField { get; set; } =
            new Fields("Kingdom",
                $"Kingdom in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/kingdom {Environment.NewLine}" +
                "• e.g., \"Animalia\"");

        public Fields PhylumField { get; set; } =
            new Fields("Phylum",
                $"Phylum or division in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/phylum {Environment.NewLine}" +
                "• e.g., \"Chordata\"");

        public Fields ClassField { get; set; } =
            new Fields("Class",
                $"Class in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/class {Environment.NewLine}" +
                "• e.g., \"Mammalia\"");

        public Fields OrderField { get; set; } =
            new Fields("Order",
                $"Order in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/order {Environment.NewLine}" +
                "• e.g., \"Carnivora\"");

        public Fields FamilyField { get; set; } =
            new Fields("Family",
                $"Family in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/family {Environment.NewLine}" +
                "• e.g., \"Canidae\"");

        public Fields GenusField { get; set; } =
            new Fields("Genus",
                $"Genus in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/genus {Environment.NewLine}" +
                "• e.g., \"Canis\"");

        public Fields VernacularNamesField { get; set; } =
            new Fields("Vernacular names",
                $"Common or vernacular names of the taxon, as languageCode: vernacular name pairs.{Environment.NewLine}" +
                $"Language codes should follow ISO 693-3 (e.g. eng for English).{Environment.NewLine}" +
                $"See https://iso639-3.sil.org/code_tables/639/data {Environment.NewLine}" +
                "• e.g., \"{'eng': 'wolf', 'fra': 'loup gris'}\"");
        #endregion

        #region Private variables
        private bool DontUpdate = false;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPTaxonomic(Window owner, string jsonTaxonomicList)
        {
            InitializeComponent();
            this.DataContext = this;
            this.Owner = owner;
            this.JsonTaxonomicList = jsonTaxonomicList;
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
                this.TaxonomicList = new ObservableCollection<Standards.taxonomic>(JsonConvert.DeserializeObject<List<Standards.taxonomic>>(JsonTaxonomicList));
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
            this.dataGrid.ItemsSource = this.TaxonomicList;
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
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {
                this.DeleteRow.IsEnabled = true;
                Standards.taxonomic taxonomic = this.TaxonomicList[dataGrid.SelectedIndex];
                this.DataFieldScientificName.Text = taxonomic.scientificName;
                this.DataFieldTaxonID.Text = taxonomic.taxonID;
                this.DataFieldTaxonRank.Text = taxonomic.taxonRank;
                this.DataFieldKingdom.Text = taxonomic.kingdom;
                this.DataFieldPhylum.Text = taxonomic.phylum;
                this.DataFieldClass.Text = taxonomic.class_;
                this.DataFieldOrder.Text = taxonomic.order;
                this.DataFieldFamily.Text = taxonomic.family;
                this.DataFieldGenus.Text = taxonomic.genus;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;
                this.DataFieldScientificName.Text = string.Empty;
                this.DataFieldTaxonID.Text = string.Empty;
                this.DataFieldTaxonRank.Text = string.Empty;
                this.DataFieldKingdom.Text = string.Empty;
                this.DataFieldPhylum.Text = string.Empty;
                this.DataFieldClass.Text = string.Empty;
                this.DataFieldOrder.Text = string.Empty;
                this.DataFieldFamily.Text = string.Empty;
                this.DataFieldGenus.Text = string.Empty;
            }
            this.DontUpdate = false;
            if (this.SetFocus)
            {
                this.DataFieldScientificName.Focus();
            }
        }
        #endregion

        #region Callbacks: Buttons 
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.TaxonomicList.Add(new Standards.taxonomic());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            this.DataFieldScientificName.Focus();
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {
                this.DontUpdate = true;
                this.TaxonomicList.RemoveAt(dataGrid.SelectedIndex);
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
        // When a data field is edited, update the taxonomic list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldScientificName":
                        this.TaxonomicList[dataGrid.SelectedIndex].scientificName = tb.Text;
                        break;
                    case "DataFieldTaxonID":
                        this.TaxonomicList[dataGrid.SelectedIndex].taxonID = tb.Text;
                        break;
                    case "DataFieldKingdom":
                        this.TaxonomicList[dataGrid.SelectedIndex].kingdom = tb.Text;
                        break;
                    case "DataFieldPhylum":
                        this.TaxonomicList[dataGrid.SelectedIndex].phylum = tb.Text;
                        break; break;
                    case "DataFieldClass":
                        this.TaxonomicList[dataGrid.SelectedIndex].class_ = tb.Text;
                        break; break;
                    case "DataFieldOrder":
                        this.TaxonomicList[dataGrid.SelectedIndex].order = tb.Text;
                        break; break;
                    case "DataFieldFamily":
                        this.TaxonomicList[dataGrid.SelectedIndex].family = tb.Text;
                        break; break;
                    case "DataFieldGenus":
                        this.TaxonomicList[dataGrid.SelectedIndex].genus = tb.Text;
                        break;
                }
            }
            this.SetFocus = false;
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
            this.SetFocus = true;
        }

        // Rank updates are via combobox changes
        private void DataFieldTaxonRank_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DontUpdate)
            {
                return;
            }
            if (sender is ComboBox cb && dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count && cb.SelectedValue != null)
            {
                this.TaxonomicList[dataGrid.SelectedIndex].taxonRank = (string)((ComboBoxItem)cb.SelectedValue).Content;
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
            List<Standards.taxonomic> taxonomicListForExport = new List<Standards.taxonomic>();
            foreach (Standards.taxonomic taxonomic in this.TaxonomicList)
            {
                PropertyInfo[] properties = typeof(Standards.taxonomic).GetProperties();
                bool allNull = true;
                Standards.taxonomic newTaxonomic = new Standards.taxonomic();
                foreach (PropertyInfo property in properties)
                {
                    // Ignore vernacularCounts, as that is just used for display purposes internally
                    if (property.Name != "vernacularCount" && property.GetValue(taxonomic) != null && !string.IsNullOrWhiteSpace(property.GetValue(taxonomic).ToString()))
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
 
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new Util.JsonConverters.WhiteSpaceToNullConverter());
            this.JsonTaxonomicList = JsonConvert.SerializeObject(taxonomicListForExport, settings);
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

        #region VernacularNames editor callbacks

        private void VernacularButton_OnOpened(object sender, RoutedEventArgs e)
        {
            if (this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {
                TBVernacularItemsEditor.Text = string.Empty;
                if (null != this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
                {
                    foreach (KeyValuePair<string, string> vItem in this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
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
                TBVernacularItemsEditor.Text.Split(new string[] { Environment.NewLine },
                StringSplitOptions.None).ToList());
            DataGrid_Refresh();
        }
        #endregion

        #region VernacularItems Helpers
        private void SetVernacularItemsListFromStringList(List<string> stringList)
        {

            if (this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {

                if (null == this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
                {
                    this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames = new Dictionary<string, string>();
                }
                else
                {
                    this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames.Clear();
                }

                // Populate the list
                foreach (string str in stringList)
                {
                    KeyValuePair<string, string> vItem = VernacularItemFromString(str);
                    if (vItem.Key != string.Empty)
                    {
                        this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames.Add(vItem.Key, vItem.Value);
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
                return new KeyValuePair<string, string>(string.Empty, string.Empty);
            }

            return new KeyValuePair<string, string>(items[0].Trim(), items[1].Trim());
        }
        #endregion
    }
    #endregion
}