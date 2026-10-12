# 三期方案：布局与信息架构优化（2026-10-12）

二期（三页导航 + 卡片化 + 全局样式表）完成后，用户仍反馈"UI 不行"。
本方案先给出可复现的证据，再给改造点。所有尺寸均为实测值，不是观感描述。

## 一、诊断（实测证据）

### P0-1 初始窗口小于布局所需（根因，其他问题都被它放大）

`MainWindow.ConfigureWindow()` 调用 `AppWindow.Resize(new SizeInt32(1180, 800))`。
这个 API 的单位是**物理像素**，不是 DIP。本机 150% 缩放（dpi=144）：

```
initial: 1180x800 physical, dpi=144 => 787x533 DIP
```

同一处又用 DIP 设了 `PreferredMinimumWidth = 900`，两者互相矛盾；实测
`PreferredMinimum*` 只约束用户拖拽，程序化 Resize 不受它限制，于是应用**以
787x533 DIP 启动**，比设计稿所需的宽度还窄 113 DIP。

后果：导航栏吃掉 200 DIP（25%），内容列只剩 587 DIP，转换页第三张卡片与
「开始转换」按钮在首屏完全不可见。

### P0-2 主操作不在首屏

「开始转换」位于 `ScrollViewer` 底部。窗口矮时（787x533 DIP，即默认启动状态）
必须滚动才能看到。转换器的心智是"选文件 → 调参数 → 点开始"，主操作必须常驻。

### P0-3 空态展示了一批无意义控件

未选文件时，「当前文件」卡片同时显示：180x120 空缩略图占位框、"尚未选择文件"、
三行 `-`（时长/分辨率/大小）、**缩略图时间点 NumberBox**、**重新截取**按钮、
**详细信息** Expander。后三者在没有文件时点不动也不该出现。

### P0-4 标题区重复 + 噪音

- OS 标题栏写"视频格式转换器"，页内头部又写一遍（还带一个文件夹图标）
- 右上角常驻 36 字技术串 `ffmpeg 就绪 · 随包捆绑 · n9.0.1-11-ge47273f4d9-20260831`

### P0-5 导航栏固定展开，无自动折叠

`PaneDisplayMode="Left"` + `OpenPaneLength="200"`，窄窗口下恒定占用 200 DIP，
不随宽度自适应。

### P0-6 卡片堆叠与信息重复

- 转换页竖排 4 张带边框卡片，读起来是"卡片汤"而非工具界面
- 卡片头部已显示 `时长 · 分辨率 · 大小`，「详细信息」展开后又重复一遍
- 「添加文件」单独占一张卡片，内含一个按钮 + 一行说明

### P0-7 字号层缺少体系

现有 11 / 12 / 14 / 15 / 22 px 混用，无令牌。其中 11px 低于 WinUI 正文字号下限
（12px），在高 DPI 下已经偏小。

### P0-8 拖拽没有视觉反馈

`.trellis/spec/frontend/component-guidelines.md` 明确要求
`DragOver`/`DragLeave` 切换拖放区边框做高亮反馈。二期重写把 `DropZone` 元素
删掉了，现在只剩系统自带的 drag caption，窗口本身没有任何变化。

### P1-9 设置页过于稀疏

每条设置一张全宽卡片、一行一个控件；ffmpeg 的版本与路径不可见（只存在于状态串），
无法确认当前实际用的是哪份二进制。

## 二、改造方案

### 1. 骨架：固定头部 + 滚动内容区 + 固定操作条

```
┌──────────────────────────────────────────────┐
│ 顶部：页面标题            [ffmpeg 就绪] 小药丸 │  固定
├──────────┬───────────────────────────────────┤
│ 导航     │ 滚动内容区（各页自己的卡片）        │  滚动
│ (自适应) │                                   │
├──────────┴───────────────────────────────────┤
│ 底部：提示文本   [取消] [全部停止] [开始转换]  │  固定
│       ─────────── 进度条 ───────────          │
└──────────────────────────────────────────────┘
```

- 操作条常驻：`Grid.Row` 固定行，不参与滚动；进度条与批次提示（"将转换 3 个勾选任务"）
  都在这条里
