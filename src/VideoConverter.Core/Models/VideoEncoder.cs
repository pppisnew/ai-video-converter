namespace VideoConverter.Core.Models;

/// <summary>视频编码格式（与容器无关的编码选择）。</summary>
public enum VideoCodecKind
{
    H264,
    H265,
    Av1,
    Vp9,
}

/// <summary>用户在参数面板选择的编码器策略。</summary>
public enum VideoEncoderChoice
{
    /// <summary>自动：优先可用硬件编码器，否则软件。</summary>
    Auto,

    H264,
    H265,
    Av1,
    Vp9,
}

public enum ResolutionScale
{
    Original,
    P1080,
    P720,
    P480,
}

/// <summary>
/// 一个具体 ffmpeg 编码器的描述：质量参数名、质量上限、额外参数。
/// 质量上限的差异（x264 是 51，VP9/AV1 是 63）在这里归一化。
/// </summary>
public sealed record EncoderDescriptor
{
    public required string FfmpegName { get; init; }
    public required VideoCodecKind Codec { get; init; }
    public required bool IsHardware { get; init; }
    public required string QualityFlag { get; init; }

    /// <summary>该编码器质量参数的实际上限（51 或 63）。</summary>
    public int QualityMax { get; init; } = 51;

    /// <summary>附加视频参数，如 VP9 需要 "-b:v 0" 才能让 crf 真正生效。</summary>
    public IReadOnlyList<string> ExtraVideoArgs { get; init; } = Array.Empty<string>();

    /// <summary>UI 的 0-51 质量值映射到该编码器的实际参数值。</summary>
    public int MapQuality(int uiValue)
    {
        var clamped = Math.Clamp(uiValue, 0, 51);
        return QualityMax == 51 ? clamped : (int)Math.Round(clamped * QualityMax / 51.0);
    }

    /// <summary>摘要里是否需要标注"实际参数"（参数名不同，或数值被缩放过）。</summary>
    public bool NeedsQualityAnnotation(int uiValue) =>
        QualityFlag != "-crf" || MapQuality(uiValue) != uiValue;

    /// <summary>摘要展示用的参数名（去掉前导横线）。</summary>
    public string QualityFlagDisplay => QualityFlag.TrimStart('-');
}

/// <summary>编码器目录：软件编码器 + 各硬件厂商候选（按优先级排列）。</summary>
public static class EncoderCatalog
{
    public static EncoderDescriptor Software(VideoCodecKind codec) => codec switch
    {
        VideoCodecKind.H264 => new()
        {
            FfmpegName = "libx264", Codec = VideoCodecKind.H264, IsHardware = false, QualityFlag = "-crf",
        },
        VideoCodecKind.H265 => new()
        {
            FfmpegName = "libx265", Codec = VideoCodecKind.H265, IsHardware = false, QualityFlag = "-crf",
        },
        VideoCodecKind.Av1 => new()
        {
            FfmpegName = "libsvtav1", Codec = VideoCodecKind.Av1, IsHardware = false,
            QualityFlag = "-crf", QualityMax = 63,
        },
        VideoCodecKind.Vp9 => new()
        {
            FfmpegName = "libvpx-vp9", Codec = VideoCodecKind.Vp9, IsHardware = false,
            QualityFlag = "-crf", QualityMax = 63, ExtraVideoArgs = new[] { "-b:v", "0" },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(codec)),
    };

    /// <summary>某编码格式下的硬件候选，按 NVENC -> QSV -> AMF 优先级排列。</summary>
    public static IReadOnlyList<EncoderDescriptor> HardwareCandidates(VideoCodecKind codec) => codec switch
    {
        VideoCodecKind.H264 => new[]
        {
            Descriptor("h264_nvenc", VideoCodecKind.H264, "-cq"),
            Descriptor("h264_qsv", VideoCodecKind.H264, "-global_quality"),
            Descriptor("h264_amf", VideoCodecKind.H264, "-qp"),
        },
        VideoCodecKind.H265 => new[]
        {
            Descriptor("hevc_nvenc", VideoCodecKind.H265, "-cq"),
            Descriptor("hevc_qsv", VideoCodecKind.H265, "-global_quality"),
            Descriptor("hevc_amf", VideoCodecKind.H265, "-qp"),
        },
        VideoCodecKind.Av1 => new[]
        {
            Descriptor("av1_nvenc", VideoCodecKind.Av1, "-cq"),
            Descriptor("av1_qsv", VideoCodecKind.Av1, "-global_quality"),
            Descriptor("av1_amf", VideoCodecKind.Av1, "-qp"),
        },
        // VP9 没有 NVENC/AMF 编码器（ffmpeg 9 起移除），仅 QSV
        VideoCodecKind.Vp9 => new[]
        {
            Descriptor("vp9_qsv", VideoCodecKind.Vp9, "-global_quality"),
        },
        _ => Array.Empty<EncoderDescriptor>(),
    };

    private static EncoderDescriptor Descriptor(string name, VideoCodecKind codec, string qualityFlag) =>
        new() { FfmpegName = name, Codec = codec, IsHardware = true, QualityFlag = qualityFlag };

    /// <summary>Auto 模式下的编码格式优先级。</summary>
    public static IReadOnlyList<VideoCodecKind> AutoPreference { get; } =
        new[] { VideoCodecKind.H264, VideoCodecKind.H265, VideoCodecKind.Av1, VideoCodecKind.Vp9 };

    /// <summary>
    /// 容器支持的视频编码器。WebM 只支持 VP8/VP9/AV1，AVI 是遗留容器，
    /// 非法组合会被 ffmpeg muxer 直接拒绝（曾因 WebM+H264 导致转换失败）。
    /// </summary>
    public static IReadOnlyList<VideoEncoderChoice> AllowedEncoders(string container) =>
        container.ToLowerInvariant() switch
        {
            ".webm" => new[] { VideoEncoderChoice.Vp9, VideoEncoderChoice.Av1 },
            ".avi" => new[] { VideoEncoderChoice.H264 },
            ".mkv" => new[] { VideoEncoderChoice.H264, VideoEncoderChoice.H265, VideoEncoderChoice.Av1, VideoEncoderChoice.Vp9 },
            ".mp3" => Array.Empty<VideoEncoderChoice>(),
            _ => new[] { VideoEncoderChoice.H264, VideoEncoderChoice.H265, VideoEncoderChoice.Av1 },
        };

    /// <summary>该容器是否接受指定编码器。</summary>
    public static bool IsCompatible(string container, VideoEncoderChoice choice) =>
        choice == VideoEncoderChoice.Auto || AllowedEncoders(container).Contains(choice);

    /// <summary>把可能非法的编码器选择修正为合法值（Auto 或首个可用项）。</summary>
    public static VideoEncoderChoice Sanitize(string container, VideoEncoderChoice choice) =>
        IsCompatible(container, choice) ? choice : VideoEncoderChoice.Auto;

    public static VideoCodecKind ToKind(VideoEncoderChoice choice) => choice switch
    {
        VideoEncoderChoice.H264 => VideoCodecKind.H264,
        VideoEncoderChoice.H265 => VideoCodecKind.H265,
        VideoEncoderChoice.Av1 => VideoCodecKind.Av1,
        VideoEncoderChoice.Vp9 => VideoCodecKind.Vp9,
        _ => VideoCodecKind.H264,
    };
}
