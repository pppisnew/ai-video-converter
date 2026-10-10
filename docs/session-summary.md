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





## 实现结果（2026-10-10）

技术栈：WinUI 3（WinAppSDK 2.5.1）+ C# + .NET 10，unpackaged，MVVM（CommunityToolkit.Mvvm 8.4）

- 构建：0 警告 0 错误
- 测试：**59/59 通过**（解析器/映射/定位/预设单测 + 真实 ffmpeg 9.0.1 集成测试：
  6 种目标格式转换、进度单调性、取消清理、中文与空格路径）
- 发布：`dotnet publish` 产物自带 `tools/ffmpeg/`（138MB x 2），发布版启动后
  状态栏显示"ffmpeg 就绪 · 随包捆绑 · n9.0.1-11"
- ffmpeg 获取：`tools/fetch-ffmpeg.ps1`（winget 优先、GitHub 直链兜底），
  本机 GitHub 下载 CDN 不可达，实际通过 winget 的 BtbN.FFmpeg.GPL.9.0 取得

### 自动化验证通过

应用启动与界面渲染、ffmpeg 三级定位（仓库目录/随包捆绑/PATH）、
按钮启用禁用状态、文件加载后全部媒体信息字段、缩略图渲染、输出路径自动填充、
转换产物有效性（ffprobe 验证 720p h264+aac）、发布版自包含可运行。

### 待人工验证（GUI 自动化不稳定项）

1. **文件选择对话框交互**：新版 Windows 选择器（PickerHost 进程）对 UI 自动化不友好，
   自动键入路径不稳定（中文输入法下必然乱码，英文输入法下也偶发失败）。
   请手动点"选择视频文件"选一个视频，确认信息与缩略图正常显示。
2. **拖拽加载**：从资源管理器拖视频到左下角拖放区。
3. **转换过程可视化**：点"开始转换"观察进度条/百分比/倍速滚动，确认完成后状态栏提示。
4. **取消转换**：转换中点"取消"，确认状态栏提示"已取消转换"且输出文件被删除。
5. **切换目标格式**：分别选 AVI / WebM / MP3 转换，确认产物可用（MP3 应只有音频）。
6. **ffmpeg 缺失场景**：把 tools/ffmpeg 改名后启动，确认显示"未找到 ffmpeg"、
   右列禁用、"获取 ffmpeg"按钮可见可点。

### 踩坑记录（已写入 .trellis/spec/）

- unpackaged WinUI 3 的 `dotnet publish` 不带 .xbf/.pri，运行数秒后崩在 Microsoft.UI.Xaml.dll
  （事件码 c000027b，无 stderr）→ csproj 加 `CopyXamlBinariesToPublish` 目标
- 本机用户级 NuGet.Config 的 packageSources 为空导致 NU1100 → 仓库根放 NuGet.Config（含国内镜像）
- `x:Bind` + Converter 在 Window 根元素上报 CS1503 → 改由 VM 暴露 Brush/Visibility
- WinUI 3 无 Window.Width/Height、Panel 无 IsEnabled、FileSavePicker 用 FileTypeChoices
- MVVM Toolkit 的 partial 属性+初始值设定项在本工具链报 CS8050 → 回退字段式 + NoWarn MVVMTK0045
- PowerShell：`$pid` 是只读变量；函数内委托里 `$script:x` 与局部变量不是同一个；
  命令行传中文字面量会被转码损坏（自动化脚本要用码点构造或用 ASCII AutomationId）

## 当前状态

- Trellis 任务 `10-10-video-converter` 进行中，代码已提交 3 个 checkpoint
- 剩余：Phase 3.4 提交剩余变更