- 操作条由 `MainWindow` 拥有（跨页一致），通过 VM 命令直接绑定，页面无需重复实现
- 顶部只保留"当前页标题 + ffmpeg 药丸"，删掉重复的应用名

### 2. 顶部状态药丸：短文本 + 详情下沉

- 显示 `ffmpeg 就绪` / `未找到 ffmpeg` + 状态色圆点；ToolTip 里放完整版本串
- 版本、来源、路径、"获取 ffmpeg" 按钮统一放到设置页
- 保留 `FfmpegStatusBrush` / `FfmpegWarningVisibility` 语义，避免动 VM 契约

### 3. 转换页：空态与有文件态分离

**空态**（未选任何文件）：整页只做一件事——居中拖放区

```
        ╭───────────────────────────╮
        │        ⬇ (大图标)          │
        │   把视频拖到这里            │
        │   支持 mp4/mov/mkv/avi…     │
        │   [ 选择视频文件 ]          │
        ╰───────────────────────────╯
```

不再出现缩略图占位框、`-`、缩略图时间点、重新截取、详细信息。

**有文件态**：两列（宽 ≥ 1000 DIP 时），窄时自动堆叠

| 左列（文件） | 右列（设置） |
|---|---|
| 缩略图 220x124 + 文件名 + 源路径 | 容器格式 |
| 时长 · 分辨率 · 大小 · 编码（单行） | 视频编码器 |
| 缩略图时间点 + 重新截取（次要，小号） | 质量滑杆 + 数值 |
| [详细信息 ▾]（帧率/编码/声道/码率） | [高级选项 ▾]（分辨率/音频码率） |
|  | 参数摘要卡（等宽感、强调） |

- 头部单行指标与「详细信息」去重：头部只留时长/分辨率/大小，详细信息里只保留
  帧率、视频编码、音频编码、声道数、总码率、输出路径
- 输出路径从"卡片里一行小字"改为可点击复制的小按钮（带 ToolTip）

### 4. 队列页：保留卡片，补三处

- 空闲时**隐藏进度区**（现在恒显示"队列空闲"，白占一行）
- 正在转换的项在卡片内加一条细进度条（现在只有文字百分比）
- 工具栏二级操作（重试失败项 / 清除已完成 / 清空队列）收进一个"更多"菜单，
  首行只留 全选 / 开始转换 / 取消当前 / 全部停止

### 5. 设置页：Windows 11 设置式分组行

每组一张卡片，卡内每行是"标题 + 说明"在左、控件在右，行间 1px 分隔线：

| 分组 | 行 |
|---|---|
| 编码 | 编码器状态（描述行）· 强制软件编码（ToggleSwitch）· 重新探测（按钮） |
| 输出 | 输出目录（TextBox + 浏览按钮同一行） |
| ffmpeg | 版本 · 来源 · 路径（可复制）· 打开所在目录 · 获取 ffmpeg |

### 6. 设计令牌（Overrides.xaml 里集中定义）

- 字号：`AppTitleFontSize 20` / `AppSubtitleFontSize 14` / `AppBodyFontSize 13` /
  `AppCaptionFontSize 12`（删除全部 11px）
- 间距：内容内边距 24、卡片内边距 16、卡片间距 12、行间距 8（只用 4 的倍数）
- 圆角：卡片 8、控件 4（沿用）

### 7. 窗口与导航

- 初始尺寸按 DIP 换算：`1180x800 DIP × dpi/96`；最小 900x600 DIP（保持一致）
- 记忆上次窗口尺寸与位置（写入 `%LOCALAPPDATA%`，与探测缓存同目录）
- 导航：`PaneDisplayMode="Auto"`，宽 → 展开，中 → 图标栏，窄 → 收起

### 8. 拖拽反馈

- 根 Grid 上加一个 `IsHitTestVisible="False"` 的高亮层（2px 强调色内描边 + 半透明蒙层），
  `DragOver` 显示、`DragLeave`/`Drop` 隐藏
- 文案沿用系统 drag caption（"释放以加入队列"）

### 9. 反馈消息：状态栏 → InfoBar

