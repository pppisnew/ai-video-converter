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

## 用户实测反馈与修复（2026-10-10 第二轮）

| # | 反馈 | 结论 | 处理 |
|---|------|------|------|
| 1 | 文件选择正常 | 通过 | - |
| 2 | 拖拽到拖放区无效 | **真 bug** | 根因：`app.manifest` 缺 `PerMonitorV2` DPI 声明，WinUI 3 拖放事件根本不触发。已补声明，并把 `AllowDrop` 提到根 Grid（全窗口可拖入），拖放区加边框高亮反馈 |
| 3 | 进度条与实际进度不匹配（17s 视频显示 01:49:54 / 100%） | **真 bug** | 根因：ffmpeg 的 `out_time_ms` 字段名有误导性，实际输出与 `out_time_us` 相同的**微秒**值，解析器按毫秒算放大了 1000 倍。已改为只解析 `out_time_us`，并加回归测试（含用户场景复现 + 集成测试断言 processed <= 2x total） |
| 4 | 取消后文件删除 | 通过 | - |
| 5 | 产物可用 | 通过（ffprobe 复核 720p h264+aac） | - |
| 6 | 改名 tools/ffmpeg 为 ffmpeg-1 后仍显示"ffmpeg 就绪" | **非 bug，测试方法问题** | 用户测试的是发布版，它自带 `publish-v3\win-x64\tools\ffmpeg\`，与仓库目录无关；且按 D3 策略捆绑优先本就是预期行为。验证"ffmpeg 缺失"场景需改发布目录内的那份 |

修复后状态：60/60 测试通过，构建 0 警告 0 错误，新版发布在 `publish-v3/win-x64/`。

## 最终验收（2026-10-11）

用户对最新构建（`publish/win-x64/`，12:25 重新发布）完成实测，**全部通过**：

- 文件选择、拖拽加载、媒体信息展示、缩略图、格式转换、进度显示、取消清理、产物可用
- 上一轮两个 bug（拖拽无效、进度放大 1000 倍）均已修复并确认

**MVP 完成。** 范围外增强项（批量队列、编码参数面板、硬件加速、MSIX 安装器）记录在
`.trellis/tasks/10-10-video-converter/implement.md` 末尾备查。

> 备注：`publish-old-buggy`（修复前）与 `publish-v3` 是历史构建目录，均为本地产物不入库；
> 唯一有效入口是 `publish/win-x64/VideoConverter.exe`。

## 增强轮决策（2026-10-11）

用户确认三项功能均为推荐方案（AAA），并给出细化要求：

- **D6 批量队列**：顺序处理。理由（用户原话）：视频编码本身高度多线程，ffmpeg 默认吃满所有
  CPU 核心，并行跑两个任务只会互相抢 CPU 和磁盘 I/O，总耗时不会减半；除非做分段并行
  （同文件切段并行转再合并），否则多文件并行在编码场景几乎无收益。
  队列项四态（等待中/转换中/已完成/失败），当前任务高亮，已完成项可定位输出文件。
- **D7 参数粒度**：全局参数。理由：可"先调好参数再拖一堆文件"，心智模型清晰；
  单文件参数会让用户困惑"改的是全局还是这一个"，而 90% 用户确实只想一套参数全用。
  要求：保留"重置为默认"按钮 + 实时参数摘要。
- **D8 硬件加速**：自动探测 + 自动启用 + 软件回退。用户特别强调必须**真实试编码**
  （`-encoders` 列表里有不代表驱动版本够用/显卡在线/能初始化成功），
  并要求按"编码器 × 编码格式"组合分别探测（h264_nvenc 与 hevc_nvenc 相互独立）。
  探测缓存到 `%LOCALAPPDATA%`，提供"重新探测"，保留"强制软件编码"开关。

破坏性变更迁移方案（用户给定）：`ConvertPreset` -> `ConversionSettings`
（Container / VideoEncoderChoice / Crf / ResolutionScale / AudioBitrateKbps），
旧预设保留为工厂方法；`IConversionService.ConvertAsync` 入参随之变更，
`ConversionResult` 增加 `UsedEncoder`。

关键调研证据（`research/hardware-accel-survey.md`）：本机 RTX 4050 驱动 591.74 上
NVENC（需 610+）/QSV（无 Intel GPU）/AMF（无 AMD GPU）**全部实测不可用**，
恰好验证了"必须真实探测 + 软件回退"的设计。

用户将重点审查三处：探测缓存失效策略、队列错误恢复（失败是否续跑）、参数摘要 UI 呈现。

### 增强轮补充确认（2026-10-11 审批后）

用户批准规划，并纠正了一处实质缺陷 + 两个边界情况，均已补入 design.md：

- **D9 CRF 量纲归一化**：原设计只做了参数名映射，没处理取值范围差异。
  实测确认 libvpx-vp9 / libsvtav1 / libaom-av1 的 crf 范围是 **0-63**，
  而 x264/x265/NVENC 是 0-51。采用方案 A：UI 滑杆固定 0-51，
  对 0-63 的编码器按 `round(u * 63 / 51)` 映射，摘要里显示实际值（如 `质量 23 (实际 crf 28)`）。
  实测另确认本机 AV1 软件编码器（libsvtav1/libaom-av1/librav1e）齐全，AV1 选项可用。
- **D10 输出命名冲突**：队列内不同目录同名文件输出到同一目录时自动追加 `_1`/`_2` 后缀，
  序号单调不回收，绝不静默覆盖。
- **D11 取消残留文件**：取消即删除不完整输出（沿用 MVP 行为），单任务取消与取消全部均适用。
### 增强轮实现完成（2026-10-11）

三项功能已实现并通过验证：

- **批量队列**：`ConversionQueue` 严格顺序、四态、失败不中断、两级取消
  （实现期修正：`取消当前` = 跳过该文件且队列继续，`取消全部` = 停摆，
  更贴合"我不想转这个了 / 我不想转了"两种意图）
- **参数面板**：容器 / 编码器 / 质量 0-51 / 分辨率 / 音频码率，实时摘要 + 重置默认
- **硬件加速**：`EncoderProbeService` 真实试编码探测 + `%LOCALAPPDATA%` 缓存（五类失效），
  本机实测 NVENC/QSV/AMF 全部不可用（驱动 591.74 < 610、无 Intel/AMD GPU），正确回退软件

测试：122/122 通过。构建 0 警告 0 错误。新版发布在 `publish/win-x64/`。

UI 自动化已验证：容器切换联动（WebM 只剩 VP9/AV1）、参数摘要实时刷新、
质量归一化端到端（滑杆 51 → 实际 crf 63）、重置默认。

### 误触 bug 修复（2026-10-11）

用户实测发现：**ComboBox 未展开时，滚轮滑动会静默修改选中项**——滚动页面时参数被意外改动。
这是 WinUI 3 ComboBox 的已知行为（收起状态也响应 PointerWheelChanged）。
Slider 与 NumberBox 存在同样问题，一并修复。

实现：`src/VideoConverter/Controls/NoWheelControls.cs` 三个子类，
重写 `OnPointerWheelChanged`，仅在"下拉已展开 / 控件已获焦点"时才放行到基类。
（WinUI 3 无 `IsFocused` 属性，需用 `FocusState == FocusState.Unfocused` 判断。）

自动化验证：鼠标悬停编码器下拉框连发 10 次滚轮，选中项始终为"自动（优先硬件）"；
质量滑杆与音频码率框在未聚焦状态下各发 5-6 次滚轮，数值均无变化。

### 用户实测反馈与修复（2026-10-11 第二轮）

用户实测发现四类问题，根因与处理：

**1. 状态机不同步（最严重）**：任务已完成但队列项停留在"转换中"，右下角进度与左下角"队列执行失败"矛盾，
"清除已完成"无效，"定位"按钮不出现。

根因：`ConversionQueue.RunAsync` 内的 `await` 用了 `ConfigureAwait(false)`，
续体跑在线程池线程，`MarkCompleted` 触发的 `PropertyChanged` 在非 UI 线程抛出，
WinUI 绑定直接丢弃 → UI 永远停在"转换中"。
异常最终冒到 VM 的 catch，显示成"队列执行失败："。

修复：去掉队列内的 `ConfigureAwait(false)`，让续体回到调用方同步上下文。
新增回归测试 `RunAsync_NotifiesOnCallerSynchronizationContext`（用自定义
`SingleThreadSynchronizationContext` 验证所有通知都发生在调用方上下文）。

GUI 实测验证通过：等待中 → 转换中 77% → 已完成（libx264），
汇总"完成 1 个，失败 0 个，取消 0 个"，定位按钮可见，清除已完成生效。

**2. 缺少一键开始 / 勾选转换**：队列项新增复选框，`开始转换` 只转换勾选项；
工具栏新增"全选"复选框。未勾选项跳过但保留在队列。

**3. 队列不可管理**：新增 上移 / 下移 / 清空队列；修复单项移除（此前因上述线程 bug
连带失效）。

**4. 顺序不可调**：`MoveUp` / `MoveDown` 调整队列顺序。

取消语义修正：`取消当前` = 跳过该文件且队列继续（对应"我不想转这个了"）；
`取消全部` = 停摆（对应"我不想转了"）。原设计文档写的是前者也停摆，与用户意图矛盾。

另修复两个实现期引入的缺陷：`RemoveSelected` 的 `[RelayCommand]` 特性被插入的新命令挤掉
（导致 XAML 编译崩溃成 WMC9999 资源错误，极具误导性）；入队后的 `if` 语句被插坏。

测试 127/127 通过。

### 用户实测反馈与修复（2026-10-11 第三轮）

**1. 多文件入队无去重** → `IConversionQueue.Contains(path)` 按完整路径（忽略大小写）判重，
重复文件跳过并提示"跳过 N 个重复文件"。

**2. 全选样式与行为** → 改为带"全选"文字标签的标准复选框；去掉 Command 与 IsChecked
双向绑定并存导致的双重触发，逻辑收敛到 `AllSelected` setter（直接调用 `SetAllSelected`）。

**3. 全部停止后残留"等待中"** → `CancelAll` 现在把未启动的 Pending 项一并标记为"已取消"，
队列状态不再自相矛盾（可用"重试失败"恢复）。

**4. H.265 / AV1 / VP9 全部转换失败（最严重）** → 根因是 `Auto` 编码器解析**没有考虑容器限制**：
按 H264→H265→AV1→VP9 扫描后回落到 `libx264`，而 WebM 容器只支持 VP8/VP9/AV1，
ffmpeg 直接报 "Only VP8 or VP9 or AV1 video ... supported for WebM"。
用户看到的失败是入队时捕获的旧容器（.webm）+ Auto 解析出的 libx264。

修复：`ResolveEncoder` 跳过容器不接受的编码格式，最终回退也改为"容器允许的第一个编码格式的软件编码器"。
同时参数面板的任何变更会同步到队列中所有**待转换**项（保证"一键转成同一格式"的语义），
队列行新增目标格式摘要（如 "MKV · H.265"）让捕获的参数可见。

回归测试：`ResolveEncoder_AutoForWebM_NeverReturnsSoftwareH264`、
`ConvertAsync_AutoEncoder_ProducesPlayableOutput`(mp4/mkv/webm)、去重、取消语义、参数更新。
共 138 个测试全绿。GUI 实测：WebM+Auto → VP9（软件），非法组合自动纠正。

### 队列管理重构（2026-10-11 第四轮）

参考 TransCoda4Windows（WinUI 3 同栈）、ffmpeg_batch、HandBrake 的队列设计后重构：

**1. 修复"运行中勾选会被卷入当前批次"** → `RunAsync` 在**启动时快照批次**，
运行中途勾选的新项要等下一次开始转换才会被处理。回归测试：
`RunAsync_CheckingAnItemMidRun_DoesNotJoinTheCurrentBatch`。

**2. 参数改为"任务开始时捕获"（TransCoda 模式）** → 原来入队时捕获，
用户改面板对已入队项无效，是之前 H.265/AV1/VP9 失败的间接原因。
现在 `SettingsProvider` 在每个任务开始前取当前面板值，未跑的任务自动用新参数。

**3. 新增全局输出目录** → 留空 = 与源文件同目录（TransCoda 同款），
可浏览选择；变更会重算所有待转换项的输出路径。

**4. 去重提示** → 全部是重复文件时也给出反馈（原来 added=0 时提示被吞）。

队列行已显示每项目标格式（如 MKV · H.265）和实时进度。
142 个测试全绿。
