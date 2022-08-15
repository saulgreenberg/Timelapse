using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Timelapse.Util;

namespace Timelapse.Images
{
    /// <summary>
    /// A home-grown magnifying lens
    /// Note that we don't use the xceed one (or this one in place of the xceed one as:
    /// - this one can display the original unaltered images (for image differencing) while the xceed one cannot
    /// - this one cannot display the video while the xceed one can
    /// </summary>
    internal class MagnifyingGlass : Canvas
    {
        #region Public properties
        /// <summary>
        /// Set / Get the Zoom value on the magnifying glass
        /// </summary>
        public double ZoomFactor { get; set; }

        /// <summary>
        /// Set/Get whether the magnifying lens is showing (visible)
        /// </summary>
        public bool Show
        {
            get
            {
                return this.IsVisible;
            }
            set
            {
                this.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region private properties and variables
        // This hides the actual parent, which is why we set it as a property
        private new MarkableCanvas Parent { get; set; }

        // The lens is constructed within a canvas
        private readonly Canvas lensCanvas;

        // current angle of the lens
        private double lensAngle;

        // The lens part of the magnifying glass, which contains the magnified image
        private readonly Ellipse magnifierLens;

        // current angle of the entire magnifying glass
        private double magnifyingGlassAngle;
        #endregion

        #region Constructor
        public MagnifyingGlass(MarkableCanvas markableCanvas)
        {
            this.IsEnabled = false;
            this.IsHitTestVisible = false;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.Parent = markableCanvas;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Visibility = Visibility.Collapsed;
            this.ZoomFactor = Constant.MarkableCanvas.MagnifyingGlassDefaultZoom; // A 'just in case' default

            this.lensAngle = 0;
            this.magnifyingGlassAngle = 0;

            // Create the handle of the magnifying glass
            Line handle = new Line
            {
                StrokeThickness = 5,
                X1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart,
                Y1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart,
                X2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd
            };
            LinearGradientBrush handleBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0.78786, 1),
                EndPoint = new Point(1, 0.78786)
            };
            handleBrush.GradientStops.Add(new GradientStop(Colors.DarkGreen, 0));
            handleBrush.GradientStops.Add(new GradientStop(Colors.LightGreen, 0.9));
            handleBrush.GradientStops.Add(new GradientStop(Colors.Green, 1));
            handle.Stroke = handleBrush;
            this.Children.Add(handle);

            // Create the lens of the magnifying glass
            this.lensCanvas = new Canvas();
            this.Children.Add(this.lensCanvas);

            // lens has a white backgound
            Ellipse lensBackground = new Ellipse
            {
                Width = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Height = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Fill = Brushes.White
            };
            this.lensCanvas.Children.Add(lensBackground);

            this.magnifierLens = new Ellipse
            {
                Width = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Height = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                StrokeThickness = 3
            };

            // fill the lens
            VisualBrush lensFill = new VisualBrush
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 50, 50),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1)
            };
            this.magnifierLens.Fill = lensFill;

            // outline the lens
            LinearGradientBrush outlineBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            ColorConverter cc = new ColorConverter();
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#AAA"), 0));
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#111"), 1));
            this.magnifierLens.Stroke = outlineBrush;
            this.lensCanvas.Children.Add(this.magnifierLens);

            Ellipse lensImage = new Ellipse();
            Canvas.SetLeft(lensImage, 2);
            Canvas.SetTop(lensImage, 2);
            lensImage.StrokeThickness = 4;
            lensImage.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            lensImage.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            this.lensCanvas.Children.Add(lensImage);

            // crosshairs
            Line verticalCrosshair = new Line
            {
                StrokeThickness = 0.25,
                X1 = 5,
                Y1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                Stroke = Brushes.Black,
                Opacity = 0.5
            };
            this.lensCanvas.Children.Add(verticalCrosshair);

            Line horizontalCrosshair = new Line
            {
                StrokeThickness = 0.25,
                X1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                Y1 = 5,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5,
                Stroke = Brushes.Black,
                Opacity = 0.5
            };
            this.lensCanvas.Children.Add(horizontalCrosshair);
        }
        #endregion

        #region Public methods
        public void RedrawIfVisible(Point mouseLocation, Canvas canvasToMagnify)
        {
            // nothing to draw
            if ((this.IsEnabled == false) ||
                (this.IsVisible == false) ||
                (this.Visibility != Visibility.Visible) ||
                (canvasToMagnify == null) ||
                (this.Parent.ImageToMagnify.Source == null))
            {
                return;
            }

            // Given a mouse position over the displayed image, we need to know where the equivalent position is over the magnified image (which is a different size)
            // We do this by calculating the ratio of the point over the displayed image, and then using that to calculate the position over the cached image
            Point mousePosition = NativeMethods.GetCursorPos(this.Parent.ImageToDisplay);
            Point mouseLocationRatio = Marker.ConvertPointToRatio(mousePosition, this.Parent.ImageToDisplay.ActualWidth, this.Parent.ImageToDisplay.ActualHeight);
            Point magnifiedLocation = Marker.ConvertRatioToPoint(mouseLocationRatio, canvasToMagnify.Width, canvasToMagnify.Height);

            // Create an Visual brush from the unaltered image in the magnification canvas magCanvas, set its properties, and use it to fill the magnifying glass.
            // This includes calculating the position and zoom of the viewbox within that brush
            VisualBrush magnifierBrush = new VisualBrush(canvasToMagnify)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1),
                Viewbox = new Rect(magnifiedLocation.X - this.ZoomFactor / 2.0, magnifiedLocation.Y - this.ZoomFactor / 2.0, this.ZoomFactor, this.ZoomFactor)
            };
            // Finally, fill the magnifying glass with this brush
            this.magnifierLens.Fill = magnifierBrush;

            // Figure out the magnifying glass angle needed
            // The idea is that we will start rotating when the magnifying glass is near the top and the left of the display
            // The critical distance is size for the Y direction, and somewhat larger than size for the X direction (as we have to start
            // rotating earlier so it doesn't get clipped). xsize is somewhat arbitrary, i.e., determined by trial and error
            // positions of edges where angle should change 
            const double EdgeThreshold = Constant.MarkableCanvas.MagnifyingGlassDiameter; // proximity to an edge where the magnifying glass change angles
            double leftEdge = EdgeThreshold;
            double rightEdge = this.Parent.ImageToDisplay.ActualWidth - EdgeThreshold;
            double topEdge = EdgeThreshold;
            double bottomEdge = this.Parent.ImageToDisplay.ActualHeight - EdgeThreshold;

            double newMagnifyingGlassAngle;  // the new angle to rotate the magnifying glass to
            // In various cases, several angles can work so choose a new angle whose difference from the existing angle will cause the least amount of animation 
            if ((mouseLocation.X < leftEdge) && (mouseLocation.Y < topEdge))
            {
                newMagnifyingGlassAngle = 180;                                                  // upper left corner
            }
            else if ((mouseLocation.X < leftEdge) && (mouseLocation.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 90;                                                   // lower left corner
            }
            else if (mouseLocation.X < leftEdge)
            {
                newMagnifyingGlassAngle = AdjustAngle(this.magnifyingGlassAngle, 90, 180);      // middle left edge
            }
            else if ((mouseLocation.X > rightEdge) && (mouseLocation.Y < topEdge))
            {
                newMagnifyingGlassAngle = 270;                                                      // upper right corner
            }
            else if ((mouseLocation.X > rightEdge) && (mouseLocation.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 0;                                                         // lower right corner
            }
            else if (mouseLocation.X > rightEdge)
            {
                newMagnifyingGlassAngle = AdjustAngle(this.magnifyingGlassAngle, 270, 0);       // middle right edge
            }
            else if (mouseLocation.Y < topEdge)
            {
                newMagnifyingGlassAngle = AdjustAngle(this.magnifyingGlassAngle, 270, 180);     // top edge, middle
            }
            else if (mouseLocation.Y > bottomEdge)
            {
                newMagnifyingGlassAngle = AdjustAngle(this.magnifyingGlassAngle, 0, 90);       // bottom edge, middle
            }
            else
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle;                           // far enough from edges, any angle will work: magnifer stays on the display image at any angle; 
            }

            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            double lensDiameter = this.magnifierLens.Width;
            if (this.magnifyingGlassAngle != newMagnifyingGlassAngle)
            {
                // Correct the rotation in those cases where it would turn the long way around. 
                // Note that the new lens angle correction is hard coded rather than calculated, as it works. 
                double newLensAngle;
                double uncorrectedNewLensAngle = -newMagnifyingGlassAngle;
                if (this.magnifyingGlassAngle == 270 && newMagnifyingGlassAngle == 0)
                {
                    this.magnifyingGlassAngle = -90;
                    newLensAngle = -360; // subtract the rotation of the magnifying glass to counter that rotational effect
                }
                else if (this.magnifyingGlassAngle == 0 && newMagnifyingGlassAngle == 270)
                {
                    this.magnifyingGlassAngle = 360;
                    newLensAngle = 90;
                }
                else
                {
                    newLensAngle = uncorrectedNewLensAngle;
                }

                // Rotate the lens within the magnifying glass
                Duration animationDuration = new Duration(new TimeSpan(0, 0, 0, 0, 500));
                DoubleAnimation lensAnimation = new DoubleAnimation(this.lensAngle, newLensAngle, animationDuration);
                RotateTransform rotateTransformLens = new RotateTransform(this.magnifyingGlassAngle, lensDiameter / 2, lensDiameter / 2);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, lensAnimation);
                this.lensCanvas.RenderTransform = rotateTransformLens;

                // Now rotate and position the entire magnifying glass
                RotateTransform rotateTransformMagnifyingGlass = new RotateTransform(this.magnifyingGlassAngle, lensDiameter, lensDiameter);
                DoubleAnimation magnifyingGlassAnimation = new DoubleAnimation(this.magnifyingGlassAngle, newMagnifyingGlassAngle, animationDuration);
                rotateTransformMagnifyingGlass.BeginAnimation(RotateTransform.AngleProperty, magnifyingGlassAnimation);
                this.RenderTransform = rotateTransformMagnifyingGlass;

                // Save the angle so we can compare it on the next iteration. If any of them are 360, swap it to 0
                if (newMagnifyingGlassAngle % 360 == 0)
                {
                    newMagnifyingGlassAngle = 0;
                }
                if (newLensAngle % 360 == 0)
                {
                    uncorrectedNewLensAngle = 0;
                }
                this.magnifyingGlassAngle = newMagnifyingGlassAngle;
                this.lensAngle = uncorrectedNewLensAngle;
            }
            Canvas.SetLeft(this, mouseLocation.X - lensDiameter);
            Canvas.SetTop(this, mouseLocation.Y - lensDiameter);
        }
        #endregion

        #region Private (internal) methods
        // return the current angle if it matches one of the desired angle, or the the desired angle that is closest to the angle in degrees
        private static double AdjustAngle(double currentAngle, double angle1, double angle2)
        {
            if (currentAngle == angle2)
            {
                return angle2;
            }
            else if (Math.Abs(currentAngle - angle1) > 180)
            {
                return angle2;
            }
            return angle1;
        }
        #endregion
    }
}
