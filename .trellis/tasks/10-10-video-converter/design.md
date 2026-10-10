# 技术设计：视频格式转换器（Windows 桌面端）

关联 PRD：`prd.md` | 任务：`.trellis/tasks/10-10-video-converter/`

## 1. 架构总览

- 技术栈：WinUI 3（Windows App SDK 2.5.1）+ C# + .NET，MVVM（CommunityToolkit.Mvvm）
- 三层边界：
  - **UI 层**（`src/VideoConverter/`）：XAML 视图 + ViewModel，不直接触碰 ffmpeg 进程
  - **Core 层**（`src/VideoConverter.Core/`）：纯 .NET 类库，封装 ffmpeg 定位、ffprobe 解析、
     缩略图截取、转换执行；无 UI 依赖，可完整单测
  - **进程层**（Core 内部）：通过 `System.Diagnostics.Process` 管理 ffmpeg.exe / ffprobe.exe
- 选择理由：把"与 ffmpeg CLI 交互"的全部逻辑收在可单测的 Core 层，UI 只做绑定；
     后续加批量队列时只需在 UI 层扩展，Core 契约不变。

## 2. 目录结构

```
src/VideoConverter.Core/                 # 类库：服务与模型（无 UI 依赖）
  Models/MediaInfo.cs                    # 探测结果模型
  Models/ConvertPreset.cs                # 目标格式预设（格式->编码器映射）
  Models/ConversionProgress.cs           # 进度 DTO
  Models/ConversionResult.cs             # 转换结果 DTO
  Services/IFfmpegLocator.cs             # ffmpeg 定位契约
  Services/FfmpegLocator.cs              # 捆绑优先 + PATH 回退定位实现
  Services/IMediaProbeService.cs         # ffprobe 探测契约
  Services/FfprobeMediaProbeService.cs   # ffprobe JSON 解析实现
  Services/IThumbnailService.cs          # 缩略图契约
  Services/FfmpegThumbnailService.cs     # ffmpeg 抽帧实现
  Services/IConversionService.cs         # 转换契约
  Services/FfmpegConversionService.cs    # ffmpeg CLI 转换实现（进度/取消/错误）
  Internal/FfprobeJson.cs                # ffprobe JSON 的 System.Text.Json DTO
  Internal/ProgressParser.cs             # ffmpeg -progress 输出解析
src/VideoConverter/                      # WinUI 3 应用
  App.xaml / App.xaml.cs
  MainWindow.xaml / MainWindow.xaml.cs
  ViewModels/MainViewModel.cs            # 唯一 ViewModel（MVP 单页）
  Helpers/                               # 值转换器等 UI 辅助
tests/VideoConverter.Core.Tests/         # xUnit：解析、定位、预设、转换
tools/fetch-ffmpeg.ps1                   # 下载 BtbN 静态构建 -> tools/ffmpeg/
tools/ffmpeg/                            # ffmpeg.exe / ffprobe.exe（gitignore）
docs/session-summary.md                  # 会话总结（用户要求维护）
```

## 3. 数据流与契约

### 3.1 加载与探测

1. 用户通过 FileOpenPicker 或拖拽选择视频文件
2. `IMediaProbeService.ProbeAsync(path)` 执行：
   `ffprobe -v quiet -print_format json -show_format -show_streams "<file>"`
3. JSON 经 `FfprobeJson` DTO 反序列化，映射为 `MediaInfo`：
   容器格式、时长、文件大小、总码率、视频编码、分辨率、帧率、音频编码、声道、采样率
4. `IThumbnailService.CaptureAsync(path, at)` 执行：
   `ffmpeg -ss <t> -i "<file>" -frames:v 1 -q:v 2 -y "<temp>.jpg"`
   （`-ss` 置于 `-i` 前为快速定位 seek；默认截取时长 25% 处，用户可调整时间点重截）
5. UI 绑定展示

### 3.2 转换

- 命令模板：`ffmpeg -nostats -progress pipe:1 -y -i "<input>" -c:v <vcodec> -c:a <acodec> "<output>"`
- 进度：读 stdout 的 `key=value` 流（`out_time_ms`、`progress=continue|end`），
  结合探测到的总时长计算百分比；不使用 stderr 正则，避免格式漂移
- 取消：`CancellationToken` 触发后 Kill 进程树并删除不完整的输出文件
- 失败：退出码非 0 时取 stderr 尾部摘要写入 `ConversionResult.Error`
- 输出路径：默认源文件同目录 `<name>_converted.<ext>`，提供"另存为"覆盖

### 3.3 目标格式预设（固定，MVP 不可自定义）

| 目标格式 | 扩展名 | 视频编码 | 音频编码 |
|---|---|---|---|
| MP4 | .mp4 | libx264 | aac |
| MOV | .mov | libx264 | aac |
| MKV | .mkv | libx264 | aac |
| AVI | .avi | mpeg4 | libmp3lame |
| WebM | .webm | libvpx-vp9 | libopus |
| MP3（仅音频） | .mp3 | -（`-vn`） | libmp3lame |

