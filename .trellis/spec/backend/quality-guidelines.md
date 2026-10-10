# Backend Quality Guidelines

> 项目：视频格式转换器（WinUI 3 + C# + .NET 10）| 更新：2026-10-10

## 外部进程执行（ffmpeg CLI）

- 统一走 `Internal/ProcessRunner.RunAsync`，不要在服务里直接 `new Process()`
- 参数必须用 `ProcessStartInfo.ArgumentList` 逐项添加，禁止字符串拼接命令行
  （中文/空格路径会静默失败）
- 进度只认 `-progress pipe:1` 的 key=value 输出，**不要**用正则解析 stderr 的 `time=`
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
