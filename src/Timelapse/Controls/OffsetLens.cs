using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.Controls
{
    /// <summary>
    /// An implementation of the Offset lens, which inherits the toolkit Magnifier Adorner
    /// To be used over videos
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
        // Persistent transforms kept across direction changes so BeginAnimation can be called repeatedly.
        // (Adorners track layout position, not RenderTransform, so the adorner needs its own matching translate.)
        private TranslateTransform lensTranslate;
        private TranslateTransform adornerTranslate;
        #endregion

        #region Arc-animation DependencyProperty
        // All four lens positions sit on the same circle (r = |Offset|*√2) centred on the cursor,
        // exactly 90° apart.  Animating a single angle and deriving (x,y) in the callback gives a
        // circular arc — the same rigid-unit-rotation feel as the MagnifyingGlass — without
        // rotating the lens content.  The callback also calls InvalidateVisual on the handle
        // adorner each frame, so no separate CompositionTarget.Rendering hook is needed.
        private static readonly DependencyProperty CurrentAngleProperty =
            DependencyProperty.Register(
                "CurrentAngle", typeof(double), typeof(OffsetLens),
                new PropertyMetadata(-45.0, OnCurrentAngleChanged));

        private static void OnCurrentAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not OffsetLens lens || lens.lensTranslate == null) return;

            double theta = (double)e.NewValue * Math.PI / 180.0;
            double r = Math.Sqrt(lens.Offset.X * lens.Offset.X + lens.Offset.Y * lens.Offset.Y);
            double x = r * Math.Cos(theta);
            double y = r * Math.Sin(theta);

            lens.lensTranslate.X = x;
            lens.lensTranslate.Y = y;
            lens.adornerTranslate.X = x;
            lens.adornerTranslate.Y = y;
            lens.magHandleAdorner?.InvalidateVisual();
        }
        #endregion

        #region Internal accessor for MagHandleAdorner
        // Exposes the current animated offset so OnRender can compute the handle position
        // dynamically at every frame during the arc.
        internal TranslateTransform AdornerTranslate => adornerTranslate;
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

            // Handle adorner — give it its own persistent TranslateTransform so we can
            // animate it in sync with the lens transform (adorners follow layout position,
            // not the adorned element's RenderTransform, so the adorner needs its own offset).
            myAdornerLayer = AdornerLayer.GetAdornerLayer(this);
            magHandleAdorner = new(this)
            {
                IsHitTestVisible = false
            };
            adornerTranslate = new TranslateTransform(Offset.X, Offset.Y);
            magHandleAdorner.RenderTransform = adornerTranslate;
            if (null != myAdornerLayer)
            {
                myAdornerLayer.Add(magHandleAdorner);
            }
            else
            {
                TracePrint.NullException(nameof(myAdornerLayer));
            }

            // Initialize lens at default TopRight position (no animation on first placement)
            lensTranslate = new TranslateTransform(Offset.X, Offset.Y);
            RenderTransform = lensTranslate;
            Direction = OffsetLensDirection.TopRight;
        }
        #endregion

        #region Private Methods (internal use)
        // Swing the lens to the new direction along the shortest circular arc around the cursor.
        //
        // All four positions lie on a circle of radius r = |Offset|*√2 at angles:
        //   TopRight = −45°,  TopLeft = −135°,  BottomRight = +45°,  BottomLeft = +135°
        // Animating the angle DP (instead of X and Y separately) keeps r constant throughout,
        // so the lens sweeps in an arc exactly like the MagnifyingGlass rotates around its handle.
        public void SetDirection(OffsetLensDirection direction)
        {
            // Loaded hasn't fired yet — transforms aren't initialised; the initial TopRight
            // position will be set correctly once Loaded runs.
            if (lensTranslate == null || adornerTranslate == null)
                return;

            double targetAngle = direction switch
            {
                OffsetLensDirection.TopLeft    => -135.0,
                OffsetLensDirection.BottomLeft =>  135.0,
                OffsetLensDirection.BottomRight =>  45.0,
                _                              =>  -45.0   // TopRight
            };

            // Current angle from the live transform values (correct even mid-arc, because the
            // OnCurrentAngleChanged callback keeps lensTranslate in sync each frame).
            double currentAngle = Math.Atan2(lensTranslate.Y, lensTranslate.X) * (180.0 / Math.PI);

            // Shortest arc: normalise delta to (−180, +180]
            double delta = targetAngle - currentAngle;
            if (delta >  180.0) delta -= 360.0;
            if (delta < -180.0) delta += 360.0;

            Direction = direction;
            BeginAnimation(CurrentAngleProperty,
                new DoubleAnimation(currentAngle, currentAngle + delta,
                    new Duration(TimeSpan.FromMilliseconds(500))));
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
        #endregion
    }

    #region Class: Magnifier Handle and Crosshairs Adorner. Used internal to construct the magnifying glass appearance
    // Create an adorner for the magnifier that attaches a handle to it and draws crosshairs at its center
    internal class MagHandleAdorner : Adorner
    {
        internal MagHandleAdorner(UIElement adornedElement)
         : base(adornedElement)
        {
        }

        // Called by the layout system as part of each rendering pass, and explicitly via
        // InvalidateVisual() on every animation frame while the lens is swinging.
        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect adornedElementRect = new(AdornedElement.DesiredSize);
            double radius = adornedElementRect.Width / 2;   // 125
            Point center = new(radius, radius);

            // Crosshairs at lens center (unchanged)
            const int crosshairHalf = 75;
            Pen crosshairPen = new(new SolidColorBrush(Colors.LightGray), .5);
            drawingContext.DrawLine(crosshairPen,
                center with { X = center.X - crosshairHalf },
                center with { X = center.X + crosshairHalf });
            drawingContext.DrawLine(crosshairPen,
                center with { Y = center.Y - crosshairHalf },
                center with { Y = center.Y + crosshairHalf });

            // Handle: bottom fixed at mouse cursor, top at the nearest point on the lens border.
            //
            // Coordinate derivation:
            //   The adorner's layout origin is the lens's layout position (cursor centre).
            //   adornerTranslate(tx, ty) is the post-render shift applied by WPF, so a point
            //   drawn at (x, y) appears on screen at (layoutOrigin + tx + x, layoutOrigin + ty + y).
            //   Cursor on screen == layoutOrigin, so:
            //       cursor_draw = (radius − tx,  radius − ty)
            //   The lens circle: centre (radius, radius), radius r.
            //   Border point closest to cursor = centre + radius * normalise(cursor_draw − centre)
            //                                  = centre + radius * normalise(−tx, −ty)
            TranslateTransform at = (AdornedElement as OffsetLens)?.AdornerTranslate;
            if (at == null) return;

            Point cursorDraw = new(radius - at.X, radius - at.Y);
            Vector toCursor = cursorDraw - center;  // == (−tx, −ty)
            if (toCursor.Length < 1.0) return;      // cursor at lens centre — degenerate, skip

            toCursor.Normalize();
            Point borderPoint = new(center.X + toCursor.X * radius,
                                    center.Y + toCursor.Y * radius);

            Pen handlePen = new(new SolidColorBrush(Colors.Green), 4);
            drawingContext.DrawLine(handlePen, cursorDraw, borderPoint);
        }
    }
    #endregion
}
