# .NET / WinUI 生态可复用开源模块调研

调研时间：2026-10-10 | 方式：GitHub API + NuGet API（raw.githubusercontent.com 在本网络不可达，
代码细节通过 API contents/readme 端点获取）

## 结论速览

| 用途 | 项目 | Stars | License | 活跃度 | 结论 |
|---|---|---|---|---|---|
| ffmpeg CLI 封装模块 | [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) | 2099 | MIT | 2026-10-09 仍在提交 | 最强候选模块 |
| ffmpeg CLI 封装模块 | [Xabe.FFmpeg](https://github.com/tomaszzmuda/Xabe.FFmpeg) | 778 | NOASSERTION | 2026-09 活跃 | License 不明确，不推荐 |
| ffmpeg CLI 封装模块 | [FFmpeg.NET](https://github.com/cmxl/FFmpeg.NET) | 682 | MIT | 较旧 | 备选 |
| 基础项目（同栈同形态） | [TransCoda4Windows](https://github.com/albertolicea00/TransCoda4Windows) | 1 | MIT | 2026-07 活跃 | 架构参照可用，作基础项目风险高 |
| 播放（非转换） | [Flyleaf](https://github.com/SuRGeoNix/Flyleaf) | 917 | - | 活跃 | 与需求无关（播放器库） |
| 解码（非转换） | [FFmpegInteropX](https://github.com/ffmpeginteropx/FFmpegInteropX) | 237 | - | 活跃 | UWP/WinUI 播放解码，与需求无关 |

## FFMpegCore 详解（最值得引入的模块）

- NuGet：`FFMpegCore` 5.5.0（最新），目标框架 netstandard2.0，依赖 Instances + System.Text.Json
- License：MIT，可自由商用/修改
- 核心能力（README 实证）：
  - 探测：`await FFProbe.AnalyseAsync(path)` 直接返回结构化媒体信息
  - 转换：`FFMpegArguments.FromFileInput(in).OutputToFile(out, false, o => o.WithVideoCodec(...).WithAudioCodec(...))`，
    fluent 构建参数，底层仍是 spawn ffmpeg CLI（符合"使用 ffmpeg 命令行"的需求）
  - 截图：`FFMpeg.Snapshot(input, output, size, TimeSpan)` / `FFMpegImage.Snapshot`（SkiaSharp 扩展可内存位图）
  - 二进制配置：`GlobalFFOptions.Configure(o => o.BinaryFolder = "./bin")`，或 ffmpeg.config.json
  - 运行时安装：`FFMpegCore.Extensions.Downloader` 可在运行时把 ffmpeg/ffprobe 装到配置的 BinaryFolder
- 待验证点：`ProcessAsynchronously` 的 CancellationToken 进度回调签名需在集成时实测；
  若不支持取消，则取消能力仍需自管理进程

## TransCoda4Windows 详解（同形态 WinUI 3 项目）

- MIT，.NET 8，WinUI 3（Mica/Fluent），单页 + 队列
- 架构（仓库结构实证）：`FfmpegLocator`（app folder -> known paths -> PATH）、`FfmpegCommandBuilder`
  （设置 -> 参数列表，不经 shell）、`TranscodeEngine`（spawn ffmpeg + 解析 `-progress` + 取消）、
  `Ffprobe`（时长探测算进度百分比）、`JobQueue`、`HardwareCapabilities`（解析 ffmpeg -encoders）
- 与我们的 design.md 架构几乎逐项对应，验证了设计方向
- 风险：仅 1 star，无社区验证；含批量队列/硬件加速等超出 MVP 的功能需要删减；
  仓库含 AGENTS.md/CLAUDE.md，判断为 AI 辅助生成项目，代码质量需人工审查

## 分析与建议

1. **TransCoda4Windows 适合当"架构参照"，不适合当"基础项目"**：
   它的分层与我们的 design 一致，说明设计路线正确；但 1 star + 未验证质量 +
   需要删减多余功能，作为基础项目改造风险高于收益。笔试场景下也更适合交付自己写的代码。
2. **FFMpegCore 适合作为可选模块引入**：能省掉 ffprobe JSON 解析、参数构建、Snapshot 命令构造
   这些"已解决"的问题；但会多一层抽象，"直接调 CLI"的透明度下降，且取消/进度需实测。
3. **播放类库（Flyleaf / FFmpegInteropX）与需求无关**：本需求是转码不是播放，不引入。

## 决策记录（2026-10-10）

用户先选择引入 FFMpegCore（方案 A），随后改回自研（方案 B）。最终结论：

- **不引入 FFMpegCore**，Core 层保持零第三方依赖，自研薄封装（见 design.md）
- 核心理由：
  1. 需求面极窄（一次探测、一次截图、一组固定预设），库收益有限
  2. 进度与取消是最需自掌控的部分；已通过 DLL 元数据核实 FFMpegCore 确有
     CancellationToken / NotifyOnProgress / SnapshotAsync / AnalyseAsync 等 API，
     但引入后 CLI 语义透明度下降
  3. 其下载扩展走 ffbinaries.com，最新仅 ffmpeg 6.1（2023 年版本），不如 BtbN 当前构建
- **TransCoda4Windows 仅作架构参照**，不作为基础项目（1 star、无社区验证、需删减功能）
- 备查：若未来功能扩展到批量/参数面板/滤镜，再重新评估引入 FFMpegCore
