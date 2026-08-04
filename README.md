# YScreenshot

[English](README.md) | [简体中文](README_zh-CN.md)

YScreenshot is a lightweight Windows screenshot utility with full-screen capture, rectangle capture, and user-controlled scrolling screenshots.

It is built with Windows Forms and targets .NET Framework 4.8. Captured images are copied directly to the clipboard, and capture data is not written to disk by default.

## Features

- **Full-screen capture** across the virtual desktop and multiple monitors.
- **Rectangle capture** with multi-monitor and mixed-DPI-aware geometry.
- **Scrolling screenshots** based on a fixed user-selected rectangle. A blue guide frame stays visible while the user manually scrolls, and the app samples the region every 200 ms before stitching the frames.
- **Draggable floating toolbar** that can collapse to the edge of the screen.
- **Rebindable global hotkeys** through the Settings dialog.
- **Direct clipboard output** for captured images.
- **Recent Captures** in the system tray for re-copying recent images.
- **Configurable feedback** with Toast, tray-balloon, or silent modes.
- **Optional Windows startup** from the Settings dialog.

## Scrolling screenshots

1. Start the Scrolling capture mode.
2. Drag a rectangle around the content to capture.
3. Keep the blue guide frame visible and place the pointer over the target content.
4. Scroll the content manually while the app samples the fixed rectangle.
5. Press the scrolling hotkey again to stop and copy the stitched image to the clipboard.

For reliable stitching, keep each scroll step smaller than the selected rectangle height so adjacent frames overlap. Animated pages, rotating ads, video, and rapidly changing content can reduce stitching accuracy.

## Requirements

- Windows 10/11 or a compatible Windows desktop environment.
- .NET Framework 4.8 runtime.
- .NET SDK 8.0 or later for restore, testing, and building.

The application targets `.NET Framework 4.8`. The .NET SDK is used to drive the build; a full Visual Studio installation is not required because the project restores .NET Framework 4.8 reference assemblies through NuGet.

## Build and test

From the repository root, run:

```powershell
dotnet restore YScreenshot.sln
dotnet build YScreenshot.sln -c Release
dotnet test tests/YScreenshot.Capture.Tests/YScreenshot.Capture.Tests.csproj -c Release
```

The Release executable is generated at:

```text
src/YScreenshot.App/bin/Release/net48/YScreenshot.exe
```

## Repository layout

```text
src/YScreenshot.App        Windows Forms application, toolbar, hotkeys, and tray menu
src/YScreenshot.Capture    Capture modes, clipboard output, and frame stitching
src/YScreenshot.Overlay    Rectangle selection and scrolling-capture guide overlays
tests/                      Automated tests for capture and stitching logic
```

## Documentation

- [Development plan](DEVELOPMENT_PLAN.md)
- [Development log](DEVELOPMENT_LOG.md)
- [Chinese project status](PROJECT_STATUS_ZH.md)
- [Chinese verification and test plan](VERIFICATION_TEST_PLAN_ZH.md)
- [Contributing guide](CONTRIBUTING.md)

## License

The project currently does not declare a separate license. Add a `LICENSE` file before distributing the repository as an open-source project.
