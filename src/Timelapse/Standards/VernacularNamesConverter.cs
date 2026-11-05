using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Timelapse.Standards
{
    // Shows a definition's vernacular names in the taxonomic list, e.g., eng: wolf, fra: loup gris
    public class VernacularNamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Text(value as Dictionary<string, string>);
        }

        public static string Text(Dictionary<string, string> names)
        {
            return names == null ? string.Empty : string.Join(", ", names.Select(name => $"{name.Key}: {name.Value}"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
