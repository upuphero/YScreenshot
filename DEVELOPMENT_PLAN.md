# YScreenshot — Development Plan

## 1. Goal

A lightweight Windows screenshot utility. Requirements:

- Small binary, minimal memory/CPU footprint, near-instant startup.
- Trigger via global hotkey or button click.
- Capture modes: full screen, freeform rectangle (mouse-drawn), scrolling screenshot.
- Result always goes to the **clipboard only** — never auto-saved to disk.
- Architecture must allow new capture modes to be added later without rework.
- UI is not a full window: a single thin rectangular strip (a small always-on-top bar holding one button per capture mode plus a hide toggle), not a titled app window with menus/panels.
- The strip can be manually collapsed/hidden via its own hide button, and must auto-hide itself immediately before every capture so it never appears in the resulting screenshot, then reappear right after.

## 2. Tech Stack

**C# / .NET Framework 4.8, WinForms.**

Rationale:

- .NET Framework 4.8 ships pre-installed on every current Windows 10/11 machine — nothing to bundle, so the exe is genuinely small (tens of KB to a few MB), not a repackaged runtime.
- Because the runtime is already resident/warmed by the OS, cold start is effectively native-speed — no self-contained-deployment extraction overhead like modern .NET publish modes carry.
- WinForms has lower overhead than WPF (no DirectX-based rendering pipeline) — the right weight class for a background tray tool — and gives the same full Win32 interop (P/Invoke for `RegisterHotKey`, `BitBlt`/`CopyFromScreen`, DPI APIs) needed here.
- Rejected alternatives: .NET 8/.NET Core requires either bundling the runtime (self-contained, 15–35 MB even trimmed) or depending on a runtime most Windows machines don't have preinstalled — worse on both size and "just works" grounds for this project. Raw C++/Win32 is marginally smaller/faster still but multiplies dev time for the selection overlay and image stitching. Rust is viable but has weaker Windows UI tooling and a steeper ramp-up. WPF was considered but rejected in favor of WinForms for the lower baseline overhead.
- Known trade-off: .NET Framework is feature-frozen (security/servicing updates only, no new language/runtime features). Acceptable for a small, scope-limited tool; revisit only if the project's ambitions grow well beyond a screenshot utility.

No installer initially — ship as a single portable `.exe`. Add an installer later only if needed.

## 3. Project Structure

```
YScreenshot/
  src/
    YScreenshot.App/            # WinForms app, entry point, toolbar strip, hotkey registration
      Program.cs                # STA entry point, main form is the toolbar strip itself
      ToolbarForm.cs            # the thin rectangular strip: one button per capture mode + hide toggle
      TrayIconManager.cs        # minimal NotifyIcon (Restore / Exit only — not the primary UI)
      HotkeyManager.cs          # hidden message-only window, RegisterHotKey / WM_HOTKEY pump
      AppSettings.cs            # hotkey bindings, strip position, persisted to %AppData%\YScreenshot\settings.json
    YScreenshot.Capture/        # Core capture engine, no UI dependencies where possible
      ICaptureMode.cs           # interface every capture mode implements
      FullScreenCapture.cs
      RegionCapture.cs
      ScrollingCapture.cs
      CaptureResult.cs
      ClipboardWriter.cs
    YScreenshot.Overlay/        # Transparent selection overlay window + rectangle UI
      SelectionOverlayForm.cs   # borderless topmost Form, TransparencyKey-based overlay
      MonitorHelper.cs          # multi-monitor + per-monitor DPI geometry
  tests/
    YScreenshot.Capture.Tests/  # unit tests for geometry, stitching, capture-mode registry
  YScreenshot.sln
  DEVELOPMENT_PLAN.md
```

Key design rule: every capture mode implements a common interface:

```csharp
public interface ICaptureMode
{
    string Id { get; }              // e.g. "fullscreen", "region", "scrolling"
    string DisplayName { get; }
    Task<CaptureResult?> CaptureAsync(CaptureContext ctx);
}
```

A `CaptureModeRegistry` holds all registered modes; the tray menu, hotkey table, and future settings UI all read from this registry instead of hardcoding modes. This is what makes adding a new capture type later a matter of writing one new class and registering it — no changes elsewhere.

## 4. Core Technical Approaches

