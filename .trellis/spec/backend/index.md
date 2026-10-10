# Backend Development Guidelines

> Best practices for backend development in this project (C# / .NET 10, WinUI 3 desktop app).

## Guidelines Index

| Guide | Description | Status |
|-------|-------------|--------|
| [Directory Structure](./directory-structure.md) | Core 类库布局与文件放置规则 | 已填写 |
| [Error Handling](./error-handling.md) | ConversionResult vs 异常、部分产物清理 | 已填写 |
| [Quality Guidelines](./quality-guidelines.md) | ffmpeg CLI 集成规范、publish 缺口、NuGet 源 | 已填写 |
| [Database Guidelines](./database-guidelines.md) | 本项目无数据库 | 不适用 |
| [Logging Guidelines](./logging-guidelines.md) | MVP 阶段用状态栏文案；后续接 ILogger | 待补充 |

## Pre-Development Checklist

- [ ] 涉及外部进程：读 [quality-guidelines.md](./quality-guidelines.md) 的进程执行规范
- [ ] 涉及失败路径：读 [error-handling.md](./error-handling.md)
- [ ] 改动 csproj / 发布流程：确认 `CopyXamlBinariesToPublish` 目标未被破坏

**Language**: All documentation should be written in **English**.
