# 视频格式转换器 - 会话总结

> 本文档记录整个开发过程中的关键决策、用户反馈与进展，随开发持续推进更新。
> 关联 Trellis 任务：`.trellis/tasks/10-10-video-converter/`

## 项目目标

Windows 桌面端视频格式转换器：加载视频信息与缩略图，基于 ffmpeg CLI 完成格式转换。

## 决策记录

| # | 日期 | 决策点 | 选项 | 结论 | 理由 |
|---|------|--------|------|------|------|
| D1 | 2026-10-10 | 技术栈 | Electron+Web / Python+PySide6 / Tauri / WinUI 3 / WPF / Avalonia | **WinUI 3 + C# + .NET** | Windows 原生框架，与岗位匹配度最高；界面需求简单（列表+信息面板+缩略图+进度条），可避开 WinUI 3 已知坑区；曾先推荐 Electron，用户提出原生框架后重新评估并采纳 |
| D2 | 2026-10-10 | 任务管理 | Trellis 流程 / 直接开发 | **Trellis 流程** | 用户要求先生成 PRD，进入 Phase 1 规划 |
| D3 | 2026-10-10 | 功能范围 | 单文件 MVP / 批量队列 / 批量+精细参数 | **单文件 MVP**（MP4/MKV/MOV/AVI/WebM + MP3 提取；界面中文；固定预设） | 先验证核心链路，批量队列与参数面板列为后续增强 |
| D4 | 2026-10-10 | ffmpeg 二进制来源 | 内置优先+回退 / 纯 PATH / 运行时自动下载 / **安装包捆绑** | **安装包捆绑 ffmpeg + PATH 回退，无运行时下载**（当日由"内置优先+下载兜底"调整而来） | 用户理由：竞品如格式工厂安装包已内置完整 ffmpeg，用户对"首次使用下载 150MB"容忍度下降 |
| D5 | 2026-10-10 | 第三方依赖 | 引入 FFMpegCore / 自研薄 Core 层 | **自研薄 Core 层，不引入 FFMpegCore**（用户先选 A 后改 B） | 需求面窄、进度与取消需自掌控；FFMpegCore 下载扩展的 ffmpeg 版本过旧（6.1）；CLI 语义透明 |

## 用户反馈记录

| 日期 | 反馈内容 | 处理 |
|------|----------|------|
| 2026-10-10 | 项目是 Windows 桌面端，先生成 PRD 文档 | 已创建 Trellis 任务 `10-10-video-converter` 并生成 PRD |
| 2026-10-10 | 技术栈是否满足 Windows 桌面端？是否可以用 WinUI 3 等原生框架？ | 核实本机环境（无 .NET SDK、winget 可用、WinAppSDK 2.5.1）后给出四方案对比，推荐 WinUI 3 |
| 2026-10-10 | 选择方案 1 | 确认 WinUI 3 + C# + .NET，已写入 PRD |
| 2026-10-10 | 询问 GitHub 开源项目能否作模块/基础 | 调研 FFMpegCore（2099 stars, MIT）与 TransCoda4Windows（WinUI3+ffmpeg），结论写入 research/ |
| 2026-10-10 | 先选 A（引入 FFMpegCore），随后改回 B（自研薄 Core 层） | 已更新 design/prd/research，决策链完整记录 |
| 2026-10-10 | 调整 ffmpeg 策略：改为安装器直接捆绑 ffmpeg 二进制，不要运行时下载 | 已更新 PRD(R7/AC5/D3)、design(3.4/第5节/风险表)、implement(步骤3/10)；理由记录在案 |

## 环境事实（已实测）

- Windows 11 家庭版 (10.0.26200)，磁盘剩余约 107GB
- 未安装 .NET SDK / Visual Studio；winget v1.29 可用（可安装 .NET 8/9/10）
- Node v22.23.2、Python 3.14.7 可用
- ffmpeg 未安装（需在设计中解决二进制来源）
- NuGet 访问走 nuget.azure.cn 镜像（国内网络）

## 关键调研结论

- 开源参照：ffmpeg_batch（1.6k, C#）、Axiom（1.7k, C#）、StaxRip（3k）、HBBatchBeast（Electron 架构参照）
- ffmpeg 二进制来源：BtbN/FFmpeg-Builds（Windows 静态构建，最主流）
- Windows App SDK 最新稳定版 2.5.1（NuGet 已核实）

## 进展时间线

- 2026-10-10 创建 Trellis 任务，完成开源调研（`research/open-source-survey.md`），生成 PRD
- 2026-10-10 确认技术栈 WinUI 3 + C# + .NET
- 2026-10-10 ffmpeg 策略初定（内置优先 + PATH 回退 + 下载兜底）；三份规划文档成稿（prd/design/implement），PRD 已做收敛 pass
- 2026-10-10 依赖决策：先选 FFMpegCore 后改回自研薄 Core 层（D5）
- 2026-10-10 ffmpeg 策略调整为安装包捆绑 + PATH 回退（无运行时下载），设计/计划同步更新
- 下一步：等待用户审批规划汇总 -> task.py start -> 进入 Phase 2 实现




