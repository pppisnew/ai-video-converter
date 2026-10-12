# Frontend Component Guidelines

> 项目：视频格式转换器（WinUI 3）| 更新：2026-10-10

## WinUI 3 与 WPF 的差异（踩坑记录）

| 需求 | WPF 写法 | WinUI 3 正确做法 |
|---|---|---|
| 窗口初始尺寸 | `Width/Height`（DIP） | `AppWindow.Resize(new SizeInt32(pxW, pxH))` **物理像素** + `OverlappedPresenter.PreferredMinimum*`（**DIP**）——两者单位不同，必须按 DPI 换算，见下节 |
| 禁用一整块 UI | 容器 `IsEnabled` | `Panel`/`Border` 没有 `IsEnabled`，用 `ContentControl` 包裹 |
| 保存文件对话框 | `SaveFileDialog.Filter` | `FileSavePicker.FileTypeChoices`（字典），**没有** `FileTypeFilter` |
| 文件选择器 | 直接 `ShowDialog()` | `FileOpenPicker` + `InitializeWithWindow.Initialize(picker, hwnd)` |

## 窗口尺寸与 DPI（物理像素 vs DIP）

**症状**：代码写 `AppWindow.Resize(new SizeInt32(1180, 800))`，150% 缩放下窗口只有
787x533 DIP，比设计稿窄一大截；同处的 `PreferredMinimumWidth = 900`（DIP）又管不住它，
于是"最小尺寸"和"实际尺寸"互相矛盾，主操作按钮被挤出首屏。

**原因**：WinUI 3 里两套单位混用——

- 物理像素：`AppWindow.Resize`、Win32 的 `SetWindowPos` / `GetWindowRect` /
  `GetWindowPos`、`GetDpiForWindow` 的坐标
- DIP：XAML 布局尺寸、`OverlappedPresenter.PreferredMinimum*` /
  `PreferredMaximum*`、鼠标 event 坐标

**约定**：设计尺寸用 DIP 常量表达，落盘到 API 前乘 `dpi / 96`。

```csharp
private const int DesignWidth = 1180;   // DIP
private const int DesignHeight = 800;   // DIP
private const int MinimumWidth = 900;   // DIP

[DllImport("user32.dll")]
private static extern uint GetDpiForWindow(IntPtr hwnd);

private void ConfigureWindow()
{
    var hwnd = WindowNative.GetWindowHandle(this);
    var dpi = GetDpiForWindow(hwnd);
    var scale = dpi == 0 ? 1.0 : dpi / 96.0;   // dpi==0 必须兜底

    var width = (int)Math.Round(DesignWidth * scale);
    var height = (int)Math.Round(DesignHeight * scale);

    // 不允许开出屏幕外；夹取时以最小尺寸为下限，避免"最小值反被压小"
    var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
    if (area is not null)
    {
        var margin = (int)Math.Round(24 * scale);
        width = Math.Min(width, Math.Max(area.WorkArea.Width - margin, (int)Math.Round(MinimumWidth * scale)));
        height = Math.Min(height, Math.Max(area.WorkArea.Height - margin, (int)Math.Round(MinimumHeight * scale)));
    }

    AppWindow.Resize(new SizeInt32(width, height));
    if (AppWindow.Presenter is OverlappedPresenter presenter)
    {
        presenter.PreferredMinimumWidth = MinimumWidth;    // DIP
        presenter.PreferredMinimumHeight = MinimumHeight;  // DIP
    }
}
```

> **Warning**: `PreferredMinimum*` 只约束**用户拖拽**，程序化 `Resize` / `SetWindowPos`
> 不受其限制。UI 自动化脚本若用 `SetWindowPos` 压到最小尺寸以下，可以造出应用真实运行中
> 到不了的尺寸，别据此判断布局缺陷。

## 自动化截图（WinUI 3）

**约定**：抓 WinUI 3 窗口一律用 `PrintWindow` + `PW_RENDERFULLCONTENT`，
并且调用前先声明 Per-Monitor-V2 DPI 感知。

```powershell
[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
$ok = [Shot]::PrintWindow($hwnd, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
```

**原因**：

- `CopyFromScreen` 拍的是屏幕区域，压在上层的窗口会一起进图（本项目实测被 Codex 窗口遮住右半边）。
- 不设 DPI 感知时，`GetWindowRect` 的坐标会被虚拟化：150% 缩放下得到 2/3 的尺寸，
  截图区域也跟着错位（787x533 而非 1180x800）。
