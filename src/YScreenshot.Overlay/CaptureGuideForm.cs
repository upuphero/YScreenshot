using System;
using System.Drawing;
using System.Windows.Forms;

namespace YScreenshot.Overlay
{
    /// <summary>
    /// A click-through guide frame that remains around a scrolling-capture region.
    /// The colored frame is drawn outside the selected rectangle, so it stays visible
    /// while the user scrolls but is not included in the captured pixels.
    /// </summary>
    public sealed class CaptureGuideForm : Form
    {
        private const int BorderThickness = 3;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private static readonly Color TransparentKeyColor = Color.Magenta;
        private static readonly Color GuideColor = Color.DeepSkyBlue;

        public CaptureGuideForm(Rectangle captureBounds)
        {
            var guideBounds = captureBounds;
            guideBounds.Inflate(BorderThickness, BorderThickness);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = guideBounds;
            TopMost = true;
            ShowInTaskbar = false;
            ShowIcon = false;
            BackColor = TransparentKeyColor;
            TransparencyKey = TransparentKeyColor;
            DoubleBuffered = true;

            // Keep only the four border strips as the actual window region. The
            // interior is not part of this window at all, so mouse wheel input reaches
            // the browser/document underneath instead of depending only on HTTRANSPARENT.
            var borderRegion = new Region(new Rectangle(0, 0, Width, BorderThickness));
            borderRegion.Union(new Rectangle(0, Height - BorderThickness, Width, BorderThickness));
            borderRegion.Union(new Rectangle(0, 0, BorderThickness, Height));
            borderRegion.Union(new Rectangle(Width - BorderThickness, 0, BorderThickness, Height));
            Region = borderRegion;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return parameters;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (var brush = new SolidBrush(GuideColor))
            {
                e.Graphics.FillRectangle(brush, 0, 0, Width, BorderThickness);
                e.Graphics.FillRectangle(brush, 0, Height - BorderThickness, Width, BorderThickness);
                e.Graphics.FillRectangle(brush, 0, 0, BorderThickness, Height);
                e.Graphics.FillRectangle(brush, Width - BorderThickness, 0, BorderThickness, Height);
            }
        }
    }
}
