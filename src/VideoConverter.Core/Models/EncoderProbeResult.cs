namespace VideoConverter.Core.Models;

/// <summary>单个编码器的探测结果。</summary>
public sealed record EncoderProbeResult
{
    public required string EncoderName { get; init; }
    public bool IsAvailable { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>一次完整探测的缓存快照。</summary>
public sealed class EncoderProbeSnapshot
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxAgeDays = 30;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string? FfmpegPath { get; set; }
    public string? FfmpegVersion { get; set; }
    public DateTime ProbedAtUtc { get; set; }
    public Dictionary<string, EncoderProbeResult> Results { get; set; } = new();

    /// <summary>缓存是否对当前 ffmpeg 仍然有效。</summary>
    public bool IsValidFor(string ffmpegPath, string? ffmpegVersion, DateTime nowUtc)
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            return false;
        }

        if (!string.Equals(FfmpegPath, ffmpegPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(FfmpegVersion, ffmpegVersion, StringComparison.Ordinal))
        {
            return false;
        }

        return nowUtc - ProbedAtUtc <= TimeSpan.FromDays(MaxAgeDays);
    }
}