- 底部 12px 状态栏信息容易被忽略（"已加入 3 个文件"、"跳过 N 个重复文件"）
- 改为内容区下方的 `InfoBar`（Success/Informational/Warning/Error），可关闭；
  保留 `StatusMessage` / `StatusIsError` 绑定语义

## 三、改动范围

改动文件（预计）：

- `src/VideoConverter/MainWindow.xaml` / `.xaml.cs`：头部、操作条、InfoBar、拖拽高亮、导航自适应
- `src/VideoConverter/Views/ConvertPage.xaml` / `.xaml.cs`：空态、两列、去重、去噪
- `src/VideoConverter/Views/QueuePage.xaml`：进度区显隐、卡片进度条、更多菜单
- `src/VideoConverter/Views/SettingsPage.xaml`：分组设置行
- `src/VideoConverter/Styles/Overrides.xaml`：令牌 + 设置行样式 + InfoBar 样式
- `src/VideoConverter/ViewModels/MainViewModel.cs`：仅新增展示态属性
  （如 `HasFileSelected`、`IsQueueIdle`、`FfmpegShortStatus`、`OutputPathCopyCommand`），
  不改动既有属性名与命令名
- `src/VideoConverter/App.xaml`：如需新增转换器（BoolToVisibility 已有）

明确不做：

- 不改转换/队列/探测的 Core 逻辑，不动 `IConversionQueue`、`IConversionService` 契约
- 不做换肤、不做自定义标题栏（`ExtendContentIntoTitleBar` 风险高、收益低）
- 不加新功能（仅加"复制输出路径""打开 ffmpeg 目录"这类已有信息的可达性入口）

## 四、验收标准

- [ ] 默认启动（787x533 DIP 等效高度）下，「开始转换」按钮与进度条可见，无需滚动
- [ ] 转换页空态只出现拖放区与选择按钮，没有任何 `-` 占位与不可用控件
- [ ] 900x600 DIP 与 1600x1000 DIP 两档窗口下，三页内容均不裁切、不横向滚动
- [ ] 设置页能看到 ffmpeg 版本与路径
- [ ] 拖拽悬停时窗口有可见高亮反馈
- [ ] 既有 AutomationId 全部保留（PickFileButton / ConvertButton / SelectAllCheck /
      MoveUpButton / MoveDownButton / ClearQueueButton / OutputDirBox / BrowseOutputDirButton /
      RecaptureButton）
- [ ] 142 个既有测试保持通过；构建 0 警告 0 错误
- [ ] 每档窗口尺寸下逐页截图人工确认

## 五、实施顺序

| 阶段 | 内容 | 目的 |
|---|---|---|
| P0 | 窗口 DPI 尺寸修正、固定操作条、空态、头部去重、导航自适应 | 解决"首屏看不到主操作"这一根因 |
| P1 | 设置页分组行、设计令牌、拖拽高亮、InfoBar 反馈、卡片去重 | 观感与信息层级 |
| P2 | 队列卡片进度条 + 更多菜单、窗口尺寸记忆、可复制路径 | 打磨与效率 |

每阶段结束跑构建 + 测试 + 三档尺寸截图，P0 完成后再给用户看一次再继续。

## 六、决策记录（用户批准 2026-10-12）

- **P0 范围**：DPI 尺寸修正、固定底部操作条、转换页空态重做、头部去重、导航自适应。
  其中固定操作条必须放在 `ScrollViewer` 之外，否则改了 DPI 仍会被滚动带走。
- **导航自适应**：批准。阈值定为 `ExpandedModeThresholdWidth=1000`、
  `CompactModeThresholdWidth=600` → ≥1000 DIP 展开（图标+文字）、600–1000 DIP 图标栏
  （hover 出文字）、<600 DIP 收起（汉堡展开）。队列项 InfoBadge 保留，
  保证导航收起时仍能感知后台任务。
- **队列工具栏**：一级保留 全选 / 开始转换 / 取消当前 / 全部停止；
  重试失败项 / 清除已完成 / 清空队列 收进"更多"菜单；
  **定位到文件 下沉到队列项行内按钮 + 右键菜单**（属于单文件操作，不放全局工具栏）。
  该项与队列卡片进度条一并放在后续阶段实施，不属于 P0。
