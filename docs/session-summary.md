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

### 前端优化（2026-10-11）

用户反馈"控件简陋、信息密度过高、输出目录找不到"。重构：

**布局**：三列 → 两列。左侧固定为文件队列（选择/拖拽/工具栏/列表），
右侧为主内容区（最大宽度 680px 居中），从上到下三张卡片：
当前文件 / 转换设置 / 编码器状态，底部操作区。

**信息渐进展示（用官方 Expander）**：
- 文件卡片默认只显示 缩略图 + 文件名 + 时长 + 分辨率，10 项完整元数据收进"详细信息" Expander
- 设置卡片默认只显示 容器格式/视频编码器/质量 三项，分辨率/音频码率/输出目录收进"高级选项" Expander
- 队列行压缩为单行（复选框 + 文件名 + 状态）

**官方控件**：`Expander`（渐进展示）、`InfoBadge`（ffmpeg 状态）、
`Border+CornerRadius` 统一卡片容器。已核实这些控件在 WinAppSDK 2.5.1 中原生存在。

**修复**：输出目录输入框去掉误导性占位符（原来显示"与源文件同目录"被当成标签），
空态即默认值，标签改为独立说明行。

验证：构建 0 警告 0 错误，142 测试通过，Expander 展开/收起经 UIA 自动化确认正常。

### 布局自适应修复（2026-10-11）

用户反馈：窗口大小变化时右侧内容被裁切，而不是自适应收缩。

根因：右列 `ScrollViewer` 设置了 `HorizontalContentAlignment="Center"` 且允许横向溢出，
窗口变窄时卡片不收缩，超出部分被裁掉。

修复：禁用横向滚动（`HorizontalScrollMode="Disabled"` + `HorizontalScrollBarVisibility="Disabled"`），
强制内容适应视口宽度；同时去掉固定 `MaxWidth=680` 限制，改为随窗口伸缩。
已实测 1040x660 窗口下内容完整可见，无裁切。

---

## 前端重构二期：三页导航 + 队列卡片化（2026-10-12）

用户批准五步改造方案（NavigationView 骨架 → 全局样式表 → 队列页重构+BUG修复 → 转换页重排 → 轻量按钮）。
调研结论：参考 Bili.Copilot / FluentLauncher 的 NavigationView + Overrides.xaml 模式。

### 已实施

**1. NavigationView + Frame 三页骨架**（转换 / 队列 / 设置）
- `MainWindow.xaml`：顶栏（ffmpeg 状态）+ NavigationView + Frame 内容区 + 底部状态栏
- 拖拽保留在窗口根 Grid 上，任何页面都能拖入文件
- 队列导航项 InfoBadge 实时显示 运行中+等待中 数量（新增 `ActiveQueueCount` / `QueueBadgeVisibility`）

**2. 全局样式表 `Styles/Overrides.xaml`**（三主题字典 Light/Dark/HighContrast）
- 语义画刷：AppCardBackground / AppCardBackgroundHover / AppSubtleText / AppQueueItemSelected( Border)
- 统一样式：CardBorderStyle（卡片容器）、PageScrollViewerStyle（禁横向滚动）、
  PageTitleStyle / SectionHeaderStyle / SubtleTextStyle / SubtleButtonStyle / SubtleIconButtonStyle
- 队列项容器 QueueItemContainerStyle：圆角 + hover 背景 + 选中 2px 高亮边框（自绘 ControlTemplate）

**3. 队列页重构 + BUG 修复合并**
- 卡片化列表项：复选框 + 状态图标（QueueStateToGlyph/BrushConverter）+ 文件名 + 目标格式 + 状态 + 定位/删除按钮
- 一键开始（开始转换）+ 取消当前 + 全部停止；上移/下移/移除/重试失败项/清除已完成/清空队列
- 空态提示（队列为空 + 操作引导）

**4. 转换页重排**：添加文件卡片 / 当前文件卡片（缩略图+摘要+详细信息 Expander）/
转换设置卡片（容器/编码器/质量 + 高级选项 Expander）/ 操作区（开始+取消+进度+批次提示）
**5. 设置页**：编码器探测状态、强制软件编码、重新探测、输出目录、ffmpeg 来源信息

