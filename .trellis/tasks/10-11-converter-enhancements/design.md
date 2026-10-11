# 技术设计：批量队列、编码参数面板与硬件加速

关联 PRD：`prd.md` | 任务：`.trellis/tasks/10-11-converter-enhancements/`

## 1. 架构总览与变更边界

MVP 三层结构不变（UI / Core / ffmpeg CLI）。本次改动集中在 Core 契约与 UI 布局：

```
src/VideoConverter.Core/
  Models/
    ConversionSettings.cs        # 新增：取代 ConvertPreset 的转换参数（含旧预设工厂方法）
    VideoEncoder.cs              # 新增：VideoCodec 枚举 + EncoderDescriptor + 质量参数映射表
    EncoderProbeResult.cs        # 新增：探测结果
    QueueItem.cs                 # 新增：队列项与状态机
    MediaInfo.cs                 # 不变
    ConversionProgress.cs        # 不变
    ConversionResult.cs          # 扩展：增加 UsedEncoder 字段
    ConvertPreset.cs             # 废弃：保留类型但不再作为转换入参（迁移期兼容）
  Services/
    IEncoderProbeService.cs      # 新增：探测 + 缓存
    EncoderProbeService.cs       # 新增
    IConversionQueue.cs          # 新增：顺序队列
    ConversionQueue.cs           # 新增
    IConversionService.cs        # 变更：ConvertAsync 入参 ConvertPreset -> ConversionSettings
    FfmpegConversionService.cs   # 变更：编码器解析、质量/分辨率/码率参数、硬件失败回退
    FfmpegLocator.cs             # 不变
    FfprobeMediaProbeService.cs  # 不变
    FfmpegThumbnailService.cs    # 不变
src/VideoConverter/
  ViewModels/MainViewModel.cs    # 重写：队列集合 + 参数面板状态 + 探测状态
  MainWindow.xaml(.cs)           # 调整：左列改队列列表，右列改参数面板
```

## 2. 核心契约变更（破坏性变更与迁移）

### 2.1 ConversionSettings 取代 ConvertPreset

```csharp
public enum VideoCodecKind { H264, H265, Av1, Vp9 }

public sealed record ConversionSettings
{
    public required string Container { get; init; }        // ".mp4" 等
    public VideoEncoderChoice Encoder { get; init; }        // Auto / H264 / H265 / Av1 / Vp9
    public int Crf { get; init; } = 23;                     // 0-51
    public ResolutionScale? Scale { get; init; }            // null = 原始
    public int AudioBitrateKbps { get; init; } = 128;
    public bool AudioOnly { get; init; }                    // MP3 时为 true
}

public enum VideoEncoderChoice { Auto, H264, H265, Av1, Vp9 }

public enum ResolutionScale { Original, P1080, P720, P480 }
```

**迁移方式**（用户指定）：旧预设保留为工厂方法，调用方改动最小：

```csharp
public static class ConversionSettingsPresets
{
    public static ConversionSettings Mp4H264Default() => new() { Container = ".mp4", Encoder = VideoEncoderChoice.H264 };
    public static ConversionSettings Mp3AudioOnly() => new() { Container = ".mp3", AudioOnly = true, ... };
    // 六个容器各一份
}
```

`IConversionService.ConvertAsync` 签名：`ConvertPreset` -> `ConversionSettings`。
返回的 `ConversionResult` 增加 `UsedEncoder`（实际使用的 ffmpeg 编码器名），
供 UI 显示"当前编码器：H.264 (NVENC)"。

### 2.2 编码器描述符、质量参数量纲与参数映射

UI 质量滑杆固定 **0-51**（与 x264 CRF 一致，用户最熟悉的量纲）。
但各编码器实际取值范围不同，必须做**量纲归一化**，否则 VP9/AV1 的 52-63 段永远调不到。

实测（`ffmpeg -h encoder=<name>`）取值范围：

| 编码器 | 质量参数 | 实际范围 | 默认 |
|---|---|---|---|
| libx264 / libx265 | `-crf` | 0-51 | 23 |
| libvpx-vp9 | `-crf` | **0-63** | 31 |
| libsvtav1 / libaom-av1 / librav1e | `-crf` | **0-63** | 35 / 32 / - |
| h264_nvenc / hevc_nvenc | `-cq` | 0-51 | 0(=auto) |
| h264_qsv / hevc_qsv | `-global_quality` | 1-51 | 25 |
| h264_amf / hevc_amf | `-qp` | 0-51 | 22 |

**归一化规则（方案 A，用户选定）**：UI 值 u ∈ [0,51]，
目标范围为 [0,63] 的编码器实际值 = `round(u * 63 / 51)`，其余编码器原样透传。

编码器描述符因此携带完整的映射信息：

