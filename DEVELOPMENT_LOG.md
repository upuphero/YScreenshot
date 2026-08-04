# YScreenshot — Development Log

Running record of what's been implemented against [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md),
phase by phase. Each entry is the completion summary given at the time.

## Phase 1 — Core MVP (2026-08-03)

Phase 1 (Core MVP) is fully implemented per the plan. Summary of what's in place:

**Solution & projects** — YScreenshot.sln with 4 SDK-style net48 projects (App,
Capture, Overlay, Capture.Tests), using `Microsoft.NETFramework.ReferenceAssemblies` so
`dotnet build` works without the classic .NET Framework Developer Pack installed.

**Architecture** — ICaptureMode.cs + CaptureModeRegistry.cs give the extensible-mode
design the plan calls for; FullScreenCapture and RegionCapture are fully implemented,
ScrollingCapture is a registered stub (button present but disabled) for Phase 2.

**UI** — ToolbarForm.cs is the thin draggable strip with hide/collapse-to-edge-tab,
auto-hide-before-capture, toast feedback, and settings persistence; HotkeyManager.cs
wires `PrintScreen`/`Ctrl+Shift+A`; TrayIconManager.cs is the Restore/Exit fallback.

**Geometry** — SelectionOverlayForm.cs does drag-to-select by painting the
pre-captured snapshot as its own background (avoids the Opacity-dims-the-border
pitfall); MonitorHelper.cs/GeometryUtil.cs handle multi-monitor math; app.manifest
declares PerMonitorV2 DPI awareness.

**Tests** — GeometryUtilTests.cs covers rectangle crop/normalize math including
negative-origin (secondary-monitor) translation; CaptureModeRegistryTests.cs covers the
registry.

**Caveat**: this machine has no C# build toolchain at all (no MSBuild, no Visual
Studio, no .NET SDK), and per the user's choice the code was written without installing
one, so none of it has actually been compiled or run yet. README.md has the build/test
commands and the Phase 1 manual test matrix for whenever it gets built (on this machine
after installing the .NET SDK, or in Visual Studio elsewhere).

## Phase 2 — Scrolling Screenshot (2026-08-03)

Phase 2 (Scrolling Screenshot) is complete. Summary of what's new since Phase 1:

**Scrolling capture** — ScrollingCapture.cs replaces the stub: discovers the target
window's client bounds, simulates `WM_MOUSEWHEEL` to its deepest child window,
captures+stitches each frame, and stops on end-of-content (2 consecutive no-progress
frames), a manual stop, a ~20000px height cap, or a 200-iteration safety cap.

**Stitching algorithm** — FrameStitcher.cs is a pure, UI-free row-overlap detector +
vertical appender, with FrameStitcherTests.cs covering partial overlap, no-overlap,
full-duplicate (end-of-content), and a 3-frame scroll sequence that must reassemble
byte-for-byte into the original synthetic "document" — directly matching the plan's
"no duplicate or missing rows at seams" criterion.

**Target-window discovery** — ForegroundWindowTracker.cs polls `GetForegroundWindow()`
and remembers the last non-toolbar window, solving a real bug caught while designing
this: clicking the toolbar's own Scroll button activates the toolbar itself, so
querying the foreground window at that moment would return the toolbar, not the
intended target.

**Manual stop** — CaptureContext.cs gained `TargetWindowHandle` and a cooperative
`CancellationToken`; ToolbarForm.cs now treats re-triggering the same in-progress mode
as "stop now and finalize," rather than the previous Phase 1 behavior of silently
ignoring repeat triggers.

