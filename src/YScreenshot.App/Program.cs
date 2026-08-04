using System;
using System.Windows.Forms;
using YScreenshot.Capture;

namespace YScreenshot.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var registry = new CaptureModeRegistry();
            registry.Register(new FullScreenCapture());
            registry.Register(new RegionCapture());
            registry.Register(new ScrollingCapture());

            Application.Run(new ToolbarForm(registry));
        }
    }
}
