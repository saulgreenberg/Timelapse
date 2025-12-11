using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Timelapse.DebuggingSupport;
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
            get => IsVisible;
            set => Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region private properties and variables
        // This hides the actual parent, which is why we set it as a property
        private new MarkableCanvas Parent { get; }

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
            IsEnabled = false;
            IsHitTestVisible = false;
            HorizontalAlignment = HorizontalAlignment.Left;
            Parent = markableCanvas;
            VerticalAlignment = VerticalAlignment.Top;
            Visibility = Visibility.Collapsed;
            ZoomFactor = Constant.MarkableCanvas.MagnifyingGlassDefaultZoom; // A 'just in case' default

            lensAngle = 0;
            magnifyingGlassAngle = 0;

            // Create the handle of the magnifying glass
            Line handle = new()
            {
                StrokeThickness = 5,
                X1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart,
                Y1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart,
                X2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd
            };
            LinearGradientBrush handleBrush = new()
            {
                StartPoint = new(0.78786, 1),
                EndPoint = new(1, 0.78786)
            };
            handleBrush.GradientStops.Add(new(Colors.DarkGreen, 0));
            handleBrush.GradientStops.Add(new(Colors.LightGreen, 0.9));
            handleBrush.GradientStops.Add(new(Colors.Green, 1));
            handle.Stroke = handleBrush;
            Children.Add(handle);

            // Create the lens of the magnifying glass
            lensCanvas = new();
            Children.Add(lensCanvas);

            // lens has a white backgound
            Ellipse lensBackground = new()
            {
                Width = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Height = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Fill = Brushes.White
            };
            lensCanvas.Children.Add(lensBackground);

            magnifierLens = new()
            {
                Width = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                Height = Constant.MarkableCanvas.MagnifyingGlassDiameter,
                StrokeThickness = 3
            };

            // fill the lens
            VisualBrush lensFill = new()
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new(0, 0, 50, 50),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new(0, 0, 1, 1)
            };
            magnifierLens.Fill = lensFill;

            // outline the lens
            LinearGradientBrush outlineBrush = new()
            {
                StartPoint = new(0, 0),
                EndPoint = new(0, 1)
            };
            ColorConverter cc = new();
            object stop1 = cc.ConvertFrom("#AAA");
            object stop2 = cc.ConvertFrom("#111");
            if (stop1 != null && stop2 != null)
            {
                outlineBrush.GradientStops.Add(new((Color)stop1, 0));
                outlineBrush.GradientStops.Add(new((Color)stop2, 1));
            }
            else
            {
                TracePrint.NullException(nameof(stop1) + " and " + nameof(stop2));
            }
            magnifierLens.Stroke = outlineBrush;
            lensCanvas.Children.Add(magnifierLens);

            Ellipse lensImage = new();
            SetLeft(lensImage, 2);
            SetTop(lensImage, 2);
            lensImage.StrokeThickness = 4;
            lensImage.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            lensImage.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            lensCanvas.Children.Add(lensImage);

            // crosshairs
            Line verticalCrosshair = new()
            {
                StrokeThickness = 0.25,
                X1 = 5,
                Y1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5.0,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0,
                Stroke = Brushes.Black,
                Opacity = 0.5
            };
            lensCanvas.Children.Add(verticalCrosshair);

            Line horizontalCrosshair = new()
            {
                StrokeThickness = 0.25,
                X1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0,
                Y1 = 5,
                X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0,
                Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5.0,
                Stroke = Brushes.Black,
                Opacity = 0.5
            };
            lensCanvas.Children.Add(horizontalCrosshair);
        }
        #endregion

        #region Public methods
        public void RedrawIfVisible(Point mouseLocation, Canvas canvasToMagnify)
        {
            // nothing to draw
            if ((IsEnabled == false) ||
                (IsVisible == false) ||
                (Visibility != Visibility.Visible) ||
                (canvasToMagnify == null) ||
                (Parent.ImageToMagnify.Source == null))
            {
                return;
            }

            // Given a mouse position over the displayed image, we need to know where the equivalent position is over the magnified image (which is a different size)
            // We do this by calculating the ratio of the point over the displayed image, and then using that to calculate the position over the cached image
            Point mousePosition = NativeMethods.GetCursorPos(Parent.ImageToDisplay);
            Point mouseLocationRatio = Marker.ConvertPointToRatio(mousePosition, Parent.ImageToDisplay.ActualWidth, Parent.ImageToDisplay.ActualHeight);
            Point magnifiedLocation = Marker.ConvertRatioToPoint(mouseLocationRatio, canvasToMagnify.Width, canvasToMagnify.Height);

            // Create an Visual brush from the unaltered image in the magnification canvas magCanvas, set its properties, and use it to fill the magnifying glass.
            // This includes calculating the position and zoom of the viewbox within that brush
            VisualBrush magnifierBrush = new(canvasToMagnify)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new(0, 0, 1, 1),
                Viewbox = new(magnifiedLocation.X - ZoomFactor / 2.0, magnifiedLocation.Y - ZoomFactor / 2.0, ZoomFactor, ZoomFactor)
            };
            // Finally, fill the magnifying glass with this brush
            magnifierLens.Fill = magnifierBrush;

            // Figure out the magnifying glass angle needed
            // The idea is that we will start rotating when the magnifying glass is near the top and the left of the display
            // The critical distance is size for the Y direction, and somewhat larger than size for the X direction (as we have to start
            // rotating earlier so it doesn't get clipped). xsize is somewhat arbitrary, i.e., determined by trial and error
            // positions of edges where angle should change 
            const double EdgeThreshold = Constant.MarkableCanvas.MagnifyingGlassDiameter; // proximity to an edge where the magnifying glass change angles
            double leftEdge = EdgeThreshold;
            double rightEdge = Parent.ImageToDisplay.ActualWidth - EdgeThreshold;
            double topEdge = EdgeThreshold;
            double bottomEdge = Parent.ImageToDisplay.ActualHeight - EdgeThreshold;

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
                newMagnifyingGlassAngle = AdjustAngle(magnifyingGlassAngle, 90, 180);      // middle left edge
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
                newMagnifyingGlassAngle = AdjustAngle(magnifyingGlassAngle, 270, 0);       // middle right edge
            }
            else if (mouseLocation.Y < topEdge)
            {
                newMagnifyingGlassAngle = AdjustAngle(magnifyingGlassAngle, 270, 180);     // top edge, middle
            }
            else if (mouseLocation.Y > bottomEdge)
            {
                newMagnifyingGlassAngle = AdjustAngle(magnifyingGlassAngle, 0, 90);       // bottom edge, middle
            }
            else
            {
                newMagnifyingGlassAngle = magnifyingGlassAngle;                           // far enough from edges, any angle will work: magnifer stays on the display image at any angle; 
            }

            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            double lensDiameter = magnifierLens.Width;
            if (Math.Abs(magnifyingGlassAngle - newMagnifyingGlassAngle) > .0001)
            {
                // Correct the rotation in those cases where it would turn the long way around. 
                // Note that the new lens angle correction is hard coded rather than calculated, as it works. 
                double newLensAngle;
                double uncorrectedNewLensAngle = -newMagnifyingGlassAngle;
                if (Math.Abs(magnifyingGlassAngle - 270) < .0001 && newMagnifyingGlassAngle == 0)
                {
                    magnifyingGlassAngle = -90;
                    newLensAngle = -360; // subtract the rotation of the magnifying glass to counter that rotational effect
                }
                else if (magnifyingGlassAngle == 0 && Math.Abs(newMagnifyingGlassAngle - 270) < .0001)
                {
                    magnifyingGlassAngle = 360;
                    newLensAngle = 90;
                }
                else
                {
                    newLensAngle = uncorrectedNewLensAngle;
                }

                // Rotate the lens within the magnifying glass
                Duration animationDuration = new(new(0, 0, 0, 0, 500));
                DoubleAnimation lensAnimation = new(lensAngle, newLensAngle, animationDuration);
                RotateTransform rotateTransformLens = new(magnifyingGlassAngle, lensDiameter / 2, lensDiameter / 2);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, lensAnimation);
                lensCanvas.RenderTransform = rotateTransformLens;

                // Now rotate and position the entire magnifying glass
                RotateTransform rotateTransformMagnifyingGlass = new(magnifyingGlassAngle, lensDiameter, lensDiameter);
                DoubleAnimation magnifyingGlassAnimation = new(magnifyingGlassAngle, newMagnifyingGlassAngle, animationDuration);
                rotateTransformMagnifyingGlass.BeginAnimation(RotateTransform.AngleProperty, magnifyingGlassAnimation);
                RenderTransform = rotateTransformMagnifyingGlass;

                // Save the angle so we can compare it on the next iteration. If any of them are 360, swap it to 0
                if (newMagnifyingGlassAngle % 360 == 0)
                {
                    newMagnifyingGlassAngle = 0;
                }
                if (newLensAngle % 360 == 0)
                {
                    uncorrectedNewLensAngle = 0;
                }
                magnifyingGlassAngle = newMagnifyingGlassAngle;
                lensAngle = uncorrectedNewLensAngle;
            }
            SetLeft(this, mouseLocation.X - lensDiameter);
            SetTop(this, mouseLocation.Y - lensDiameter);
        }
        #endregion

        #region Private (internal) methods
        // return the current angle if it matches one of the desired angle, or the the desired angle that is closest to the angle in degrees
        private static double AdjustAngle(double currentAngle, double angle1, double angle2)
        {
            if (Math.Abs(currentAngle - angle2) < .0001)
            {
                return angle2;
            }

            if (Math.Abs(currentAngle - angle1) > 180)
            {
                return angle2;
            }
            return angle1;
        }
        #endregion
    }
}
