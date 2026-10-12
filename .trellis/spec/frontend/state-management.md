# Frontend State Management

> 项目：视频格式转换器（WinUI 3）| 更新：2026-10-10

## MVVM 约定

- 唯一 ViewModel：`MainViewModel`（CommunityToolkit.Mvvm 8.4，`ObservableObject`）
- 用**字段式** `[ObservableProperty] private T _x;`
  - 不要用 `public partial T X { get; set; } = 初始值;` 写法：本工具链下报 CS8050
  - csproj 中 `<NoWarn>MVVMTK0045</NoWarn>`：该警告只影响 NativeAOT 发布，本项目不做
- 计算属性（如 `HasVideo`）在依赖项变更的 `partial void OnXxxChanged` 里手动
  `OnPropertyChanged(nameof(...))`
- 异步命令用 `[RelayCommand] private async Task XxxAsync()`，框架自动管理 CanExecute

## Converter 的使用边界

**规则不是"不用 Converter"，而是"不要在 Window 根目标的 x:Bind 上用 Converter"。**

`x:Bind` + `Converter={StaticResource ...}` 若绑定根是 Window，生成代码会调用
`SetConverterLookupRoot(this)`，而 `Window` 不是 `FrameworkElement`，编译报 CS1503。
`MainWindow.xaml` 里出现任何 `Converter=` 都会触发；目前该文件确实一处都没有。

**Page / DataTemplate 里可以正常用**（查找根是 `FrameworkElement`）。本项目现有转换器
注册在 `App.xaml`：

| Key | 类型 | 用途 |
|---|---|---|
| `NotNullToBoolean` | `NotNullToBooleanConverter` | 非空判断 |
| `InverseBoolToBoolean` | `InverseBoolToVisibilityConverter` | 布尔取反 |
| `StateToGlyph` | `QueueStateToGlyphConverter` | 队列状态 → 图标字符 |
| `StateToBrush` | `QueueStateToBrushConverter` | 队列状态 → 颜色 |
| `BoolToVisibility` | `BoolToVisibilityConverter` | bool → Visibility（x:Bind 不隐式转换） |
| `BoolToVisibilityInverse` | `InverseBoolToVisibilityConverter` | 上一项的取反（空态/有数据态切换） |

MainWindow（Window 根）需要 Visibility / Brush 时，仍由 ViewModel 直接暴露属性：
`StatusBrush`、`FfmpegStatusBrush`、`FfmpegWarningVisibility`。

## 进度回传

- `Progress<T>` 在 UI 线程构造即可自动切回 UI 线程
- ViewModel 里仍用 `DispatcherQueue.HasThreadAccess` 兜底，防止非 UI 线程直连
