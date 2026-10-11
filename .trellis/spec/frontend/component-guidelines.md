# Frontend Component Guidelines

> 项目：视频格式转换器（WinUI 3）| 更新：2026-10-10

## WinUI 3 与 WPF 的差异（踩坑记录）

| 需求 | WPF 写法 | WinUI 3 正确做法 |
|---|---|---|
| 窗口初始尺寸 | `Width/Height` | `AppWindow.Resize(new SizeInt32(w,h))` + `OverlappedPresenter.PreferredMinimum*` |
| 禁用一整块 UI | 容器 `IsEnabled` | `Panel`/`Border` 没有 `IsEnabled`，用 `ContentControl` 包裹 |
| 保存文件对话框 | `SaveFileDialog.Filter` | `FileSavePicker.FileTypeChoices`（字典），**没有** `FileTypeFilter` |
| 文件选择器 | 直接 `ShowDialog()` | `FileOpenPicker` + `InitializeWithWindow.Initialize(picker, hwnd)` |

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
