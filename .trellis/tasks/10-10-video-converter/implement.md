# 执行计划：视频格式转换器（Windows 桌面端）

关联 PRD：`prd.md` | 设计：`design.md` | 任务：`.trellis/tasks/10-10-video-converter/`

## 实施清单（按序执行，每步可独立验证）

1. **环境准备**：winget 安装 .NET SDK（先试当前 LTS，失败按 design.md 风险表降级）；
   `dotnet --version` 验证
2. **脚手架最小验证**：创建 WinUI 3 项目（WinAppSDK 2.5.1），`dotnet build` 通过，
   确认 TFM 兼容性（这是后续一切的前提，失败立即按风险表切换 .NET 版本）
3. **ffmpeg 获取（捆绑前提）**：编写 `tools/fetch-ffmpeg.ps1`（BtbN win-gpl 静态构建 ->
   `tools/ffmpeg/`），执行并验证 `tools/ffmpeg/ffmpeg.exe -version`；
   二进制不进 git，构建/发布前必须已存在
4. **Core 模型与解析器**：`MediaInfo` / `ConvertPreset` / `ConversionProgress` / `ConversionResult`、
   `FfprobeJson` DTO、`ProgressParser`；**先写单测再实现**
5. **FfmpegLocator**：捆绑优先（AppContext.BaseDirectory/tools/ffmpeg -> 仓库 tools/ffmpeg）+
   PATH 回退 + 单测（临时目录注入 + PATH 注入两种场景）
6. **MediaProbeService**：ffprobe 调用 + JSON 映射 + 单测（实测样本视频）
7. **ThumbnailService**：抽帧命令 + 单测（验证输出文件存在且非空）
8. **ConversionService**：预设命令构造、进度解析、取消（Kill 进程树 + 清理残file）、
   错误摘要 + 单测（真实转换小样本，六种目标格式各至少一次）
9. **WinUI 3 UI**：MainWindow 三区布局 + MainViewModel + 拖拽/选择 + 信息展示 +
   缩略图（时间点可调）+ 转换面板 + ffmpeg 状态卡片（来源 + 版本）
10. **发布捆绑验证**：`dotnet publish` 后确认输出目录含 `tools/ffmpeg/ffmpeg.exe` 与
    `ffprobe.exe`；把发布目录拷贝到无 ffmpeg 的干净环境验证开箱即用
11. **端到端验证**：真实视频全流程（加载 -> 信息 -> 缩略图 -> 转换 -> 播放产物）
12. **文档与规范**：README（构建/运行/分发/捆绑 ffmpeg 说明）、按 3.3 更新 `.trellis/spec/`、
    更新 `docs/session-summary.md`
13. **质量检查**：全量单测 + E2E + trellis-check 逐项过
14. **提交**：按 Phase 3.4 批量提交计划执行

## 验证命令

```bash
dotnet build
dotnet test
tools/fetch-ffmpeg.ps1        # 首次环境准备（构建/发布前必须）
dotnet publish -c Release      # 验证输出目录捆绑 ffmpeg
```

样本视频：用 `ffmpeg -f lavfi -i testsrc2=duration=5:size=320x240:rate=15 -f lavfi -i sine=frequency=440:duration=5`
合成（测试与 E2E 共用，脚本内置生成逻辑）。

## 风险文件与回退点

- 步骤 2 是硬门槛：TFM 不兼容时切换 .NET 版本后重试，不带着错误配置往下走
- 步骤 4-8 全部在 Core 层，失败回退不影响 UI
- 步骤 9 若 WinUI 3 控件出现框架级问题，优先换等价控件而非改架构（MVP 控件都是成熟件）
- 步骤 10 若 publish 未带上二进制，检查 csproj 的 Content/CopyToOutputDirectory 配置

## task.py start 前检查

- [x] prd.md / design.md / implement.md 三份文档已成稿并覆盖最新决策（含 D3 捆绑策略调整）
- [ ] 用户明确批准本规划汇总
- [ ] 阻塞性开放问题为空

## 后续增强（本期不做，记录备查）

批量队列、编码参数面板（分辨率/码率/帧率/编码器）、硬件加速、剪辑、跨平台、
MSIX 安装器正式打包（本期以发布目录捆绑形态交付）。
