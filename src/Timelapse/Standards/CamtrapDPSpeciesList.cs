using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimelapseTemplateEditor.Dialog;

namespace Timelapse.Standards
{
    /// <summary>
    /// The contents of a species list file, as definitions that suit the CamtrapDP taxonomic list.
    /// Problem says why the file can't be used at all. Notes describe what couldn't be carried over from a file that can be used.
    /// </summary>
    public class CamtrapDPSpeciesList
    {
        private const int MaxNamesInNote = 10;

        // The fields a definition can have. class_ is how earlier versions saved class.
        private static readonly string[] Fields = ["scientificName", "taxonID", "taxonRank", "kingdom", "phylum", "class", "class_", "order", "family", "genus", "vernacularNames"];

        // The choices in the CamtrapDPTaxonomic dialog's rank menu
        private static readonly string[] Ranks = ["kingdom", "phylum", "class", "order", "family", "genus", "species", "subspecies"];

        public List<taxonomic> Definitions { get; } = [];
        public List<string> Notes { get; } = [];
        public string Problem { get; private init; }

        // This reads the first token before anything else, so it stops right away on a large file that is something else, e.g., a recognition file
        public static CamtrapDPSpeciesList Read(string filePath)
        {
            JArray rows;
            try
            {
                using StreamReader streamReader = new(filePath);
                using JsonTextReader reader = new(streamReader);
                if (false == reader.Read() || reader.TokenType != JsonToken.StartArray)
                {
                    return new() { Problem = "The file doesn't contain a list of taxonomic definitions." };
                }
                rows = JArray.Load(reader);
            }
            catch (JsonReaderException ex)
            {
                return new() { Problem = $"The file isn't valid json. The first error is on line {ex.LineNumber}." };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new() { Problem = $"The file could not be read: {ex.Message}" };
            }

            return rows.Count == 0
                ? new() { Problem = "The file's list of taxonomic definitions is empty." }
                : FromRows(rows);
        }

        // Keeps the existing definitions and adds the new ones, leaving out any already there, or already seen in the new ones.
        // Definitions match on their scientific name or, if they don't have one, on their common name.
        public static List<taxonomic> Merged(List<taxonomic> existing, List<taxonomic> newDefinitions, out int skippedCount)
        {
            List<taxonomic> merged = [.. existing];
            HashSet<string> names = new(existing.Select(NameForMatching), StringComparer.OrdinalIgnoreCase);
            foreach (taxonomic definition in newDefinitions)
            {
                if (names.Add(NameForMatching(definition)))
                {
                    merged.Add(definition);
                }
            }
            skippedCount = existing.Count + newDefinitions.Count - merged.Count;
            return merged;
        }

        private static CamtrapDPSpeciesList FromRows(JArray rows)
        {
            CamtrapDPSpeciesList speciesList = new();
            List<string> unusedFields = [];
            List<string> unusedRanks = [];
            List<string> withoutScientificName = [];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is not JObject row)
                {
                    return new() { Problem = $"Item {i + 1} in the file isn't a taxonomic definition." };
                }

                unusedFields.AddRange(row.Properties()
                    .Select(property => property.Name)
                    .Where(name => false == Fields.Contains(name, StringComparer.OrdinalIgnoreCase)));

                taxonomic definition;
                try
                {
                    definition = row.ToObject<taxonomic>();
                }
                catch (JsonException)
                {
                    return new() { Problem = $"Definition {i + 1} in the file has a field containing the wrong kind of value." };
                }

                string commonName = definition.vernacularNames?.Values.FirstOrDefault(name => false == string.IsNullOrWhiteSpace(name));
                if (string.IsNullOrWhiteSpace(definition.scientificName) && string.IsNullOrWhiteSpace(commonName))
                {
                    return new() { Problem = $"Definition {i + 1} in the file has neither a scientific name nor a common name." };
                }

                // CamtrapDP wants the name of the taxon by itself, while a species list may have added a qualifier, e.g., Ursus sp.
                definition.scientificName = string.IsNullOrWhiteSpace(definition.scientificName)
                    ? null
                    : GbifSpeciesLookup.WithoutQualifier(definition.scientificName.Trim());
                if (definition.scientificName == null)
                {
                    withoutScientificName.Add(commonName);
                }

                if (false == string.IsNullOrWhiteSpace(definition.taxonRank))
                {
                    string rank = Ranks.FirstOrDefault(item => item.Equals(definition.taxonRank.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (rank == null)
                    {
                        unusedRanks.Add($"{definition.scientificName ?? commonName} ({definition.taxonRank.Trim()})");
                    }
                    definition.taxonRank = rank;
                }
                speciesList.Definitions.Add(definition);
            }

            AddNote(speciesList.Notes, "These fields aren't used in CamtrapDP, so they were ignored:", unusedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
            AddNote(speciesList.Notes, "These ranks aren't in the rank menu, so the rank was left empty:", unusedRanks);
            AddNote(speciesList.Notes, "These have only a common name, so the scientific name was left empty. CamtrapDP requires one, so enter it before exporting:", withoutScientificName);
            return speciesList;
        }

        private static void AddNote(List<string> notes, string text, List<string> names)
        {
            if (names.Count > 0)
            {
                string more = names.Count > MaxNamesInNote ? $", and {names.Count - MaxNamesInNote} more" : string.Empty;
                notes.Add($"{text} {string.Join(", ", names.Take(MaxNamesInNote))}{more}.");
            }
        }

        // A definition without either name (e.g., one with only a family) gets a name that matches nothing else
        private static string NameForMatching(taxonomic definition)
        {
            if (false == string.IsNullOrWhiteSpace(definition.scientificName))
            {
                return definition.scientificName.Trim();
            }
            string commonName = definition.vernacularNames?.Values.FirstOrDefault(name => false == string.IsNullOrWhiteSpace(name));
            return commonName == null ? Guid.NewGuid().ToString() : $"common name: {commonName.Trim()}";
        }
    }
}
