using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using YScreenshot.Overlay;

namespace YScreenshot.Capture
{
    public sealed class RegionCapture : ICaptureMode
    {
        public string Id => "region";
        public string DisplayName => "Rectangle";

        public Task<CaptureResult> CaptureAsync(CaptureContext ctx)
        {
            var virtualBounds = MonitorHelper.GetVirtualScreenBounds();
            var fullScreenBitmap = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(fullScreenBitmap))
            {
                g.CopyFromScreen(virtualBounds.Location, Point.Empty, virtualBounds.Size, CopyPixelOperation.SourceCopy);
            }

            // The overlay paints this same snapshot as its background, so the overlay
            // itself (dimming, selection border) never ends up baked into the capture.
            var selection = SelectionOverlayForm.ShowAndSelect(fullScreenBitmap, virtualBounds);

            if (selection == null || !GeometryUtil.IsValidSelection(selection.Value))
            {
                fullScreenBitmap.Dispose();
                return Task.FromResult<CaptureResult>(null);
            }

            var localRect = GeometryUtil.ToLocalRectangle(selection.Value, virtualBounds);

            using (fullScreenBitmap)
            {
                var cropped = fullScreenBitmap.Clone(localRect, fullScreenBitmap.PixelFormat);
                return Task.FromResult(new CaptureResult(cropped));
            }
        }
    }
}
