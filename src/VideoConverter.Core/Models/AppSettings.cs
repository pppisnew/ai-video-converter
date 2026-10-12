namespace VideoConverter.Core.Models;

/// <summary>主题偏好。System = 跟随系统。</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// 用户偏好设置，持久化到 <c>%LOCALAPPDATA%\VideoConverter\settings.json</c>。
/// 只放"重启后应该保留"的偏好；运行期状态（队列、探测缓存）不在这里。
/// </summary>
public sealed class AppSettings
{
    /// <summary>主题：跟随系统 / 浅色 / 深色。</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>默认输出容器（".mp4" / ".mkv" / …）。</summary>
    public string Container { get; set; } = ".mp4";

    /// <summary>默认质量（0-51，内部按编码器量纲归一化）。</summary>
    public int Crf { get; set; } = 23;

    /// <summary>默认分辨率缩放。</summary>
    public ResolutionScale Scale { get; set; } = ResolutionScale.Original;

    /// <summary>默认帧率；null = 保持原始帧率。</summary>
    public int? Fps { get; set; }

    /// <summary>默认音频码率（kbps）。</summary>
    public int AudioBitrateKbps { get; set; } = 128;

    /// <summary>整队成功结束后在资源管理器中定位产物。</summary>
    public bool OpenFolderAfterConversion { get; set; } = true;

    /// <summary>启动时把上次未完成的任务放回队列（一律置等待中，不自动开跑）。</summary>
    public bool RestoreQueueOnStartup { get; set; } = true;

    /// <summary>
    /// 把可能越界的值夹回合法范围。设置文件可能被手改坏，
    /// 也可能来自旧版本，进入 UI 前必须收敛。
    /// </summary>
    public AppSettings Sanitized() => new()
    {
        Theme = Enum.IsDefined(Theme) ? Theme : AppTheme.System,
        Container = string.IsNullOrWhiteSpace(Container) ? ".mp4" : Container,
        Crf = Math.Clamp(Crf, 0, 51),
        Scale = Enum.IsDefined(Scale) ? Scale : ResolutionScale.Original,
        Fps = Fps is { } fps ? Math.Clamp(fps, 1, 240) : null,
        AudioBitrateKbps = Math.Clamp(AudioBitrateKbps, 32, 512),
        OpenFolderAfterConversion = OpenFolderAfterConversion,
        RestoreQueueOnStartup = RestoreQueueOnStartup,
    };
}
