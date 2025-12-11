using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;

namespace Timelapse.Extensions
{
    public static class TextBlockExtension
    {
        public static Size MeasureStringSize(this TextBlock sourceTextBlock, string stringToMeasure)
        {
            FormattedText formattedText = new(
                stringToMeasure,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new(sourceTextBlock.FontFamily, sourceTextBlock.FontStyle, sourceTextBlock.FontWeight, sourceTextBlock.FontStretch),
                sourceTextBlock.FontSize,
                Brushes.Black,
                new(),
                VisualTreeHelper.GetDpi(sourceTextBlock).PixelsPerDip);
            return new(formattedText.Width, formattedText.Height);
        }

        public static double MeasureStringWidth(this TextBlock sourceTextBlock, string stringToMeasure)
        {
            return sourceTextBlock.MeasureStringSize(stringToMeasure).Width;
        }
    }
}
