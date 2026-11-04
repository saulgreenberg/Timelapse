using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Dialog;
using Timelapse.Standards;
using Timelapse.Util;
using ConstantFile = Timelapse.Constant.File;
using File = System.IO.File;
using TextBox = System.Windows.Controls.TextBox;
#pragma warning disable IDE1006

namespace TimelapseTemplateEditor.Dialog
{
    /// <summary>
    /// Edit a species list (a list of taxonomic definitions), which can be imported from and exported to a json file.
    /// </summary>
    public partial class EditSpeciesList
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
        public Fields CommonNameField { get; set; } =
            new("Common name*",
                $"Common (vernacular) name of the taxon, in English.{Environment.NewLine}" +
                "• e.g., \"wolf\"");

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
        #endregion

        #region Private variables
        // Common names are stored as vernacular names under this ISO 639-3 language code
        private const string CommonNameLanguageCode = "eng";
        private const string JsonFileFilter = "Json files (*.json)|*.json";
        private bool DontUpdate;
        private bool SetFocus = true;
        private int dataGridSelectedRow = -1;
        #endregion 

        #region Constructor / Loaded
        public EditSpeciesList(Window owner, string jsonTaxonomicList)
        {
            InitializeComponent();
            DataContext = this;
            Owner = owner;
            JsonTaxonomicList = jsonTaxonomicList;
        }

        private void EditSpeciesList_OnLoaded(object sender, RoutedEventArgs e)
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
                DataFieldCommonName.Text = GetCommonName(taxonomic);
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
                DataFieldCommonName.Text = string.Empty;
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
                DataFieldCommonName.Focus();
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
            DataFieldCommonName.Focus();
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

        // Replace the current list with the species list in a json file chosen by the user
        private void ImportList_OnClick(object sender, RoutedEventArgs e)
        {
            if (false == Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a json file containing a species list",
                    string.Empty,
                    JsonFileFilter,
                    ConstantFile.JsonFileExtension,
                    out string filePath,
                    this))
            {
                return;
            }

            List<taxonomic> importedList;
            try
            {
                importedList = JsonConvert.DeserializeObject<List<taxonomic>>(File.ReadAllText(filePath));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Dialogs.EditorSpeciesListCouldNotImportDialog(this, filePath, ex.Message);
                return;
            }

            // Valid json may still not be a species list, e.g., an array of unrelated objects would deserialize as empty rows
            importedList?.RemoveAll(item => item == null);
            if (importedList == null || (importedList.Count > 0 && importedList.TrueForAll(item => string.IsNullOrWhiteSpace(item.scientificName) && (item.vernacularNames == null || item.vernacularNames.Count == 0))))
            {
                Dialogs.EditorSpeciesListCouldNotImportDialog(this, filePath, "It does not contain a list of definitions with scientific or common names.");
                return;
            }

