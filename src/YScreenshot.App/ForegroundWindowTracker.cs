using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YScreenshot.App
{
    /// <summary>
    /// Tracks the last foreground window that isn't our own, by polling
    /// GetForegroundWindow. Scrolling capture needs to know which window to scroll, but
    /// by the time its button's Click handler runs, our own toolbar has already become
    /// the foreground window (clicking any of its buttons activates it) -- so the
    /// answer has to be remembered from just before that happened, not queried live.
    /// Polling (instead of a SetWinEventHook) trades a few hundred ms of staleness for
    /// simplicity; the user is expected to dwell on the target window far longer than
    /// that before triggering a capture.
    /// </summary>
    public sealed class ForegroundWindowTracker : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private readonly Timer _timer;
        private readonly Func<IntPtr, bool> _isOwnWindow;

        public IntPtr LastExternalForegroundWindow { get; private set; }

        public ForegroundWindowTracker(Func<IntPtr, bool> isOwnWindow, int pollIntervalMs = 250)
        {
            _isOwnWindow = isOwnWindow ?? (hwnd => false);

            Poll();

            _timer = new Timer { Interval = pollIntervalMs };
            _timer.Tick += (s, e) => Poll();
            _timer.Start();
        }

        private void Poll()
        {
            var current = GetForegroundWindow();
            if (current != IntPtr.Zero && !_isOwnWindow(current))
            {
                LastExternalForegroundWindow = current;
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
