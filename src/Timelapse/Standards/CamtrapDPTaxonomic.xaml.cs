using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using Timelapse.Util;
using TimelapseTemplateEditor.Dialog;
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
        private DataGridColumn sortColumn;
        private ListSortDirection sortDirection = ListSortDirection.Ascending;
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public CamtrapDPTaxonomic(Window owner, string jsonTaxonomicList) : base(owner)
        {
            InitializeComponent();
            DataContext = this;
            JsonTaxonomicList = jsonTaxonomicList;
        }

        private void CamptrapDP_OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeGbifLookups(BusyCancelIndicator);
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
                return;
            }
            sortColumn = ScientificNameColumn;
            SortTaxonomicList();
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
            if (sortColumn != null)
            {
                // Setting the ItemsSource clears the column header's sort arrow
                sortColumn.SortDirection = sortDirection;
            }
            DontUpdate = false;
            dataGrid.SelectedIndex = dataGridSelectedRow < dataGrid.Items.Count ? dataGridSelectedRow : -1;
            EditGrid.IsEnabled = dataGrid.Items.Count > 0;
            UpdateScientificNameHeader();
        }

        // Highlight the header if any definition is missing its required scientific name, as that row may be scrolled out of view
        private void UpdateScientificNameHeader()
        {
            ScientificNameColumn.HeaderStyle = TaxonomicList?.Any(IsMissingScientificName) == true
                ? (Style)dataGrid.Resources["MissingScientificNameHeaderStyle"]
                : dataGrid.ColumnHeaderStyle;
        }

        private static bool IsMissingScientificName(taxonomic item)
        {
            return string.IsNullOrWhiteSpace(item.scientificName) && RecognitionFileTaxonomy.HasContent(item);
        }

        // Sort by the clicked column, or reverse the order if it is already the sorted column.
        // This sorts the list itself rather than the grid's view of it, as the code relies on the grid's rows matching the list's order.
        private void DataGrid_OnSorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            sortDirection = e.Column == sortColumn && sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            if (sortColumn != null)
            {
                sortColumn.SortDirection = null;
            }
            sortColumn = e.Column;

            taxonomic selectedItem = dataGrid.SelectedIndex >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count ? TaxonomicList[dataGrid.SelectedIndex] : null;
            SortTaxonomicList();
            dataGridSelectedRow = TaxonomicList.IndexOf(selectedItem);
            SetFocus = false;
            DataGrid_Refresh();
            SetFocus = true;
        }

        // Sort the list by the sort column
        private void SortTaxonomicList()
        {
            IOrderedEnumerable<taxonomic> groupedRows = TaxonomicList.OrderBy(SortGroup);
            TaxonomicList = new(sortDirection == ListSortDirection.Ascending
                ? groupedRows.ThenBy(SortValue, StringComparer.CurrentCultureIgnoreCase)
                : groupedRows.ThenByDescending(SortValue, StringComparer.CurrentCultureIgnoreCase));
        }

        // Empty values are last, except that an A to Z sort by scientific name puts missing ones first to make them evident.
        // A row with nothing filled in isn't missing anything, as it won't be saved.
        private int SortGroup(taxonomic item)
        {
            if (false == string.IsNullOrWhiteSpace(SortValue(item)))
            {
                return 1;
            }
            return sortColumn == ScientificNameColumn && sortDirection == ListSortDirection.Ascending && IsMissingScientificName(item) ? 0 : 2;
        }

        private string SortValue(taxonomic item)
        {
            return sortColumn.SortMemberPath switch
            {
                nameof(taxonomic.taxonID) => item.taxonID,
                nameof(taxonomic.taxonRank) => item.taxonRank,
                nameof(taxonomic.kingdom) => item.kingdom,
                nameof(taxonomic.phylum) => item.phylum,
                nameof(taxonomic.class_) => item.class_,
                nameof(taxonomic.order) => item.order,
                nameof(taxonomic.family) => item.family,
                nameof(taxonomic.genus) => item.genus,
                nameof(taxonomic.vernacularNames) => VernacularNamesConverter.Text(item.vernacularNames),
                _ => item.scientificName
            };
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
                DataFieldFirstVernacularName.Text = FirstVernacularName(taxonomic);
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
                DataFieldFirstVernacularName.Text = string.Empty;
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
                UpdateScientificNameHeader();
            }
        }

        // Fill in the list with the definitions in a species list file located by the user
        private void PopulateFromSpeciesList_OnClick(object sender, RoutedEventArgs e)
        {
            List<taxonomic> definitions = [.. TaxonomicList.Where(RecognitionFileTaxonomy.HasContent)];
            CamtrapDPPopulateFromSpeciesList populateDialog = new(this, definitions.Count);
            if (true != populateDialog.ShowDialog())
            {
                return;
            }

            List<taxonomic> definitionsToKeep = populateDialog.ReplaceExistingDefinitions ? [] : definitions;
            List<taxonomic> merged = CamtrapDPSpeciesList.Merged(definitionsToKeep, populateDialog.SpeciesList.Definitions, out int skippedCount);
            TaxonomicList = new(merged);
            SortTaxonomicList();
            dataGridSelectedRow = 0;
            SetFocus = false;
            DataGrid_Refresh();
            SetFocus = true;

            List<string> notes = populateDialog.SpeciesList.Notes;
            if (skippedCount > 0)
            {
                notes.Add($"{skippedCount} definition(s) were left out, as their name was already in the list.");
            }
            if (notes.Count > 0)
            {
                Dialogs.CamtrapDPSpeciesListNotesDialog(this, merged.Count - definitionsToKeep.Count, notes);
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

        #region Callbacks: GBIF lookups
        // Fill in the selected definition's fields from GBIF, using its scientific name
        private async void GbifLookup_OnClick(object sender, RoutedEventArgs e)
        {
            string scientificName = GbifSpeciesLookup.WithoutQualifier(DataFieldScientificName.Text.Trim());
            await FillFieldsFromGbifAsync(scientificName, token => GbifSpeciesLookup.LookupAsync(scientificName, token));
        }

        private async void GbifKingdomLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldKingdom.Text.Trim(), GbifSpeciesLookup.KingdomRank);
        }

        private async void GbifPhylumLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldPhylum.Text.Trim(), GbifSpeciesLookup.PhylumRank);
        }

        private async void GbifClassLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldClass.Text.Trim(), GbifSpeciesLookup.ClassRank);
        }

        private async void GbifOrderLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldOrder.Text.Trim(), GbifSpeciesLookup.OrderRank);
        }

        private async void GbifFamilyLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldFamily.Text.Trim(), GbifSpeciesLookup.FamilyRank);
        }

        private async void GbifGenusLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldGenus.Text.Trim(), GbifSpeciesLookup.GenusRank);
        }

        private Task FillFieldsFromGbifAtRankAsync(string name, string rank)
        {
            return FillFieldsFromGbifAsync(name, token => GbifSpeciesLookup.LookupAtRankAsync(name, rank, token));
        }

        private async Task FillFieldsFromGbifAsync(string name, Func<System.Threading.CancellationToken, Task<GbifLookupResult>> lookup)
        {
            if (dataGrid.SelectedIndex < 0 || dataGrid.SelectedIndex >= TaxonomicList.Count)
            {
                return;
            }
            taxonomic item = TaxonomicList[dataGrid.SelectedIndex];

            GbifSpeciesMatch match = await ResolveGbifMatchAsync(name, lookup);
            if (match == null)
            {
                return;
            }

            // The definition is now for the matched taxon, so fields GBIF doesn't return end up empty, e.g., a family has no genus.
            // The exception is vernacular names: GBIF only provides the English one, so names in other languages are kept.
            item.scientificName = match.ScientificName;
            item.taxonID = match.TaxonID;
            item.taxonRank = match.TaxonRank;
            item.kingdom = match.Kingdom;
            item.phylum = match.Phylum;
            item.class_ = match.Class;
            item.order = match.Order;
            item.family = match.Family;
            item.genus = match.Genus;
            if (false == string.IsNullOrWhiteSpace(match.CommonName))
            {
                item.vernacularNames ??= [];
                item.vernacularNames[GbifSpeciesLookup.CommonNameLanguageCode] = match.CommonName;
            }

            SetFocus = false;
            DataGrid_Refresh();
            SetFocus = true;
        }
        #endregion

        #region Callbacks: DataField edits
        // When a data field is edited, update the taxonomic list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            GbifLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldScientificName.Text);
            GbifKingdomLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldKingdom.Text);
            GbifPhylumLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldPhylum.Text);
            GbifClassLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldClass.Text);
            GbifOrderLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldOrder.Text);
            GbifFamilyLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldFamily.Text);
            GbifGenusLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldGenus.Text);
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
                [.. TBVernacularItemsEditor.Text.Split([Environment.NewLine],
                StringSplitOptions.None)]);
            DataGrid_Refresh();
        }
        #endregion

        #region VernacularItems Helpers
        private static string FirstVernacularName(taxonomic taxonomic)
        {
            if (taxonomic.vernacularNames == null || taxonomic.vernacularNames.Count == 0)
            {
                return string.Empty;
            }
            KeyValuePair<string, string> first = taxonomic.vernacularNames.First();
            return $"{first.Key}: {first.Value}";
        }

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