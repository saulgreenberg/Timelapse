using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Dialog;
using TimelapseTemplateEditor.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// Imports taxonomic definitions from GBIF for the names in the Scientific Name field's menu.
    /// Used by both the Populate CamtrapDP fields dialog and the CamtrapDP taxonomic dialog, so they behave the same.
    /// </summary>
    public static class CamtrapDPGbifImport
    {
        // Descriptions of the two ways to import taxonomic definitions, used as option text and tooltips
        public const string SpeciesListDescription = "Import the taxonomic information from a Species List json file previously created using Template editor's "
                                                     + "File | Edit a species list menu. This option produces the most reliable list.";
        public const string ScientificNameMenuDescription = "Import the taxonomic information from the Scientific Name data field's menu. "
                                                            + "Timelapse will try to retrieve the full taxon of that species from GBIF (internet required). "
                                                            + "You will be asked to resolve ambiguities, if any.";

        #region Public: the menu's names
        // The Scientific Name field's menu items, with any common name removed (e.g., Puma concolor (Cougar) becomes Puma concolor).
        // Qualifiers such as sp. are kept, as they say which rank is meant. Empty if the field has no menu.
        public static List<string> ScientificNameMenuItems(FileDatabase fileDatabase)
        {
            ControlRow control = fileDatabase?.GetControlFromControls(CamtrapDPConstants.Observations.ScientificName);
            if (control == null || (control.Type != Control.FixedChoice && control.Type != Control.MultiChoice))
            {
                return [];
            }
            return [.. Choices.ChoicesFromJson(control.List).ChoiceList
                .Select(ScientificNameInMenuItem)
                .Where(item => item.Length > 0)
                .GroupBy(GbifSpeciesLookup.WithoutQualifier, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())];
        }

        // The menu items whose names aren't already in the list
        public static List<string> ItemsNotInList(List<string> menuItems, IEnumerable<taxonomic> existing)
        {
            HashSet<string> existingNames = new(existing
                .Select(definition => definition.scientificName?.Trim())
                .Where(name => false == string.IsNullOrEmpty(name)), StringComparer.OrdinalIgnoreCase);
            return [.. menuItems.Where(item => false == existingNames.Contains(GbifSpeciesLookup.WithoutQualifier(item)))];
        }
        #endregion

        #region Public: looking up the names
        // Look up each menu item in GBIF, returning the definitions found and the items that weren't.
        // Throws OperationCanceledException if the token is cancelled, and HttpRequestException, TaskCanceledException
        // (on a timeout) or JsonException if GBIF can't be reached or understood.
        // setBusy hides the busy indicator while the user is asked something.
        public static async Task<(List<taxonomic> Found, List<string> NotFound)> LookupAsync(Window owner, List<string> menuItems,
            CancellationToken token, IProgress<ProgressBarArguments> progress, Action<bool> setBusy)
        {
            List<taxonomic> found = [];
            List<string> notFound = [];
            for (int i = 0; i < menuItems.Count; i++)
            {
                string name = GbifSpeciesLookup.WithoutQualifier(menuItems[i]);
                progress?.Report(new(Convert.ToInt32(100.0 * i / menuItems.Count), $"Looking up {name} in GBIF ({i + 1} of {menuItems.Count})...", true, false));
                GbifSpeciesMatch match = await LookupMenuItemAsync(owner, menuItems[i], name, token, setBusy);
                if (match == null)
                {
                    notFound.Add(menuItems[i]);
                }
                else
                {
                    found.Add(TaxonomicFromGbifMatch(match));
                }
            }
            return (found, notFound);
        }
        #endregion

        #region Private helpers
        // A menu item may also include the common name, e.g., Canis lupus (Gray Wolf)
        private static string ScientificNameInMenuItem(string item)
        {
            string name = item?.Trim() ?? string.Empty;
            int commonNameStart = name.IndexOf(" (", StringComparison.Ordinal);
            return commonNameStart > 0 && name.EndsWith(')') ? name[..commonNameStart].Trim() : name;
        }

        // GBIF's taxon for this menu item. If GBIF offers several, the one with exactly this name is used: a species, or the genus
        // (say) if the item has a qualifier such as Ursus sp. Otherwise the user chooses, or confirms a near match. Null if there is none.
        private static async Task<GbifSpeciesMatch> LookupMenuItemAsync(Window owner, string menuItem, string name, CancellationToken token, Action<bool> setBusy)
        {
            GbifLookupResult result = await GbifSpeciesLookup.LookupAsync(name, token);
            if (result.Candidates.Count > 0)
            {
                bool hasQualifier = name.Length != menuItem.Length;
                List<GbifCandidate> sameName = [.. result.Candidates.Where(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase) && (candidate.Rank == "species") != hasQualifier)];
                GbifCandidate chosen = sameName.Count == 1 ? sameName[0] : null;
                if (chosen == null)
                {
                    setBusy(false);
                    SelectGbifCandidate selectCandidate = new(owner, name, result.Candidates);
                    bool isSelected = true == selectCandidate.ShowDialog();
                    setBusy(true);
                    if (false == isSelected)
                    {
                        return null;
                    }
                    chosen = selectCandidate.SelectedCandidate;
                }
                return await GbifSpeciesLookup.GetMatchAsync(chosen.Key, token);
            }

            GbifSpeciesMatch match = result.Match;
            if (match == null || match.IsExactMatch)
            {
                return match;
            }
            setBusy(false);
            bool useMatch = true == Dialogs.EditorSpeciesListGbifUseClosestMatchDialog(owner, name, match.ScientificName, match.TaxonRank, match.IsSynonym);
            setBusy(true);
            return useMatch ? match : null;
        }

        // As the CamtrapDP taxonomic dialog fills in a definition from GBIF
        private static taxonomic TaxonomicFromGbifMatch(GbifSpeciesMatch match)
        {
            taxonomic definition = new()
            {
                scientificName = match.ScientificName,
                taxonID = match.TaxonID,
                taxonRank = match.TaxonRank,
                kingdom = match.Kingdom,
                phylum = match.Phylum,
                class_ = match.Class,
                order = match.Order,
                family = match.Family,
                genus = match.Genus
            };
            if (false == string.IsNullOrWhiteSpace(match.CommonName))
            {
                definition.vernacularNames = new() { [GbifSpeciesLookup.CommonNameLanguageCode] = match.CommonName };
            }
            return definition;
        }
        #endregion
    }
}
