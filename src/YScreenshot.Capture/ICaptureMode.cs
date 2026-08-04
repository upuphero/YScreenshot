using System.Threading.Tasks;

namespace YScreenshot.Capture
{
    /// <summary>
    /// Contract every capture mode implements. The toolbar strip, hotkey table, and any
    /// future settings UI all read from a <see cref="CaptureModeRegistry"/> of these
    /// instead of hardcoding modes -- adding a new capture type later is one new class
    /// plus one registration call, no changes elsewhere.
    /// </summary>
    public interface ICaptureMode
    {
        string Id { get; }
        string DisplayName { get; }

        /// <summary>
        /// Runs the capture and returns the result, or null if the user cancelled
        /// (e.g. Esc during region selection, or a zero-size selection).
        /// </summary>
        Task<CaptureResult> CaptureAsync(CaptureContext ctx);
    }
}
