# 开源方案调研：Windows 桌面视频格式转换器

调研时间：2026-10-10 | 方式：GitHub API 检索 + 星标/license/活跃度核对

## 候选开源项目（按可参考度排序）

| 项目 | Stars | 语言 | License | 价值 |
|---|---|---|---|---|
| [eibol/ffmpeg_batch](https://github.com/eibol/ffmpeg_batch) | 1.6k | C# | LGPL-2.1 | 最成熟的功能参照：批量队列、多格式预设、进度显示 |
| [MattMcManis/Axiom](https://github.com/MattMcManis/Axiom) | 1.7k | C# | - | FFmpeg GUI 的预设/参数面板设计参照 |
| [staxrip/StaxRip](https://github.com/staxrip/StaxRip) | 3.0k | VB.NET | - | 编码任务队列与滤镜配置参照 |
| [66HEX/frame](https://github.com/66HEX/frame) | 2.0k | Rust | - | 现代桌面 UI + ffmpeg CLI 调用架构参照 |
| [HaveAGitGat/HBBatchBeast](https://github.com/HaveAGitGat/HBBatchBeast) | 711 | HTML/JS (Electron) | GPL-3.0 | 与 Electron 方案最同构：Web UI + 子进程调 ffmpeg |
| [HandBrake/HandBrake](https://github.com/HandBrake/HandBrake) | 24.6k | C | GPL-2.0 | 编码引擎参照，整体过重，不适合二次开发嵌入 |

结论：没有一个项目能"直接套壳"满足本需求（加载信息 + 缩略图 + 指定 CLI 转换的简洁桌面工具），
但 ffmpeg_batch / Axiom 的功能拆分（输入列表 -> 探测信息 -> 预设 -> 队列 -> 进度）是公认的成熟形态，
HBBatchBeast 证明了 Electron + ffmpeg 子进程的架构完全可行。

## ffmpeg 二进制分发方案（Windows）

| 方案 | Stars | 说明 |
|---|---|---|
| [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) | 11.8k | Windows 静态构建最主流来源，提供 ffmpeg.exe / ffprobe.exe，自动构建最新版 |
| [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static) (npm) | 1.4k | npm 包，安装即得各平台静态二进制，可与 Electron 打包流程集成 |
| 系统 PATH 中已安装的 ffmpeg | - | 本机当前未安装 ffmpeg（已实测 `ffmpeg -version` 不存在） |

结论：需要项目内提供 ffmpeg 获取机制。推荐"启动时探测 PATH -> 未找到则引导下载/使用内置"
三级回退，具体分发策略待与用户确认。

## 技术栈候选（待用户决策）

1. Electron + TypeScript + 前端框架（React/Vue）
   - 优：与仓库现有 frontend/backend spec 分层吻合；UI 表现力最强；Node 直接 spawn ffmpeg CLI；生态成熟
   - 劣：打包体积大（~150MB+）
2. Python + PySide6（Qt）
   - 优：单语言、PyInstaller 打包直接；Qt 桌面控件原生
   - 劣：UI 定制与动效弱于 Web 栈；与本仓库 spec 分层不完全吻合
3. Tauri + Rust
   - 优：体积最小、性能好
   - 劣：需要 Rust 工具链，前端仍需 Web 技术，复杂度最高

## 本机环境事实（已实测）

- Windows（PowerShell），Python 3.14.7，Node v22.23.2
- ffmpeg 未安装（`ffmpeg -version` 不可用）
- GitHub API 网络可达
- 仓库当前仅有 Trellis 骨架，无业务代码；spec 目录为待填充模板