- `PrintWindow` 失败时再回退 `CopyFromScreen`。

## 页面骨架约定

- **页标题归属**：三个页面各自用 `PageTitleStyle` 渲染页标题；`MainWindow` 只有导航 +
  右下浮层提示，**没有** `HeaderTitle`。（更早一版 spec 写"标题统一放窗口头部、页面不再渲染标题"，
  那条从未落地；代码与界面都按"标题在页内"自洽，2026-10-12 据此改正。）
- **主操作常驻**：主操作按钮（如「开始转换」）必须放在滚动容器**之外**的固定行。
  宽窗的做法是设置面板内部 `卡头 / 滚动区(*) / 操作条(Auto)` 三段式，面板随窗口撑满高度，
  操作条钉在面板底部——600 DIP 高、20 项队列时实测按钮仍在屏内。
  窄窗单列时页面本身要滚，按钮需要滚到面板处才可见（已知取舍，见 `总结文档.md` 第十一节）。
  空闲时隐藏进度条与进度文本，避免与底部状态栏重复文案。
- **空态只留一件事**：无数据时不显示 `-` 占位、空缩略图框和不可用控件，
  只保留引导动作（拖放区 + 选择按钮）。空态与有数据态用同一套 `Visibility` 绑定切换。

## 滚动归属随断点切换（2026-10-12）

**约定**：同一时刻只让**一个**容器当纵向滚动的"主动方"，另一方向的滚动显式关掉——否则滚轮会先喂给
内层容器，用户滚半天页面不动。

- **宽窗（≥1100 DIP）**：`PageScroll.VerticalScrollMode = Disabled`（内容按视口高度测量，
  于是 `*` 行成为真正的内部滚动容器）→ 队列 `ListView` 自己滚（`Auto` + `Auto`），
  设置面板内部 `SettingsScroll` 自己滚，主操作条在面板 `Auto` 尾行常驻。
- **窄窗（<1100 DIP，单列）**：`PageScroll` 恢复滚动，`QueueList` 与 `SettingsScroll` 的
  `VerticalScrollMode` 一并置 `Disabled`，队列行高从 `*` 退回 `Auto`，设置面板落到下一行整宽。
- 两套状态在 `ConvertPage.xaml.cs` 的 `ApplyBreakpoint()` 里按**窗口宽度**（`XamlRoot.Size.Width`）
  切换，`Loaded` 与 `SizeChanged` 都会重算；**不要**改用 `VisualStateManager`（见上文断点约定）。

**验收方式**：UIA 读 `ScrollPattern.VerticallyScrollable`——宽窗下整页必须为 `false`、
队列列表为 `true`；窄窗下整页为 `true`、两处内部容器为 `false`。只截一张图看不出滚动归属。

## 列表行：展开态与选中态解耦（2026-10-12）

- 行的"媒体信息展开"由 `QueueItem.IsExpanded` 驱动（行尾 `▼/▲`，`ExpandToggleButton`），
  **不**跟选中绑定；选中只负责高亮（`ListViewItem` 的 `Selected` 视觉态）与"右侧面板作用于哪一项"。
  `QueueItem.IsCurrent` 这类"当前选中项"标志位因此是多余的，本轮已删除——凡是"仅由 VM 写、没人读"
  的展示字段，都应视为死状态。
- 默认规则：入队与启动恢复一律收起；**切换选中**时展开新选中项；**取消选中不收起**，
  因此允许多行同时展开。展开状态不写进 `queue.json`。
- 后果：展开区的字段必须绑到**所属 item**（`QueueItem.*DetailText`），不能绑"当前选中项"的
  ViewModel 属性——否则多行展开时所有行会显示同一份数据。
- 集合重建（上移/下移会 `Queue.Clear()` 再填）会让选中项短暂变 null 再变回来，从而触发一次
  "新选中项自动展开"；重建前后要保存/还原展开集合，否则会把用户没碰过的行撑开。

**验收方式**：① 行高往返（折叠 ≈91 DIP ↔ 展开 ≈195 DIP）；② 用**真实鼠标点击**行尾按钮
（`InvokePattern` 绕过指针事件，测不出"点按钮顺带改选中"）；③ 同时展开两行后核对两行字段各自独立。

## 无障碍 / 可测试性

- 所有图标按钮必须写 `AutomationProperties.Name`（工具提示只对鼠标用户有效）
- 关键按钮额外写 `AutomationProperties.AutomationId`（ASCII 标识），
  这是 UI 自动化测试唯一可靠的定位手段——中文 Name 经命令行传递会被转码损坏

