using System;
using System.Drawing;

namespace YScreenshot.Capture
{
    public sealed class CaptureResult
    {
        public Bitmap Image { get; }

        public CaptureResult(Bitmap image)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
        }
    }
}
