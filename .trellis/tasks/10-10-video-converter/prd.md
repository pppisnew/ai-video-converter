# 视频格式转换器（Windows 桌面端）

## Goal

一款 Windows 桌面端视频格式转换器：加载视频文件后展示其媒体信息与缩略图，
并基于 ffmpeg 命令行（CLI）完成视频格式转换。

## Background / Confirmed Facts

用户原始需求：

1. 能够加载视频信息并显示
2. 获取视频缩略图并显示
3. 视频转换使用 ffmpeg 命令行（不要求接入 ffmpeg API）
4. 可参考 GitHub 优秀开源项目二次开发或引入其模块
5. 项目类型：Windows 桌面端（用户补充确认）

流程要求：整个开发过程中维护一份"会话总结" Markdown 文档，记录决策选择与反馈
（已建立于 `docs/session-summary.md`）。

本机环境（已实测）：

- Windows 11 家庭版 (10.0.26200)，磁盘剩余约 107GB
- 未安装 .NET SDK / Visual Studio；winget v1.29 可用（可装 .NET 8/9/10）
- Node v22.23.2、Python 3.14.7 可用
- ffmpeg 未安装（`ffmpeg -version` 不可用）
- NuGet 访问自动重定向到 nuget.azure.cn 镜像（国内网络）

仓库现状：仅 Trellis 骨架，无业务代码；`.trellis/spec/` 为待填充模板。

调研结论（详见 `research/open-source-survey.md`）：

- 功能形态参照：ffmpeg_batch（1.6k, C#）、Axiom（1.7k, C#）、StaxRip（3k）
- ffmpeg 二进制来源：BtbN/FFmpeg-Builds（Windows 静态构建，最主流）
- Windows App SDK 最新稳定版 2.5.1（NuGet 已核实）

## Key Decisions

| # | 决策点 | 结论 | 时间 |
|---|--------|------|------|
| D1 | 技术栈 | WinUI 3 + C# + .NET（Windows App SDK 2.5.1） | 2026-10-10 |
| D2 | 功能范围 | 单文件 MVP；目标格式 MP4/MKV/MOV/AVI/WebM + MP3 音频提取；界面中文；固定预设 | 2026-10-10 |
| D3 | ffmpeg 来源 | 安装包捆绑 ffmpeg + PATH 回退（无运行时下载） | 2026-10-10（当日按用户反馈调整） |
| D4 | 会话总结位置 | `docs/session-summary.md`（已建立，可随时迁移） | 2026-10-10 |
| D5 | 第三方依赖 | 自研薄 Core 层，不引入 FFMpegCore（评估后放弃） | 2026-10-10 |

## Requirements

- R1 视频加载：支持文件选择与拖拽添加视频文件（单文件，批量队列为后续增强）
- R2 信息展示：展示探测到的媒体信息（容器格式、时长、分辨率、帧率、视频编码、
  音频编码、码率、文件大小等，数据来自 ffprobe）
- R3 缩略图：从视频抽取帧生成缩略图并展示，时间点可指定
- R4 格式转换：通过 ffmpeg CLI 子进程执行转换，目标格式在固定预设集中选择
  （MP4/MKV/MOV/AVI/WebM/MP3）
- R5 进度反馈：转换过程显示进度，可取消
- R6 技术栈：WinUI 3 + C# + .NET（Windows App SDK 2.5.1），MVVM 模式
- R7 ffmpeg 获取：ffmpeg 二进制随安装包捆绑分发（BtbN 静态构建作为应用内容），运行期不联网下载；
  系统 PATH 作为次级回退；界面明示当前使用的 ffmpeg 来源与版本

## Acceptance Criteria

- [ ] AC1 添加视频文件后，界面展示其完整媒体信息（来源：ffprobe 探测结果）
- [ ] AC2 界面展示该视频的缩略图，可指定截取时间点
- [ ] AC3 选择目标格式后执行转换，产物文件可正常播放，转换由 ffmpeg CLI 完成
- [ ] AC4 转换过程中显示进度，可取消任务
- [ ] AC5 交付物（发布目录/安装包）自带 ffmpeg 二进制，未安装 ffmpeg 的机器开箱即用；
      若捆绑缺失则回退系统 PATH；界面明示当前使用的 ffmpeg 来源与版本

## Out of Scope

- 批量队列转换（后续增强项）
- 音频提取、滤镜/转码参数精细调节面板（后续增强项）
- 视频剪辑、拼接等编辑功能
- macOS / Linux 支持（仅 Windows）
- 硬件加速编码（后续增强项）

## Related Artifacts

- 技术设计：`design.md`
- 执行计划：`implement.md`
- 开源调研：`research/open-source-survey.md`、`research/dotnet-ffmpeg-modules.md`
- 会话总结：仓库根 `docs/session-summary.md`