## 布局

- 单页三列（文件区 / 信息区 / 转换区），列间用 1px `DividerStrokeColorDefaultBrush` 分隔
- 不做卡片套卡片；分区用细边框，不用阴影和圆角浮层
- 桌面工具优先信息密度：标签 12-13px、`Opacity="0.6"` 做次级信息层级

## 拖放（Drag & Drop）

- `app.manifest` 必须声明 `<dpiAwareness>PerMonitorV2</dpiAwareness>`（asm.v3 块）。
  缺失时 WinUI 3 拖放会静默失效：`DragOver`/`Drop` 事件根本不触发（用户反馈"拖拽无效"的根因）。
  WinUI 3 默认模板自带该声明，手写最小 manifest 不会带。
- `AllowDrop` 放在根 Grid（整个窗口）而不是可见的拖放区上，拖歪了也能命中；
  拖放区 Border 仅作视觉提示，在 `DragOver`/`DragLeave` 里换边框色做高亮反馈。

## x:Bind 的两个硬限制（增强轮踩坑）

1. **x:Bind + Converter 的查找根不能是 Window**。只要 Window 作用域内任一处
   `{x:Bind X, Converter={StaticResource C}}`，生成代码就会调用
   `SetConverterLookupRoot(this)`，而 `Window` 不是 `FrameworkElement`，直接 CS1503。
   - 解决：把 Converter 需要的值（Brush / bool）由 ViewModel 直接暴露，XAML 只做普通绑定；
   - 或改用经典 `{Binding X, Converter=...}`（DataTemplate 内尤其好用，无此限制）。
2. **x:Bind OneWay 要求源对象支持变更通知**。绑定到普通 CLR 对象属性会报 WMC1506
   （"OneWay bindings require at least one of their steps to support raising notifications"），
   且运行时属性变化不会刷新界面。
   - 解决：列表项模型（如 `QueueItem`）实现 `INotifyPropertyChanged`，
     状态变更时对相关属性名逐个 `PropertyChanged?.Invoke`。
   - 对永不变化的展示属性（如选项标签），用经典 `{Binding}` 可避开该警告。

## 自动化测试定位约定

- 命令行/脚本里**不要出现中文字面量**：经 exec 层传递会被转码损坏（实测变成乱码），
  导致 UIA 按 Name 匹配失败。
- 所有需要自动化操作的控件必须设置 ASCII 的 `AutomationProperties.AutomationId`，
  测试脚本按 Id 定位；中文只用于人眼可见的 `AutomationProperties.Name`。
- **脚本文件本身也要带 BOM**：无 BOM 的 `.ps1` 会被 Windows PowerShell 5.1 按 ANSI 解码，
  脚本**内部**写的中文字面量同样变成乱码，按 Name 的查找会静默失败。
  工具脚本统一存成 `UTF-8 with BOM`（首三字节 `EF BB BF`）。
- **同名元素要按类型过滤**：页标题与导航项同名（转换 / 队列 / 设置）后，
  `FindFirst(Name=...)` 会命中标题 TextBlock 而不是导航项（标题没有 SelectionItem/Invoke 模式，
  表现为 "no usable pattern"）。导航项要按 `ControlType=ListItem` 过滤
  （ClassName 是全限定的 `Microsoft.UI.Xaml.Controls.NavigationViewItem`）。
  查找策略应为：`AutomationId` → 限定 `ControlType` 的 Name → 序号回退。
- **同一动作的多个入口都要给 Id**：例如"选择文件"在空态是 `PickFileButton`、
  在有文件态是 `AddFilesButton`，脚本按顺序尝试，避免依赖中文 Name。
  隐藏（`Visibility=Collapsed`）的按钮可能仍在 UIA 树里，
  `InvokePattern` 调用要 try/catch，否则会中断整个脚本。

### Shell 文件对话框（自动化不稳定，优先人工）

新版 Shell 打开对话框的遗留控件不响应自动化：`Edit`(aid=1148) 不支持 `ValuePattern`、
`打开`(aid=1) 不支持 `InvokePattern`，`WM_SETTEXT` / `BM_CLICK` 也无效。
可行但**不稳定**的路径是：鼠标点进文件名框 → `Ctrl+A` → `Ctrl+V`（剪贴板粘贴）→ 回车。
逐字 `SendKeys` 会被输入框的自动补全吞掉/改写，不推荐。

