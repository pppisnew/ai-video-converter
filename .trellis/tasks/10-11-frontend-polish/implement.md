# 执行计划：前端优化

关联 PRD：`design.md`（本任务为纯 UI 重构，PRD 与设计合并）

## 三期改造（2026-10-12，当前）

方案与决策：`ui-optimization-v2.md`（含 P0/P1/P2 三阶段与实测诊断）。

### P0 清单（已完成）

- [x] 窗口初始尺寸按 DPI 换算（物理像素 = DIP x scale），并用显示器工作区夹取
- [x] 固定底部操作条：置于 ConvertPage 的 ScrollViewer 之外（进度条 + 批次提示 + 三个按钮）
- [x] 转换页空态重做：只保留居中拖放区与选择按钮，移除 `-` 占位与不可用控件
- [x] 头部去重：页内标题删除，改由窗口 HeaderTitle 显示；ffmpeg 状态收成药丸
- [x] 导航自适应：PaneDisplayMode=Auto + 600/1000 DIP 阈值；导航项补 AutomationId
- [x] 进度区空闲隐藏（不再与底部状态栏重复文案）
- [x] 缩略图 DecodePixelWidth=440
- [x] 构建 0/0 + 142 测试全绿 + 三档尺寸逐页截图验证

### P1 / P2 清单（待做）

- [ ] P1 设置页分组设置行；设计令牌；拖拽高亮；状态栏改 InfoBar；缩略图卡片信息去重
- [ ] P2 队列卡片内进度条；工具栏「更多」+ 定位下沉到行内/右键；窗口尺寸记忆；输出路径可复制

## 实施清单

1. **新建 MainWindow.xaml**：两列布局 + 卡片容器 + Expander + InfoBadge
2. **ViewModel 补充**：详细信息需要的展示字段已具备，补 `IsDetailsExpanded` /
   `IsAdvancedExpanded` 状态（如需要）与输出目录提示逻辑
3. **队列行压缩**：DataTemplate 改单行，进度并入状态文本
4. **保留全部 AutomationId**（PickFileButton / ConvertButton / SelectAllCheck /
   MoveUpButton / MoveDownButton / ClearQueueButton / OutputDirBox / BrowseOutputDirButton）
5. **构建 + 跑 142 个既有测试**（Core 不受影响，但要防回归）
6. **UI 自动化冒烟**：窗口可开、关键控件在、参数摘要正确
7. **提交**

## 验证命令

```bash
dotnet build
dotnet test
```

## 风险与回退

- 纯 XAML 重构，VM 契约尽量不动；如必须动 VM，保持属性名不变
- Expander 在 Window 根下用 x:Bind 无 Converter 即安全
- 若新布局有控件不可用，回退到上一版 git 提交

## task.py start 前检查

- [x] design.md 已成稿
- [ ] 用户批准方案