### 3.4 ffmpeg 定位（`FfmpegLocator`，安装包捆绑优先）

1. **捆绑优先**：`AppContext.BaseDirectory/tools/ffmpeg/ffmpeg.exe`（发布时随包落盘，见第 5 节）
2. **开发态**：仓库根 `tools/ffmpeg/ffmpeg.exe`（由 `tools/fetch-ffmpeg.ps1` 在开发/发布前填充）
3. **系统 PATH 回退**：等价 which 的 PATH 探测（用户/开发者自备 ffmpeg 时生效）
4. 均未找到：返回未定位状态，UI 显示引导卡片（说明捆绑缺失原因 + 打开 BtbN releases 页的静态指引），
   不做运行时联网下载（2026-10-10 用户决策：竞品安装包均内置 ffmpeg，首次运行再下 150MB 体验差）。

### 3.5 关键契约（C# record）

- `MediaInfo`：FilePath, Container, Duration, SizeBytes, BitRate, VideoCodec, Width, Height,
  FrameRate, AudioCodec, AudioChannels, SampleRate
- `ConvertPreset`：Format, Extension, VideoCodec, AudioCodec, DisplayName
- `ConversionProgress`：Percent, Processed, Total, Speed
- `ConversionResult`：Success, OutputPath, Error, ExitCode

## 4. UI 设计（WinUI 3，中文界面）

单窗口单页，三个水平分区：

- **顶栏**：应用标题 + ffmpeg 状态徽标（来源目录 + 版本号；未定位时显示引导入口）
- **左列（文件区）**："选择视频"按钮 + 拖放目标区；选中后显示文件名/大小
- **中列（信息区）**：`MediaInfo` 字段网格 + 缩略图 `Image`；
  缩略图时间点输入框 + "重新截取"按钮
- **右列（转换区）**：目标格式选择（RadioButton 列表，显示编码器说明）、输出路径、
  "开始转换"/"取消"按钮、`ProgressBar` + 百分比 + 状态文本、结果提示

交互约束：转换中禁用开始按钮；无文件时转换区禁用；ffmpeg 未定位时禁用转换并提示。

## 5. 兼容性与部署

- 目标 OS：Windows 11（本机已验证）；最低 Win10 1809（WinAppSDK 底线）
- **ffmpeg 捆绑**：`tools/ffmpeg/` 下的 BtbN 静态构建在 publish 时复制到输出目录，
  随发布目录/MSIX 安装包一同分发；二进制文件本身不进 git（约 150MB），由 fetch 脚本按需拉取
- 发布形态：unpackaged（`WindowsPackageType=None`）为主，便于直接运行；
  MSIX 打包（单项目 MSIX，二进制作为应用内容自然入包）为正式安装器形态；自包含发布作为可选交付

## 6. 重要取舍

- **单文件 MVP**：不为队列预留 UI，但 Core 服务签名按"可被循环调用"设计，后续加队列零契约变更
- **`-progress pipe:1` 而非 stderr 正则**：机器可读输出，跨 ffmpeg 版本稳定
- **参数用 `ProcessStartInfo.ArgumentList`**：每个参数独立转义，避免空格/中文路径踩坑
- **固定预设而非参数面板**：控制初版范围，预设表集中在 `ConvertPreset`，扩展成本低
- **不引入 FFMpegCore（2026-10-10 决策）**：调研了 FFMpegCore（2099 stars, MIT, v5.5.0，能力覆盖探测/参数构建/截图，详见 research/dotnet-ffmpeg-modules.md），最终放弃引入。理由：需求面极窄（一次探测、一次截图、一组固定预设），库收益有限；进度与取消是最需要自己掌控的部分（已通过 DLL 元数据核实其确有 CancellationToken/NotifyOnProgress，但封装后 CLI 语义透明度下降）；其下载扩展走 ffbinaries.com 且最新仅 ffmpeg 6.1（2023 年），不如 BtbN 当前构建。Core 层保持零第三方依赖。

## 7. 风险与回退

| 风险 | 影响 | 缓解 |
|---|---|---|
| .NET 版本与 WinAppSDK 2.5.1 兼容性未实测 | 脚手架失败 | 实现第 2 步即做最小构建验证；不行则降 .NET 版本（9/8 LTS） |
| ffmpeg 二进制体积大（约 150MB）不进 git | 仓库膨胀 | fetch 脚本按需拉取；发布时复制进输出目录；CI/交付前必须执行
| ffmpeg 进度输出格式差异 | 进度不准 | 用 `-progress pipe:1` 机器可读协议 |
| 路径含空格/中文 | 进程启动失败 | `ArgumentList` 逐参数传递 |
| 无样本视频可测 | 单测/E2E 无法进行 | 用 ffmpeg testsrc 合成测试视频（脚本内置） |

回退策略：Core 层先于 UI 层落地，任一步骤失败可独立回退不影响已合入代码。

## 8. 明确不做（初版）

批量队列、编码参数精细调节、硬件加速、剪辑拼接、macOS/Linux 支持。


