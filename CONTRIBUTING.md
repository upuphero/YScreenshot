# Contributing

## Adding a new capture mode

The extension point described in [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) is
[`CaptureModeRegistry`](src/YScreenshot.Capture/CaptureModeRegistry.cs). The toolbar
strip, the hotkey table, and the tray's Settings dialog all read from whatever's
registered there instead of hardcoding modes — adding a new capture type is:

1. **Implement [`ICaptureMode`](src/YScreenshot.Capture/ICaptureMode.cs)** in
   `YScreenshot.Capture` (or a new project, if the mode needs its own UI the way
   `RegionCapture` needs `YScreenshot.Overlay`):

   ```csharp
   public sealed class MyCapture : ICaptureMode
   {
       public string Id => "my-mode";        // stable identifier: hotkey table, settings, etc.
       public string DisplayName => "My Mode"; // shown in tray tooltips, history entries

       public Task<CaptureResult> CaptureAsync(CaptureContext ctx)
       {
           // Return null for "the user cancelled" / "nothing to capture" -- see
           // RegionCapture for the Esc-cancel convention, and ScrollingCapture for
           // the CaptureContext.CancellationToken "stop now" convention.
       }
   }
   ```

2. **Register it** in [`Program.cs`](src/YScreenshot.App/Program.cs):

   ```csharp
   registry.Register(new MyCapture());
   ```

   That's it — `ToolbarForm` adds a button for it automatically (see `ShortLabel` in
   [ToolbarForm.cs](src/YScreenshot.App/ToolbarForm.cs) if you want a short button
   label other than the first few characters of `DisplayName`), and
   `AppSettings`/`SettingsForm`/`HotkeyManager` already support giving any mode ID a
   rebindable hotkey (see `RegisterDefaultHotkeys` in `ToolbarForm.cs` — add one line
   there and one default hotkey field in `AppSettings.cs` if the new mode should ship
   with a default binding).

## Conventions worth keeping

- **Clipboard-only.** No capture mode should write a file. `ClipboardWriter` is the
  only sink; `CaptureHistory` is an in-memory ring buffer, not a disk cache.
- **`null` means cancelled, not failed.** Returning `null` from `CaptureAsync` is the
  normal way to signal "nothing to copy" (user pressed Esc, zero-size selection, no
  target window found). `ToolbarForm` treats it as a silent no-op. Don't throw for
  ordinary cancellation.
- **`CaptureContext.CancellationToken` is cooperative, not throw-based.** It means
  "finish now with whatever you have," not ".NET's usual abort-with-exception
  cancellation." Check `IsCancellationRequested`; don't call `ThrowIfCancellationRequested`.
- **The toolbar strip stays a strip.** It's deliberately not a titled window with
  menus — one button per registered mode plus Hide, nothing else. Anything that needs
  more UI (Settings, Recent Captures) belongs behind the tray icon's context menu
  instead, per `TrayIconManager`.
- **Keep new modes unit-testable where the logic is pure.** `GeometryUtil` and
  `FrameStitcher` hold the pure math extracted out of `SelectionOverlayForm` and
  `ScrollingCapture` respectively, specifically so it can be tested against synthetic
  data without a real window or screen. Follow that pattern for new modes with
  non-trivial geometry/pixel logic.