```csharp
public sealed record EncoderDescriptor
{
    public required string FfmpegName { get; init; }      // "libvpx-vp9"
    public required VideoCodecKind Codec { get; init; }   // Vp9
    public required bool IsHardware { get; init; }
    public required string QualityFlag { get; init; }     // "-crf" / "-cq" / "-global_quality" / "-qp"
    public int QualityMax { get; init; } = 51;            // 51 或 63
    public string? ExtraVideoArgs { get; init; }          // 如 VP9 的 "-b:v 0"

    /// UI 的 0-51 值 -> 该编码器实际参数值
    public int MapQuality(int uiValue) =>
        QualityMax == 51 ? uiValue : (int)Math.Round(uiValue * QualityMax / 51.0);

    /// 摘要展示用：实际参数是否与 UI 值不同（参数名不同或数值被缩放）
    public bool NeedsQualityAnnotation(int uiValue) =>
        QualityFlag != "-crf" || MapQuality(uiValue) != uiValue;
}
```

**参数摘要透明化**（用户要求）：摘要中质量部分显示为 `质量 N`，
当 `NeedsQualityAnnotation` 为真时追加 `(实际 <flag去横线> <映射值>)`，例如：

- x264：`H.264 · 质量 23 · 原始分辨率 · 音频 128kbps`
- VP9：`VP9 · 质量 23 (实际 crf 28) · 原始分辨率 · 音频 128kbps`
- NVENC：`H.264 (NVENC) · 质量 23 (实际 cq 23) · 原始分辨率 · 音频 128kbps`

其余视频参数：

- 分辨率缩放：`-vf scale=-2:{h}`（保持宽高比、强制偶数）；Original 时不加
- 音频码率：`-b:a {k}k`
- MP3（AudioOnly）：加 `-vn`，忽略全部视频参数与质量滑杆（UI 置灰）

## 3. 硬件加速探测（关注点一：缓存失效策略）

### 3.1 探测方法

必须真实试编码，不查列表。对每个"编码格式 × 硬件厂商"组合执行：

```
ffmpeg -hide_banner -loglevel error -y -f lavfi -i testsrc=duration=1:size=320x240:rate=1 -c:v <enc> -f null -
```

退出码 0 = 可用。单次 < 1s，全部组合（H264/H265/AV1/VP9 × NVENC/QSV/AMF）约 12 次，总量 < 5s，
启动后异步执行，不阻塞 UI。

### 3.2 缓存

- 位置：`%LOCALAPPDATA%\VideoConverter\encoder-probe.json`
- 内容：
  ```json
  {
    "schemaVersion": 1,
    "ffmpegPath": "C:\\...\\ffmpeg.exe",
    "ffmpegVersion": "n9.0.1-11-ge47273f4d9-20260831",
    "probedAtUtc": "2026-10-11T04:30:00Z",
    "results": { "h264_nvenc": { "available": false, "reason": "Driver does not support..." } }
  }
  ```
- **失效条件（任一命中即重新探测）**：
  1. `ffmpegPath` 或 `ffmpegVersion` 与当前不一致（用户换了 ffmpeg / 跑了 fetch 脚本）
  2. `probedAtUtc` 距今超过 30 天
  3. JSON 损坏或 schemaVersion 不匹配
  4. 用户点击"重新探测"
  5. **运行时某硬件编码器实际转换失败**（自愈）：写入缓存标记为不可用并立即重探该项
- 探测进行中/失败时的 UI 表现：状态显示"正在探测编码器…"，失败不阻塞转换（按软件编码继续）

### 3.3 编码器选择逻辑

1. 用户在参数面板选 Auto / H264 / H265 / AV1 / VP9
2. Auto：按 H264 → H265 → AV1 → VP9 找第一个"该编码格式下探测可用"的硬件编码器，
   优先级 NVENC → QSV → AMF；全不可用则 libx264
3. 指定了具体编码格式：找该格式下可用的硬件编码器；不可用则用该格式的软件编码器
   （H264→libx264、H265→libx265、AV1→软件 AV1 或报错、VP9→libvpx-vp9）
4. "强制软件编码"开启时跳过所有硬件编码器
5. AV1 若无可用编码器（硬件不可用且软件编码器不存在），该选项禁用并提示

## 4. 批量队列（关注点二：错误恢复）

### 4.1 队列模型与状态机

```csharp
public enum QueueItemState { Pending, Converting, Completed, Failed }

public sealed class QueueItem
{
    public string Id { get; }                     // GUID
    public string InputPath { get; init; }
    public string OutputPath { get; set; }
    public ConversionSettings Settings { get; init; }
    public QueueItemState State { get; private set; }
    public ConversionProgress? Progress { get; private set; }
    public string? Error { get; private set; }
    public string? UsedEncoder { get; private set; }
    public MediaInfo? Info { get; init; }         // 入队时探测，供信息面板展示
}
```