### 修复的 BUG（含 2 个既有隐患）

1. **WMC9999 XAML 编译器崩溃**：Page 的 XAML 用 `{x:Bind ViewModel.Xxx}` 但 Page 类没有
   `ViewModel` 属性 → 编译器 NRE。三个 Page 统一加 `public MainViewModel ViewModel => App.ViewModel;`
   （App.ViewModel 懒加载 + DispatcherQueue.GetForCurrentThread()）
2. **队列事件未挂接**（既有 bug）：`AttachQueueEvents()` 从未被调用 → 汇总文本/进度条/
   InfoBadge 全部不更新。MainWindow 构造函数补回调用
3. **进度条跟踪错对象**（既有 bug）：原来只在 SelectedItem == item 时更新进度，
   用户选中其他项时进度条冻结。改为始终跟踪正在转换的项
4. **全选态不同步**：单项取消勾选后全选复选框不回退；空队列时显示已选。
   新增 SyncAllSelected() 只回写展示态（不触发 SetAllSelected），
   并订阅每个 QueueItem 的 PropertyChanged（新增 TrackItem/UntrackItem）
5. **RemoveSelected 与核心队列不同步**：_queue.Remove 失败（转换中）时 UI 仍移除 → 双链脱节。
   改为公共 RemoveItem() 统一处理，失败给状态提示
6. RetryFailed 无失败项时误启动转换；ClearQueue 运行中可清空正在跑的任务 —— 都加了守卫

### 验证状态

**自动化已通过**：构建 0 错 0 警；142 单测全绿；三页渲染截图验证（空态/有文件/
转换中/已完成/设置页）；队列卡片四态、选中高亮、定位按钮、InfoBadge 计数、
完整转换流程（3 文件 libx264 成功）、冲突后缀 _converted_1、参数摘要回退注解。

**自动化不稳定转人工**（文件选择对话框）：
新版 Shell 对话框的遗留控件（Edit aid=1148 / 打开 aid=1）不响应 WM_SETTEXT/BM_CLICK，
UIA ValuePattern/InvokePattern 均不支持；键盘输入被 autocomplete 下拉吞掉首个字符；
模拟 OLE 拖拽未产生真实放置。多次卡壳后按用户规则转人工。
遗留工具：tools/ui-shot.ps1（截图，需先设 Per-Monitor-V2 DPI 感知否则坐标被虚拟化）、
tools/ui-nav.ps1（UIA 点击/选中控件）。

### 待人工验收

- 文件选择对话框选文件入队、去重提示
- 转换中汇总文本/进度条实时更新（AttachQueueEvents 修复）
- 全选/取消全选、单项移除、清除已完成、定位产物、上移/下移
- 取消当前/全部停止后的状态与残留文件处理

---

## 三期 P0：布局根因修复与首屏主操作（2026-10-12）

用户对二期结果仍不满意，要求先给方案。方案见
`.trellis/tasks/10-11-frontend-polish/ui-optimization-v2.md`（诊断 + P0/P1/P2 三阶段）。

### 根因（实测，不是观感）

`MainWindow.ConfigureWindow()` 用 `AppWindow.Resize(new SizeInt32(1180, 800))`，
该 API 的单位是**物理像素**；同一处又用 DIP 设了 `PreferredMinimumWidth = 900`，两者矛盾，
而且 `PreferredMinimum*` 只约束用户拖拽、管不住程序化 Resize。150% DPI 下实测启动尺寸：

```
initial: 1180x800 physical, dpi=144 => 787x533 DIP
```

比设计稿窄 113 DIP，导航栏又固定占 200 DIP（25%），于是「转换设置」卡片与
**「开始转换」按钮在首屏完全不可见**。这才是"UI 怎么改都不行"的来源。

### 用户批准的三项决策

1. **导航自适应**（批准）：≥1000 DIP 展开、600–1000 DIP 图标栏、<600 DIP 收起；
   队列项 InfoBadge 保留，保证导航收起时仍能感知后台任务。
2. **队列工具栏二级化**（批准）：一级只留 全选 / 开始转换 / 取消当前 / 全部停止；
   重试失败项 / 清除已完成 / 清空队列 收进「更多」；
   **定位到文件下沉到队列项行内按钮与右键菜单**（属于单文件操作，不放全局工具栏）。
