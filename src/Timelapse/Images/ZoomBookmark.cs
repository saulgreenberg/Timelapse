using System.Windows;
using System.Windows.Media;

namespace Timelapse.Images
{
    // Stores, sets and retrives Zoom Bookmark data
    internal class ZoomBookmark
    {
        #region Public Properties
        public Point Scale { get; private set; }
        public Point Translation { get; private set; }
        #endregion

        #region Constructore
        public ZoomBookmark()
        {
            Reset();
        }
        #endregion

        #region Set / Get / Apply Scaliing and Translation
        public void Apply(ScaleTransform scale, TranslateTransform translation)
        {
            scale.ScaleX = Scale.X;
            scale.ScaleY = Scale.Y;
            translation.X = Translation.X;
            translation.Y = Translation.Y;
        }
        public Point GetScale()
        {
            return Scale;
        }

        public Point GetTranslation()
        {
            return Translation;
        }

        public void Reset()
        {
            Scale = new(1.0, 1.0);
            Translation = new(0.0, 0.0);
        }
        #endregion

        #region Set Bookmark
        public void Set(ScaleTransform scale, TranslateTransform translation)
        {
            // bookmarks use absolute positions and are therefore specific to a particular display size
            // A corollary of this is the scale transform's center need not be persisted as the bookmark's reset when the display size changes.
            Scale = new(scale.ScaleX, scale.ScaleY);
            Translation = new(translation.X, translation.Y);
        }

        public void Set(Point scale, Point translation)
        {
            // bookmarks use absolute positions and are therefore specific to a particular display size
            // A corollary of this is the scale transform's center need not be persisted as the bookmark's reset when the display size changes.
            Scale = scale;
            Translation = translation;
        }
        #endregion
    }
}
