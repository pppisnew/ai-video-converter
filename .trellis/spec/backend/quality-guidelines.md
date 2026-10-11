# Backend Quality Guidelines

> 项目：视频格式转换器（WinUI 3 + C# + .NET 10）| 更新：2026-10-10

## 外部进程执行（ffmpeg CLI）

- 统一走 `Internal/ProcessRunner.RunAsync`，不要在服务里直接 `new Process()`
- 参数必须用 `ProcessStartInfo.ArgumentList` 逐项添加，禁止字符串拼接命令行
  （中文/空格路径会静默失败）
- Progress only from `-progress pipe:1` key=value output; never regex-parse stderr `time=`
- **Only parse `out_time_us`.** The `out_time_ms` field name is misleading: ffmpeg emits the same
  microsecond value as `out_time_us` (at 17.2s both print 17200000). Parsing it as milliseconds
  inflates progress by 1000x (symptom: a 17s clip reported as 01:49:54 at 100%).
  `ProgressState` carries a regression test for this.
- 取消：`CancellationToken` → Kill 进程树（`Kill(entireProcessTree: true)`）→ 删除不完整输出
- 错误摘要取 stderr 末尾 5 行（ffmpeg 把真正原因放在最后）

## 已知坑：unpackaged WinUI 3 的 dotnet publish

`dotnet publish` **不会**把 XAML 编译产物（`*.xbf` / `*.pri`）复制进发布目录，
但普通 `dotnet build` 会。缺文件时应用启动数秒后崩溃，事件日志为
`Microsoft.UI.Xaml.dll` + `c000027b`，且没有任何 stderr 输出，极易误判。

修复：csproj 中 `AfterTargets="Publish"` 目标从 `$(OutputPath)` 拷贝 `*.xbf;*.pri` 到 `$(PublishDir)`
（见 `src/VideoConverter/VideoConverter.csproj` 的 `CopyXamlBinariesToPublish`）。
**任何 unpackaged WinUI 3 项目发布后必须验证 XBF/PRI 是否存在。**

## NuGet 源

本机用户级 `NuGet.Config` 的 `packageSources` 为空，会清掉默认源导致 NU1100。
仓库根已放置 `NuGet.Config`（nuget.org + nuget.azure.cn 镜像，国内网络必要）。
新克隆此仓库的机器不要删除该文件。

## 依赖选择

Core 层保持零第三方依赖。评估过 FFMpegCore（2099 stars, MIT）后放弃：
需求面窄、进度与取消需自掌控、其下载扩展的 ffmpeg 版本过旧（6.1）。

## Auto 编码器选择必须考虑容器限制

按优先级扫描编码格式时，**必须先过滤掉当前容器不支持的格式**，否则最终回退值可能非法。
踩坑：`Auto` 按 H264→H265→AV1→VP9 扫描，都没命中硬件编码器时回落到 `libx264`；
若容器是 `.webm`，ffmpeg 直接拒绝（"Only VP8 or VP9 or AV1 video ... supported for WebM"），
用户看到的就是"转换失败"且错误信息指向 muxer，很难反推到编码器选择逻辑。

规则：`EncoderCatalog.IsCompatible(container, choice)` 不通过的 kind 直接 `continue`；
兜底值取 `AllowedEncoders(container).First()` 的软件编码器，取不到就返回 null。

另：参数面板变更要同步到队列中所有 Pending 项，并在队列行展示每项的目标格式，
否则用户改了面板以为生效，实际用的是入队时捕获的旧参数。
