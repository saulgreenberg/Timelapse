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
            this.Reset();
        }
        #endregion

        #region Set / Get / Apply Scaliing and Translation
        public void Apply(ScaleTransform scale, TranslateTransform translation)
        {
            scale.ScaleX = this.Scale.X;
            scale.ScaleY = this.Scale.Y;
            translation.X = this.Translation.X;
            translation.Y = this.Translation.Y;
        }
        public Point GetScale()
        {
            return this.Scale;
        }

        public Point GetTranslation()
        {
            return this.Translation;
        }

        public void Reset()
        {
            this.Scale = new Point(1.0, 1.0);
            this.Translation = new Point(0.0, 0.0);
        }
        #endregion

        #region Set Bookmark
        public void Set(ScaleTransform scale, TranslateTransform translation)
        {
            // bookmarks use absolute positions and are therefore specific to a particular display size
            // A corollary of this is the scale transform's center need not be persisted as the bookmark's reset when the display size changes.
            this.Scale = new Point(scale.ScaleX, scale.ScaleY);
            this.Translation = new Point(translation.X, translation.Y);
        }

        public void Set(Point scale, Point translation)
        {
            // bookmarks use absolute positions and are therefore specific to a particular display size
            // A corollary of this is the scale transform's center need not be persisted as the bookmark's reset when the display size changes.
            this.Scale = scale;
            this.Translation = translation;
        }
        #endregion
    }
}
