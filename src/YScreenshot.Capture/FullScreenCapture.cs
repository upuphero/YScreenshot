using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using YScreenshot.Overlay;

namespace YScreenshot.Capture
{
    public sealed class FullScreenCapture : ICaptureMode
    {
        public string Id => "fullscreen";
        public string DisplayName => "Full Screen";

        public Task<CaptureResult> CaptureAsync(CaptureContext ctx)
        {
            var bounds = MonitorHelper.GetVirtualScreenBounds();
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            return Task.FromResult(new CaptureResult(bitmap));
        }
    }
}