### 4.2 处理策略

- **严格顺序**：同一时刻只有一个任务在转换（`SemaphoreSlim(1)` 或直接 for 循环 + await）
- **失败不中断**（用户明确要求）：单个任务失败后记录错误、标记 Failed，**继续处理后续任务**；
  队列全部跑完后汇总"完成 N 个，失败 M 个"
- 每个任务独立的 `CancellationToken`（取消单个）与队列级 `CancellationToken`（取消全部）
- 取消语义（实现期修正，更贴合两种真实意图）：
  - **取消当前** = "我不想转这个了"：仅放弃当前任务（标记为已取消），队列继续处理后续项
  - **取消全部** = "我不想转了"：取消队列级 token，当前任务中止且后续 Pending 项不再启动

### 4.3 队列操作

- 追加入队（文件选择/拖拽均可追加，不覆盖已有队列）
- 移除单个（仅 Pending/Failed/Completed 可移除，Converting 不可）
- 清除已完成
- 重试全部失败项（重置为 Pending，保留原参数）
- 完成后在资源管理器中定位输出文件（`explorer /select,"<path>"`）

### 4.4 输出文件命名冲突（用户指出的边界情况）

队列中不同目录的同名文件（`a/clip.mp4` 与 `b/clip.mp4`）转成同一格式、输出到同一目录时会互相覆盖。
规则：入队时计算输出路径，若该路径**已被本队列其他项占用**，自动追加 `_1`、`_2`… 后缀
（`clip.mp4` -> `clip_1.mp4` -> `clip_2.mp4`）；后缀序号在队列内单调递增，移除项后不回收序号
（避免用户困惑"刚才还是 _1"）。已存在于磁盘但未被队列占用的同名文件同样递增，绝不静默覆盖。

### 4.5 取消后的残留文件

取消当前任务时**删除**已写入部分的不完整输出（沿用 MVP 的 `DeletePartialOutput` 行为），
否则用户会在输出目录看到一堆无法播放的残缺文件。删除失败（文件被占用）时静默忽略。
该行为同时适用于单任务取消与"取消全部"。
### 4.4 事件

`ConversionQueue` 通过事件通知 UI：`ItemStateChanged`、`ItemProgressChanged`、`QueueFinished`。
ViewModel 订阅后用 `DispatcherQueue.TryEnqueue` 切回 UI 线程。

## 5. UI 设计（关注点三：参数摘要呈现）

三列布局调整为：

- **左列**：选择视频按钮 + 拖放区（追加入队）+ **队列列表**
  （ListView，每项显示：序号、文件名、状态徽标、目标格式；当前任务高亮；
   已完成项显示"定位"按钮；失败项显示错误tooltip与"重试"）
- **中列**：选中队列项的媒体信息 + 缩略图（时间点可调）
- **右列**：
  - 编码器状态卡：`当前编码器：H.264 (NVENC)` 或 `H.264 (软件)`，
    附"强制软件编码"开关与"重新探测"按钮；探测中显示"正在探测编码器…"
  - 参数面板：容器格式 / 视频编码器 / 质量滑杆(0-51) / 分辨率 / 音频码率
  - **参数摘要条**：实时单行文本，如 `H.265 · CRF 23 · 原始分辨率 · 音频 128kbps`，
    随任何参数变动即时刷新；下方一个"重置为默认"按钮
  - 输出路径（默认与源文件同目录，可改）
  - 开始转换 / 取消按钮 + 总进度（`3/8 · 当前 45%`）

交互约束：无队列项时禁用开始转换；转换中禁用参数面板与入队操作；
完成后汇总提示。

## 6. 兼容性与回退

- MVP 全部行为必须保留（R9/AC10）：单文件场景 = 队列长度为 1，行为一致
- 软件编码路径全量可测（本机无可用硬件编码器）；硬件路径按 research 中的参数表实现，
  并通过"运行时失败自动回退软件"保证不会让用户拿到失败任务
- 回退点：Core 契约变更先改并跑通旧测试（迁移期），再改 UI

## 7. 风险

| 风险 | 影响 | 缓解 |
|---|---|---|
| 硬件编码器参数在各家驱动上行为差异 | 硬件路径产出异常 | 参数表按官方文档实现 + 运行时失败自动回退软件并失效缓存 |
| AV1 无软件编码器可用 | AV1 选项不可用 | 探测时确认；不可用则禁用该选项并提示 |
| 队列 UI 与选中项信息联动复杂 | 状态错乱 | 单一 SelectedItem 驱动中列，所有变更走队列事件 |
| 探测耗时阻塞启动 | 启动慢 | 异步 + 缓存，UI 立即可用 |