3. **P0 优先级**：DPI 修正 + 固定底部操作条（必须放在 ScrollViewer 之外）+ 空态重做 +
   头部去重 + 导航自适应。

### P0 完成效果（主会话逐项截图验证）

- 启动尺寸 787x533 DIP → **1180x800 DIP**
- 「开始转换」在 800x600 / 900x600 / 1180x800 DIP 下均常驻可见
- 窗口缩到 800 DIP 时导航正确折叠为图标栏
- 转换页空态只剩居中拖放区 + 选择按钮；进度区空闲时不显示，不再与底部状态栏重复
- 页内标题删除，统一由窗口头部显示；ffmpeg 长版本串收成状态药丸，
  完整版本/来源进 ToolTip 与设置页
- 端到端：2 个文件入队 → 一键开始 → `共 2 项 · 转换中 1 · 等待中 1` → 已完成 2，
  产物冲突后缀 `_converted_1` 正确，定位按钮出现
- 构建 0 警告 0 错误；142 个测试全绿

### 工具与规范更新

- 新增 `tools/ui-verify.ps1`（按 DIP 指定多档窗口尺寸、跨页自动截图）；
  新增 `tools/ui-pick.ps1`（文件对话框辅助，脚本内已标注不稳定）
- `tools/ui-shot.ps1`、`tools/ui-nav.ps1` 改用 PrintWindow(PW_RENDERFULLCONTENT) + DPI 感知，
  并修复无 BOM 导致的中文乱码问题
- 验收截图存 `shots/`，已加入 .gitignore（`.trellis/tasks/*/shots/`）

### 踩坑记录（已同步进 frontend spec）

1. 物理像素 vs DIP：`AppWindow.Resize`/`GetWindowRect`/`SetWindowPos` 用物理像素，
   `PreferredMinimum*` 用 DIP；截图脚本必须先设 Per-Monitor-V2 DPI 感知，
   否则 150% 下拿到 2/3 的错误坐标。
2. WinUI 3 窗口截图用 `PrintWindow(..., PW_RENDERFULLCONTENT=2)`，
   `CopyFromScreen` 会把上层窗口一起拍进去。
3. 无 BOM 的 `.ps1` 会被 Windows PowerShell 5.1 按 ANSI 解码，脚本内中文字面量变乱码，
   按 Name 的 UIA 查找静默失败；工具脚本一律带 BOM 且优先按 AutomationId 定位。
4. 页头标题与导航项同名后，按 Name 查找会命中标题；需按 `ControlType=ListItem` 筛选。
5. Shell 文件对话框文件名框很窄，光标在末尾时只显示尾部，
   看起来像"开头被吞"其实是横向滚动；剪贴板粘贴 + 回车比逐字 SendKeys 可靠。

### 待做

- **P1**：设置页改分组设置行、设计令牌（字号/间距）、拖拽高亮反馈、
  状态栏改 InfoBar、缩略图卡片信息去重（与 BUG-01 状态机一起验证）
- **P2**：队列卡片内进度条、工具栏「更多」菜单 + 定位下沉、窗口尺寸记忆、输出路径可复制

---

## 三期补充：内容列规则与紧凑度（2026-10-12）

### 用户反馈

> 正文部分为什么没有居中，而且中间区域周围有那么多空白，紧凑一点，现在显得很空旷

### 实测证据（150% DPI，窗口 1180x800 DIP）

内容区中心 x=1263，空态块中心 x=1523 —— **偏右 260 物理像素（173 DIP）**，
且只占内容区 1470 像素中的 215 像素。根因：滚动内容被塞进 `MaxWidth="720"` 的窄列，
空态又是其中带固定上边距的居中子块，于是变成"浮在右侧的孤岛"。

### 修正

1. **内容容器规则**：滚动内容根只写 `Padding="24,20"`，不加 `MaxWidth`/`HorizontalAlignment`，
   宽度恒等于视口；需要收窄时在控件层收窄（输入框/侧栏 420、缩略图 220）。
