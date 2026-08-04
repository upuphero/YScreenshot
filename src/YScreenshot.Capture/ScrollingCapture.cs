using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using YScreenshot.Overlay;

namespace YScreenshot.Capture
{
    /// <summary>
    /// Captures a user-selected screen rectangle at a fixed interval while the user
    /// manually scrolls the content underneath it, then stitches the frames vertically.
    /// The scrolling itself is deliberately left to the user so the mode works with
    /// browsers, document viewers, chats, and custom scroll containers alike.
    /// </summary>
    /// <remarks>
    /// Press the scrolling hotkey again to stop and finalize the capture while the
    /// toolbar remains hidden during periodic capture.
    /// Exact pixel-row overlap matching works best for static content; animated or
    /// rapidly changing content can make a seam impossible to identify reliably.
    /// </remarks>
    public sealed class ScrollingCapture : ICaptureMode
    {
        private const int FrameCaptureIntervalMs = 200;
        private const int MaxStitchedHeightPx = 20000;
        private const int MaxFrames = 600;

        public string Id => "scrolling";
        public string DisplayName => "Scrolling";

        public async Task<CaptureResult> CaptureAsync(CaptureContext ctx)
        {
            var virtualBounds = MonitorHelper.GetVirtualScreenBounds();
            Rectangle? selectedBounds;
            using (var backgroundSnapshot = CaptureScreenRegion(virtualBounds))
            {
                selectedBounds = SelectionOverlayForm.ShowAndSelect(backgroundSnapshot, virtualBounds);
            }

            if (selectedBounds == null || !GeometryUtil.IsValidSelection(selectedBounds.Value))
            {
                return null;
            }

            using (var guide = new CaptureGuideForm(selectedBounds.Value))
            {
                guide.Show();
                return await CaptureSelectedRegionAsync(selectedBounds.Value, ctx);
            }
        }

        private static async Task<CaptureResult> CaptureSelectedRegionAsync(
            Rectangle selectedBounds,
            CaptureContext ctx)
        {
            Bitmap previousFrame = null;
            Bitmap accumulated = null;

            try
            {
                previousFrame = CaptureScreenRegion(selectedBounds);
                accumulated = new Bitmap(previousFrame);

                for (int frameIndex = 1;
                     frameIndex < MaxFrames && accumulated.Height < MaxStitchedHeightPx;
                     frameIndex++)
                {
                    await Task.Delay(FrameCaptureIntervalMs);

                    if (ctx.CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    Bitmap nextFrame = null;
                    try
                    {
                        nextFrame = CaptureScreenRegion(selectedBounds);
                        int maxOverlap = Math.Min(previousFrame.Height, nextFrame.Height);
                        int overlap = FrameStitcher.FindVerticalOverlap(previousFrame, nextFrame, maxOverlap);

                        // A paused manual scroll produces identical frames. Keep waiting
                        // instead of treating the pause as end-of-content.
                        if (overlap >= nextFrame.Height)
                        {
                            continue;
                        }

                        // Appending a frame with no reliable overlap duplicates an entire
                        // viewport and is worse than dropping one overly-large scroll step.
                        // Keep the last accepted frame as the seam anchor so a temporary
                        // window switch or a fast wheel movement cannot poison the result.
                        if (overlap <= 0)
                        {
                            continue;
                        }

                        var stitched = FrameStitcher.AppendBelow(accumulated, nextFrame, overlap);
                        accumulated.Dispose();
                        accumulated = stitched;

                        previousFrame.Dispose();
                        previousFrame = nextFrame;
                        nextFrame = null;
                    }
                    finally
                    {
                        nextFrame?.Dispose();
                    }
                }

                var completedImage = accumulated;
                accumulated = null;
                return new CaptureResult(completedImage);
            }
            finally
            {
                previousFrame?.Dispose();
                accumulated?.Dispose();
            }
        }

        private static Bitmap CaptureScreenRegion(Rectangle screenBounds)
        {
            var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    screenBounds.Location,
                    Point.Empty,
                    screenBounds.Size,
                    CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }
    }
}
