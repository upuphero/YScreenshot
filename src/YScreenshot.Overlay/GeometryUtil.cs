using System.Drawing;

namespace YScreenshot.Overlay
{
    /// <summary>
    /// Pure rectangle math shared between the selection overlay (live drag rendering)
    /// and region capture (cropping). Kept dependency-free from any Form so it is
    /// directly unit-testable.
    /// </summary>
    public static class GeometryUtil
    {
        /// <summary>
        /// Normalizes two arbitrary drag points (start may be after, above, right of end)
        /// into a well-formed rectangle with a non-negative width/height.
        /// </summary>
        public static Rectangle NormalizeRectangle(Point a, Point b)
        {
            int x = a.X < b.X ? a.X : b.X;
            int y = a.Y < b.Y ? a.Y : b.Y;
            int width = System.Math.Abs(b.X - a.X);
            int height = System.Math.Abs(b.Y - a.Y);

            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Translates a rectangle expressed in virtual-screen coordinates (which can be
        /// negative when a monitor sits left of or above the primary) into coordinates
        /// local to a bitmap captured at <paramref name="containerBounds"/>.
        /// </summary>
        public static Rectangle ToLocalRectangle(Rectangle selection, Rectangle containerBounds)
        {
            return new Rectangle(
                selection.X - containerBounds.X,
                selection.Y - containerBounds.Y,
                selection.Width,
                selection.Height);
        }

        public static bool IsValidSelection(Rectangle selection)
        {
            return selection.Width > 0 && selection.Height > 0;
        }
    }
}
