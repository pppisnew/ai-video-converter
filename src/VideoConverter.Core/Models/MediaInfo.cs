namespace VideoConverter.Core.Models;

/// <summary>探测到的媒体文件信息（来自 ffprobe）。</summary>
public sealed record MediaInfo
{
    public required string FilePath { get; init; }
    public string? ContainerFormat { get; init; }
    public TimeSpan? Duration { get; init; }
    public long? SizeBytes { get; init; }
    public long? BitRate { get; init; }
    public string? VideoCodec { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public double? FrameRate { get; init; }
    public string? AudioCodec { get; init; }
    public int? AudioChannels { get; init; }
    public int? SampleRate { get; init; }

    public bool HasVideo => VideoCodec is not null;
    public bool HasAudio => AudioCodec is not null;
}