2. **空态**：改为铺满内容区的拖放面板（图标 + 说明 + 选择按钮居中），留白变成可拖放区域。
3. **有文件态**：宽窗（≥1120 DIP）两列 —— 左文件卡 `*`、右参数卡 400 DIP；
   窄窗用 `AdaptiveTrigger` 把参数卡堆叠到第二行；两张卡 `VerticalAlignment="Top"`，
   避免被拉成等高留下空段。
4. **设置页**：改为 Windows 11 式行结构（标题+说明在左、控件右对齐），
   输出目录输入框限宽 420。

### 二次踩坑（值得记住）

把 `MaxWidth="1280" HorizontalAlignment="Stretch"` 直接加在滚动内容根上，
在 1600x1000 DIP 下会让内容右移并**溢出被裁**：设置行的 `*` 列吃掉 967 DIP，
右侧 `Auto` 控件列被推出窗口（UIA 里复选框矩形为 ∞，用户点不到）。
同一错误也出现在底部操作条内层 Grid（宽屏下"开始转换"会被推出可视区）。
教训：**限宽只能加在控件层，不能加在滚动内容根**；已写入 frontend spec
「内容容器：不要给滚动内容根加 MaxWidth」。

### 验收

- 1180x800：空态铺满且居中（实测偏差 <6 DIP）、有文件态两列
- 800x600：参数卡堆叠、开始转换可见
- 1600x1000：内容贴左铺满，复选框/浏览按钮矩形有限且在窗口内
- 构建 0/0，142 测试全绿
- 截图：`shots/10-compact-empty.png`、`11-compact-file.png`、`12-compact-empty.png`、
  `13-compact-file.png`、`1600x1000-0.png`、`800x600-0.png`


---

## 四期：P1–P2 按阶段验收执行（2026-10-12）

> 本节取代上文「## 当前状态」。上一节起用户改为**一个阶段一轮验收**：
> 每个 P 做完跑构建 + 全量测试 + 截图，停下来等用户验收，通过才进下一个。

### 计划与执行方式

- 计划文件：`.trellis/tasks/10-11-frontend-polish/ui-remaining-plan.md`
  （§五 是 P1–P6 顺序，§四 是已锁定决策，§七 是已砍项）
- P1 曾在用户叫停前派发过一次，半成品停在 `git stash`；本轮 `git stash pop` 复用，
  没有从零重做
- Trellis 活动任务：`10-11-frontend-polish`

### P1：缩略图配色 + 危险操作红色

- `ThumbnailBrushConverter` 由按文件名哈希改为按**行序号**取色
  （`ConverterParameter=queue` / `task` 区分转换页队列序与任务页筛选后序）
- 新增 `AppDangerSolidBackground` / `AppDangerOnSolidForeground` 三主题画刷，
  以及 `DangerIconButtonStyle` / `LargeDangerButtonStyle`
- **用户对照原型后修正了轻重**：原计划写「清空队列常驻红（满色块）」，
  原型里 `.btn.danger` 只改 `color`、底色仍是次级底。最终定为
  **红字/红图标 + 透明底，悬停才出淡红底**；转换页队列行 ✕ 保持"悬停才红"
- 实测三行色相 224.9° / 174.7° / 270.2°，不再撞色

### 原型对比的核实（重要，避免被错误结论带偏）

用户转来一份"差距清单"，称**任务列表页是只读列表、每个任务行只有一个 ×**。
逐条核对代码后该结论**不成立**：`QueuePage.xaml` 行内本就有三个按钮，
`定位输出文件`（已完成时）、`重试该任务`（失败时）、`移除该队列项`（始终），
且都按项绑定 `RevealOutputFor(item)` / `RetryItem(item)`。
用户截图里只看到 ×，是因为当时 3 个文件全是"等待中"态。

真正的缺口只有两个，都已处理：

1. **「转换中」行没有取消按钮**（原型给的是 暂停+取消；暂停已确认不做）
   → 补 `CancelItem(item)` + 行内按钮
2. **`MP4 · 自动` 是只读文本，容易被误认为可点下拉**
   → `TargetSummary` 追加 `（继承全局）`

另外核实：副标题文案本来就诚实（无 GIF），全项目无 GIF 字样，无需改动。

### P2：主题 + 设置持久化 + 逐项取消

