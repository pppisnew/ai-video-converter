namespace VideoConverter.Core.Models;

/// <summary>一次转换的结果。</summary>
public sealed record ConversionResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public string? Error { get; init; }

    public static ConversionResult Ok(string outputPath) =>
        new() { Success = true, OutputPath = outputPath };

    public static ConversionResult Failed(string outputPath, int exitCode, string? error) =>
        new() { Success = false, OutputPath = outputPath, ExitCode = exitCode, Error = error };
}
