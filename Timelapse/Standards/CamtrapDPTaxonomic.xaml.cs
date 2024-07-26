using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Timelapse.Dialog;
using static Timelapse.Standards.CamtrapDPTaxonomic;
using TextBox = System.Windows.Controls.TextBox;

namespace Timelapse.Standards
{
    /// <summary>
    /// Interaction logic for CamtrapDPTaxonomic.xaml
    /// </summary>
    public partial class CamtrapDPTaxonomic : Window
    {
        #region JsonTaxonomicList, TaxonomicList, and Fields
        public string JsonTaxonomicList { get; set; }

        // The main list of taxonomic definitions. It is
        // - initially populated by the initial jsonTaxonomicList
        // - updated by changes to the EditList (including adding and deleting rows)
        // - used to populate the dataGrid
        // - its contents is returned as a json string when done.
        public ObservableCollection<Taxonomic> TaxonomicList { get; set; }

        // The fields used to construct the EditList
        public string kingdom { get; set; } = string.Empty;
        public string phylum { get; set; } = string.Empty;
        public string class_ { get; set; } = string.Empty;
        public string order { get; set; } = string.Empty;
        public string family { get; set; } = string.Empty;
        public string genus { get; set; } = string.Empty;
        
        public ObservableCollection<string> vernacularNames { get; set; }

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
                $"See http://rs.tdwg.org/dwc/terms/taxonRank" +
                 "• e.g., \"species\"");

        public Fields KingdomField { get; set; } =
            new Fields("Kingdom",
                $"Kingdom in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/kingdom" +
                "• e.g., \"Animalia\"");

        public Fields PhylumField { get; set; } =
            new Fields("Phylum",
                $"Phylum or division in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/phylum" +
                "• e.g., \"Chordata\"");

        public Fields ClassField { get; set; } =
            new Fields("Class",
                $"Class in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/class" +
                "• e.g., \"Mammalia\"");

        public Fields OrderField { get; set; } =
            new Fields("Order",
                $"Order in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/order" +
                "• e.g., \"Carnivora\"");

        public Fields FamilyField { get; set; } =
            new Fields("Family",
                $"Family in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/family" +
                "• e.g., \"Canidae\"");

        public Fields GenusField { get; set; } =
            new Fields("Genus",
                $"Genus in which the taxon is classified.{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/genus" +
                "• e.g., \"Canis\"");

        public Fields VernacularNamesField { get; set; } =
            new Fields("Vernacular names",
                $"Common or vernacular names of the taxon, as languageCode: vernacular name pairs.{Environment.NewLine}" +
                $"Language codes should follow ISO 693-3 (e.g. eng for English).{Environment.NewLine}" +
                $"See http://rs.tdwg.org/dwc/terms/kingdom" +
                "• e.g., \"{'eng': 'wolf', 'fra': 'loup gris'}\"");
        #endregion

        #region Private variables
        private bool DontUpdate = false;
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
                this.TaxonomicList = new ObservableCollection<Taxonomic>(JsonConvert.DeserializeObject<List<Taxonomic>>(JsonTaxonomicList));
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

