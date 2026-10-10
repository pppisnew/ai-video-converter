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

## 不要在 x:Bind 上用 Converter（Window 根元素）

`x:Bind` + `Converter={StaticResource ...}` 若绑定根是 Window，生成代码会调用
`SetConverterLookupRoot(this)`，而 `Window` 不是 `FrameworkElement`，编译报 CS1503。

做法：由 ViewModel 直接暴露 `Brush` / `Visibility` 属性
（见 `StatusBrush`、`FfmpegStatusBrush`、`FfmpegWarningVisibility`），XAML 直接绑定。
本项目因此没有定义任何 IValueConverter。

## 进度回传

- `Progress<T>` 在 UI 线程构造即可自动切回 UI 线程
- ViewModel 里仍用 `DispatcherQueue.HasThreadAccess` 兜底，防止非 UI 线程直连
