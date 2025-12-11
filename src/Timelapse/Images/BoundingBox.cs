using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Timelapse.Constant;
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
        public int FrameNumber { get; set; }
        #endregion

        #region Constructors
        public BoundingBox(float x1, float y1, float width, float height, float confidence, int frameNumber, string detectionCategory, string detectionLabel, List<KeyValuePair<string, string>> classifications)
        {
            SetValues(x1, y1, width, height, confidence, frameNumber, detectionCategory, detectionLabel, classifications);
        }
        public BoundingBox(string coordinates, float confidence, int frameNumber, string detectionCategory, string detectionLabel, List<KeyValuePair<string, string>> classifications)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(coordinates, nameof(coordinates));


            // Data should always be using decimal places, so use invariant culture.
            // float[] coords = Array.ConvertAll(coordinates.Split(','), float.Parse);  // This crashed before when the culture used a comma for the decimal
            float[] coords = Array.ConvertAll(coordinates.Split(','), s => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
            this.SetValues(coords[0], coords[1], coords[2], coords[3], confidence, frameNumber, detectionCategory, detectionLabel, classifications);
        }
        #endregion

        #region Public Methods
        public static Point ConvertRatioToPoint(double x, double y, double width, double height)
        {
            Point point = new(x * width, y * height);
            return point;
        }
        #endregion

        #region Private Utilities
        private void SetValues(float x1, float y1, float width, float height, float confidence, int frameNumber, string detectionCategory, string detectionlabel, List<KeyValuePair<string, string>> classifications)
        {
            Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Defaults.StandardColour);
            Rectangle = new(new(x1, y1), new Point(x1 + width, y1 + height));
            Confidence = confidence;
            DetectionCategory = detectionCategory;
            DetectionLabel = detectionlabel;
            Classifications = classifications;
            FrameNumber = frameNumber;
        }
        #endregion
    }
}

