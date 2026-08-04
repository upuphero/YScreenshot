using System;
using System.Drawing;
using System.Windows.Forms;

namespace YScreenshot.Overlay
{
    /// <summary>
    /// Virtual-screen geometry shared by every capture mode and by the toolbar strip's
    /// own on-screen positioning. Correctness here depends on the app declaring
    /// PerMonitorV2 DPI awareness in its manifest -- without that, WinForms reports
    /// logical (scaled) coordinates instead of the real per-monitor pixels this class
    /// (and screen capture) needs.
    /// </summary>
    public static class MonitorHelper
    {
        public static Rectangle GetVirtualScreenBounds()
        {
            return SystemInformation.VirtualScreen;
        }

        /// <summary>
        /// Keeps a rectangle of the given size fully within the current virtual screen,
        /// e.g. for restoring a saved window position after a monitor was unplugged.
        /// </summary>
        public static Rectangle ClampToVirtualScreen(Point topLeft, Size size)
        {
            var virtualBounds = GetVirtualScreenBounds();

            int maxX = Math.Max(virtualBounds.Left, virtualBounds.Right - size.Width);
            int maxY = Math.Max(virtualBounds.Top, virtualBounds.Bottom - size.Height);

            int x = Math.Min(Math.Max(topLeft.X, virtualBounds.Left), maxX);
            int y = Math.Min(Math.Max(topLeft.Y, virtualBounds.Top), maxY);

            return new Rectangle(new Point(x, y), size);
        }
    }
}