> **Warning**: 该文件名框很窄，光标在末尾时只显示**尾部**，看起来像"开头被吞掉"，
> 实际内容完整。不要据此判断输入失败——按 Home 或读回全文再判断。

## 滚轮误触（WinUI 3 已知行为）

WinUI 3 的 `ComboBox` 在**未展开**状态下也会响应 `PointerWheelChanged` 改变选中项，
`Slider` / `NumberBox` 同样会在未聚焦时响应滚轮改值。用户滚动页面时参数被静默修改。

修复：子类化并重写 `OnPointerWheelChanged`，不满足条件时置 `e.Handled = true` 且不调用 base
（见 `src/VideoConverter/Controls/NoWheelControls.cs`）：

- ComboBox：仅 `IsDropDownOpen == true` 时放行
- Slider / NumberBox：仅 `FocusState != FocusState.Unfocused` 时放行
  （WinUI 3 没有 `IsFocused` 属性）

注意：必须在**子类 override** 里拦截。在 XAML 上给元素挂 `PointerWheelChanged` 事件处理器
来不及——类的 On* 虚方法先于实例事件处理器执行。

## NumberBox 的步进按钮：不要用 Compact（2026-10-12 实测）

`NumberBox` 的 `SpinButtonPlacementMode="Compact"` 在本机 SDK（Windows App SDK 2.5.1 /
`Microsoft.WinUI.dll` 3.0.0.2609）下，控件**获得焦点**时会把步进按钮渲染成一个
**脱离控件的悬浮面板**：尺寸远大于控件、带阴影、盖住输入框里的数值，且点击其他位置不会关闭。
用户表现为"数值显示不全 + 上下按钮突兀地大 + 退不出焦点"（`Inline` 正常但会占掉输入框宽度，
`Hidden` 完全不渲染步进按钮）。

本项目的约定：参数输入框一律 `SpinButtonPlacementMode="Hidden"`，数值由用户直接键入
（Enter / 失焦提交），保留 `Minimum`/`Maximum` 校验与 `NoWheelNumberBox` 的滚轮保护。
**不要**再给这两个控件加自绘的上下按钮——用户已明确否掉（"改动太麻烦了"）。

> **验收陷阱**：`tools/ui-shot.ps1` 用的是 `PrintWindow(..., PW_RENDERFULLCONTENT)`，
> **抓不到这个悬浮面板**（截图里一切正常）。凡是"浮层/弹出物"类的渲染问题，
> 必须改用真实屏幕截图（`System.Drawing` + `CopyFromScreen`，先 `SetForegroundWindow`）复核；
> 焦点类问题可对 `AutomationId="InputBox"` 调 UIA `SetFocus()` 稳定复现，无需精确坐标点击。

## WinUI 3 陷阱：异步续体的线程亲和性（最坑的一条）

`await ... .ConfigureAwait(false)` 会让续体跑在线程池线程。如果续体里做了
**触发 `INotifyPropertyChanged.PropertyChanged`** 或操作 UI 的事，WinUI 绑定会静默丢弃
（或抛错），表现为"状态明明变了，界面就是不刷新"。

本项目踩过的坑：`ConversionQueue.RunAsync` 里对 `ConvertAsync` 用了
`ConfigureAwait(false)`，`MarkCompleted` 在线程池线程触发 `PropertyChanged`，
结果队列项永远显示"转换中"，`IsCompleted` 永远为 false（定位按钮、清除已完成全部连带失效）。

规则：
- 凡是续体中要改"被 UI 绑定的对象"状态，就**不要**用 `ConfigureAwait(false)`
- 或者显式把通知切回捕获的 `SynchronizationContext`
- 用 `SingleThreadSynchronizationContext` 写回归测试可以稳定复现和防护这类问题

## XAML 编译崩溃成 WMC9999 时的排查方向

`Xaml Internal Error error WMC9999: 未能找到任何适合于指定区域性的资源` 看起来像环境/资源问题，
实际经常是 **XAML 引用了不存在的成员**（例如 ViewModel 的 `[RelayCommand]` 特性被挤掉，
导致 `XxxCommand` 没生成）。排查步骤：

1. 先单独 `dotnet build` 看有没有 CS 错误（级联失败时 XAML 错误会掩盖真正原因）
2. 检查 XAML 里 `{x:Bind ViewModel.XxxCommand}` 对应的方法是否真的带 `[RelayCommand]`
3. 用 `git stash` / 逐个回退文件做二分，确认是 XAML 还是 C# 侧引起

另：按行号插入代码时极易把前一行（如 `[RelayCommand]` 特性）据为己有，插完必须复查 diff。

