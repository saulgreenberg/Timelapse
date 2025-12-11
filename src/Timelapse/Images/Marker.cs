using System.Windows;
using System.Windows.Media;
using Timelapse.Constant;

namespace Timelapse.Images
{
    /// <summary>
    /// A Marker instance contains data describing a marker's appearance and the data associated with that marker.
    /// </summary>
    public class Marker
    {
        #region PublicProperties
        /// <summary>
        /// Gets or sets the marker's outline color
        /// </summary>
        public Brush Brush { get; set; }

        /// <summary>
        /// Gets or sets the data label associated with this marker
        /// </summary>
        public string DataLabel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to visually emphasize the marker
        /// </summary>
        public bool Emphasise { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the label has already been displayed (so we can turn it off)
        /// </summary>
        public bool LabelShownPreviously { get; set; }

        /// <summary>
        /// Gets or sets the marker's normalized location in the canvas, as a coordinate point on [0, 1], [0, 1].
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the label next to the marker
        /// </summary>
        public bool ShowLabel { get; set; }

        /// <summary>
        /// Gets or sets the marker's tooltip text 
        /// </summary>
        public string Tooltip { get; set; } // the label (not data label) associated with this marker.
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize an instance of the marker
        /// </summary>
        public Marker(string dataLabel, Point point)
        {
            Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Defaults.StandardColour);
            DataLabel = dataLabel;
            Emphasise = false;
            LabelShownPreviously = true;
            Position = point;
            ShowLabel = false;
            Tooltip = string.Empty;
        }
        #endregion

        #region Public Methods - Point Conversion
        // Calculate the point as a ratio of its position on the image, so we can locate it regardless of the actual image size
        public static Point ConvertPointToRatio(Point p, double width, double height)
        {
            // Avoid possible divide by zero errors by setting the value to 0 if it occurs
            double x = width != 0 ? p.X / width : 0;
            double y = height != 0 ? p.Y / height : 0;
            Point ratio = new(x, y);
            return ratio;
        }

        // The inverse of the above operation. Convert a relative location to a specific screen location
        public static Point ConvertRatioToPoint(Point p, double width, double height)
        {
            Point point = new(p.X * width, p.Y * height);
            return point;
        }

        // Return true if the point is between 0,0 and 1,1
        public static bool IsPointValidRatio(Point p)
        {
            return p is { X: >= 0, Y: >= 0 } and { X: <= 1, Y: <= 1 };
        }
        #endregion
    }
}