- **P1 联动**：状态栏改 InfoBar 与卡片去重需与 BUG-01 状态机修复一起验证。
- **补充验收**：
  1. 窗口放大后确认缩略图不糊，必要时给 `BitmapImage` 设 `DecodePixelWidth`；
  2. P0 完成后重跑功能验收清单，重点确认窄窗口下"开始转换"可见、
     "取消当前/全部停止"不因固定底部条而溢出屏幕。
- **P0 评审关注点**（用户指定）：首屏"开始转换"是否可见；窗口缩到 800px 时导航栏是否
  正确折叠；空态是否干净。

## 七、P0 实施与验证结果（2026-10-12）

### 实施内容

| 项 | 落点 |
|---|---|
| DPI 尺寸修正 | `MainWindow.xaml.cs` 用 `GetDpiForWindow` 求 scale，按 `1180x800 x scale` 物理像素 Resize，并用 `DisplayArea.WorkArea` 夹取（留 24 DIP 边距，不低于 900x600 DIP） |
| 固定底部操作条 | `ConvertPage.xaml` 根改为两行 Grid，操作条（进度条 + 批次提示 + 取消当前/全部停止/开始转换）在 ScrollViewer 之外 |
| 空态重做 | 无文件时只显示居中拖放区（图标 + "把视频拖到这里" + 格式说明 + 选择视频文件）；`-` 占位、空缩略图框、缩略图时间点/重新截取/详细信息全部移除 |
| 头部去重 | 页内标题删除，改由窗口头部 `HeaderTitle` 显示；ffmpeg 长版本串收成状态药丸，完整信息进 ToolTip 与设置页 |
| 导航自适应 | `PaneDisplayMode=Auto` + `CompactModeThresholdWidth=600` + `ExpandedModeThresholdWidth=1000` |
| 进度区去重 | 转换页进度条与进度文本仅 `IsRunning` 时显示，空闲不再重复底部状态栏文案 |
| 缩略图清晰度 | `BitmapImage.DecodePixelWidth = 440`（显示约 220 DIP，预留 2x） |

### 验证证据（150% DPI，实测窗口尺寸换算为 DIP）

- 启动尺寸：`1180x800 physical @144dpi` → **1180x800 DIP**（修正前为 787x533 DIP）
- 三档窗口 800x600 / 1180x800 / 1600x1000 DIP 逐页截图：无裁切、无横向滚动
- 800x600 DIP：导航正确折叠为图标栏，"开始转换"仍在首屏可见
- 空态：只有拖放区与选择按钮
- 端到端：2 个文件入队 → 一键开始 → `共 2 项 · 转换中 1 · 等待中 1` → `共 2 项 · 已完成 2`，
  状态栏 `完成 2 个，失败 0 个，取消 0 个`，产物冲突时后缀 `_converted_1` 正确，定位按钮出现
- 构建 0 警告 0 错误；142 个测试全绿
- 截图存于 `shots/`（本地目录，已在 .gitignore 忽略）

### 自动化经验（工具脚本）

1. `AppWindow.Resize`/`GetWindowRect`/`SetWindowPos` 走物理像素，`PreferredMinimum*` 走 DIP，
   混用会让"实测尺寸"和"设计尺寸"差一个缩放系数。截图脚本必须先
   `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)`，否则坐标被虚拟化（150% 下会得到 2/3）。
2. 抓 WinUI 3 窗口要用 `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=2)`；
   `CopyFromScreen` 会把压在上层的窗口一起拍进来。
3. 无 BOM 的 `.ps1` 会被 Windows PowerShell 5.1 按 ANSI 解码，脚本内的中文字面量变乱码，
   导致按 Name 的 UIA 查找静默失败；工具脚本一律存成带 BOM 的 UTF-8，且优先按 `AutomationId` 定位。
4. 页头标题与导航项同名后，`FindFirst(Name=...)` 会命中标题而非导航项；
   需按 `ControlType=ListItem`（ClassName 为全限定的 `Microsoft.UI.Xaml.Controls.NavigationViewItem`）筛选。
5. Shell 文件对话框的单行文件名框很窄：光标在末尾时只显示尾部，
   **看起来像开头被吞掉，其实是横向滚动**。剪贴板粘贴 + 回车比逐字 SendKeys 可靠。