- **Toolbar strip UI**: `ToolbarForm` is a single small, borderless, `TopMost` WinForms `Form` shaped as a thin horizontal (or vertical) rectangle — just a row of icon buttons (Full Screen, Rectangle, Scrolling, Hide) with no title bar, menu, or resizable frame. `FormBorderStyle = None`, sized to fit its buttons (e.g. ~220x36 px), draggable via a small grip area or by holding the strip itself (`WM_NCHITTEST` trick to let clicking-and-dragging the background move the form). Position persisted to settings so it reopens where the user left it.
- **Manual hide**: the strip's own "hide" button collapses it down to a small edge tab (e.g. a 10px sliver docked to the nearest screen edge) rather than closing the app; clicking the tab restores the full strip. This is separate from the tray icon, which exists only as a fallback to fully restore or exit the app.
- **Auto-hide during capture**: every `ICaptureMode.CaptureAsync` call is wrapped by the app so the strip hides itself first (`Form.Hide()`), then awaits one render frame (`Task.Delay` of ~30-50 ms, enough for DWM to composite the screen without the strip) before invoking the actual screen capture, then restores itself (`Form.Show()`) once the capture (or region-selection overlay) completes. This guarantees the strip never appears in a full-screen or rectangle capture, and is skipped/re-applied on each frame for scrolling capture.
- **Global hotkeys**: `RegisterHotKey` (user32.dll) on a hidden message-only `NativeWindow`/`Form`, listening for `WM_HOTKEY`. Default bindings: `PrintScreen` = full screen, `Ctrl+Shift+A` = region, `Ctrl+Shift+S` = scrolling. All rebindable via settings.json (UI for rebinding can come in a later phase).
- **Full screen capture**: `Graphics.CopyFromScreen` across the full virtual screen bounds (`SystemInformation.VirtualScreen`) to correctly handle multi-monitor setups; capture at native pixel resolution per monitor DPI.
- **Rectangle/region capture**: Show a borderless, topmost `SelectionOverlayForm` (using `TransparencyKey` or a low-alpha `Opacity` layer) spanning the virtual screen. Track mouse down/drag/up to draw the selection rectangle with a live dimension label. On mouse up, crop the pre-captured full virtual-screen bitmap to the selected rect and discard the overlay. Esc cancels.
- **Scrolling capture**: Phase 2 approach — let the user drag a rectangle around the content, then capture that fixed screen region at a regular interval while the user manually scrolls. Detect overlap between consecutive frames (pixel-row correlation) → stitch vertically → repeat until the user stops with the repeat hotkey or safety limits are reached. This avoids depending on application-specific simulated scroll messages and works with custom scroll containers.
- **Clipboard only**: `Clipboard.SetImage(Image)` (STA thread required — mark `Main` with `[STAThread]`, standard for WinForms). No file dialogs, no temp files, no auto-save path. Explicitly do not add a "save as" feature unless requested later.
- **DPI awareness**: declare per-monitor-V2 DPI awareness in `app.manifest` (`<dpiAwareness>PerMonitorV2</dpiAwareness>`) since .NET Framework WinForms doesn't default to it — required for correct rectangle geometry on mixed-DPI multi-monitor setups.
- **Feedback**: brief non-blocking toast/flash near the strip (or a tray balloon if hidden) confirming "Copied to clipboard," shown only after the strip has restored so it doesn't interfere with the auto-hide-during-capture step.
- **Idle footprint**: no title-barred app window ever; the only visible UI at idle is the small strip (or its collapsed edge tab), plus an optional tray icon for Exit/Restore. Keeps idle memory/CPU near zero regardless of whether the strip is shown or collapsed.

## 5. Phased Delivery

### Phase 1 — Core MVP
Toolbar strip UI, full-screen capture, rectangle capture, clipboard-only output, hotkeys, auto-hide-during-capture, manual hide/collapse, multi-monitor + DPI correctness.

