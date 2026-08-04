using System;
using System.Drawing;
using System.Windows.Forms;

namespace YScreenshot.Overlay
{
    /// <summary>
    /// Borderless, topmost form spanning the virtual screen that lets the user drag out
    /// a rectangle. It paints the caller's pre-captured screen snapshot as its own
    /// background and dims everything outside the live selection -- this avoids real
    /// window transparency (TransparencyKey/Opacity would dim the selection border and
    /// text just as much as the backdrop, since Form.Opacity applies to the whole
    /// composited window).
    /// </summary>
    public sealed class SelectionOverlayForm : Form
    {
        private static readonly Color DimColor = Color.FromArgb(120, 0, 0, 0);
        private static readonly Color SelectionBorderColor = Color.DeepSkyBlue;

        private readonly Bitmap _backgroundSnapshot;
        private readonly Rectangle _virtualBounds;

        private Point _startPoint;
        private Point _currentPoint;
        private bool _isSelecting;

        public Rectangle? SelectedBounds { get; private set; }

        private SelectionOverlayForm(Bitmap backgroundSnapshot, Rectangle virtualBounds)
        {
            _backgroundSnapshot = backgroundSnapshot;
            _virtualBounds = virtualBounds;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualBounds;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.Black;
        }

        /// <summary>
        /// Shows the overlay modally and returns the selected rectangle in virtual-screen
        /// coordinates, or null if the user pressed Esc / made a zero-size selection.
        /// Does not take ownership of <paramref name="backgroundSnapshot"/>; the caller
        /// still owns and must dispose it.
        /// </summary>
        public static Rectangle? ShowAndSelect(Bitmap backgroundSnapshot, Rectangle virtualBounds)
        {
            using (var form = new SelectionOverlayForm(backgroundSnapshot, virtualBounds))
            {
                form.ShowDialog();
                return form.SelectedBounds;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _isSelecting = true;
            _startPoint = e.Location;
            _currentPoint = e.Location;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_isSelecting)
            {
                return;
            }

            _currentPoint = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!_isSelecting || e.Button != MouseButtons.Left)
            {
                return;
            }

            _isSelecting = false;

            var localSelection = GeometryUtil.NormalizeRectangle(_startPoint, _currentPoint);
            if (GeometryUtil.IsValidSelection(localSelection))
            {
                SelectedBounds = new Rectangle(
                    localSelection.X + _virtualBounds.X,
                    localSelection.Y + _virtualBounds.Y,
                    localSelection.Width,
                    localSelection.Height);
            }

            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape)
            {
                SelectedBounds = null;
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(_backgroundSnapshot, Point.Empty);

            using (var dimBrush = new SolidBrush(DimColor))
            {
                if (!_isSelecting)
                {
                    g.FillRectangle(dimBrush, ClientRectangle);
                    return;
                }

                var selection = GeometryUtil.NormalizeRectangle(_startPoint, _currentPoint);

                using (var region = new Region(ClientRectangle))
                {
                    region.Exclude(selection);
                    g.FillRegion(dimBrush, region);
                }

                using (var pen = new Pen(SelectionBorderColor, 2))
                {
                    g.DrawRectangle(pen, selection.X, selection.Y, selection.Width, selection.Height);
                }

                DrawDimensionLabel(g, selection);
            }
        }

        private static void DrawDimensionLabel(Graphics g, Rectangle selection)
        {
            string label = $"{selection.Width} x {selection.Height}";
            int labelX = selection.X + 4;
            int labelY = Math.Max(0, selection.Y - 20);

            using (var font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold))
            using (var shadowBrush = new SolidBrush(Color.Black))
            using (var textBrush = new SolidBrush(Color.White))
            {
                g.DrawString(label, font, shadowBrush, labelX + 1, labelY + 1);
                g.DrawString(label, font, textBrush, labelX, labelY);
            }
        }
    }
}
