# 执行计划：批量队列、编码参数面板与硬件加速

关联 PRD：`prd.md` | 设计：`design.md`

## 实施清单（按序执行，每步可独立验证）

1. **Core 契约**：新增 `ConversionSettings` / `VideoEncoder` / `EncoderProbeResult` / `QueueItem`；
   保留 `ConvertPreset` 但标注废弃，提供 `ConversionSettingsPresets` 工厂方法；
   先跑现有测试确认迁移未破坏行为
2. **编码器描述符与参数映射**：`EncoderDescriptor` 表（编码器名 × 质量参数 × 是否硬件），
   单元测试覆盖每种编码器生成的参数列表
3. **EncoderProbeService**：真实试编码探测 + JSON 缓存 + 五类失效条件；
   单测用假 ffmpeg 目录注入验证缓存读写与失效逻辑
4. **ConversionService 改造**：入参改 `ConversionSettings`；编码器解析（Auto/指定/强制软件）；
   质量/分辨率/音频码率参数；硬件失败自动回退软件并失效缓存；
   `ConversionResult` 增加 `UsedEncoder`
5. **ConversionQueue**：严格顺序、四态状态机、失败不中断、单任务/全队列取消、
   事件通知；单测覆盖：顺序执行、失败后续继续、取消语义、重试
6. **UI 左列改队列**：ListView + 四态徽标 + 当前高亮 + 定位/重试按钮 + 移除/清除已完成
7. **UI 右列改参数面板**：容器/编码器/质量滑杆/分辨率/音频码率 + 编码器状态卡
   （当前编码器、强制软件开关、重新探测）+ **参数摘要条 + 重置默认**
8. **中列联动**：选中队列项展示其媒体信息与缩略图
9. **端到端验证**：多文件队列、H.265/AV1/VP9 软件编码、分辨率缩放（ffprobe 验证）、
   失败续跑、取消、定位输出
10. **文档与规范**：README 更新、`.trellis/spec/` 补充（队列与探测约定）、会话总结更新
11. **质量检查**：全量单测 + E2E + trellis-check
12. **提交**：按 Phase 3.4 批量提交计划

## 验证命令

```bash
dotnet build
dotnet test
dotnet publish src/VideoConverter/VideoConverter.csproj -c Release -o publish/win-x64
```

样本视频：继续用 `C:\Users\24774\Videos\Captures\` 下的 mp4（用户已放置），
单元测试内仍用 lavfi 合成。

## 风险文件与回退点

- 步骤 1 是契约变更点：先确保旧测试全绿再往前
- 步骤 4 的编码器解析逻辑与步骤 2 的描述符表必须保持一致（单测锁定）
- 硬件路径本机不可实测，依赖参数表正确性 + 运行时回退兜底

## task.py start 前检查

- [x] prd.md / design.md / implement.md 已成稿
- [ ] 用户批准规划（重点审查：探测缓存失效策略、队列错误恢复、参数摘要 UI）
- [ ] 阻塞性开放问题为空
