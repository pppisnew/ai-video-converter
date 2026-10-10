# Frontend Development Guidelines

> Best practices for frontend development in this project (WinUI 3 / XAML / C#).

## Guidelines Index

| Guide | Description | Status |
|-------|-------------|--------|
| [Directory Structure](./directory-structure.md) | WinUI 应用布局与代码后置边界 | 已填写 |
| [Component Guidelines](./component-guidelines.md) | WinUI3 vs WPF 差异表、无障碍与自动化 Id | 已填写 |
| [State Management](./state-management.md) | MVVM 约定、x:Bind Converter 限制、进度回传 | 已填写 |
| [Hook Guidelines](./hook-guidelines.md) | 不适用（无 React 类 hooks） | 不适用 |
| [Quality Guidelines](./quality-guidelines.md) | 同 backend/quality-guidelines.md | 已填写 |
| [Type Safety](./type-safety.md) | nullable 已全局启用；XAML 用 x:Bind 编译期检查 | 已填写 |

## Pre-Development Checklist

- [ ] 新增控件：读 [component-guidelines.md](./component-guidelines.md) 的差异表
- [ ] 新增绑定：确认没有在 Window 根元素上用 Converter（见 state-management.md）
- [ ] 新增交互控件：补 `AutomationProperties.Name` 与 `AutomationId`

**Language**: All documentation should be written in **English**.