**Known trade-offs** (documented in README.md): wheel-message delivery and
exact-pixel-row overlap matching are best-effort — solid for ordinary browser pages and
static documents/chats (the plan's stated targets), less reliable against
custom-scroll-handling apps or highly dynamic content. No global `Esc` support for
stopping mid-scroll (would need a low-level keyboard hook); repeat-hotkey is the
supported manual stop, which the plan explicitly allows as an alternative to `Esc`.

**Caveat**: still uncompiled/untested on this machine, same reason as Phase 1.

## Interaction revision — manual rectangle-based scrolling (2026-08-03)

The scrolling interaction was revised after the initial Phase 2 implementation to
match the intended user workflow:

- Scrolling now begins with the same full-screen rectangle-selection experience as the
  Rectangle mode.
- After the selection closes, the selected screen region is captured every 200 ms while
  the user manually scrolls the content underneath it.
- Consecutive frames are stitched using the existing `FrameStitcher`; identical frames
  during a pause are ignored rather than treated as end-of-content.
- Pressing the scrolling hotkey again stops the periodic capture and copies
  the partial or complete stitched image to the clipboard.
- The toolbar now uses a white rounded floating strip with icon buttons, a visible dot
  drag handle, and gaps/padding that act as additional drag surfaces.
- A click-through blue guide frame now remains around the selected scrolling region;
  its border is drawn outside the capture rectangle so it does not enter the result.
- Frames with no reliable overlap are ignored instead of appended whole, preventing a
  missed seam from duplicating an entire viewport.

The old simulated-wheel and foreground-window tracking path is no longer used by the
scrolling mode. The remaining limitation is that the content must stay under the
selected screen rectangle while the user scrolls.

## Phase 3 — Extensibility & Polish (2026-08-03)

Phase 3 (Extensibility & Polish) is complete. Summary of what's new since Phase 2:

**Settings UI** — SettingsForm.cs is a normal titled dialog (unlike the toolbar strip,
Settings is an explicit, occasional action, so it doesn't need to stay a thin
borderless strip) reachable from a new "Settings..." tray menu item. It supports
hotkey rebinding (click a box, press a combination, validated against
`HotkeyManager.TryParse` before saving), a Start-with-Windows checkbox backed by
StartupManager.cs (reads/writes the per-user `HKCU\...\Run` registry value, no
elevation needed), and a capture-feedback choice (Toast / TrayBalloon / None).
HotkeyManager.cs gained `UnregisterAll()` so ToolbarForm can live-reload hotkeys after
Save without recreating its hidden message-only window.

**Capture-mode registry docs** — CONTRIBUTING.md documents `CaptureModeRegistry` as
the extension point: implement `ICaptureMode`, register it in `Program.cs`, and the
toolbar button, hotkey table, and Settings dialog all pick it up automatically. Also
records the conventions worth keeping (clipboard-only, `null` means cancelled not
failed, cooperative cancellation, keep pure logic unit-testable) so they don't erode
as new modes get added.

**Capture history (the plan's optional Phase 3 item)** — CaptureHistory.cs is an
in-memory ring buffer (last 5 captures, clipboard-only -- never written to disk),
exposed via a "Recent Captures" tray submenu that lists entries newest-first by
timestamp and mode name; clicking one re-copies it to the clipboard. Scoped
deliberately smaller than a thumbnail-preview submenu (text labels instead of
generated image thumbnails) to keep the risk/complexity down for something the plan
explicitly marked optional.

**Packaging pass** — Since this machine still has no build toolchain, the actual
Release build/size/cold-start/idle-RAM verification from the plan's non-functional
targets couldn't be run. README.md's new "Packaging pass" section documents exactly
what to check and where (binary size under the satellite DLLs, cold start, hotkey
latency, idle RAM, and a Process Monitor pass confirming the only file write is
`settings.json`) for whenever it gets built.

**Caveat**: still uncompiled/untested on this machine, same reason as Phases 1-2.

## Verification update — 2026-08-03

After installing .NET SDK 8.0.423, the solution was restored and built in Release:

- `dotnet restore YScreenshot.sln` completed successfully.
- `dotnet build YScreenshot.sln -c Release --no-restore` completed with 0 warnings and 0 errors.
- `dotnet test tests/YScreenshot.Capture.Tests/YScreenshot.Capture.Tests.csproj -c Release --no-restore` passed all 19 tests.
- Manual desktop interaction and non-functional packaging measurements remain to be run.