- **Core 层新增** `AppSettings`（Theme/Container/Crf/Scale/AudioBitrateKbps，
  带 `Sanitized()` 夹取越界值）与 `AppSettingsStore`
  （`%LOCALAPPDATA%\VideoConverter\settings.json`，读失败回退默认、写失败静默忽略）
- `MainViewModel` 构造时**先读回再赋默认值**（顺序反了会在启动瞬间覆盖掉存下来的设置）；
  落盘 250ms 防抖，拖质量滑杆只写一次
- 主题：`MainWindow` 把 `RequestedTheme` 施加到**窗口内容根**
  （WinUI 3 的 Window 没有该属性）；设置页新增「界面」卡片 + 三个单选，
  选中态每次页面加载从 ViewModel 回填
- 任务列表页「转换中」行补取消按钮（队列严格顺序，取消一项即取消当前项）
- 新增 6 个单测，总计 **148/148 通过**，构建 0 警告 0 错误
- 实测：切深色内容区立即变深；`settings.json` 落盘 `"Theme": "Dark"`；
  **重启后仍是深色且单选态正确**

### 本轮踩坑（值得记住）

1. **横向排列的多个 TextBlock 无法表达"整体截断"**：给 `TargetSummary` 追加
   `（继承全局）` 后，元信息行里靠后的元素被**整个挤出视野**
   （"· 中等"消失，`自动（`被切一半）。改成单个 `QueueItem.MetaLine` 属性 +
   单个 TextBlock，超宽只在末端截断加省略号。同时删掉与缩略图角标重复的时长。
2. **`.ps1` 里不要写中文字面量**：本环境 `Set-Content -Encoding utf8` 不带 BOM，
   且用 Windows PowerShell 5.1 执行，脚本内的中文被按 ANSI 解码损坏，
   UIA 按 Name 匹配静默失败（表现为"点了没反应"）。一律用 `[char]0xXXXX` 构造。
3. **`SelectionItemPattern.Select()` 远优于合成鼠标点击**：同样点单选按钮，
   前者稳定生效，后者在设置页上完全没反应。
4. **构建前必须关掉正在运行的应用**：`VideoConverter.exe` 会锁住输出 DLL，
   `dotnet build` 重试 10 次后失败（MSB3027）。

### 遗留（建议并入 P3）

- 转换页队列行比任务列表页窄，1180 DIP 下「（继承全局）」仍被省略号吃掉
  （已加 ToolTip 保证完整信息可达）。P3 把上移/下移收进行内右键菜单后
  可腾出约 60 DIP，届时能完整显示。

### 下一步

P2 等用户验收。通过后 P3：帧率参数（Core+面板+测试）+
转换完成后打开输出文件夹 + 开源许可对话框 + 三条视觉打磨
（面板行距收紧、缩略图图标 22→28px、上移/下移收进右键菜单）。

---

## 三轮计划全部完成：P3 / P4 / P5 / P6（2026-10-12）

P1、P2 由另一模型完成后，我接手收尾 P3–P6（用户要求"快速完成"，但保留验证强度）。

| 阶段 | 内容 | 提交 |
|---|---|---|
| P3 | 帧率参数（Core `ConversionSettings.Fps` → `-r`，实测产物 r_frame_rate 30/1；面板与设置页各一处，分辨率与帧率并排）、转换完成后打开输出文件夹、开源许可对话框 | `d9985ea` |
| P4 | 队列持久化：`QueueSnapshot`/`QueueStore` → `%LOCALAPPDATA%\VideoConverter\queue.json`，只存未完成项，启动恢复为「等待中」且绝不自动开跑，跳过已缺失文件 | `d9985ea` |
| P5 | 逐项覆盖（继承全局 + 可单独覆盖，**字段级**而非整份冻结） | `09d14e2` |
| P6 | 检查更新按钮改为「打开发布页」 | `d9985ea` |

测试从 148 → 162（P3/P4）→ 178（P5）。构建始终 0 警告 0 错误。

### P5 的模型（用户批准）

