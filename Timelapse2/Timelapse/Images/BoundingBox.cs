using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Timelapse.Util;

namespace Timelapse.Images
{
    /// <summary>
    /// A BoundingBox instance contains data describing a bounding box's appearance and the data associated with that bounding box.
    /// </summary>
    public class BoundingBox
    {
        #region Public Properties
        // Gets or sets the bounding box's outline color
        public Brush Brush { get; set; }

        // Gets or sets the bounding box's normalized location in the canvas,
        // with coordinates specifying its fractional position relative to the image topleft corner
        public Rect Rectangle { get; set; }

        // The detection category and label
        public string DetectionCategory { get; set; }
        public string DetectionLabel { get; set; }
        public List<KeyValuePair<string, string>> Classifications { get; set; }

        // Gets or sets the bounding box's normalized location in the canvas, as a relative rectangle .
        public float Confidence { get; set; }
        #endregion

        #region Constructors
        public BoundingBox(float x1, float y1, float width, float height, float confidence, string detectionCategory, string detectionLabel, List<KeyValuePair<string, string>> classifications)
        {
            this.SetValues(x1, y1, width, height, confidence, detectionCategory, detectionLabel, classifications);
        }
        public BoundingBox(string coordinates, float confidence, string detectionCategory, string detectionLabel, List<KeyValuePair<string, string>> classifications)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(coordinates, nameof(coordinates));

            float[] coords = Array.ConvertAll(coordinates.Split(','), float.Parse);
            this.SetValues(coords[0], coords[1], coords[2], coords[3], confidence, detectionCategory, detectionLabel, classifications);
        }
        #endregion

        #region Public Methods
        public static Point ConvertRatioToPoint(double x, double y, double width, double height)
        {
            Point point = new Point(x * width, y * height);
            return point;
        }
        #endregion

        #region Private Utilities
        private void SetValues(float x1, float y1, float width, float height, float confidence, string detectionCategory, string detectionlabel, List<KeyValuePair<string, string>> classifications)
        {
            this.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.Defaults.StandardColour);
            this.Rectangle = new Rect(new Point(x1, y1), new Point(x1 + width, y1 + height));
            this.Confidence = confidence;
            this.DetectionCategory = detectionCategory;
            this.DetectionLabel = detectionlabel;
            this.Classifications = classifications;
        }
        #endregion
    }
}

