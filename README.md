# 视频格式转换器（Windows 桌面端）

基于 **WinUI 3 + C# + .NET 10** 的 Windows 原生桌面工具：加载视频后展示媒体信息与缩略图，
通过 **ffmpeg 命令行**完成格式转换。

## 功能

- 选择或拖拽加载视频文件（单文件 MVP）
- 展示 ffprobe 探测结果：容器格式、时长、分辨率、帧率、视频/音频编码、声道数、总码率、文件大小
- ffmpeg 抽帧生成缩略图，时间点可调、可重新截取
- 固定预设集转换：MP4 / MOV / MKV / AVI / WebM / MP3（仅音频）
- 转换进度（百分比 + 已处理时长 + 倍速）与取消；失败或取消时清理不完整产物
- ffmpeg 来源三级策略：**随包捆绑 → 仓库 tools 目录 → 系统 PATH**，界面明示当前来源与版本

## 环境要求

- Windows 10 1809+ / Windows 11
- .NET 10 SDK（构建）；运行需 Windows App SDK 运行时（unpackaged 应用由系统提供）
- ffmpeg 二进制（见下）

## 快速开始

```powershell
# 1. 安装 .NET 10 SDK（如未安装）
winget install --id Microsoft.DotNet.SDK.10 --source winget

# 2. 获取 ffmpeg 静态二进制到 tools/ffmpeg/（约 280MB，winget 优先、GitHub 直链兜底）
./tools/fetch-ffmpeg.ps1

# 3. 构建并运行
dotnet build
dotnet run --project src/VideoConverter

# 4. 测试（59 个用例，含真实 ffmpeg 集成测试）
dotnet test
```

## 发布（自带 ffmpeg）

```powershell
dotnet publish src/VideoConverter/VideoConverter.csproj -c Release -o publish/win-x64
```

发布目录包含 `VideoConverter.exe` 与 `tools/ffmpeg/(ffmpeg|ffprobe).exe`，
拷到未安装 ffmpeg 的机器上即可直接使用（应用顶部会显示"随包捆绑"）。

> 说明：unpackaged WinUI 3 的 `dotnet publish` 不会自动带入 XAML 编译产物（.xbf/.pri），
> 项目内已加 `CopyXamlBinariesToPublish` 目标补齐，否则运行时会直接崩溃。

## 架构

```
src/VideoConverter.Core/     # 纯 .NET 类库：与 ffmpeg CLI 交互的全部逻辑（可单测、零第三方依赖）
  Services/                  # FfmpegLocator / FfprobeMediaProbeService / FfmpegThumbnailService / FfmpegConversionService
  Internal/                  # ProcessRunner（进程执行+取消）、ProgressState（-progress 解析）、MediaInfoMapper
  Models/                    # MediaInfo / ConvertPreset / ConversionProgress / ConversionResult
src/VideoConverter/          # WinUI 3 应用：MainWindow + MainViewModel（CommunityToolkit.Mvvm）
tests/VideoConverter.Core.Tests/
tools/fetch-ffmpeg.ps1       # ffmpeg 二进制获取脚本
```

设计要点：

- ffmpeg 进度用 `-progress pipe:1` 机器可读协议，不解析 stderr 文本
- 命令行参数经 `ProcessStartInfo.ArgumentList` 逐项传递，空格/中文路径安全
- 取消时 Kill 进程树并删除不完整输出，避免误当成品播放
- UI 层不直接触碰 ffmpeg，全部经 Core 服务；后续加批量队列无需改 Core 契约

## 目标格式预设

| 格式 | 视频编码 | 音频编码 |
|---|---|---|
| MP4 / MOV / MKV | libx264 | aac |
| AVI | mpeg4 | libmp3lame |
| WebM | libvpx-vp9 | libopus |
| MP3 | -（`-vn`） | libmp3lame |

## 已知限制 / 后续增强

批量队列、编码参数面板（码率/分辨率/帧率）、硬件加速、MSIX 安装器打包。

## 文档

- 需求与验收：`.trellis/tasks/10-10-video-converter/prd.md`
- 技术设计与执行计划：同目录 `design.md` / `implement.md`
- 开源调研：同目录 `research/`
- 会话总结（含决策记录）：`docs/session-summary.md`
