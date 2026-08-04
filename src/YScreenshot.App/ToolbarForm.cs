using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using YScreenshot.Capture;
using YScreenshot.Overlay;

namespace YScreenshot.App
{
    /// <summary>
    /// The app's only real UI: a thin, borderless, always-on-top strip holding one
    /// button per registered capture mode plus a hide toggle. Not a titled window --
    /// no menu, no resize frame.
    /// </summary>
    public sealed class ToolbarForm : Form
    {
        private const int ButtonWidth = 50;
        private const int ButtonHeight = 48;
        private const int StripPadding = 8;
        private const int DragHandleWidth = 20;
        private const int DragHandleRightMargin = 6;
        private const int SeparatorWidth = 1;
        private const int SeparatorMargin = 4;
        private const int CollapsedTabThickness = 10;
        private const int CaptureHideDelayMs = 40;

        private static readonly Color ToolbarBackColor = Color.FromArgb(255, 255, 255);
        private static readonly Color ButtonHoverColor = Color.FromArgb(245, 246, 250);
        private static readonly Color ButtonPressedColor = Color.FromArgb(232, 234, 240);
        private static readonly Color ButtonForeColor = Color.FromArgb(35, 35, 42);
        private static readonly Color SeparatorColor = Color.FromArgb(224, 225, 231);

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly CaptureModeRegistry _registry;
        private readonly AppSettings _settings;
        private readonly HotkeyManager _hotkeyManager;
        private readonly TrayIconManager _trayIconManager;
        private readonly CaptureHistory _history = new CaptureHistory(5);
        private readonly FlowLayoutPanel _buttonPanel;
        private Panel _dragHandle;
        private readonly ToolTip _toolTip = new ToolTip();

        private bool _isCollapsed;
        private bool _captureInProgress;
        private string _activeCaptureModeId;
        private CancellationTokenSource _activeCaptureCts;
        private Size _expandedSize;
        private Point _expandedLocation;

        public ToolbarForm(CaptureModeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _settings = AppSettings.Load();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = ToolbarBackColor;
            ForeColor = Color.White;
            DoubleBuffered = true;

            _buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(StripPadding),
                BackColor = ToolbarBackColor
            };
            _buttonPanel.MouseDown += BeginWindowDrag;
            Controls.Add(_buttonPanel);

            AddDragHandle();

            int buttonCount = 0;
            foreach (var mode in _registry)
            {
                AddModeButton(mode);
                buttonCount++;
            }
            AddSeparator();
            AddHideButton();
            buttonCount++;

            int width = StripPadding * 2
                + DragHandleWidth + DragHandleRightMargin
                + buttonCount * (ButtonWidth + 4)
                + SeparatorWidth + SeparatorMargin * 2;
            Size = new Size(width, ButtonHeight + StripPadding * 2);

            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
            RegisterDefaultHotkeys();

            _trayIconManager = new TrayIconManager(() => _history.Entries);
            _trayIconManager.RestoreRequested += OnRestoreRequested;
            _trayIconManager.ExitRequested += () => Close();
            _trayIconManager.SettingsRequested += OnSettingsRequested;
            _trayIconManager.HistoryEntrySelected += OnHistoryEntrySelected;

        }

        private void AddDragHandle()
        {
            _dragHandle = new Panel
            {
                Size = new Size(DragHandleWidth, ButtonHeight),
                Margin = new Padding(0, 0, DragHandleRightMargin, 0),
                BackColor = ToolbarBackColor,
                Cursor = Cursors.SizeAll
            };
            _dragHandle.Paint += PaintDragHandle;
            _dragHandle.MouseDown += BeginWindowDrag;
            _toolTip.SetToolTip(_dragHandle, "拖动工具条");
            _buttonPanel.Controls.Add(_dragHandle);
        }

