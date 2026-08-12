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

        // Every layout constant above is expressed at 96 DPI (100% scaling) and is
        // multiplied by the current monitor's scale factor at runtime, so the strip
        // renders at the right physical size on whichever monitor it currently sits on.
        private const int BaseDpi = 96;

        private static readonly Color ToolbarBackColor = Color.FromArgb(255, 255, 255);
        private static readonly Color ButtonHoverColor = Color.FromArgb(245, 246, 250);
        private static readonly Color ButtonPressedColor = Color.FromArgb(232, 234, 240);
        private static readonly Color ButtonForeColor = Color.FromArgb(35, 35, 42);
        private static readonly Color SeparatorColor = Color.FromArgb(224, 225, 231);

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_DPICHANGED = 0x02E0;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly CaptureModeRegistry _registry;
        private readonly AppSettings _settings;
        private readonly HotkeyManager _hotkeyManager;
        private readonly TrayIconManager _trayIconManager;
        private readonly CaptureHistory _history = new CaptureHistory(5);
        private readonly FlowLayoutPanel _buttonPanel;
        private Panel _dragHandle;
        private readonly ToolTip _toolTip = new ToolTip();

        private int _dpi = BaseDpi;
        private int _buttonCount;
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

            // The strip is entirely custom-drawn and manually sized, so drive all DPI
            // scaling ourselves (see ApplyScale) instead of letting WinForms font-based
            // auto-scaling bake in a single monitor's scale factor that never updates.
            AutoScaleMode = AutoScaleMode.None;

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

            _buttonCount = buttonCount;
            // A provisional size at 96 DPI; ApplyScale() re-sizes to the real monitor
            // DPI once the handle (and thus the true DPI) is known.
            Size = ComputeExpandedSize();

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

        private void PaintDragHandle(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(160, 161, 170)))
            {
                float dot = SF(3f);
                for (int row = 0; row < 3; row++)
                {
                    float y = SF(13 + row * 7);
                    e.Graphics.FillEllipse(brush, SF(5f), y, dot, dot);
                    e.Graphics.FillEllipse(brush, SF(12f), y, dot, dot);
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

            _dpi = GetCurrentDpi();
            ApplyScale();
            if (!_isCollapsed)
            {
                Size = _expandedSize;
            }

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

            int radius = S(14);
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

        // Scales a 96-DPI design value to the current monitor's DPI.
        private int S(int value) => (int)Math.Round(value * (_dpi / (double)BaseDpi));

        private float SF(float value) => value * (_dpi / (float)BaseDpi);

        private Size ComputeExpandedSize()
        {
            int width = S(StripPadding) * 2
                + S(DragHandleWidth) + S(DragHandleRightMargin)
                + _buttonCount * (S(ButtonWidth) + S(4))
                + S(SeparatorWidth) + S(SeparatorMargin) * 2;
            int height = S(ButtonHeight) + S(StripPadding) * 2;
            return new Size(width, height);
        }

        private int GetCurrentDpi()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    uint dpi = GetDpiForWindow(Handle);
                    if (dpi > 0)
                    {
                        return (int)dpi;
                    }
                }
                catch (EntryPointNotFoundException) { /* pre-Windows 10; fall back below. */ }
                catch (DllNotFoundException) { }
            }

            using (var g = CreateGraphics())
            {
                return (int)g.DpiX;
            }
        }

        /// <summary>
        /// Re-applies every scaled dimension (button/handle/separator sizes and margins,
        /// panel padding, per-button icon scale, and the cached expanded size) for the
        /// current <see cref="_dpi"/>. Safe to call repeatedly, e.g. on a DPI change.
        /// </summary>
        private void ApplyScale()
        {
            _buttonPanel.SuspendLayout();
            _buttonPanel.Padding = new Padding(S(StripPadding));

            foreach (Control control in _buttonPanel.Controls)
            {
                if (control == _dragHandle)
                {
                    control.Size = new Size(S(DragHandleWidth), S(ButtonHeight));
                    control.Margin = new Padding(0, 0, S(DragHandleRightMargin), 0);
                }
                else if (control is ToolbarIconButton button)
                {
                    button.Size = new Size(S(ButtonWidth), S(ButtonHeight));
                    button.Margin = new Padding(S(1), 0, S(1), 0);
                    button.Scale = _dpi / (float)BaseDpi;
                }
                else
                {
                    // The only remaining child is the separator panel.
                    control.Size = new Size(S(SeparatorWidth), S(26));
                    control.Margin = new Padding(S(SeparatorMargin), S(8), S(SeparatorMargin), S(8));
                }
            }

            _buttonPanel.ResumeLayout();
            _expandedSize = ComputeExpandedSize();
        }

        // Windows sends WM_DPICHANGED when the strip is dragged onto a monitor with a
        // different scaling setting. Without handling it, the strip keeps the previous
        // monitor's pixel size and looks oversized/misshapen on the new one.
        private void HandleDpiChanged(int newDpi, RECT suggested)
        {
            if (newDpi <= 0 || newDpi == _dpi)
            {
                return;
            }

            _dpi = newDpi;
            ApplyScale();

            if (_isCollapsed)
            {
                ApplyCollapsedLayout();
            }
            else
            {
                // Honor the OS-suggested top-left so the strip stays under the cursor as
                // it crosses the monitor boundary, but use our own content-driven size.
                _expandedLocation = new Point(suggested.Left, suggested.Top);
                Location = _expandedLocation;
                Size = _expandedSize;
            }

            UpdateRoundedRegion();
            Invalidate(true);
        }

        // Windows sends WM_DISPLAYCHANGE when the desktop resolution or monitor layout
        // changes without any DPI change (e.g. lowering a monitor's resolution in place).
        // A smaller desktop can leave the strip's remembered position partly or fully
        // off-screen, so re-clamp it back onto the virtual screen -- the same safeguard
        // OnLoad applies at startup, now applied live while the app is running too.
        private void HandleDisplayChange()
        {
            var clamped = MonitorHelper.ClampToVirtualScreen(_expandedLocation, _expandedSize);
            _expandedLocation = clamped.Location;

            if (_isCollapsed)
            {
                ApplyCollapsedLayout();
            }
            else
            {
                Location = _expandedLocation;
            }
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

            // A restored position may live on a monitor whose DPI differs from the one
            // the handle was first created on; re-apply so the strip matches its monitor.
            int currentDpi = GetCurrentDpi();
            if (currentDpi != _dpi)
            {
                _dpi = currentDpi;
                ApplyScale();
                Size = _expandedSize;
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
            if (m.Msg == WM_DPICHANGED)
            {
                int newDpi = (int)(m.WParam.ToInt64() & 0xFFFF);
                var suggested = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                HandleDpiChanged(newDpi, suggested);
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == WM_DISPLAYCHANGE)
            {
                HandleDisplayChange();
                return;
            }

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

            ApplyCollapsedLayout();
            _isCollapsed = true;
        }

        // Docks a thin tab against the work-area edge nearest the strip's expanded
        // position. Reads _expandedSize/_expandedLocation (never the current, possibly
        // already-collapsed, bounds) so it can also be re-run after a DPI change.
        private void ApplyCollapsedLayout()
        {
            var workArea = Screen.FromControl(this).WorkingArea;
            int thickness = S(CollapsedTabThickness);

            int distLeft = _expandedLocation.X - workArea.Left;
            int distRight = workArea.Right - (_expandedLocation.X + _expandedSize.Width);
            int distTop = _expandedLocation.Y - workArea.Top;
            int distBottom = workArea.Bottom - (_expandedLocation.Y + _expandedSize.Height);
            int min = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            _buttonPanel.Visible = false;

            if (min == distLeft)
            {
                Size = new Size(thickness, _expandedSize.Height);
                Location = new Point(workArea.Left, _expandedLocation.Y);
            }
            else if (min == distRight)
            {
                Size = new Size(thickness, _expandedSize.Height);
                Location = new Point(workArea.Right - thickness, _expandedLocation.Y);
            }
            else if (min == distTop)
            {
                Size = new Size(_expandedSize.Width, thickness);
                Location = new Point(_expandedLocation.X, workArea.Top);
            }
            else
            {
                Size = new Size(_expandedSize.Width, thickness);
                Location = new Point(_expandedLocation.X, workArea.Bottom - thickness);
            }
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

        /// <summary>
        /// Current monitor scale factor (1.0 == 96 DPI). Set by the toolbar so the
        /// glyph's stroke width and fixed offsets track the monitor DPI rather than
        /// staying pinned at their 96-DPI values.
        /// </summary>
        public float Scale { get; set; } = 1f;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float sc = Scale <= 0f ? 1f : Scale;
            float size = Math.Min(24f * sc, Math.Min(ClientSize.Width, ClientSize.Height) * 0.48f);
            float left = (ClientSize.Width - size) / 2f;
            float top = (ClientSize.Height - size) / 2f;
            float right = left + size;
            float bottom = top + size;
            float centerX = left + size / 2f;
            float centerY = top + size / 2f;

            using (var pen = new Pen(ForeColor, 2.1f * sc))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                switch (Icon)
                {
                    case ToolbarIconKind.FullScreen:
                        DrawCornerBox(e.Graphics, pen, left, top, right, bottom, 7f * sc);
                        break;
                    case ToolbarIconKind.Region:
                        e.Graphics.DrawRectangle(pen, left + 2f * sc, top + 2f * sc, size - 4f * sc, size - 4f * sc);
                        break;
                    case ToolbarIconKind.Scrolling:
                        e.Graphics.DrawLine(pen, centerX, top + 3f * sc, centerX, bottom - 3f * sc);
                        DrawArrow(e.Graphics, pen, centerX, top + 2f * sc, centerX, top + 7f * sc, 4f * sc);
                        DrawArrow(e.Graphics, pen, centerX, bottom - 2f * sc, centerX, bottom - 7f * sc, 4f * sc);
                        break;
                    case ToolbarIconKind.Hide:
                        e.Graphics.DrawLine(pen, left + 4f * sc, top + 4f * sc, right - 4f * sc, bottom - 4f * sc);
                        e.Graphics.DrawLine(pen, right - 4f * sc, top + 4f * sc, left + 4f * sc, bottom - 4f * sc);
                        break;
                    default:
                        using (var brush = new SolidBrush(ForeColor))
                        {
                            e.Graphics.FillEllipse(brush, centerX - 2f * sc, centerY - 2f * sc, 4f * sc, 4f * sc);
                        }
                        break;
                }
            }
        }

        private static void DrawCornerBox(Graphics graphics, Pen pen, float left, float top, float right, float bottom, float length)
        {
            graphics.DrawLine(pen, left, top + length, left, top);
            graphics.DrawLine(pen, left, top, left + length, top);
            graphics.DrawLine(pen, right - length, top, right, top);
            graphics.DrawLine(pen, right, top, right, top + length);
            graphics.DrawLine(pen, left, bottom - length, left, bottom);
            graphics.DrawLine(pen, left, bottom, left + length, bottom);
            graphics.DrawLine(pen, right - length, bottom, right, bottom);
            graphics.DrawLine(pen, right, bottom, right, bottom - length);
        }

        private static void DrawArrow(Graphics graphics, Pen pen, float tipX, float tipY, float shaftX, float shaftY, float head)
        {
            graphics.DrawLine(pen, tipX, tipY, shaftX, shaftY);
            float direction = tipY < shaftY ? 1f : -1f;
            graphics.DrawLine(pen, tipX, tipY, tipX - head, tipY + direction * head);
            graphics.DrawLine(pen, tipX, tipY, tipX + head, tipY + direction * head);
        }
    }
}
