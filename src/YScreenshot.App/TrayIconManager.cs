using System;
using System.Collections.Generic;
using System.Windows.Forms;
using YScreenshot.Capture;

namespace YScreenshot.App
{
    /// <summary>
    /// The tray icon: a fallback for when the strip itself is collapsed or somehow
    /// lost (Restore/Exit), plus the two things that don't belong on the thin strip
    /// itself -- Settings and Recent Captures -- since the strip is deliberately just
    /// capture buttons, no menus.
    /// </summary>
    public sealed class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _recentCapturesMenu;
        private readonly Func<IReadOnlyList<CaptureHistoryEntry>> _getHistoryEntries;

        public event Action SettingsRequested;
        public event Action RestoreRequested;
        public event Action ExitRequested;
        public event Action<CaptureHistoryEntry> HistoryEntrySelected;

        public TrayIconManager(Func<IReadOnlyList<CaptureHistoryEntry>> getHistoryEntries)
        {
            _getHistoryEntries = getHistoryEntries ?? (() => Array.Empty<CaptureHistoryEntry>());

            _recentCapturesMenu = new ToolStripMenuItem("Recent Captures");

            var menu = new ContextMenuStrip();
            menu.Items.Add(_recentCapturesMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings...", null, (s, e) => SettingsRequested?.Invoke());
            menu.Items.Add("Restore", null, (s, e) => RestoreRequested?.Invoke());
            menu.Items.Add("Exit", null, (s, e) => ExitRequested?.Invoke());
            menu.Opening += (s, e) => RebuildRecentCapturesMenu();

            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "YScreenshot",
                ContextMenuStrip = menu,
                Visible = true
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreRequested?.Invoke();
        }

        private void RebuildRecentCapturesMenu()
        {
            _recentCapturesMenu.DropDownItems.Clear();

            var entries = _getHistoryEntries();
            if (entries.Count == 0)
            {
                _recentCapturesMenu.DropDownItems.Add(new ToolStripMenuItem("(none yet)") { Enabled = false });
                return;
            }

            foreach (var entry in entries)
            {
                string label = $"{entry.CapturedAtUtc.ToLocalTime():HH:mm:ss} -- {entry.ModeDisplayName}";
                var item = new ToolStripMenuItem(label);
                item.Click += (s, e) => HistoryEntrySelected?.Invoke(entry);
                _recentCapturesMenu.DropDownItems.Add(item);
            }
        }

        public void ShowBalloon(string title, string text)
        {
            _notifyIcon.ShowBalloonTip(1500, title, text, ToolTipIcon.None);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