覆盖只作用于**容器格式与质量**两个字段，其余（分辨率/帧率/音频码率）始终跟随全局面板：
`item.ApplyOverride(SettingsProvider?.Invoke() ?? item.Settings)`。
行内 `参数` 浮出层提供两个下拉 + "恢复继承"；面板提示语为"应用到所有未单独设置的任务"。
真机验证：覆盖行输出 `.mkv -crf 23`，继承行输出 `.mp4 -crf 18`，两者分辨率/帧率都取全局。

### 接手验收中发现并修掉的两个问题（我修的，不是子代理）

1. **容器覆盖重启后静默丢失（真 bug）**：用 `queue.json` fixture 复现——启动后行文案显示
   `→ MP4`，应用回写的 `OverrideContainer` 变成 `null`。根因是浮出层 ComboBox 的
   `SelectedItem` 双向绑定在 `ItemsSource` 赋值后会回写 null，而桥接 setter 把 null
   当成"清除覆盖"。修法：忽略 null 写入并刷新展示值 + 回归测试。
   （质量覆盖当时碰巧也是 23 才没暴露，属于典型的"看起来没问题"的隐藏数据丢失。）
2. **`（已覆盖）/（继承全局）` 标注在 1180 DIP 下不可见**：行内文字列约 250 DIP，
   去掉编码器标签后仍在标注前截断。改为状态徽章旁的独立小标签，行内元信息保留
   `大小 · 分辨率 · 原格式 → 目标容器 · 质量`（与原型一致）；三个主题字典补 `AppAccentForeground`。

### 原型对齐整体收尾

原型里画过但**经用户确认不做**的：托盘常驻、系统通知、并发任务数、语言本地化、
自定义标题栏、GIF/ProRes/保留字幕/保留章节（Core 无对应能力），均不出现在设置页，避免假开关。
设置页现为六组真实分组：转换 / 编码器 / ffmpeg / 关于 / 常规 / 界面。

### 全功能测试与会话总结（2026-10-12，收尾轮）

按用户要求做了一轮端到端全功能测试（自动化优先），结果见根目录
`总结文档.md` 第四节（14 项用例全部通过）。

测试中发现并修复两个真问题：

1. **深色主题只覆盖内容区**：`RequestedTheme` 只影响子树，而导航栏半透明底叠加的是
   跟随系统的 Mica 背景，导致深色模式下导航栏与窗口底色仍为白色。
   修法：给窗口根 Grid 加 `Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"`
   实心底；同时把深色卡片色调到原型值 `#292929`。
2. **重启恢复队列后媒体信息（缩略图）不显示**：`RestoreQueueFromSnapshot()` 恢复了项但
   没有设置 `SelectedItem`，于是 `HasSelectedItem` 为 false、「媒体信息」一直隐藏——
   需求 1"加载视频信息并显示缩略图"在重启场景静默失效。修法：恢复后
   `SelectedItem ??= Queue.FirstOrDefault()`。

**需求 1 的落实决策**（用户授权自行决定）：把「媒体信息」默认展开，使缩略图与十项字段
在选中文件后默认可见；每行真实缩略图仍列为可选升级（每文件一次取帧 + 缓存）。

同时把用户要求的会话总结写到**项目根目录** `总结文档.md`（含决策记录、用户反馈、
测试结果、问题证据链、限制与后续可选项）。

### P7 轮次：真实缩略图、行内详情、设置页两列（2026-10-12）

用户给出缩略图与媒体信息的策略分析，据此实施：
- 队列行/任务行改为**真实帧**（后台流水线 + `%LOCALAPPDATA%\VideoConverter\thumbs` 缓存，
  SHA1(路径) 命名，默认取帧时间 `max(1s, 10%)`），占位块降级为中性底（删除彩色按行取色转换器）
- 媒体信息**移入选中行**（用户圈定的位置）：192×108 主预览 + 两列 label/value 字段（值降为 12px 次级色），
  由 `QueueItem.IsCurrent` 驱动，点哪行就跟哪行；输出路径与"缩略图时间点/重新截取"在其下
- 点击缩略图 → 先选中该行，再弹出 640 宽放大预览（复用缓存文件）
- 「重新截取」改为改写该行缓存并重新触发绑定（此前更新的对象已不存在，属于失效功能）
- 设置页改为**两个独立列容器**：左列 常规/编码器/ffmpeg，右列 界面/关于，
  消除"同行矮卡片被撑出的空白带"（<920 DIP 单列时右列整块下移）

