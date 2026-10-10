namespace VideoConverter.Core.Models;

public enum TargetFormat
{
    Mp4,
    Mov,
    Mkv,
    Avi,
    WebM,
    Mp3,
}

/// <summary>目标格式预设：容器扩展名 + 音视频编码器。MVP 固定集合，不可自定义。</summary>
public sealed record ConvertPreset
{
    public required TargetFormat Format { get; init; }
    public required string Extension { get; init; }
    public required string VideoCodec { get; init; }
    public required string AudioCodec { get; init; }
    public required string DisplayName { get; init; }
    public bool AudioOnly { get; init; }

    public static readonly IReadOnlyList<ConvertPreset> All = new[]
    {
        new ConvertPreset
        {
            Format = TargetFormat.Mp4, Extension = ".mp4",
            VideoCodec = "libx264", AudioCodec = "aac",
            DisplayName = "MP4（H.264 + AAC）",
        },
        new ConvertPreset
        {
            Format = TargetFormat.Mov, Extension = ".mov",
            VideoCodec = "libx264", AudioCodec = "aac",
            DisplayName = "MOV（H.264 + AAC）",
        },
        new ConvertPreset
        {
            Format = TargetFormat.Mkv, Extension = ".mkv",
            VideoCodec = "libx264", AudioCodec = "aac",
            DisplayName = "MKV（H.264 + AAC）",
        },
        new ConvertPreset
        {
            Format = TargetFormat.Avi, Extension = ".avi",
            VideoCodec = "mpeg4", AudioCodec = "libmp3lame",
            DisplayName = "AVI（MPEG-4 + MP3）",
        },
        new ConvertPreset
        {
            Format = TargetFormat.WebM, Extension = ".webm",
            VideoCodec = "libvpx-vp9", AudioCodec = "libopus",
            DisplayName = "WebM（VP9 + Opus）",
        },
        new ConvertPreset
        {
            Format = TargetFormat.Mp3, Extension = ".mp3",
            VideoCodec = string.Empty, AudioCodec = "libmp3lame",
            DisplayName = "MP3（仅音频）",
            AudioOnly = true,
        },
    };

    public static ConvertPreset FromFormat(TargetFormat format) =>
        All.First(preset => preset.Format == format);
}
