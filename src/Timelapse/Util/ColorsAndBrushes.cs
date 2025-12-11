using System.Windows.Media;

namespace Timelapse.Util
{
    public static class ColorsAndBrushes
    {
        /// <summary>
        /// Create a brush with the given opacity and color
        /// Opacity should be between 0 - 255 if provided, otherwise defaults to 255 (no transparency)
        /// </summary>
        public static SolidColorBrush SolidColorBrushFromColor(Color color, int opacity = 255)
        {
            return new(color)
            {
                Opacity = opacity
            };
        }

        // Unused
        // Create a brush with the given opacity and color as hex e.g., 255, #FF00BFFF
        //public static SolidColorBrush SolidColorBrushFromColor(string colorAsHex, int opacity = 255)
        //{
        //    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorAsHex))
        //    {
        //        Opacity = opacity
        //    };
        //}
    }
}