提交 `3b7a0f3`；构建 0/0，测试 178 → **191**。

### 交付整理：README 重写与全功能复跑（2026-10-12）

用户给出线上笔试的 7 条原始要求，要求按**评审视角**优化 README 与会话总结
（其中第 6 条「界面选型与客户端关联为 0 不考虑」是硬门槛，第 7 条要求"自己真的测过"）。

- README 重排为：一句话定位 + 状态行 → **七条要求逐条回应表** → 截图 → 功能 →
  环境要求 → 快速开始 → 发布 → 架构 → 验证 → 已知限制 → 文档索引；
  新增「为什么是 WinUI 3」一节，写明不选 Electron/Tauri（套壳网页正是该要求要避开的形态）
  与不选 WPF/WinForms 的理由
- 截图**全部重采**：先前的任务列表截图还是彩色渐变占位块（P7 之后已换成真实帧），与现状不符；
  本次用 `tools/ui-verify.ps1`（1180×800 DIP）+ `tools/ui-nav.ps1` 采集 6 张，存入
  `docs/screenshots/`（原先放在 `.trellis/tasks/*/shots/`，被 gitignore，GitHub 上看不到）
- 快速开始里的每条命令都实跑过：`dotnet build`（0 警告 0 错误）、`dotnet test`（191/191）、
  `dotnet run --project src/VideoConverter`（窗口正常拉起）、直接双击构建产物
- 全功能复跑：恢复 3 项队列 → 一键开始转换 → 3/3 完成；ffprobe 校验产物
  （h264、720×1280 / 1280×720、`r_frame_rate=30/1`、时长与源一致）；
  统计卡 3/0/3/0 与列表逐行一致；转换完成后 `queue.json` 只剩 `{"Items":[]}`
- 修正 `总结文档.md` 的自相矛盾（"每行真实缩略图尚未实现" 实际 P7 已实现；测试数 178 → 191），
  并补上「深色标题栏仍是系统浅色」这条真实限制
- 顺手整理 `.gitignore`：原文件有一段被字面量 `\n` 拼在一起的失效行与三份重复块，
  重写为分类清晰的一份；用「改写前后 `git status --ignored -uall` 的集合完全一致」
  验证没有意外放行任何本地产物

### 转换页布局与队列交互（2026-10-12，第十一轮）

用户给出完整方案：整页不滚、队列区独立滚动、右面板固定；齿轮换 `▼/▲`、点击行自动展开、
允许多项展开、展开状态不持久化；展开区从约 340 DIP 压到 200 DIP 以内；媒体信息分两层。
规划与决策落在 `.trellis/tasks/10-11-frontend-polish/ui-layout-scroll-expand.md`（含 7 条验收点）。

**分工**：转换页 + 展开交互 + 模型/VM 由 `trellis-implement` 子代理执行；任务列表页由主会话改
（两行改动，见下），避免同一文件并发编辑。

**实现要点**

- `ConvertPage`：`PageScroll` 只在窄窗承担整页滚动；宽窗队列列表回到自身滚动（`Grid` 行高 `*`）；
  `SettingsPanel` → `Row=1 RowSpan=3 Stretch`，内部"卡头 / `SettingsScroll` / 固定操作条"，
  600 DIP 高时参数区滚动而 `开始转换` 仍常驻（实测 `y=474`）。
- 行模板：新增 `ExpandToggleButton`（`▼` E70D / `▲` E70E，`SubtleIconButtonStyle`），
  展开区 = `120×68` 预览 + 3 行 × 2 列 12px 字段 + `输出到 <path>` 行 + 齿轮（逐项覆盖 Flyout 原样下移）；
  `缩略图时间点` / `RecaptureButton` 迁到右侧面板。
- `QueueItem`：`IsExpanded`（不落盘）、`ExpandToggleLabel`、6 个只读详情字段——
  字段必须绑所属 item 而不是 VM，否则多行展开时会显示同一份数据。
- `MainViewModel`：选中即展开（取消选中不收起）；入队与启动恢复显式收起；
  `RebuildQueueFromCore` 保存/还原展开集合，避免上移下移把没碰过的行撑开；
  删掉 12 个已无引用的 VM 级展示属性与三个辅助方法。
