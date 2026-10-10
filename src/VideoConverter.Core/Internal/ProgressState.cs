using System.Globalization;

namespace VideoConverter.Core.Internal;

/// <summary>
/// 解析 ffmpeg `-progress pipe:1` 的 key=value 输出流。
/// 该协议是机器可读的，比解析 stderr 文本稳定。
/// </summary>
internal sealed class ProgressState
{
    public TimeSpan Processed { get; private set; }
    public double? Speed { get; private set; }
    public bool Finished { get; private set; }

    /// <summary>喂入一行输出；返回 true 表示该行产生了状态变化。</summary>
    public bool Apply(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        var key = line[..separator];
        var value = line[(separator + 1)..];

        switch (key)
        {
            case "out_time_us":
                if (TryParseMicroseconds(value, out var microseconds))
                {
                    return AdvanceProcessed(TimeSpan.FromTicks(microseconds * 10));
                }

                break;

            case "out_time_ms":
                if (TryParseMilliseconds(value, out var milliseconds))
                {
                    return AdvanceProcessed(TimeSpan.FromMilliseconds(milliseconds));
                }

                break;

            case "speed":
                Speed = ParseSpeed(value);
                return true;

            case "progress":
                if (value == "end")
                {
                    Finished = true;
                    return true;
                }

                break;
        }

        return false;
    }

    /// <summary>已处理时长只前进不回退（进度条不允许倒退），返回是否发生更新。</summary>
    private bool AdvanceProcessed(TimeSpan candidate)
    {
        if (candidate <= Processed)
        {
            return false;
        }

        Processed = candidate;
        return true;
    }

    internal static bool TryParseMicroseconds(string value, out long microseconds) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out microseconds);

    internal static bool TryParseMilliseconds(string value, out long milliseconds) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds);

    /// <summary>speed 形如 "1.5x" 或 "N/A"。</summary>
    internal static double? ParseSpeed(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "N/A")
        {
            return null;
        }

        var trimmed = value.TrimEnd('x', 'X');
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed)
            ? speed
            : null;
    }
}
