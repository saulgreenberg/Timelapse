using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Timelapse.DataStructures;
using Timelapse.Util;
using static System.FormattableString;

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
        public List<BoundingBox> Boxes { get; } = [];

        public float MaxConfidence { get; set; } = 0;

        public int InitialVideoFrame { get; set; } = 0;
        public float? FrameRate { get; set; } = 0;

        #endregion

        #region Public Methods - Draw BoundingBoxes In Canvas

        /// <summary>
        /// If detections are turned on, draw all bounding boxes relative to 0,0 and contrained by width and height within the provided
        /// The width/height should be the actual width/height of the image (also located at 0,0) as it appears in the canvas , which is required if the bounding boxes are to be drawn in the correct places
        /// If the image has a margin, that should be included as well otherwise set it to 0
        /// The canvas should also be cleared of prior bounding boxes before this is invoked.
        /// The bounding box to show should be the closest one to the frameToShow within a half second window.
        /// </summary>

        // INVOKED FOR VIDEOS
        // frameToShow is the current video frame being displayed. It may or may not have a matching bounding box defined for that frame
        // frameWindow defines how many frames before or after the frameToShow we should search for a nearby bounding box (normally equivalent to ~1/2 second of video on either side)
        // The algorithm searches the bounding box structure defined for the frame closest to the frameToShow that exists within the frameWindow
        // The first form invokes a frameWindow is 0, which draws bounding boxes only if the bounding box structure contains a match to the displayedVideoFrame.
        // That form is usually used for capturing a video still e.g., for video thumbnails or popup.
        public bool DrawBoundingBoxesInCanvas(Canvas canvas, double width, double height, int margin, TransformGroup transformGroup, int displayedVideoFrame)
        {
            return DrawBoundingBoxesInCanvas(canvas, width, height, margin, transformGroup, displayedVideoFrame, 0);
        }


        public bool DrawBoundingBoxesInCanvas(Canvas canvas, double width, double height, int margin, TransformGroup transformGroup, int displayedVideoFrame, int frameWindow)
        {
            // Note that we do this even if detections may not exist, as we need to clear things if the user had just toggled detections off
            if (canvas == null || GlobalReferences.DetectionsExists == false || GlobalReferences.HideBoundingBoxes || Keyboard.IsKeyDown(Key.H) || null == this.Boxes || this.Boxes.Count == 0)
            {
                // As detection or boxes don't exist, there won't be any bounding boxes to draw.
                // Or, if the user doesn't want them displayed, we don't show them. 
                return false;
            }

            // If the MaxConfidence over all bounding boxes is below the desired confidence threshold for displaying them, there is no point in continuing. 
            // We do add a slight correction value to the MaxConfidence, so confidences very near the threshold will still appear.
            // Implementation note: BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
            // determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
            // show bounding boxes when the confidence is .4 or more.
            double correction = 0.005;
            if (MaxConfidence + correction < GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold &&
                MaxConfidence + correction < GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
            {
                return false;
            }

            // The frame / to frame creates the bounding box search window based on the frame window 
            int fromFrame = displayedVideoFrame - frameWindow;
            int toFrame = displayedVideoFrame + frameWindow;

            // Sort the boxes. Note that its likely that the Boxes are already sorted, so this should be fast.
            List<BoundingBox> sortedBoxes = Boxes.OrderBy(s => s.FrameNumber).ToList();

            // Find the index of the frame containing bounding boxes that is closest to the displayedVideoFrame
            // As we do something different if its a frame before vs. after the displayed video Frame, we 
            // colloect that as prevFrameIndex or nextFrameIndex
            int prevFrameIndex = -1;
            int nextFrameIndex = -1;
            int currentIndex = 0;
            int difference = 10000;
            foreach (BoundingBox box in sortedBoxes)
            {
                if (currentIndex >= sortedBoxes.Count - 1)
                {
                    // If we are on the last bounding box, enlarge the frame window so that the bounding box lingers
                    // for a bit longer. That is instead of the bounding box disappearing after
                    // a 1/2 second, it will linger for a full second. This seems to be a better heuristic
                    // for visually indicating an entity that is in the process of moving off the video.The only do
                    fromFrame -= frameWindow;
                    toFrame += frameWindow;
                }
                if (box.FrameNumber < fromFrame)
                {
                    // Skip this bounding box, as its below the frame window
                    currentIndex++;
                    continue;
                }

                if (box.FrameNumber > toFrame)
                {
                    // Skip this bounding box and break, as its above the frame window
                    break;
                }
                // as we cycle through, we find the prev/next frames with boxes closest to the frame window
                if (box.FrameNumber <= displayedVideoFrame)
                {
                    // Incrementally get closer to the displayedVideoFrame
                    // Also, calculate how far the current bbox frame is from displayedVideoFrame 
                    prevFrameIndex = currentIndex;
                    difference = displayedVideoFrame - sortedBoxes[prevFrameIndex].FrameNumber;
                }
                else
                if (box.FrameNumber - displayedVideoFrame < difference)
                {
                    // We are above the displayedVideoFrame, where the difference is less than the difference found for the previous video frame.
                    // As we have now found the closest bbox frame above the displayedVideoFrame, we can stop searching.
                    nextFrameIndex = currentIndex;
                    prevFrameIndex = -1;
                    break;
                }
                currentIndex++;
            }

            // Collect the bounding boxes for the desired frame
            List<BoundingBox> bboxes = [];
            if (prevFrameIndex != -1)
            {
                // THe desired bounding box frame is below or equal to the displayedVideoFrame
                int boxFrameNumber = sortedBoxes[prevFrameIndex].FrameNumber;
                int i = prevFrameIndex;
                while (true)
                {
                    if (i < 0 || sortedBoxes[i].FrameNumber != boxFrameNumber)
                    {
                        // We reached the beginning or a box with a frame number that differs
                        break;
                    }
                    // Record the box, but only if its within the desired confidence limits
                    if (sortedBoxes[i].Confidence + correction < GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold &&
                        sortedBoxes[i].Confidence + correction < GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
                    {
                        i--;
                        continue;
                    }
                    bboxes.Add(sortedBoxes[i]);
                    i--;
                }
            }
            else if (nextFrameIndex != -1)
            {
                // The desired bounding box frame is above the displayedVideoFrame
                int boxFrameNumber = sortedBoxes[nextFrameIndex].FrameNumber;
                int i = nextFrameIndex;
                int count = sortedBoxes.Count;
                while (true)
                {
                    if (i > count - 1 || sortedBoxes[i].FrameNumber != boxFrameNumber)
                    {
                        // We reached the end or a box with a frame number that differs
                        break;
                    }
                    // Record the box, but only if its within the desired confidence limits
                    if (sortedBoxes[i].Confidence + correction < GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold &&
                        sortedBoxes[i].Confidence + correction < GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
                    {
                        i++;
                        continue;
                    }
                    bboxes.Add(sortedBoxes[i]);
                    i++;
                }
            }

            // Now draw the bounding boxes
            foreach (BoundingBox bbox in bboxes)
            {
                DoDrawBoundingBox(canvas, bbox, width, height, margin, transformGroup);
            }
            Panel.SetZIndex(canvas, 1);
            return true;

        }

        // INVOKED FOR IMAGES
        public bool DrawBoundingBoxesInCanvas(Canvas canvas, double width, double height, int margin = 0, TransformGroup transformGroup = null)
        {
            if (canvas == null)
            {
                return false;
            }
            if (GlobalReferences.DetectionsExists == false || GlobalReferences.HideBoundingBoxes || Keyboard.IsKeyDown(Key.H))
            {
                // As detection don't exist, there won't be any bounding boxes to draw.
                // Or, if the user doesn't want them displayed, we don't show them. 
                return false;
            }

            // Ignore any bounding box that is below the desired confidence threshold for displaying it.
            // We use MaxConfidence to test against, as its the max confidence over all bounding boxes for this file, regardless of the detection category.
            // Note that
            // - the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
            //   determined in the select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
            //   show bounding boxes when the confidence is .4 or more.
            // - we add a slight correction value to the MaxConfidence, so confidences near the threshold will still appear.
            double correction = 0.005;
            if (MaxConfidence + correction < GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold &&
                MaxConfidence + correction < GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
            {
                return false;
            }

            // Cycle through all the bounding boxes for this file
            foreach (BoundingBox bbox in Boxes)
            {
                // Skip the bounding box as its confidence is below the desired confidence threshold for displaying it.
                // As with the previous explanation,
                // - the BoundingBoxDisplayThreshold is the user-defined default set in preferences, while
                // - the BoundingBoxThresholdOveride is the threshold determined in the select dialog. 
                // For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
                // show bounding boxes when the confidence is .4 or more.
                if ((bbox.Confidence + correction) < GlobalReferences.TimelapseState.BoundingBoxDisplayThreshold &&
                    (bbox.Confidence + correction) < GlobalReferences.TimelapseState.BoundingBoxThresholdOveride)
                {
                    continue;
                }
                DoDrawBoundingBox(canvas, bbox, width, height, margin, transformGroup);
            }
            Panel.SetZIndex(canvas, 1);
            return true;
        }

        // Given a bounding box, draw it atop the canvas.
        // The width, height, margin and transformGroup are used to adjust how and where the bounding box is drawn in the canvas
        private void DoDrawBoundingBox(Canvas canvas, BoundingBox bbox, double width, double height, int margin = 0, TransformGroup transformGroup = null)
        {
            // Create a bounding box 
            Rectangle rect = new();
            SolidColorBrush brush;
            bool colorblind = GlobalReferences.TimelapseState.BoundingBoxColorBlindFriendlyColors;
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
                    brush = ColorsAndBrushes.SolidColorBrushFromColor(Colors.LavenderBlush, opacity);
                    break;
                case "1":
                    brush = ColorsAndBrushes.SolidColorBrushFromColor(Colors.DeepSkyBlue, opacity);
                    break;
                case "2":
                    brush = (colorblind)
                        ? ColorsAndBrushes.SolidColorBrushFromColor(Colors.Yellow)
                        : ColorsAndBrushes.SolidColorBrushFromColor(Colors.Red, opacity);
                    break;
                case "3":
                    brush = ColorsAndBrushes.SolidColorBrushFromColor(Colors.White, opacity);
                    break;
                default:
                    brush = ColorsAndBrushes.SolidColorBrushFromColor(Colors.PaleGreen, opacity);
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
                screenPositionBottomRight =
                    transformGroup.Transform(BoundingBox.ConvertRatioToPoint(bbox.Rectangle.Left + bbox.Rectangle.Width, bbox.Rectangle.Top + bbox.Rectangle.Height, width,
                        height));
            }

            // Adjust the offset by any margin value (which could be 0)
            screenPositionTopLeft.X += margin;
            screenPositionTopLeft.Y += margin;
            Point screenPostionWidthHeight = new(screenPositionBottomRight.X - screenPositionTopLeft.X, screenPositionBottomRight.Y - screenPositionTopLeft.Y);

            // We also adjust the rect width and height to take into account the stroke thickness, to avoid the stroke overlapping the contained item
            // as otherwise the border thickness would overlap with the entity in the bounding box)
            rect.Width = screenPostionWidthHeight.X + (2 * stroke_thickness);
            rect.Height = screenPostionWidthHeight.Y + (2 * stroke_thickness);


            // Now add the rectangle to the canvas, also adjusting for the stroke thickness.
            Canvas.SetLeft(rect, screenPositionTopLeft.X - stroke_thickness);
            Canvas.SetTop(rect, screenPositionTopLeft.Y - stroke_thickness);
            canvas.Children.Add(rect);
            canvas.Tag = Constant.MarkableCanvas.BoundingBoxCanvasTag;

            // Bounding box labelling: Category plus confidence for detection(classication) (to two decimal places or epsilon)
            // Use the primary detection category if there are no classifications, 
            // The bboxLabel contains just the top-ranked classification category + its confidence
            // the bboxTextBlock contains all predicted items + their confidence as a list
            string bboxLabel = (bbox.Classifications.Count == 0)
                ? $"{bbox.DetectionLabel} {ReformatFloatToTwoDecimalPlacesAndEpsilon(bbox.Confidence)}"
                : $"{bbox.Classifications[0].Key} " +
                  $"{ReformatFloatToTwoDecimalPlacesAndEpsilon(bbox.Classifications[0].Value)}" +
                  $"({ReformatFloatToTwoDecimalPlacesAndEpsilon(bbox.Confidence)})";

            string bboxTextBlock = string.Empty;
            if (bbox.Classifications.Count > 0)
            {
                foreach (KeyValuePair<string, string> classification in bbox.Classifications)
                {
                    bboxTextBlock += $"{classification.Key} {ReformatFloatToTwoDecimalPlacesAndEpsilon(classification.Value)}{Environment.NewLine}";
                }

                bboxTextBlock = bboxTextBlock.Trim('\r', '\n');
            }

            // Add information to each bounding box using a tooltip or a splitbutton
            if (GlobalReferences.TimelapseState.BoundingBoxAnnotate == false)
            {
                // Use a tooltip. Adjust its timing to be faster (in switching too) than the default
                rect.ToolTip = bbox.Classifications.Count == 0 ? bboxLabel : bboxTextBlock;
                rect.SetValue(ToolTipService.InitialShowDelayProperty, 50);
                rect.SetValue(ToolTipService.BetweenShowDelayProperty, 0);
            }
            else
            {
                //The button contains the category label, while its dropdown contains a text list of all predicted categories
                Label classificationUIObject = new()
                {
                    Opacity = 0.6,
                    Content = bboxLabel,
                    FontSize = 12,
                    Visibility = Visibility.Visible,
                    Background = Brushes.White,
                    Width = Double.NaN,
                    Height = 20, //DecimalAny.NaN,
                    Foreground = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new(0, -2, 0, -2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                double leftPosition = (screenPositionTopLeft.X - stroke_thickness) < 0
                    ? 0
                    : screenPositionTopLeft.X - stroke_thickness;
                double topPosition = (screenPositionTopLeft.Y - stroke_thickness - 20) < 0
                    ? 0
                    : screenPositionTopLeft.Y - stroke_thickness - 20;

                Canvas.SetLeft(classificationUIObject, leftPosition);
                Canvas.SetTop(classificationUIObject, topPosition);
                canvas.Children.Add(classificationUIObject);
            }
        }
        #endregion

        #region static internal methods
        private static string ReformatFloatToTwoDecimalPlacesAndEpsilon(string value)
        {
            return float.TryParse(value, out float result)
                ? ReformatFloatToTwoDecimalPlacesAndEpsilon(result)
                : string.Empty;
        }
        private static string ReformatFloatToTwoDecimalPlacesAndEpsilon(float value)
        {
            return (value >= .1) ? Invariant($"{value:#.##}") : "\u03B5";
        }
        #endregion
    }
}
