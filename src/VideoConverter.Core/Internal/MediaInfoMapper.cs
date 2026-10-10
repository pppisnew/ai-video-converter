using System.Globalization;
using System.Text.Json;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Internal;

/// <summary>把 ffprobe JSON 映射为 <see cref="MediaInfo"/>。纯函数，便于单测。</summary>
internal static class MediaInfoMapper
{
    public static MediaInfo FromJson(string json, string filePath)
    {
        var root = JsonSerializer.Deserialize<FfprobeRoot>(json, JsonOptions)
            ?? throw new InvalidOperationException("ffprobe 输出为空");

        var format = root.Format;
        var video = root.Streams?.FirstOrDefault(s => s.CodecType == "video");
        var audio = root.Streams?.FirstOrDefault(s => s.CodecType == "audio");

        return new MediaInfo
        {
            FilePath = filePath,
            ContainerFormat = ContainerName(format?.FormatName, filePath),
            Duration = ParseDuration(format?.Duration),
            SizeBytes = ParseLong(format?.Size),
            BitRate = ParseLong(format?.BitRate),
            VideoCodec = video?.CodecName,
            Width = video?.Width,
            Height = video?.Height,
            FrameRate = ParseFrameRate(video?.AvgFrameRate),
            AudioCodec = audio?.CodecName,
            AudioChannels = audio?.Channels,
            SampleRate = ParseInt(audio?.SampleRate),
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// format_name 形如 "mov,mp4,m4a,3gp,..."。ffprobe 对 .mp4 文件也会把 mov 列在首位，
    /// 因此优先匹配文件扩展名所对应的容器名，匹配不到再退回首个 token。
    /// </summary>
    private static string? ContainerName(string? formatName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return null;
        }

        var tokens = formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extension = System.IO.Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

        if (extension.Length > 0 && tokens.Any(token => string.Equals(token, extension, StringComparison.OrdinalIgnoreCase)))
        {
            return extension;
        }

        var first = tokens.FirstOrDefault();
        return string.IsNullOrEmpty(first) ? null : first.ToUpperInvariant();
    }

    internal static TimeSpan? ParseDuration(string? seconds)
    {
        if (string.IsNullOrWhiteSpace(seconds) || seconds == "N/A")
        {
            return null;
        }

        return double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? TimeSpan.FromSeconds(value)
            : null;
    }

    internal static long? ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "N/A")
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    internal static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "N/A")
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>avg_frame_rate 形如 "30000/1001"、"30/1" 或 "0/0"。</summary>
    internal static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "N/A" || !value.Contains('/'))
        {
            return null;
        }

        var parts = value.Split('/', 2);
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            || denominator == 0d)
        {
            return null;
        }

        return Math.Round(numerator / denominator, 3);
    }
}
