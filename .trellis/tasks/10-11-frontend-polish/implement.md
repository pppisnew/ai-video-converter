# 执行计划：前端优化

关联 PRD：`design.md`（本任务为纯 UI 重构，PRD 与设计合并）

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
