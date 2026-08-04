using System;
using System.Collections;
using System.Collections.Generic;

namespace YScreenshot.Capture
{
    /// <summary>
    /// Holds every registered <see cref="ICaptureMode"/>. This is the extension point
    /// described in the development plan: the toolbar strip and hotkey table enumerate
    /// this registry instead of hardcoding modes.
    /// </summary>
    public sealed class CaptureModeRegistry : IEnumerable<ICaptureMode>
    {
        private readonly List<ICaptureMode> _modes = new List<ICaptureMode>();
        private readonly Dictionary<string, ICaptureMode> _byId =
            new Dictionary<string, ICaptureMode>(StringComparer.OrdinalIgnoreCase);

        public void Register(ICaptureMode mode)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));

            _modes.Add(mode);
            _byId[mode.Id] = mode;
        }

        public bool TryGet(string id, out ICaptureMode mode)
        {
            return _byId.TryGetValue(id ?? string.Empty, out mode);
        }

        public IEnumerator<ICaptureMode> GetEnumerator()
        {
            return _modes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
