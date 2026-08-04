using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YScreenshot.App
{
    [Flags]
    public enum HotkeyModifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }

    /// <summary>
    /// Hidden message-only window that registers global hotkeys via RegisterHotKey and
    /// dispatches WM_HOTKEY to subscribers by the name each hotkey was registered under.
    /// </summary>
    public sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HWND_MESSAGE = -3;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Dictionary<int, string> _idToName = new Dictionary<int, string>();
        private int _nextId = 1;

        public event Action<string> HotkeyPressed;

        public HotkeyManager()
        {
            var cp = new CreateParams
            {
                Parent = new IntPtr(HWND_MESSAGE)
            };
            CreateHandle(cp);
        }

        public bool Register(string name, Keys key, HotkeyModifiers modifiers)
        {
            int id = _nextId++;
            if (!RegisterHotKey(Handle, id, (uint)modifiers, (uint)key))
            {
                return false;
            }

            _idToName[id] = name;
            return true;
        }

        public bool RegisterFromSpec(string name, string spec)
        {
            if (!TryParse(spec, out var modifiers, out var key))
            {
                return false;
            }

            return Register(name, key, modifiers);
        }

        /// <summary>
        /// Parses specs like "Ctrl+Shift+A" or "PrintScreen" into modifiers + a Keys value.
        /// </summary>
        public static bool TryParse(string spec, out HotkeyModifiers modifiers, out Keys key)
        {
            modifiers = HotkeyModifiers.None;
            key = Keys.None;

            if (string.IsNullOrWhiteSpace(spec))
            {
                return false;
            }

            var parts = spec.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.Length == 0)
                {
                    return false;
                }

                if (i == parts.Length - 1)
                {
                    if (!Enum.TryParse(part, true, out key))
                    {
                        return false;
                    }
                }
                else
                {
                    switch (part.ToLowerInvariant())
                    {
                        case "ctrl":
                        case "control":
                            modifiers |= HotkeyModifiers.Control;
                            break;
                        case "alt":
                            modifiers |= HotkeyModifiers.Alt;
                            break;
                        case "shift":
                            modifiers |= HotkeyModifiers.Shift;
                            break;
                        case "win":
                        case "windows":
                            modifiers |= HotkeyModifiers.Win;
                            break;
                        default:
                            return false;
                    }
                }
            }

            return key != Keys.None;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && _idToName.TryGetValue(m.WParam.ToInt32(), out var name))
            {
                HotkeyPressed?.Invoke(name);
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// Unregisters every currently-registered hotkey without destroying the hidden
        /// window, so it can be reused afterward (e.g. re-registering fresh bindings
        /// after the Settings dialog closes).
        /// </summary>
        public void UnregisterAll()
        {
            foreach (var id in _idToName.Keys)
            {
                UnregisterHotKey(Handle, id);
            }

            _idToName.Clear();
        }

        public void Dispose()
        {
            UnregisterAll();

            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }
}
