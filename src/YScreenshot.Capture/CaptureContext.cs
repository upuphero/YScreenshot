using System;
using System.Threading;
using System.Windows.Forms;

namespace YScreenshot.Capture
{
    /// <summary>
    /// Per-capture context handed to every <see cref="ICaptureMode"/>.
    /// </summary>
    public sealed class CaptureContext
    {
        public IWin32Window Owner { get; }

        /// <summary>
        /// Optional target window handle reserved for capture modes that need a specific
        /// native window. The current manual scrolling mode captures a user-selected
        /// screen rectangle instead and does not use this property.
        /// </summary>
        public IntPtr TargetWindowHandle { get; }

        /// <summary>
        /// A cooperative "stop now" signal (e.g. the user pressed the scrolling hotkey a
        /// second time). This is intentionally checked via <see cref="CancellationToken.IsCancellationRequested"/>
        /// rather than treated as .NET's usual throw-on-cancel: stopping mid-capture is a
        /// normal, successful way to end a scrolling capture, not an error, so it should
        /// still return whatever was stitched so far rather than throw.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        public CaptureContext(IWin32Window owner, IntPtr targetWindowHandle = default(IntPtr), CancellationToken cancellationToken = default(CancellationToken))
        {
            Owner = owner;
            TargetWindowHandle = targetWindowHandle;
            CancellationToken = cancellationToken;
        }
    }
}