        private static void PaintDragHandle(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(160, 161, 170)))
            {
                for (int row = 0; row < 3; row++)
                {
                    float y = 13 + row * 7;
                    e.Graphics.FillEllipse(brush, 5, y, 3, 3);
                    e.Graphics.FillEllipse(brush, 12, y, 3, 3);
                }
            }
        }

        private void AddSeparator()
        {
            var separator = new Panel
            {
                Size = new Size(SeparatorWidth, 26),
                Margin = new Padding(SeparatorMargin, 8, SeparatorMargin, 8),
                BackColor = SeparatorColor
            };
            _buttonPanel.Controls.Add(separator);
        }

        private void BeginWindowDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _isCollapsed)
            {
                return;
            }

            // A dedicated drag handle avoids relying on a tiny empty gap between
            // child buttons and works reliably for a borderless WinForms window.
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            const int radius = 14;
            var path = new GraphicsPath();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(Width - radius * 2 - 1, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(Width - radius * 2 - 1, Height - radius * 2 - 1, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, Height - radius * 2 - 1, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();

            var oldRegion = Region;
            Region = new Region(path);
            oldRegion?.Dispose();
            path.Dispose();
        }

        private void AddModeButton(ICaptureMode mode)
        {
            var button = new ToolbarIconButton
            {
                Size = new Size(ButtonWidth, ButtonHeight),
                Margin = new Padding(1, 0, 1, 0),
                Icon = IconForMode(mode.Id),
                Tag = mode
            };
            StyleToolbarButton(button);
            button.AccessibleName = mode.DisplayName;
            _toolTip.SetToolTip(button, mode.DisplayName);
            button.Click += OnModeButtonClick;

            if (mode.Id == "scrolling")
            {
                _toolTip.SetToolTip(button, "先框选固定区域，然后用鼠标手动滚动；程序每 200 ms 采集一帧，再按滚动热键停止");
            }

            _buttonPanel.Controls.Add(button);
        }

        private void AddHideButton()
        {
            var button = new ToolbarIconButton
            {
                Size = new Size(ButtonWidth, ButtonHeight),
                Margin = new Padding(1, 0, 1, 0),
                Icon = ToolbarIconKind.Hide
            };
            StyleToolbarButton(button);
            button.AccessibleName = "Hide";
            _toolTip.SetToolTip(button, "隐藏工具条");
            button.Click += (s, e) => ToggleCollapse();
            _buttonPanel.Controls.Add(button);
        }

        private static void StyleToolbarButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = ToolbarBackColor;
            button.ForeColor = ButtonForeColor;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.AutoSize = false;
            button.Padding = new Padding(0);
            button.Text = string.Empty;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ButtonHoverColor;
            button.FlatAppearance.MouseDownBackColor = ButtonPressedColor;
        }

        private static ToolbarIconKind IconForMode(string id)
        {
            switch (id)
            {
                case "fullscreen": return ToolbarIconKind.FullScreen;
                case "region": return ToolbarIconKind.Region;
                case "scrolling": return ToolbarIconKind.Scrolling;
                default: return ToolbarIconKind.Generic;
            }
        }

        private void RegisterDefaultHotkeys()
        {
            _hotkeyManager.RegisterFromSpec("fullscreen", _settings.FullScreenHotkey);
            _hotkeyManager.RegisterFromSpec("region", _settings.RegionHotkey);
            _hotkeyManager.RegisterFromSpec("scrolling", _settings.ScrollingHotkey);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (_settings.HasStoredPosition)
            {
                var clamped = MonitorHelper.ClampToVirtualScreen(new Point(_settings.StripX, _settings.StripY), Size);
                Location = clamped.Location;
            }
            else
            {
                var workArea = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(workArea.Right - Width - 16, workArea.Top + 16);
            }

            _expandedLocation = Location;
            _expandedSize = Size;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _settings.StripX = _isCollapsed ? _expandedLocation.X : Location.X;
            _settings.StripY = _isCollapsed ? _expandedLocation.Y : Location.Y;
            _settings.Save();

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotkeyManager?.Dispose();
                _trayIconManager?.Dispose();
                _activeCaptureCts?.Dispose();
                _history?.Dispose();
                _toolTip?.Dispose();
            }

            base.Dispose(disposing);
        }

        // Lets dragging the strip's background (not its buttons) move the window,
        // without a real title bar. Only active while expanded -- collapsed, the whole
        // tab should register normal clicks so it can be restored.
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT && !_isCollapsed)
            {
                var screenPoint = new Point(m.LParam.ToInt32());
                var clientPoint = PointToClient(screenPoint);
                var directChild = GetChildAtPoint(clientPoint);
                var panelPoint = _buttonPanel.PointToClient(screenPoint);
                var panelChild = directChild == _buttonPanel
                    ? _buttonPanel.GetChildAtPoint(panelPoint)
                    : directChild;

                // The FlowLayoutPanel fills the form, so checking only the form's
                // direct child would always find the panel and would make the strip
                // look like it cannot be dragged. Treat the panel's gaps/padding as
                // the drag surface while leaving the actual buttons clickable.
                if (panelChild == null)
                {
                    m.Result = (IntPtr)HTCAPTION;
                }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_isCollapsed)
            {
                Expand();
            }
        }

        private void OnRestoreRequested()
        {
            if (_isCollapsed)
            {
                Expand();
            }

            Show();
            Activate();
        }

        private void ToggleCollapse()
        {
            if (_isCollapsed)
            {
                Expand();
            }
            else
            {
                Collapse();
            }
        }

        private void Collapse()
        {
            _expandedSize = Size;
            _expandedLocation = Location;

            var workArea = Screen.FromControl(this).WorkingArea;

            int distLeft = Location.X - workArea.Left;
            int distRight = workArea.Right - (Location.X + Width);
            int distTop = Location.Y - workArea.Top;
            int distBottom = workArea.Bottom - (Location.Y + Height);
            int min = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            _buttonPanel.Visible = false;

            if (min == distLeft)
            {
                Size = new Size(CollapsedTabThickness, _expandedSize.Height);
                Location = new Point(workArea.Left, _expandedLocation.Y);
            }
            else if (min == distRight)
            {
                Size = new Size(CollapsedTabThickness, _expandedSize.Height);
                Location = new Point(workArea.Right - CollapsedTabThickness, _expandedLocation.Y);
            }
            else if (min == distTop)
            {
                Size = new Size(_expandedSize.Width, CollapsedTabThickness);
                Location = new Point(_expandedLocation.X, workArea.Top);
            }
            else
            {
                Size = new Size(_expandedSize.Width, CollapsedTabThickness);
                Location = new Point(_expandedLocation.X, workArea.Bottom - CollapsedTabThickness);
            }

            _isCollapsed = true;
        }

        private void Expand()
        {
            _buttonPanel.Visible = true;
            Size = _expandedSize;
            Location = _expandedLocation;
            _isCollapsed = false;
        }

        private async void OnModeButtonClick(object sender, EventArgs e)
        {
            var mode = (ICaptureMode)((Button)sender).Tag;
            await TriggerCaptureAsync(mode);
        }

        private async void OnHotkeyPressed(string id)
        {
            if (_registry.TryGet(id, out var mode))
            {
                await TriggerCaptureAsync(mode);
            }
        }

        private async Task TriggerCaptureAsync(ICaptureMode mode)
        {
            if (_captureInProgress)
            {
                // Re-triggering the mode that's already running is the manual-stop
                // signal -- e.g. pressing the scrolling hotkey/button again to end a
                // scroll capture early instead of waiting for end-of-content.
                if (mode.Id == _activeCaptureModeId)
                {
                    _activeCaptureCts?.Cancel();
                }

                return;
            }

            await RunCaptureAsync(mode);
        }

        private async Task RunCaptureAsync(ICaptureMode mode)
        {
            _captureInProgress = true;
            _activeCaptureModeId = mode.Id;
            _activeCaptureCts = new CancellationTokenSource();

            try
            {
                // Always hide before capturing, regardless of collapsed/expanded state --
                // even the collapsed edge tab is real pixels that could show up in a shot.
                bool wasVisible = Visible;
                if (wasVisible)
                {
                    Hide();
                    await Task.Delay(CaptureHideDelayMs);
                }

                CaptureResult result;
                try
                {
                    var ctx = new CaptureContext(this, cancellationToken: _activeCaptureCts.Token);
                    result = await mode.CaptureAsync(ctx);
                }
                finally
                {
                    if (wasVisible)
                    {
                        Show();
                    }
                }

                if (result?.Image != null)
                {
                    using (result.Image)
                    {
                        ClipboardWriter.SetImage(result.Image);
                        _history.Add(mode.DisplayName, result.Image);
                    }

                    ShowCaptureFeedback("Copied to clipboard");
                }
            }
            finally
            {
                _activeCaptureCts.Dispose();
                _activeCaptureCts = null;
                _activeCaptureModeId = null;
                _captureInProgress = false;
            }
        }

        private void OnHistoryEntrySelected(CaptureHistoryEntry entry)
        {
            // The history's own copy is never disposed here -- it stays alive in the
            // ring buffer until evicted or the app exits, so it can be re-selected again.
            ClipboardWriter.SetImage(entry.Image);
            ShowCaptureFeedback("Copied to clipboard");
        }

        private void OnSettingsRequested()
        {
            using (var dialog = new SettingsForm(_settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _hotkeyManager.UnregisterAll();
                    RegisterDefaultHotkeys();
                }
            }
        }

        private void ShowCaptureFeedback(string message)
        {
            switch (_settings.FeedbackStyle)
            {
                case "TrayBalloon":
                    _trayIconManager.ShowBalloon("YScreenshot", message);
                    break;
                case "None":
                    break;
                default:
                    ShowCaptureToast(message);
                    break;
            }
        }

        private void ShowCaptureToast(string message)
        {
            var toast = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                BackColor = Color.FromArgb(32, 32, 32),
                Size = new Size(160, 32)
            };

            var label = new Label
            {
                Text = message,
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(FontFamily.GenericSansSerif, 9f)
            };
            toast.Controls.Add(label);

            // Anchor to the strip's actual current bounds (collapsed tab or expanded
            // strip) rather than its remembered expanded position, so the toast always
            // appears next to whatever is really on screen right now.
            toast.Location = new Point(Location.X, Location.Y + Height + 4);

            toast.Show();

            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                toast.Close();
                toast.Dispose();
            };
            timer.Start();
        }
    }

    internal enum ToolbarIconKind
    {
        Generic,
        FullScreen,
        Region,
        Scrolling,
        Hide
    }

    internal sealed class ToolbarIconButton : Button
    {
        public ToolbarIconKind Icon { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float size = Math.Min(24f, Math.Min(ClientSize.Width, ClientSize.Height) * 0.48f);
            float left = (ClientSize.Width - size) / 2f;
            float top = (ClientSize.Height - size) / 2f;
            float right = left + size;
            float bottom = top + size;
            float centerX = left + size / 2f;
            float centerY = top + size / 2f;

            using (var pen = new Pen(ForeColor, 2.1f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                switch (Icon)
                {
                    case ToolbarIconKind.FullScreen:
                        DrawCornerBox(e.Graphics, pen, left, top, right, bottom);
                        break;
                    case ToolbarIconKind.Region:
                        e.Graphics.DrawRectangle(pen, left + 2, top + 2, size - 4, size - 4);
                        break;
                    case ToolbarIconKind.Scrolling:
                        e.Graphics.DrawLine(pen, centerX, top + 3, centerX, bottom - 3);
                        DrawArrow(e.Graphics, pen, centerX, top + 2, centerX, top + 7);
                        DrawArrow(e.Graphics, pen, centerX, bottom - 2, centerX, bottom - 7);
                        break;
                    case ToolbarIconKind.Hide:
                        e.Graphics.DrawLine(pen, left + 4, top + 4, right - 4, bottom - 4);
                        e.Graphics.DrawLine(pen, right - 4, top + 4, left + 4, bottom - 4);
                        break;
                    default:
                        using (var brush = new SolidBrush(ForeColor))
                        {
                            e.Graphics.FillEllipse(brush, centerX - 2, centerY - 2, 4, 4);
                        }
                        break;
                }
            }
        }

        private static void DrawCornerBox(Graphics graphics, Pen pen, float left, float top, float right, float bottom)
        {
            float length = 7f;
            graphics.DrawLine(pen, left, top + length, left, top);
            graphics.DrawLine(pen, left, top, left + length, top);
            graphics.DrawLine(pen, right - length, top, right, top);
            graphics.DrawLine(pen, right, top, right, top + length);
            graphics.DrawLine(pen, left, bottom - length, left, bottom);
            graphics.DrawLine(pen, left, bottom, left + length, bottom);
            graphics.DrawLine(pen, right - length, bottom, right, bottom);
            graphics.DrawLine(pen, right, bottom, right, bottom - length);
        }

        private static void DrawArrow(Graphics graphics, Pen pen, float tipX, float tipY, float shaftX, float shaftY)
        {
            graphics.DrawLine(pen, tipX, tipY, shaftX, shaftY);
            float direction = tipY < shaftY ? 1f : -1f;
            graphics.DrawLine(pen, tipX, tipY, tipX - 4f, tipY + direction * 4f);
            graphics.DrawLine(pen, tipX, tipY, tipX + 4f, tipY + direction * 4f);
        }
    }
}
