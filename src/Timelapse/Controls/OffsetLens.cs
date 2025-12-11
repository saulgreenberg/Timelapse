using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.Controls
{
    /// <summary>
    /// An implementation of the Offset lens, which inherits the XCEED toolkit Magnifier Adorner
    /// Some things to work on here
    /// - Direction can be explicitly set to four angles. However, I only use the NorthEast angle for now but left the code for angles intact
    /// - To implementation Direction rotation, I modified the XCEED manifier to:
    ///   - intercept RenderTransform, where it rotates the magnifier part differently from the entire object to keep it upright
    ///      as otherwise the magnified region would be rotated as well)
    ///   -implement a private RotateFactor, which when set will automatically rotate the magnifier part above
    ///    All modified code is in a well defined region. However, it can be discarded if needed if I decide to not use any magnifer render transforms.
    /// - Having said that, I don't use it as this current implementation just uses the NorthEast angle, so its for future work    
    ///     The problem is that animation doesn't seem to work on it, as part of the Magnifier uses Freeze - I have to look into that.
    ///     I also have to detect the edges of objects to decide when to do a rotation.
    /// - Using this offset lens also works on other objects. However, if those objects are scaled, the magnifier is as well, as it is an adorner.
    ///     Its possible that https://stackoverflow.com/questions/9672207/how-to-create-adorners-that-dont-scale-with-adornedelement has a solution.
    ///  - Various sizes are hard-wired. These should be dyanmic and placed in Constants.
    /// </summary>
    public class OffsetLens : Magnifier
    {
        #region Public properties
        /// <summary>
        /// Set the direction of the offset lens. NorthEast is the default
        /// Not really used publicly: see above
        /// </summary>
        public OffsetLensDirection Direction { get; set; }

        /// <summary>
        /// Set / Get whether the offset lens is visible (Showing)
        /// </summary>
        public bool Show
        {
            get => Visibility == Visibility.Visible;
            set
            {
                Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (magHandleAdorner != null)
                {
                    // check condition - why would the maghandleadorner be null if this is not null?
                    magHandleAdorner.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        // We override the base class as we have to save the current state of the current zoom.
        // In reality, we could just eliminate ZoomFactor and just used the saved state, but its
        // best to keep it part of the offset lens object for clarity
        public new double ZoomFactor
        {
            get => base.ZoomFactor;
            set
            {
                base.ZoomFactor = value;
                GlobalReferences.TimelapseState.OffsetLensZoomFactor = value;
            }
        }

        #endregion

        #region Private variables
        private Point Offset = new(125, -125);
        private MagHandleAdorner magHandleAdorner;
        private AdornerLayer myAdornerLayer;
        #endregion

        #region Constructors, Loading
        public OffsetLens()
        {
            // Lens appearance
            Radius = MarkableCanvas.MagnifyingGlassDiameter / 2.0;
            BorderBrush = MakeOutlineBrush();
            BorderThickness = new(3);
            Background = Brushes.Black;
            FrameType = FrameType.Circle;
            Loaded += OffsetLens_Loaded;

            // Makes mouse wheel operations (usually used to change the magnification level) into a no-op
            ZoomFactorOnMouseWheel = 0;
            IsUsingZoomOnMouseWheel = false;
        }

        private void OffsetLens_Loaded(object sender, RoutedEventArgs e)
        {
            IsEnabled = true;

            // Handle adorner, including calculating its original offset
            myAdornerLayer = AdornerLayer.GetAdornerLayer(this);
            magHandleAdorner = new(this)
            {
                IsHitTestVisible = false
            };
            TranslateTransform tt = new(Offset.X, Offset.Y);
            magHandleAdorner.RenderTransform = tt;
            if (null != myAdornerLayer)
            {
                myAdornerLayer.Add(magHandleAdorner);
            }
            else
            {
                TracePrint.NullException(nameof(myAdornerLayer));
            }

            SetDirection(OffsetLensDirection.TopRight);
        }
        #endregion

        #region Private Methods (internal use)
        // Actually set the direction of the offset lens
        private void SetDirection(OffsetLensDirection direction)
        {
            double x;
            double y;
            double angle;

            // Set lens transformation
            switch (direction)
            {
                case OffsetLensDirection.TopLeft: // Up and Left
                    x = -Offset.X;
                    y = Offset.Y;
                    angle = -90;
                    break;
                case OffsetLensDirection.TopRight: // Up and Right
                default:
                    x = Offset.X;
                    y = Offset.Y;
                    angle = 0;
                    break;
                case OffsetLensDirection.BottomLeft: // Lower Right
                    x = -Offset.X;
                    y = 3 * Offset.Y;
                    angle = -180;
                    break;
                case OffsetLensDirection.BottomRight: // Lower Left
                    x = Offset.X;
                    y = 3 * Offset.Y;
                    angle = -270;
                    break;
            }
            // Now rotate and position the entire magnifying glass
            RenderTransform = CreateTransformGroup(x, y, angle);
            Direction = direction;
        }

        private static LinearGradientBrush MakeOutlineBrush()
        {
            LinearGradientBrush outlineBrush = new()
            {
                StartPoint = new(0, 0),
                EndPoint = new(0, 1)
            };
            ColorConverter cc = new();
            object o1= cc.ConvertFrom("#AAA");
            Color lightGrey = o1 != null
                ? (Color)o1
                : Colors.LightGray;
            object o2 = cc.ConvertFrom("#111");
            Color darkGrey = o2 != null
                ? (Color)o2
                : Colors.DarkGray;
            outlineBrush.GradientStops.Add(new(lightGrey, 0));
            outlineBrush.GradientStops.Add(new(darkGrey, 1));
            return outlineBrush;
        }

        private static TransformGroup CreateTransformGroup(double x, double y, double angle)
        {
            TransformGroup transformGroup = new();
            TranslateTransform tt = new(x, y);
            RotateTransform rt = new(angle);
            transformGroup.Children.Add(tt);
            transformGroup.Children.Add(rt);
            return transformGroup;
        }
        #endregion
    }

    #region Class: Magnifier Handle and Crosshairs Adorner. Used internal to construct the magnifying glass appearance
    // Create an adorner for the magnifier that attahces a handle to it and draws crosshairs at its center
    internal class MagHandleAdorner : Adorner
    {
        internal MagHandleAdorner(UIElement adornedElement)
         : base(adornedElement)
        {
        }
        // A common way to implement an adorner's rendering behavior is to override the OnRender
        // method, which is called by the layout system as part of a rendering pass.
        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect adornedElementRect = new(AdornedElement.DesiredSize);
            int centerOffset = 75;
            Point handleStartOffset = new(0, 0);
            Point handleEndOffset = new(-39, 39);
            Point center = new(adornedElementRect.Width / 2, adornedElementRect.Height / 2);
            Point centerLeft = PointSubtract(center, new(-centerOffset, 0));
            Point centerRight = PointSubtract(center, new(centerOffset, 0));
            Point centerTop = PointSubtract(center, new(0, -centerOffset));
            Point centerBottom = PointSubtract(center, new(0, centerOffset));
            Point handleStart = PointSubtract(adornedElementRect.BottomLeft, handleStartOffset);
            Point handleEnd = PointSubtract(adornedElementRect.BottomLeft, handleEndOffset);

            // Draw the handle
            Pen handlePen = new(new SolidColorBrush(Colors.Green), 4);
            drawingContext.DrawLine(handlePen, handleStart, handleEnd);
            drawingContext.DrawLine(handlePen, handleStart, handleEnd);

            // Draw the crosshairs
            Pen crosshairPen = new(new SolidColorBrush(Colors.LightGray), .5);
            drawingContext.DrawLine(crosshairPen, centerLeft, centerRight);
            drawingContext.DrawLine(crosshairPen, centerTop, centerBottom);
        }

        private static Point PointSubtract(Point p1, Point p2)
        {
            return new(p1.X - p2.X, p1.Y - p2.Y);
        }
    }
    #endregion
}
