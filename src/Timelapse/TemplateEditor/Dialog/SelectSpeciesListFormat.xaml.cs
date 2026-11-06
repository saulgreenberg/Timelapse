using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Timelapse.Dialog;
using Timelapse.Standards;

namespace TimelapseTemplateEditor.Dialog
{
    public enum SpeciesListFormat
    {
        ScientificName,
        CommonName,
        ScientificThenCommonName,
        CommonThenScientificName
    }

    /// <summary>
    /// Asks how a species list's definitions should appear as choice menu items, showing an example of each format from the list.
    /// </summary>
    public partial class SelectSpeciesListFormat
    {
        public SpeciesListFormat Format { get; private set; } = SpeciesListFormat.ScientificName;

        public SelectSpeciesListFormat(Window owner, List<taxonomic> definitions)
        {
            InitializeComponent();
            Owner = owner;
            Message.Text = $"The species list has {definitions.Count} definition(s). How should each one appear in the choice menu?";

            // The best example has both names, so that the formats differ, and is preferably a species
            List<taxonomic> withBothNames = definitions.FindAll(definition => false == string.IsNullOrWhiteSpace(definition.scientificName)
                && false == string.IsNullOrWhiteSpace(RecognitionFileTaxonomy.CommonName(definition)));
            taxonomic example = withBothNames.Find(definition => string.Equals(definition.taxonRank?.Trim(), "species", StringComparison.OrdinalIgnoreCase))
                ?? withBothNames.FirstOrDefault()
                ?? definitions[0];
            SetContent(ScientificName, "Scientific name", SpeciesListFormat.ScientificName, example);
            SetContent(CommonName, "Common name", SpeciesListFormat.CommonName, example);
            SetContent(ScientificThenCommonName, "Scientific name (common name)", SpeciesListFormat.ScientificThenCommonName, example);
            SetContent(CommonThenScientificName, "Common name (scientific name)", SpeciesListFormat.CommonThenScientificName, example);
        }

        private static void SetContent(RadioButton button, string label, SpeciesListFormat format, taxonomic example)
        {
            TextBlock content = new();
            content.Inlines.Add(new Run(label) { FontWeight = FontWeights.Bold });
            content.Inlines.Add(new Run($"  e.g., {ChoiceItem(example, format)}") { Foreground = Brushes.DarkSlateGray });
            button.Content = content;
            button.Tag = format;
        }

        // The choice menu items for these definitions, each only once as a menu shouldn't repeat an item
        public static List<string> ChoiceItems(IEnumerable<taxonomic> definitions, SpeciesListFormat format)
        {
            return [.. definitions.Select(definition => ChoiceItem(definition, format)).Distinct()];
        }

        // If a definition only has one of the names, that name is used so that the definition isn't lost.
        public static string ChoiceItem(taxonomic definition, SpeciesListFormat format)
        {
            string scientificName = WithoutCommas(definition.scientificName);
            string commonName = WithoutCommas(RecognitionFileTaxonomy.CommonName(definition));
            if (scientificName.Length == 0 || commonName.Length == 0)
            {
                return scientificName.Length == 0 ? commonName : scientificName;
            }
            return format switch
            {
                SpeciesListFormat.CommonName => commonName,
                SpeciesListFormat.ScientificThenCommonName => $"{scientificName} ({commonName})",
                SpeciesListFormat.CommonThenScientificName => $"{commonName} ({scientificName})",
                _ => scientificName
            };
        }

        // Choice menus can't contain commas, as multichoice items are comma separated, e.g., Canis lupus Linnaeus, 1758
        private static string WithoutCommas(string name)
        {
            return string.Join(" ", (name ?? string.Empty).Replace(',', ' ').Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private void SelectSpeciesListFormat_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            RadioButton selected = new[] { ScientificName, CommonName, ScientificThenCommonName, CommonThenScientificName }.First(button => button.IsChecked == true);
            Format = (SpeciesListFormat)selected.Tag;
            DialogResult = true;
        }
    }
}
