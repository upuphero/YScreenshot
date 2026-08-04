using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace YScreenshot.Capture
{
    public sealed class CaptureHistoryEntry
    {
        public string ModeDisplayName { get; }
        public DateTime CapturedAtUtc { get; }
        public Bitmap Image { get; }

        public CaptureHistoryEntry(string modeDisplayName, DateTime capturedAtUtc, Bitmap image)
        {
            ModeDisplayName = modeDisplayName;
            CapturedAtUtc = capturedAtUtc;
            Image = image;
        }
    }

    /// <summary>
    /// Clipboard-only ring buffer of the last N captures, so the user can re-copy an
    /// older shot without recapturing. Never touches disk -- entries just live in
    /// memory and are disposed once evicted or when the history itself is disposed.
    /// </summary>
    public sealed class CaptureHistory : IDisposable
    {
        private readonly int _capacity;
        private readonly LinkedList<CaptureHistoryEntry> _entries = new LinkedList<CaptureHistoryEntry>();

        public CaptureHistory(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        /// <summary>Newest first.</summary>
        public IReadOnlyList<CaptureHistoryEntry> Entries => _entries.ToList();

        /// <summary>
        /// Records a copy of <paramref name="image"/>; the caller keeps ownership of
        /// its own instance and may dispose it immediately after this call returns.
        /// </summary>
        public void Add(string modeDisplayName, Bitmap image)
        {
            var copy = new Bitmap(image);
            _entries.AddFirst(new CaptureHistoryEntry(modeDisplayName, DateTime.UtcNow, copy));

            while (_entries.Count > _capacity)
            {
                var evicted = _entries.Last.Value;
                _entries.RemoveLast();
                evicted.Image.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var entry in _entries)
            {
                entry.Image.Dispose();
            }

            _entries.Clear();
        }
    }
}
