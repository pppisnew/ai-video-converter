# Backend Directory Structure

> 项目：视频格式转换器 | 更新：2026-10-10

```
src/VideoConverter.Core/          # 纯 .NET 类库，零第三方依赖，可完整单测
  Models/                         # MediaInfo / ConvertPreset / ConversionProgress / ConversionResult
  Services/                       # IFfmpegLocator, FfprobeMediaProbeService, FfmpegThumbnailService,
                                  # FfmpegConversionService（接口 + 实现同文件，MVP 规模）
  Internal/                       # ProcessRunner, ProgressState, MediaInfoMapper, FfprobeJson(JSON DTO)
tests/VideoConverter.Core.Tests/  # xUnit；*Tests.cs 与被测类同名
tools/fetch-ffmpeg.ps1            # ffmpeg 二进制获取（不进 git）
tools/ffmpeg/                     # ffmpeg.exe / ffprobe.exe（gitignore，约 280MB）
```

规则：

- 新增服务一律先定义接口再实现，构造函数注入 `FfmpegBinaries`
- 涉及 ffmpeg 命令构造的逻辑必须可单测（参数生成与进程执行分离）
- JSON DTO 用 `[JsonPropertyName]` 小写命名，与 ffprobe 输出对齐
