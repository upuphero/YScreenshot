using System;
using System.Drawing;
using System.Windows.Forms;

namespace YScreenshot.Capture
{
    /// <summary>
    /// The only place capture output goes -- never a file. Uses eager clipboard
    /// rendering (copy: true) so the image survives even if the caller disposes its
    /// bitmap right after this call, or the app exits shortly after.
    /// </summary>
    public static class ClipboardWriter
    {
        public static void SetImage(Bitmap image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            var data = new DataObject();
            data.SetData(DataFormats.Bitmap, true, image);
            Clipboard.SetDataObject(data, true);
        }
    }
}
