# 前端 UI 参考调研（GitHub 同主题优秀案例）

调研时间：2026-10-11 | 方式：GitHub API + 源码抽样

## 参考项目

| 项目 | Stars | 语言 | 借鉴点 |
|---|---|---|---|
| Richasy/Bili.Copilot | 5.2k | C# | WinUI 3 大型桌面客户端 UI 组织：NavigationView 侧边导航 + Frame 内容区 + 卡片化列表项 + 全局 Overrides.xaml 样式表 |
| Xcube-Studio/Natsurainko.FluentLauncher | 421 | C# | WinUI 3 桌面工具信息层级：NavigationView + 面包屑 + InfoBadge 计数 + 统一圆角卡片 |
| microsoft/WinUI-Gallery | 3.7k | C# | 官方控件用法基准 |
| bluday/awesome-media-player | 35 | C# | WinUI 3 媒体类应用布局参照 |
| microsoft/microsoft-ui-xaml | 8.5k | C++ | WinUI 官方控件库 |

## 提炼出的设计模式（同主题适配）

### 1. NavigationView + Frame 取代三列硬分割
Bili.Copilot 与 FluentLauncher 都用左侧 NavigationView（可折叠）+ 右侧 Frame 内容区。
对视频转换器：左侧导航 = 转换 / 队列，右侧单页内容。比当前"左队列 + 右长滚动页"
更符合 Windows 11 应用心智。

### 2. 卡片化列表项
Bili.Copilot 的 AppItemContainerStyle：统一 CornerRadius、选中态 2px 高亮边框、
hover 背景变化。当前队列行是裸 ListView 行，缺卡片质感与选中反馈。

### 3. 全局样式表集中管理
Bili.Copilot 用 Styles/Overrides.xaml 统一覆写按钮/滚动条/容器样式，
配套 ThemeDictionaries 处理 Light/Dark/HighContrast。当前样式散落在 MainWindow.xaml 内联。

### 4. InfoBadge 做计数/状态徽标
FluentLauncher 在 NavigationViewItem 上用 InfoBadge 显示数量。可迁移到：
队列待转换数、ffmpeg 状态、完成/失败计数。

### 5. 按钮分级体系
Bili.Copilot 的按钮分级：SmallIconButtonStyle(32px) / IconButtonStyle(36px) /
CircleIconButtonStyle，透明背景 + 无边框，hover 才显背景。当前工具栏按钮偏重。

### 6. 页面滚动规范
PageScrollViewerStyle：HorizontalScrollMode=Disabled + VerticalScrollBarVisibility=Auto，
与本次布局自适应修复一致，属官方推荐写法。

### 7. 图标 + 短文字按钮
成熟应用普遍采用图标 + 短文字混排，兼顾识别度与紧凑度。

## 待实现改进（按优先级）

1. 左侧 NavigationView 导航 + 右侧内容区
2. 队列项卡片化：圆角 + 选中高亮 + hover 背景
3. 全局 Overrides 样式表 + 三主题支持
4. InfoBadge 用于队列计数与 ffmpeg 状态
5. 工具栏按钮改轻量图标按钮样式
