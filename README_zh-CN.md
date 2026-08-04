# YScreenshot

[English](README.md) | [简体中文](README_zh-CN.md)

YScreenshot 是一个轻量、快速、面向 Windows 的截图工具，支持全屏截图、矩形截图和手动滚动长截图。

项目基于 Windows Forms 开发，目标框架为 .NET Framework 4.8。截图结果会直接复制到剪贴板，默认不会把截图数据写入磁盘。

## 主要功能

- **全屏截图**：捕获整个虚拟桌面，支持多显示器。
- **矩形截图**：鼠标拖动选择区域，支持跨显示器和混合 DPI 环境。
- **滚动长截图**：先选择固定矩形区域，蓝色边框会一直保留在屏幕上；用户手动滚动内容，程序每 200 ms 采集一帧并自动拼接。
- **可拖动浮动工具条**：支持隐藏到屏幕边缘，再点击恢复。
- **全局快捷键**：可在设置窗口中重新绑定。
- **剪贴板输出**：截图完成后直接复制为图片。
- **最近截图**：通过系统托盘菜单快速重新复制最近的截图。
- **可配置反馈**：支持 Toast、托盘气泡或静默模式。
- **开机启动**：可在设置窗口中选择是否随 Windows 启动。

## 滚动长截图

1. 启动滚动截图模式。
2. 鼠标拖动选择需要保留的固定区域。
3. 保持蓝色边框可见，将鼠标放到目标内容上。
4. 手动滚动内容，程序会周期性采集固定区域。
5. 再次按滚动截图快捷键，停止采集并把拼接结果复制到剪贴板。

为了保证拼接可靠，每次滚动距离应小于选区高度，使相邻帧之间存在重叠内容。动态广告、动画、视频和快速变化的页面可能降低拼接准确率。

## 环境要求

- Windows 10/11 或兼容的 Windows 桌面环境。
- .NET SDK 8.0 或更高版本，用于还原、测试和编译。

Windows 10/11 通常已内置 .NET Framework 4.8 或更高兼容版本，一般无需额外安装。如果程序提示缺少运行时，再安装 [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48)。

项目目标框架为 `.NET Framework 4.8`。`.NET SDK` 只用于还原、测试和编译，不要求安装完整 Visual Studio；项目会通过 NuGet 获取 .NET Framework 4.8 的引用程序集。

## 编译和测试

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

## Release 发布包

不要只上传 `YScreenshot.exe`。程序还需要配套程序集和配置文件。创建 GitHub Release 时，建议上传一个包含以下文件的 ZIP 压缩包：

```text
YScreenshot.exe
YScreenshot.exe.config
YScreenshot.Capture.dll
YScreenshot.Overlay.dll
```

## 项目结构

```text
src/YScreenshot.App        Windows Forms 主程序、工具条、快捷键和托盘菜单
src/YScreenshot.Capture    截图模式、剪贴板输出和滚动帧拼接
src/YScreenshot.Overlay    矩形选择层和滚动截图引导框
tests/                      截图及拼接逻辑的自动化测试
```

## 文档

- [开发计划](DEVELOPMENT_PLAN.md)
- [开发日志](DEVELOPMENT_LOG.md)
- [中文项目状态](PROJECT_STATUS_ZH.md)
- [中文验证与测试计划](VERIFICATION_TEST_PLAN_ZH.md)
- [贡献指南](CONTRIBUTING.md)

## 许可证

当前项目尚未声明独立的开源许可证。如果要以开源项目形式发布，请先添加 `LICENSE` 文件。