- **任务列表页的真实缺陷**：根是 `Grid`、外层无 `ScrollViewer`，列表却 `VerticalScrollMode="Disabled"`
  → 超过一屏的任务滚不到。改为 `Auto` 后独立滚动成立（`1180×600` 实测 `scrollable=True view=66.9%`）。

**主会话独立复测**（150% DPI，UIA 读 `BoundingRectangle`）：20 项队列滚到底后
`ConvertButton` 仍 `y=673.3 IsOffscreen=False` 而整页不可滚；展开行 194.7 DIP（≤200）；
真实鼠标点非选中行的 ▼ → 行高 26→186 且 `selected=False`；两项可同时展开；
清空后无可滚动区域、主操作置灰仍常驻。构建 0/0，测试 191 → **199**。

**留痕**：`docs/screenshots/convert-page.png` 与 `dark-theme.png` 按新界面重采；
`%LOCALAPPDATA%` 的合成 20 项队列与 16 个临时副本已清理，`queue.json` 还原为 4 项。

#### 复核（`trellis-check` 子代理）

子代理按规范逐条核查（AutomationId/Name、断点走代码后置、不给滚动内容根加 MaxWidth、
VM 画刷走主题解析、`IsExpanded` 的通知完整性、逐项覆盖 Flyout、`RecaptureButton` 作用于正确项、
勾选/全选联动、上移后展开态保持），并修掉三处：补 `RemoveItemButton` 的 AutomationId、
改正两处与现状不符的注释。

主会话据此又处理了两项判断：

- **`QueueItem.IsCurrent` 已成死状态**（只写不读，行高亮实际由 `ListViewItem` 的 `Selected` 渲染）→ 删除，
  并把右侧面板「缩略图时间点 / 重新截取」改成"存在选中项才显示"（`HasSelectedItem` 回来了，这次有绑定）。
- **「全部停止」按钮缺失**：`CancelAll` 能力与命令都在，但界面在原型对齐那轮丢了入口，
  而 README/总结一直写"取消分两级"。已在任务列表页工具条补回（`StopAllButton`，仅运行中出现，
  危险色文字），实测点击后 ffmpeg 立即结束、未处理项标记"已取消"、已完成项保留、可"重试失败项"回来。

### 控件修复：NumberBox 步进按钮（2026-10-12）

用户报"两个控件点击后数值显示不全、上下按钮突兀地大、甚至没办法退出焦点"，随后明确要求
**"取消上下键，用户要修改的时候自行填写就好"**。

- 根因：`缩略图时间点` 与 `音频码率` 两个 `NumberBox` 用的是 `SpinButtonPlacementMode="Compact"`；
  在本机 SDK（Windows App SDK 2.5.1 / `Microsoft.WinUI.dll` 3.0.0.2609）下，该模式在控件
  **获得焦点**后会把步进按钮渲染成一个**脱离控件的悬浮面板**（带阴影、尺寸远超控件），
  盖住输入框里的数值，且点击别处不关闭。与项目样式无关（`Overrides.xaml` 只有具名样式；
  换成原生 `NumberBox` 同样复现，故排除 `NoWheelNumberBox` 子类）。
- 修法：两处均改为 `SpinButtonPlacementMode="Hidden"`，取消上下键、改为直接键入；
  `Minimum/Maximum` 校验与 `NoWheelNumberBox` 的滚轮保护不变。**只改 2 个属性**，无新增样式/按钮/代码
  （曾试做自绘小步进按钮，用户否掉："改动太麻烦了"）。
- 验证：浮层类问题**必须用真实屏幕截图**（`System.Drawing` + `CopyFromScreen`，先置前台）——
  `tools/ui-shot.ps1` 的 `PrintWindow` 抓不到这个面板，会给出"看起来正常"的假结论（第一轮就误判了）；
  焦点用 UIA 对 `AutomationId="InputBox"` 调 `SetFocus()` 即可稳定复现，无需精确坐标。
  修复后两个控件聚焦时数值完整可见、无悬浮面板，点空白处焦点正常释放；构建 0 警告 0 错误。