Steps:
1. Project scaffold — solution/projects created, empty `ToolbarForm` shows and closes cleanly.
2. Toolbar strip UI — strip shape, buttons, dragging, position persistence.
3. Hide/collapse — hide button collapses strip to edge tab, tab restores it.
4. Full-screen capture — `FullScreenCapture` + clipboard write, wired to its button and `PrintScreen`.
5. Rectangle capture — `SelectionOverlayForm` + drag-to-select + clipboard write, wired to its button and `Ctrl+Shift+A`.
6. Auto-hide on capture — wraps both capture calls with hide-before/show-after so the strip never appears in output.
7. Multi-monitor/DPI — per-monitor-V2 manifest, virtual-screen math, `MonitorHelper`, verified on mixed-DPI setups.

Deliverables:
- `YScreenshot.App` opens as the strip (Full Screen / Rectangle / Scrolling-stub / Hide buttons), remembers its last screen position.
- Strip's Hide button collapses it to an edge tab; clicking the tab restores it. Tray icon offers Restore/Exit as a fallback.
- Clicking Full Screen (or `PrintScreen`) → strip auto-hides → captures full virtual screen → clipboard → strip reappears.
- Clicking Rectangle (or `Ctrl+Shift+A`) → strip auto-hides → overlay appears → user drags rectangle → clipboard on release → strip reappears; Esc cancels and still restores the strip.
- Manual test matrix: single monitor, dual monitor (different DPI), dragging selection across monitor boundary, confirming the strip itself never appears in captured output.

Exit criteria: both modes reliably produce a correct clipboard image within ~100ms of trigger (auto-hide delay included), app idle RAM under ~40MB, strip never visible in its own screenshots.

### Phase 2 — Scrolling Screenshot
Add `ScrollingCapture` implementing `ICaptureMode`. User triggers via hotkey/tray, selects a fixed screen rectangle, manually scrolls the content, and the tool periodically captures and stitches that rectangle until the user stops it with the repeat hotkey. The result is copied to the clipboard.

Steps:
1. Scroll capture core — rectangle selection plus periodic capture of a fixed screen region while the user controls scrolling.
2. Scroll stitching — frame-overlap detection and vertical stitching algorithm, with unit tests against synthetic bitmaps.
3. Scroll stop conditions — manual stop (repeat-hotkey), max-height cap, and a frame-count safety cap.

Deliverables:
- Works on standard scrollable windows/browsers and custom scroll containers where the user can scroll manually.
- Handles pauses while the user scrolls and a manual-stop control.
- Unit tests for the frame-overlap/stitching algorithm using synthetic bitmaps.

Exit criteria: stitched image visually correct on at least a browser window and a long chat/document window, no duplicate or missing rows at seams.

### Phase 3 — Extensibility & Polish
Steps:
1. Settings UI — hotkey rebinding, startup-with-Windows toggle, feedback style choice.
2. Capture-mode registry docs — finalize `CaptureModeRegistry` as the extension point, write `CONTRIBUTING.md`.
3. Capture history (optional) — clipboard-only ring buffer of last N captures.
4. Packaging pass — final Release build verification against size/cold-start targets.

## 6. Non-Functional Targets

- Cold start to tray-ready: < 300 ms.
- Hotkey press to clipboard-ready for full screen/rectangle: < 150 ms.
- Idle memory: < 40 MB.
- Published binary: target < 5 MB (no bundled runtime — .NET Framework 4.8 is already present on the target machine).
- No disk writes at any point in normal operation (verify with a file-system watcher during manual test pass).

## 7. Verification Plan

- Unit tests (`YScreenshot.Capture.Tests`) for: rectangle geometry/cropping math, multi-monitor coordinate translation, scrolling-capture stitch/overlap algorithm.
- Manual test pass per phase against the exit criteria above.
- Process Monitor (ProcMon) spot-check during Phase 1 sign-off to confirm zero unexpected disk writes.
- Measure cold-start time and idle RAM (Task Manager / `dotnet-trace`) against targets before calling a phase done.

## 8. Build & Packaging

Standard Release build against .NET Framework 4.8, x64 (or AnyCPU):

```
msbuild YScreenshot.sln -p:Configuration=Release -p:Platform=x64
```

or via `dotnet build`/Visual Studio's Release publish for a WinForms .NET Framework project. No self-contained/trimming step is needed — the framework is already on the machine, so the output `YScreenshot.exe` (plus small satellite files) is the whole deliverable.

Ship the resulting `.exe` directly — no installer for v1. Revisit installer/auto-update only if distribution needs grow.
