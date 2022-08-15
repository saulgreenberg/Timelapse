using System;
using System.Globalization;
using System.Windows.Data;

namespace Timelapse.Editor
{
    /// <summary>
    /// Converter for CellTextBlock. Removes spaces from beginning and end of string.
    /// </summary>
    public class CellTextBlockConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valString = value as string;
            if (!String.IsNullOrEmpty(valString))
            {
                return valString.Trim();
            }
            return String.Empty;
        }
    }
}