            TaxonomicList = new(importedList);
            dataGridSelectedRow = TaxonomicList.Count > 0 ? 0 : -1;
            DataGrid_Refresh();
            if (TaxonomicList.Count == 0)
            {
                NewRow_OnClick(null, null);
            }
        }

        // Save the current list to a json file chosen or created by the user.
        private void ExportList_OnClick(object sender, RoutedEventArgs e)
        {
            if (false == Dialogs.TryGetFileFromUserUsingSaveFileDialog(
                    "Export the species list to a json file",
                    string.Empty,
                    JsonFileFilter,
                    ConstantFile.JsonFileExtension,
                    out string filePath,
                    this))
            {
                return;
            }

            try
            {
                // WriteAllText writes UTF-8 without a BOM
                File.WriteAllText(filePath, SerializeTaxonomicList(Formatting.Indented));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Dialogs.EditorSpeciesListCouldNotExportDialog(this, filePath, ex.Message);
            }
        }

        // Fill in the selected definition's fields from GBIF, using its scientific name
        private async void GbifLookup_OnClick(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedIndex < 0 || dataGrid.SelectedIndex >= TaxonomicList.Count)
            {
                return;
            }
            taxonomic item = TaxonomicList[dataGrid.SelectedIndex];
            string scientificName = GbifSpeciesLookup.WithoutQualifier(DataFieldScientificName.Text.Trim());

            GbifSpeciesMatch match = await ResolveGbifMatchAsync(scientificName, () => GbifSpeciesLookup.LookupAsync(scientificName));
            if (match == null)
            {
                return;
            }

            ReplaceFieldsWithGbifMatch(item, match, match.CommonName);
        }

        // Fill in the selected definition's fields from GBIF, using its genus
        private async void GbifGenusLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldGenus.Text.Trim(), GbifSpeciesLookup.GenusRank);
        }

        // Fill in the selected definition's fields from GBIF, using its family
        private async void GbifFamilyLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldFamily.Text.Trim(), GbifSpeciesLookup.FamilyRank);
        }

        // Fill in the selected definition's fields from GBIF, using its kingdom
        private async void GbifKingdomLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldKingdom.Text.Trim(), GbifSpeciesLookup.KingdomRank);
        }

        // Fill in the selected definition's fields from GBIF, using its phylum
        private async void GbifPhylumLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldPhylum.Text.Trim(), GbifSpeciesLookup.PhylumRank);
        }

        // Fill in the selected definition's fields from GBIF, using its class
        private async void GbifClassLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldClass.Text.Trim(), GbifSpeciesLookup.ClassRank);
        }

        // Fill in the selected definition's fields from GBIF, using its order
        private async void GbifOrderLookup_OnClick(object sender, RoutedEventArgs e)
        {
            await FillFieldsFromGbifAtRankAsync(DataFieldOrder.Text.Trim(), GbifSpeciesLookup.OrderRank);
        }

        private async Task FillFieldsFromGbifAtRankAsync(string name, string rank)
        {
            if (dataGrid.SelectedIndex < 0 || dataGrid.SelectedIndex >= TaxonomicList.Count)
            {
                return;
            }
            taxonomic item = TaxonomicList[dataGrid.SelectedIndex];

            GbifSpeciesMatch match = await ResolveGbifMatchAsync(name, () => GbifSpeciesLookup.LookupAtRankAsync(name, rank));
            if (match == null)
            {
                return;
            }

            // GBIF may not list a common name, in which case use the taxon's name
            ReplaceFieldsWithGbifMatch(item, match, match.CommonName ?? QualifiedIfRequested(match.ScientificName, match.TaxonRank));
        }

        // The definition is now for the matched taxon, so nothing from the previous contents is kept.
        // Fields GBIF doesn't return for this taxon end up empty, e.g., a family has no genus.
        private void ReplaceFieldsWithGbifMatch(taxonomic item, GbifSpeciesMatch match, string commonName)
        {
            item.scientificName = QualifiedIfRequested(match.ScientificName, match.TaxonRank);
            item.taxonID = match.TaxonID;
            item.taxonRank = match.TaxonRank;
            item.kingdom = match.Kingdom;
            item.phylum = match.Phylum;
            item.class_ = match.Class;
            item.order = match.Order;
            item.family = match.Family;
            item.genus = match.Genus;
            item.vernacularNames = null;
            SetCommonName(item, commonName);

            SetFocus = false;
            DataGrid_Refresh();
            SetFocus = true;
        }

        private string QualifiedIfRequested(string name, string rank)
        {
            return true == AppendQualifiers.IsChecked ? GbifSpeciesLookup.QualifiedName(name, rank) : name;
        }

        // Do the lookup, asking the user to choose or confirm if GBIF doesn't return a clear match.
        // Returns null if there is no match to use, after telling the user why if needed.
        private async Task<GbifSpeciesMatch> ResolveGbifMatchAsync(string name, Func<Task<GbifLookupResult>> lookup)
        {
            GbifLookupResult result = await TryGbifRequestAsync(lookup);
            if (result == null)
            {
                return null;
            }

            if (result.Candidates.Count > 0)
            {
                // There is no clear single match, so let the user choose
                SelectGbifCandidate selectCandidate = new(this, name, result.Candidates);
                return true == selectCandidate.ShowDialog()
                    ? await TryGbifRequestAsync(() => GbifSpeciesLookup.GetMatchAsync(selectCandidate.SelectedCandidate.Key))
                    : null;
            }

            GbifSpeciesMatch match = result.Match;
            if (match == null)
            {
                Dialogs.EditorSpeciesListGbifNoMatchDialog(this, name);
                return null;
            }

            return match.IsExactMatch || true == Dialogs.EditorSpeciesListGbifUseClosestMatchDialog(this, name, match.ScientificName, match.TaxonRank, match.IsSynonym)
                ? match
                : null;
        }

        // Disable the dialog during the request. Returns null if the request failed (after telling the user)
        // or if the user closed the dialog while waiting.
        private async Task<T> TryGbifRequestAsync<T>(Func<Task<T>> request) where T : class
        {
            IsEnabled = false;
            Cursor = Cursors.Wait;
            string failureReason = null;
            T result = null;
            try
            {
                result = await request();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                failureReason = ex.Message;
            }
            if (false == IsLoaded)
            {
                return null;
            }
            IsEnabled = true;
            Cursor = null;
            if (failureReason != null)
            {
                Dialogs.EditorSpeciesListGbifNotAvailableDialog(this, failureReason);
            }
            return result;
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Done_OnClick(object sender, RoutedEventArgs e)
        {

            JsonTaxonomicList = SerializeTaxonomicList(Formatting.None);
            DialogResult = true;
        }
        #endregion

        #region Callbacks: DataField edits
        // When a data field is edited, update the taxonomic list to reflect that change
        // and refresh the datagrid to reflect those changes
        private void DataField_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            GbifLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldScientificName.Text);
            GbifGenusLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldGenus.Text);
            GbifFamilyLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldFamily.Text);
            GbifKingdomLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldKingdom.Text);
            GbifPhylumLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldPhylum.Text);
            GbifClassLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldClass.Text);
            GbifOrderLookup.IsEnabled = false == string.IsNullOrWhiteSpace(DataFieldOrder.Text);
            if (DontUpdate)
            {
                return;
            }
            if (sender is TextBox tb && dataGridSelectedRow >= 0 && dataGrid.SelectedIndex < TaxonomicList.Count)
            {
                switch (tb.Name)
                {
                    case "DataFieldCommonName":
                        SetCommonName(TaxonomicList[dataGrid.SelectedIndex], tb.Text);
                        break;
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
        }
        #endregion

        #region Json Serializer
        private string SerializeTaxonomicList(Formatting formatting)
        {
            // Empty strings and empty vernacular lists are set to null (to make for a cleaner json)
            // If a taxonomic object is all empty, skip it
            List<taxonomic> taxonomicListForExport = [];
            foreach (taxonomic item in TaxonomicList)
            {
                taxonomic newTaxonomic = new()
                {
                    scientificName = NullIfWhiteSpace(item.scientificName),
                    taxonID = NullIfWhiteSpace(item.taxonID),
                    taxonRank = NullIfWhiteSpace(item.taxonRank),
                    kingdom = NullIfWhiteSpace(item.kingdom),
                    phylum = NullIfWhiteSpace(item.phylum),
                    class_ = NullIfWhiteSpace(item.class_),
                    order = NullIfWhiteSpace(item.order),
                    family = NullIfWhiteSpace(item.family),
                    genus = NullIfWhiteSpace(item.genus),
                    vernacularNames = item.vernacularNames?.Count > 0 ? item.vernacularNames : null
                };
                if (newTaxonomic.scientificName != null || newTaxonomic.taxonID != null || newTaxonomic.taxonRank != null
                    || newTaxonomic.kingdom != null || newTaxonomic.phylum != null || newTaxonomic.class_ != null
                    || newTaxonomic.order != null || newTaxonomic.family != null || newTaxonomic.genus != null
                    || newTaxonomic.vernacularNames != null)
                {
                    taxonomicListForExport.Add(newTaxonomic);
                }
            }

            JsonSerializerSettings settings = new()
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = formatting
            };
            settings.Converters.Add(new JsonConverters.WhiteSpaceToNullConverter());
            return JsonConvert.SerializeObject(taxonomicListForExport, settings);
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        #endregion

        #region Class: Fields 
        //The EditFields
        public class Fields(string label, string tooltip)
        {
            public string Label { get; set; } = label;
            public string Tooltip { get; set; } = tooltip;
        }

        #region Common name helpers
        private static string GetCommonName(taxonomic item)
        {
            return item.vernacularNames != null && item.vernacularNames.TryGetValue(CommonNameLanguageCode, out string commonName)
                ? commonName
                : string.Empty;
        }

        // Only the common name is changed. Any names in other languages (e.g., from an imported file) are retained.
        private static void SetCommonName(taxonomic item, string commonName)
        {
            if (string.IsNullOrWhiteSpace(commonName))
            {
                item.vernacularNames?.Remove(CommonNameLanguageCode);
                return;
            }
            item.vernacularNames ??= [];
            item.vernacularNames[CommonNameLanguageCode] = commonName;
        }
        #endregion
    }
    #endregion
}