# YScreenshot

轻量、快速、面向 Windows 的截图工具，支持全屏截图、矩形截图和手动滚动长截图。

A lightweight Windows screenshot utility with full-screen capture, rectangle capture,
and user-controlled scrolling screenshots.

## 中文介绍

YScreenshot 是一个基于 Windows Forms 和 .NET Framework 4.8 的桌面截图工具。它常驻在桌面顶部工具条中，截图结果直接复制到剪贴板，不强制写入磁盘。

### 主要功能

- 全屏截图：捕获整个虚拟桌面，支持多显示器。
- 矩形截图：鼠标拖动选择区域，支持跨显示器和混合 DPI 环境。
- 滚动长截图：先选择固定矩形区域，蓝色边框会一直保留在屏幕上；用户手动滚动内容，程序每 200 ms 采集一帧并自动拼接。
- 可拖动浮动工具条：支持隐藏到屏幕边缘，再点击恢复。
- 全局快捷键：可在设置窗口中重新绑定。
- 剪贴板输出：截图完成后直接复制为图片。
- 最近截图：通过系统托盘菜单快速重新复制最近的截图。
- 反馈和开机启动设置：支持 Toast、托盘气泡或关闭反馈，并可选择随 Windows 启动。

### 滚动长截图的使用方式

1. 启动滚动截图模式。
2. 鼠标拖动选择需要保留的固定区域。
3. 看到蓝色边框后，将鼠标放到目标内容上并手动滚动。
4. 继续滚动，程序会按固定区域周期性采集画面。
5. 再次按滚动截图快捷键，停止采集并把结果复制到剪贴板。

滚动距离应小于选区高度，确保相邻帧之间存在重叠内容。动态广告、动画、视频和快速变化的页面可能降低拼接准确率。

### 环境要求

- Windows 10/11 或兼容的 Windows 桌面环境。
- .NET Framework 4.8 运行时。
- .NET SDK 8.0 或更高版本，用于还原、测试和编译。

项目目标框架仍然是 `.NET Framework 4.8`。构建时使用 .NET SDK 作为 MSBuild 驱动，不要求安装完整 Visual Studio；项目会通过 NuGet 获取 .NET Framework 4.8 的引用程序集。

### 编译、测试和运行

在仓库根目录执行 PowerShell 命令：

```powershell
dotnet restore YScreenshot.sln
dotnet build YScreenshot.sln -c Release
dotnet test tests/YScreenshot.Capture.Tests/YScreenshot.Capture.Tests.csproj -c Release
```

Release 可执行文件位于：

```text
src/YScreenshot.App/bin/Release/net48/YScreenshot.exe
```

### 项目结构

```text
src/YScreenshot.App        Windows Forms 主程序、工具条、快捷键和托盘菜单
src/YScreenshot.Capture    截图模式、剪贴板输出和滚动帧拼接
src/YScreenshot.Overlay    矩形选择层和滚动截图引导框
tests/                      截图及拼接逻辑的自动化测试
```

### 文档

- [开发计划](DEVELOPMENT_PLAN.md)
- [开发日志](DEVELOPMENT_LOG.md)
- [中文项目状态](PROJECT_STATUS_ZH.md)
- [中文验证与测试计划](VERIFICATION_TEST_PLAN_ZH.md)
- [贡献指南](CONTRIBUTING.md)

## English Introduction

YScreenshot is a lightweight Windows Forms screenshot utility targeting .NET Framework 4.8. It provides a small always-on-top toolbar, copies captured images directly to the clipboard, and avoids writing capture data to disk.

### Features

- Full-screen capture across the virtual desktop and multiple monitors.
- Rectangle capture with multi-monitor and mixed-DPI-aware geometry.
- Scrolling screenshots based on a fixed user-selected rectangle. A blue guide frame stays visible while the user manually scrolls, and the app samples the region every 200 ms before stitching the frames.
- A draggable floating toolbar that can collapse to the edge of the screen.
- Rebindable global hotkeys through the Settings dialog.
- Direct image output to the clipboard.
- A Recent Captures menu in the system tray for re-copying recent images.
- Configurable Toast, tray-balloon, or silent feedback, plus optional Windows startup.

### Scrolling screenshot workflow

1. Start the Scrolling capture mode.
2. Drag a rectangle around the content to capture.
3. Keep the blue guide frame visible and place the pointer over the target content.
4. Scroll the content manually while the app samples the fixed rectangle.
5. Press the scrolling hotkey again to stop and copy the stitched image to the clipboard.

For reliable stitching, keep each scroll step smaller than the selected rectangle height so adjacent frames overlap. Animated pages, rotating ads, video, and rapidly changing content can reduce stitching accuracy.

### Requirements

- Windows 10/11 or a compatible Windows desktop environment.
- .NET Framework 4.8 runtime.
- .NET SDK 8.0 or later for restore, testing, and building.

The application still targets `.NET Framework 4.8`. The .NET SDK is used to drive the build; a full Visual Studio installation is not required because the project restores .NET Framework 4.8 reference assemblies through NuGet.

### Build, test, and run

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

### Repository layout

```text
src/YScreenshot.App        Windows Forms application, toolbar, hotkeys, and tray menu
src/YScreenshot.Capture    Capture modes, clipboard output, and frame stitching
src/YScreenshot.Overlay    Rectangle selection and scrolling-capture guide overlays
tests/                      Automated tests for capture and stitching logic
```

### Documentation

- [Development plan](DEVELOPMENT_PLAN.md)
- [Development log](DEVELOPMENT_LOG.md)
- [Chinese project status](PROJECT_STATUS_ZH.md)
- [Chinese verification and test plan](VERIFICATION_TEST_PLAN_ZH.md)
- [Contributing guide](CONTRIBUTING.md)

## License

The project currently does not declare a separate license. Add a `LICENSE` file before distributing the repository as an open-source project.