            //DataGridVernacular_Refresh();
            //this.DataFieldVernacularNames.ItemsSource = this.VernacularItemsList;
        }
        #endregion

        #region DataGrid callbacks and helpers
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
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {

                this.DeleteRow.IsEnabled = true;

                this.DontUpdate = true;
                Taxonomic taxonomic = this.TaxonomicList[dataGrid.SelectedIndex];
                this.DataFieldScientificName.Text = taxonomic.scientificName;
                this.DataFieldTaxonID.Text = taxonomic.taxonID;
                this.DataFieldTaxonRank.Text = taxonomic.taxonRank;
                this.DataFieldKingdom.Text = taxonomic.kingdom;
                this.DataFieldPhylum.Text = taxonomic.phylum;
                this.DataFieldClass.Text = taxonomic.class_;
                this.DataFieldOrder.Text = taxonomic.order;
                this.DataFieldFamily.Text = taxonomic.family;
                this.DataFieldGenus.Text = taxonomic.genus;
                this.vernacularNames = this.VernacularStringListFromVernacularItemsList(taxonomic.vernacularNames);
                this.DontUpdate = false;
            }
            else
            {
                this.DeleteRow.IsEnabled = false;

                this.DontUpdate = true;
                this.DataFieldScientificName.Text = string.Empty;
                this.DataFieldTaxonID.Text = string.Empty;
                this.DataFieldTaxonRank.Text = string.Empty;
                this.DataFieldKingdom.Text = string.Empty;
                this.DataFieldPhylum.Text = string.Empty;
                this.DataFieldClass.Text = string.Empty;
                this.DataFieldOrder.Text = string.Empty;
                this.DataFieldFamily.Text = string.Empty;
                this.DataFieldGenus.Text = string.Empty;
                this.vernacularNames = new ObservableCollection<string>();

                this.DontUpdate = false;
            }
        }
        #endregion

        #region Button callbacks
        private void NewRow_OnClick(object sender, RoutedEventArgs e)
        {
            this.TaxonomicList.Add(new Taxonomic());
            dataGrid.SelectedIndex = this.dataGrid.Items.Count - 1;
            dataGridSelectedRow = dataGrid.SelectedIndex;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
        }

        private void DeleteRow_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {
                this.TaxonomicList.RemoveAt(dataGrid.SelectedIndex);
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
        // When a data field is edited, update the contributors list to reflect that change
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
                        //case "DataFieldVernacularNames":
                        //    this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames = JsonConvert.SerializeObject(vernacularNames);
                        //    //case "DataFieldVernacularNames":
                        //    //    this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames = tb.Text;
                        //    break;
                }
            }
            DataGrid_Refresh();
            dataGrid.SelectedItem = this.dataGridSelectedRow;
        }

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

        #region Helpers - Json Serializer
        private void JsonSerialize()
        {
            this.JsonTaxonomicList = JsonConvert.SerializeObject(this.TaxonomicList);
        }
        #endregion

        #region Taxonomic class 
        // A Taxonomic has these fields, as defined in the CamtrapDP specification.
        // VernacularNames is a 
        public class Taxonomic
        {
            public string scientificName { get; set; } = string.Empty;
            public string taxonID { get; set; } = string.Empty;
            public string taxonRank { get; set; } = string.Empty;
            public string kingdom { get; set; } = string.Empty;
            public string phylum { get; set; } = string.Empty;
            public string class_ { get; set; } = string.Empty;
            public string order { get; set; } = string.Empty;
            public string family { get; set; } = string.Empty;
            public string genus { get; set; } = string.Empty;
            public List<VernacularItem> vernacularNames { get; set; } = new List<VernacularItem>();

            public string vernacularCount
            {
                get { return vernacularNames == null ? "0" : vernacularNames.Count.ToString(); }
            }
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

 

        #region Class VernacularItem
        public class VernacularItem
        {
            public string lang { get; set; }
            public string vernacularName { get; set; }

            public string GetVernaculatItemAsString()
            {
                return ($"{lang}:{vernacularName}");
            }

            public static VernacularItem VernacularItemFromString(string itemAsString)
            {
                string[] items = itemAsString.Split(':');
                if (items.Length != 2)
                {
                    // Not in correct format!
                    return null;
                }
                return new VernacularItem
                {
                    lang = items[0].Trim(),
                    vernacularName = items[1].Trim()
                };
            }
        }
        #endregion

        #region VernacularNames editor callbacks

        private void VernacularButton_OnOpened(object sender, RoutedEventArgs e)
        {
            if (this.dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < this.TaxonomicList.Count)
            {
                TBVernacularItemsEditor.Text = string.Empty;
                foreach (VernacularItem vItem in this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames)
                {
                    TBVernacularItemsEditor.Text += $"{vItem.GetVernaculatItemAsString()}{Environment.NewLine}";
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
                    this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames = new List<VernacularItem>();
                }
                else
                {
                    this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames.Clear();
                }

                // Populate the list
                foreach (string str in stringList)
                {
                    VernacularItem vItem = VernacularItem.VernacularItemFromString(str);
                    if (null != vItem)
                    {
                        this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames.Add(vItem);
                    }
                }

                this.vernacularNames = VernacularStringListFromVernacularItemsList(this.TaxonomicList[dataGrid.SelectedIndex].vernacularNames);
            }
        }

        private ObservableCollection<string> VernacularStringListFromVernacularItemsList(List<VernacularItem> vItemsList)
        {
            ObservableCollection<string> vStringList = new ObservableCollection<string>();
            foreach (VernacularItem vItem in vItemsList)
            {
                vStringList.Add(vItem.GetVernaculatItemAsString());
            }
            return vStringList;
        }
        #endregion
    }
    #endregion
}

