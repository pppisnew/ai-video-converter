namespace VideoConverter.Core.Models;

/// <summary>转换进度（由 ffmpeg -progress 输出解析得到）。</summary>
public sealed record ConversionProgress
{
    /// <summary>总时长未知时为 null（此时只能展示已处理时长）。</summary>
    public double? Percent { get; init; }
    public TimeSpan Processed { get; init; }
    public TimeSpan Total { get; init; }
    public double? Speed { get; init; }

    public static ConversionProgress Create(TimeSpan processed, TimeSpan total, double? speed) =>
        new()
        {
            Processed = processed,
            Total = total,
            Speed = speed,
            Percent = total > TimeSpan.Zero
                ? Math.Clamp(processed.TotalMilliseconds / total.TotalMilliseconds * 100d, 0d, 100d)
                : null,
        };
}
