using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Images
{
    /// <summary>
    /// Maintain a list of bounding boxes as well as their collective maximum confidence level.
    /// Given a canvas of a given size, it will also draw the bounding boxes into that canvas
    /// </summary>
    public class BoundingBoxes
    {
        #region Public Properties
        // List of Bounding Boxes associated with the image
        public List<BoundingBox> Boxes { get; private set; }
        public float MaxConfidence { get; set; }
        #endregion

        #region Constructor
        public BoundingBoxes()
        {
            this.Boxes = new List<BoundingBox>();
            this.MaxConfidence = 0;
        }
        #endregion

        #region Public Methods - Draw BoundingBoxes In Canvas
        /// <summary>
        /// If detections are turned on, draw all bounding boxes relative to 0,0 and contrained by width and height within the provided
        /// The width/height should be the actual width/height of the image (also located at 0,0) as it appears in the canvas , which is required if the bounding boxes are to be drawn in the correct places
        /// if the image has a margin, that should be included as well otherwise set it to 0
        /// The canvas should also be cleared of prior bounding boxes before this is invoked.
        /// </summary>
        /// <param name="canvas"></param>
        public bool DrawBoundingBoxesInCanvas(Canvas canvas, double width, double height, int margin = 0, TransformGroup transformGroup = null)
        {
            if (canvas == null)
            {
                return false;
            }

            // Remove existing bounding boxes, if any.
            // Note that we do this even if detections may not exist, as we need to clear things if the user had just toggled detections off
            if (GlobalReferences.DetectionsExists == false || Keyboard.IsKeyDown(Key.H))
            {
                // As detection don't exist, there won't be any bounding boxes to draw.
                return false;
            }

            // Max Confidence is over all bounding boxes, regardless of the categories.
            // So we just use it as a short cut, i.e., if none of the bounding boxes are above the threshold, we can abort.
            // Also, we add a slight correction value to the MaxConfidence, so confidences near the threshold will still appear.
            double correction = 0.005;
            if (this.MaxConfidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold && this.MaxConfidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
            {
                // Ignore any bounding box that is below the desired confidence threshold for displaying it.
                // Note that the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
                // determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                // show bounding boxes when the confidence is .4 or more.
                return false;
            }

            foreach (BoundingBox bbox in this.Boxes)
            {
                if (bbox.Confidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold && bbox.Confidence + correction < Util.GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
                {
                    // Ignore any bounding box that is below the desired confidence threshold for displaying it.
                    // Note that the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
                    // determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                    // show bounding boxes when the confidence is .4 or more.
                    continue;
                }

                // Create a bounding box 
                Rectangle rect = new Rectangle();
                SolidColorBrush brush;
                bool colorblind = Util.GlobalReferences.TimelapseState.BoundingBoxColorBlindFriendlyColors;
                byte opacity;
                if (colorblind)
                {
                    opacity = 255;
                }
                else
                {
                    opacity = (byte)Math.Round(255 * bbox.Confidence);
                }

                switch (bbox.DetectionCategory)
                {
                    // The color and opacity of the bounding box depends upon its category and whether we are using color-blind friendly colors
                    case "0":
                        // In the current implementation, the first category is usually assigned to 'Empty', so this will likely never appear.
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.LavenderBlush, opacity);
                        break;
                    case "1":
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.DeepSkyBlue, opacity);
                        break;
                    case "2":
                        brush = (colorblind)
                            ? Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.Yellow)
                            : Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.Red, opacity);
                        break;
                    case "3":
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.White, opacity);
                        break;
                    default:
                        brush = Util.ColorsAndBrushes.SolidColorBrushFromColor(Colors.PaleGreen, opacity);
                        break;
                }
                rect.Stroke = brush;

                // Set the stroke thickness, which depends upon the size of the available height
                int stroke_thickness = Math.Min(width, height) > 400 ? 4 : 2;
                rect.StrokeThickness = stroke_thickness;


                Point screenPositionTopLeft;
                Point screenPositionBottomRight;
                if (transformGroup == null)
                {
                    // The image is not being transformed.
                    // Calculate the actual position of the bounding box from the ratios
                    screenPositionTopLeft = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left, bbox.Rectangle.Top, width, height);
                    screenPositionBottomRight = BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, width, height);
                }
                else
                {
                    // The image is transformed, so we  have to apply that transformation to the bounding boxes
                    screenPositionTopLeft = transformGroup.Transform(BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left, bbox.Rectangle.Top, width, height));
                    screenPositionBottomRight = transformGroup.Transform(BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, width, height));
                }
                // Adjust the offset by any margin value (which could be 0)
                screenPositionTopLeft.X += margin;
                screenPositionTopLeft.Y += margin;
                Point screenPostionWidthHeight = new Point(screenPositionBottomRight.X - screenPositionTopLeft.X, screenPositionBottomRight.Y - screenPositionTopLeft.Y);

                // We also adjust the rect width and height to take into account the stroke thickness, to avoid the stroke overlapping the contained item
                // as otherwise the border thickness would overlap with the entity in the bounding box)
                rect.Width = screenPostionWidthHeight.X + (2 * stroke_thickness);
                rect.Height = screenPostionWidthHeight.Y + (2 * stroke_thickness);


                // Now add the rectangle to the canvas, also adjusting for the stroke thickness.
                Canvas.SetLeft(rect, screenPositionTopLeft.X - stroke_thickness);
                Canvas.SetTop(rect, screenPositionTopLeft.Y - stroke_thickness);
                canvas.Children.Add(rect);
                canvas.Tag = Constant.MarkableCanvas.BoundingBoxCanvasTag;


                // Bounding box labelling: Category plus confidence (to two decimal places or epsilon)
                // Use the primary detection category if there are no classifications, 
                // The bboxLabel contains just the top-ranked classification category + its confidence
                // the bboxTextBlock contains all predicted items + their confidence as a list
                string bboxLabel = (bbox.Classifications.Count == 0)
                    ? bbox.DetectionLabel + " " + ReformatFloatToTwoDecimalPlacesAndEpsilon(bbox.Confidence)
                    : bbox.Classifications[0].Key + " " + ReformatFloatToTwoDecimalPlacesAndEpsilon(bbox.Classifications[0].Value)
                                                  + "(" + ReformatFloatToTwoDecimalPlacesAndEpsilon(bbox.Confidence) + ")";

                string bboxTextBlock = String.Empty;
                if (bbox.Classifications.Count > 0)
                {
                    foreach (KeyValuePair<string, string> classification in bbox.Classifications)
                    {
                        bboxTextBlock += classification.Key + " " + ReformatFloatToTwoDecimalPlacesAndEpsilon(classification.Value) + Environment.NewLine;
                    }
                    bboxTextBlock = bboxTextBlock.Trim('\r', '\n');
                }

                // Add information to each bounding box using a tooltip or a splitbutton
                if (Util.GlobalReferences.TimelapseState.BoundingBoxAnnotate == false)
                {
                    // Use a tooltip
                    rect.ToolTip = (bbox.Classifications.Count == 0) ? bboxLabel : bboxTextBlock;
                }
                else
                {
                    // Use a split button. The button contains the category label, while its dropdown contains a text list of all predicted categories
                    if (bbox.Classifications.Count <= 1)
                    {
                        Label classificationUIObject = new Label
                        {
                            Opacity = 0.6,
                            Content = bboxLabel,
                            FontSize = 12,
                            Visibility = Visibility.Visible,
                            Background = Brushes.White,
                            Width = Double.NaN,
                            Height = 20, //Double.NaN,
                            Foreground = Brushes.Black,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(0, -2, 0, -2),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        double leftPosition = (screenPositionTopLeft.X - stroke_thickness) < 0
                            ? 0
                            : screenPositionTopLeft.X - stroke_thickness;
                        double topPosition = (screenPositionTopLeft.Y - stroke_thickness - 20) < 0
                           ? 0
                           : screenPositionTopLeft.Y - stroke_thickness - 20;

                        //Canvas.SetLeft(classificationUIObject, screenPositionTopLeft.X - stroke_thickness);
                        Canvas.SetLeft(classificationUIObject, leftPosition);
                        Canvas.SetTop(classificationUIObject, topPosition);
                        canvas.Children.Add(classificationUIObject);
                    }
                    else
                    {
                        SplitButton classificationUIObject = new SplitButton
                        {
                            Width = Double.NaN,
                            Content = bboxLabel,
                            Opacity = 0.6,
                            FontSize = 12,
                            Background = Brushes.White,
                            Foreground = Brushes.Black,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            DropDownContent = new TextBlock
                            {
                                Opacity = 0.6,
                                Background = Brushes.White,
                                Foreground = Brushes.Black,
                                FontSize = 12,
                                Text = bboxTextBlock,
                            },
                        };

                        // classificationUIObject.SelectionChanged += this.ClassificationUIObject_SelectionChanged;
                        double leftPosition = (screenPositionTopLeft.X - stroke_thickness) < 0
                           ? 0
                           : screenPositionTopLeft.X - stroke_thickness;
                        // Canvas.SetLeft(classificationUIObject, screenPositionTopLeft.X - stroke_thickness);
                        double topPosition = (screenPositionTopLeft.Y - stroke_thickness - 20) < 0
                            ? 0
                            : screenPositionTopLeft.Y - stroke_thickness - 20;
                        Canvas.SetLeft(classificationUIObject, leftPosition);
                        Canvas.SetTop(classificationUIObject, topPosition);
                        canvas.Children.Add(classificationUIObject);
                        Canvas.SetZIndex(classificationUIObject, 1);
                    }
                }
            }
            Canvas.SetZIndex(canvas, 1);
            return true;
        }

        // NOT USED AT THIS POINT AS WE ARE NO LONGER USING A MENU - BUT IF WE DECIDE TO...
        private void ClassificationUIObject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                if (e.AddedItems.Count == 1)
                {
                    cb.SelectedItem = e.AddedItems[0];
                    // System.Diagnostics.Debug.Print(e.AddedItems[0].ToString());
                }
            }
        }
        #endregion

        #region static internal methods
        private static string ReformatFloatToTwoDecimalPlacesAndEpsilon(string value)
        {
            return float.TryParse(value, out float result)
                ? ReformatFloatToTwoDecimalPlacesAndEpsilon(result)
                : String.Empty;
        }
        private static string ReformatFloatToTwoDecimalPlacesAndEpsilon(float value)
        {
            return (value >= .1) ? String.Format("{0:#.##}", value) : "\u03B5";
        }
        #endregion
    }
}
