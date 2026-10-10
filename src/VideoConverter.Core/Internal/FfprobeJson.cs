using System.Text.Json.Serialization;

namespace VideoConverter.Core.Internal;

/// <summary>ffprobe -print_format json -show_format -show_streams 的输出结构。</summary>
internal sealed class FfprobeRoot
{
    [JsonPropertyName("format")]
    public FfprobeFormat? Format { get; set; }

    [JsonPropertyName("streams")]
    public List<FfprobeStream>? Streams { get; set; }
}

internal sealed class FfprobeFormat
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("format_name")]
    public string? FormatName { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }
}

internal sealed class FfprobeStream
{
    [JsonPropertyName("codec_type")]
    public string? CodecType { get; set; }

    [JsonPropertyName("codec_name")]
    public string? CodecName { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("avg_frame_rate")]
    public string? AvgFrameRate { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("sample_rate")]
    public string? SampleRate { get; set; }
}
