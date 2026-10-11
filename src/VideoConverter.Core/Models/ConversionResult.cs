namespace VideoConverter.Core.Models;

/// <summary>一次转换的结果。</summary>
public sealed record ConversionResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public string? Error { get; init; }

    /// <summary>实际使用的 ffmpeg 视频编码器名（失败/取消时可能为空）。</summary>
    public string? UsedEncoder { get; init; }

    public static ConversionResult Ok(string outputPath, string? usedEncoder = null) =>
        new() { Success = true, OutputPath = outputPath, UsedEncoder = usedEncoder };

    public static ConversionResult Failed(string outputPath, int exitCode, string? error) =>
        new() { Success = false, OutputPath = outputPath, ExitCode = exitCode, Error = error };
}
