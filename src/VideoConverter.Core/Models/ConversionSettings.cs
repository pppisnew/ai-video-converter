using VideoConverter.Core.Models;

namespace VideoConverter.Core.Models;

/// <summary>一次转换的完整参数。取代 MVP 阶段的 ConvertPreset。</summary>
public sealed record ConversionSettings
{
    /// <summary>容器扩展名，如 ".mp4"。</summary>
    public required string Container { get; init; }

    public VideoEncoderChoice Encoder { get; init; } = VideoEncoderChoice.Auto;

    /// <summary>UI 质量值，0-51，越小质量越高。</summary>
    public int Crf { get; init; } = 23;

    public ResolutionScale Scale { get; init; } = ResolutionScale.Original;

    /// <summary>目标帧率；null = 保持原始帧率（不向 ffmpeg 传 -r）。</summary>
    public int? Fps { get; init; }

    public int AudioBitrateKbps { get; init; } = 128;

    /// <summary>仅音频输出（MP3），忽略视频参数。</summary>
    public bool AudioOnly { get; init; }

    /// <summary>强制软件编码（忽略硬件探测结果）。入队时随设置固化。</summary>
    public bool ForceSoftware { get; init; }
}

/// <summary>
/// 旧预设的等价工厂方法。MVP 阶段的 ConvertPreset 已废弃，
/// 调用方改用这些工厂或直接构造 ConversionSettings。
/// </summary>
public static class ConversionSettingsPresets
{
    /// <summary>应用支持的输出容器（与参数面板的下拉选项同源）。</summary>
    public static IReadOnlyList<string> Containers { get; } =
        new[] { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".mp3" };

    public static ConversionSettings Mp4() => New(".mp4");
    public static ConversionSettings Mov() => New(".mov");
    public static ConversionSettings Mkv() => New(".mkv");
    public static ConversionSettings Avi() => New(".avi");
    public static ConversionSettings Webm() => New(".webm");

    public static ConversionSettings Mp3() => new()
    {
        Container = ".mp3",
        AudioOnly = true,
    };

    public static ConversionSettings New(string container) => new() { Container = container };
}
