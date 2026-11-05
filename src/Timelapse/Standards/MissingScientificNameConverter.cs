using System;
using System.Globalization;
using System.Windows.Data;
using TimelapseTemplateEditor.Dialog;

namespace Timelapse.Standards
{
    // Whether a taxonomic definition needs a scientific name: it has other values filled in, but not that one.
    // A row with nothing filled in isn't included, as it won't be saved.
    public class MissingScientificNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is taxonomic definition
                && string.IsNullOrWhiteSpace(definition.scientificName)
                && RecognitionFileTaxonomy.HasContent(definition);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
