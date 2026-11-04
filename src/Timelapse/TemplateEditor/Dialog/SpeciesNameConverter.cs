using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Timelapse.Standards;

namespace TimelapseTemplateEditor.Dialog
{
    // Derives the species shown in the species list from a definition's scientific name and rank.
    // It isn't stored, so it can't disagree with them, and the json continues to follow the CamtrapDP taxonomic format.
    public class SpeciesNameConverter : IValueConverter
    {
        // These can follow a genus without being a species, e.g., Canis sp.
        private static readonly string[] NonSpeciesWords = ["sp", "sp.", "spp", "spp.", "cf", "cf.", "aff", "aff.", "indet", "indet."];

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is taxonomic definition ? SpeciesName(definition) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        // The species is the first two words of the scientific name: the genus and the specific name, e.g.,
        // - Canis lupus (species) and Canis lupus familiaris (subspecies) are both Canis lupus,
        // - Canis lupus Linnaeus, 1758 is Canis lupus, as the author isn't part of the species,
        // - Canis, Canis sp. and Canidae have no species.
        // A rank above species rules it out. Otherwise (including when no rank is set) the name itself shows whether there is one.
        public static string SpeciesName(taxonomic definition)
        {
            if (string.IsNullOrWhiteSpace(definition.scientificName) || GbifSpeciesLookup.IsAboveSpecies(definition.taxonRank?.Trim().ToLowerInvariant()))
            {
                return null;
            }

            string[] words = definition.scientificName.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || NonSpeciesWords.Contains(words[1], StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            // The specific name is in lower case (with hybrids starting with a x), unlike an author's name
            return char.IsLower(words[1][0]) || words[1][0] == '\u00D7' ? $"{words[0]} {words[1]}" : null;
        }
    }
}