## 内容容器：不要给滚动内容根加 MaxWidth

**约定**：`ScrollViewer` 的内容根只写 `Padding`，**不写 `MaxWidth` / `HorizontalAlignment`**，
让内容宽度恒等于视口宽度。需要收窄时在**控件层**收窄（输入框 420 DIP、侧栏 400 DIP、
缩略图 220 DIP），不要给整列设上限。

**为什么**：实测在 1600x1000 DIP 窗口下，把 `MaxWidth="1280" HorizontalAlignment="Stretch"`
加在滚动内容根上会让内容整体右移并**溢出被裁**——设置行的 `*` 列吃掉 967 DIP，
右侧 `Auto` 控件列被推出窗口（UIA 里复选框矩形为 ∞，用户根本点不到）。
空态也会因此退化成"浮在右侧的孤岛"（1180 DIP 下实测中心偏右 173 DIP，
用户原话："正文部分为什么没有居中，中间区域周围有那么多空白"）。

**横向空间靠分栏消化，不靠限宽留白**：

- 宽窗（≥1120 DIP）用 `AdaptiveTrigger` 把文件卡与参数卡并排（左 `*` / 右固定 400 DIP），
  窄窗自动把参数卡堆叠到第二行
- 同一行内的卡片要设 `VerticalAlignment="Top"`，否则会被拉成等高，
  在内容较少的那张卡里留出一整段空白
- 设置类页面用"标题+说明在左、控件右对齐"的行结构，让 `*` 列自然吸收多余宽度

### 改完布局必须复测同一行其它控件的可达性

**教训**：把缩略图从 220x124 放大到 320x180 后，右侧信息列只剩约 247 DIP，
`重新截取` 按钮被挤出卡片——**截图上看不出来**，是 UIA 测出
`RecaptureButton = OFFSCREEN`（BoundingRectangle 为 ∞）才发现的。

**做法**：

- 同一行里"固定宽度控件 + `*` 列内容"组合，固定宽度改大后必须重算 `*` 列剩余空间
- 放不下的次要控件不要硬塞在窄列里，让它们单独占一整行（横跨卡片宽度）
- 验收时用 UIA 读 `BoundingRectangle`，`∞` / 超出窗口右边界都算不可达，
  不能只靠肉眼看截图

### 布局断点：不要用 VisualStateManager / AdaptiveTrigger

**实测**：把 `VisualStateGroup` + `AdaptiveTrigger` 挂在 `ScrollViewer` **内层**元素上时
触发器不生效——1000 / 800 DIP 下窄布局完全不切换，内容被裁切；同一套 setter 挂在页面
最外层 Grid 上才生效。这种"有时灵有时不灵"的差异很难在评审时看出来。

**约定**：响应式断点用代码后置实现，确定且可测。

```csharp
private const double Breakpoint = 1100;
private const double PanelWidth = 372;

// 构造函数：Loaded += OnPageLoaded; SizeChanged += OnPageSizeChanged;
private void ApplyBreakpoint()
{
    var width = XamlRoot?.Size.Width ?? 0;   // DIP，按窗口宽度判断
    if (width <= 0) { return; }              // 加载前 XamlRoot 为空，必须兜底
    var single = width < Breakpoint;
    PanelColumn.Width = single ? new GridLength(0) : new GridLength(PanelWidth);
    Grid.SetColumn(Panel, single ? 0 : 1);
    Grid.SetRow(Panel, single ? 2 : 1);
    LayoutRoot.ColumnSpacing = single ? 0 : 20;
}
```

阈值必须按**窗口宽度**（`XamlRoot.Size.Width`）判断，不要用页面 `ActualWidth`——
后者已扣掉导航栏宽度，会让断点在 1180 DIP 窗口下提前触发。

### 不要在 ViewModel 里硬编码主题画刷

**教训**：VM 里写死 `Color.FromArgb(255, 230, 230, 230)` 当"常规文字色"，在浅色主题下
等于白底白字——右下 toast 与状态文字几乎不可见，而看截图评审时容易把它当成"淡雅设计"放过。

**约定**：需要 Brush 时从主题资源解析，只留一个兜底色：

```csharp
private static Brush ThemeBrush(string key, Color fallback) =>
    Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b
        ? b
        : new SolidColorBrush(fallback);

// 关键键：TextFillColorPrimaryBrush / TextFillColorSecondaryBrush
//         SystemFillColorCriticalBrush / SystemFillColorSuccessBrush
```
