# 视频格式转换器（Windows 桌面端）

基于 **WinUI 3 + C# + .NET 10** 的 Windows 原生桌面工具：加载视频后展示媒体信息与缩略图，
通过 **ffmpeg 命令行**完成格式转换，支持批量队列、可调编码参数与硬件加速自动探测。

## 功能

- 批量队列：文件选择（可多选）或拖拽追加，顺序转换；
  队列项四态（等待中 / 转换中 / 已完成 / 失败），当前任务高亮，完成后可定位输出文件
- 队列操作：移除单项、清除已完成、重试全部失败项；**单个任务失败不中断后续任务**
- 取消分两级：`取消当前`（跳过这个文件，队列继续）/ `全部停止`（整个队列停摆）
- 媒体信息：容器格式、时长、分辨率、帧率、音视频编码、声道数、总码率、文件大小（ffprobe）
- 缩略图：ffmpeg 抽帧，时间点可调、可重新截取
- 编码参数：容器格式、视频编码器（自动/H.264/H.265/AV1/VP9）、质量（0-51）、
  分辨率缩放、音频码率；参数摘要实时显示，可一键重置
- 硬件加速：启动时真实试编码探测（NVENC → QSV → AMF），不可用自动回退软件编码，
  结果缓存到 `%LOCALAPPDATA%`，支持"强制软件编码"与"重新探测"
- ffmpeg 来源三级策略：随包捆绑 → 仓库 tools 目录 → 系统 PATH，界面明示来源与版本

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

# 4. 测试
dotnet test
```

## 发布（自带 ffmpeg）

```powershell
dotnet publish src/VideoConverter/VideoConverter.csproj -c Release -o publish/win-x64
```

发布目录包含 `VideoConverter.exe` 与 `tools/ffmpeg/(ffmpeg|ffprobe).exe`，
拷到未安装 ffmpeg 的机器上即可直接使用（应用顶部会显示"随包捆绑"）。

> unpackaged WinUI 3 的 `dotnet publish` 不会自动带入 XAML 编译产物（.xbf/.pri），
> 项目内已加 `CopyXamlBinariesToPublish` 目标补齐，否则运行时会直接崩溃。

## 架构

```
src/VideoConverter.Core/     # 纯 .NET 类库：与 ffmpeg CLI 交互的全部逻辑（可单测、零第三方依赖）
  Models/                    # ConversionSettings, VideoEncoder(EncoderCatalog), QueueItem, MediaInfo...
  Services/                  # FfmpegLocator, FfprobeMediaProbeService, FfmpegThumbnailService,
                             # FfmpegConversionService, ConversionQueue, EncoderProbeService
  Internal/                  # ProcessRunner, ProgressState, MediaInfoMapper
src/VideoConverter/          # WinUI 3 应用：MainWindow + MainViewModel（CommunityToolkit.Mvvm）
tests/VideoConverter.Core.Tests/
tools/fetch-ffmpeg.ps1       # ffmpeg 二进制获取脚本
```

设计要点：

- ffmpeg 进度只解析 `-progress pipe:1` 的 `out_time_us`（`out_time_ms` 字段名有误导性，
  实际输出微秒值，按毫秒解析会把进度放大 1000 倍）
- 命令行参数经 `ProcessStartInfo.ArgumentList` 逐项传递，空格/中文路径安全
- 取消时 Kill 进程树并删除不完整输出，避免误当成品播放
- 硬件编码器按"编码格式 × 厂商"组合分别探测；探测通过后运行时仍失败则自动回退软件并失效缓存
- 质量滑杆统一 0-51，内部按编码器量纲归一化（VP9/AV1 实际范围是 0-63），摘要显示实际值
- 容器与编码器有兼容矩阵（WebM 只接受 VP9/AV1，AVI 仅 H.264），非法组合自动纠正

## 已知限制 / 后续增强

分段并行转码（同文件切段并行）、字幕封装、HDR/色彩空间、硬件解码（-hwaccel）。
