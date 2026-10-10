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
