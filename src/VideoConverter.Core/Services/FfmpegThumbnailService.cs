using VideoConverter.Core.Internal;

namespace VideoConverter.Core.Services;

public interface IThumbnailService
{
    /// <summary>从视频指定时间点截取一帧，输出为图片文件，返回该文件路径。</summary>
    Task<string> CaptureAsync(string videoPath, TimeSpan at, string outputPath, CancellationToken cancellationToken = default);
}

/// <summary>通过 ffmpeg CLI 抽帧生成缩略图。</summary>
public sealed class FfmpegThumbnailService : IThumbnailService
{
    private readonly FfmpegBinaries _binaries;

    public FfmpegThumbnailService(FfmpegBinaries binaries)
    {
        _binaries = binaries;
    }

    public async Task<string> CaptureAsync(
        string videoPath,
        TimeSpan at,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("视频文件不存在", videoPath);
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // -ss 放在 -i 之前是快速定位 seek；-frames:v 1 只取一帧
        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-ss", at.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
            "-i", videoPath,
            "-frames:v", "1",
            "-q:v", "2",
            "-y",
            outputPath,
        };

        var result = await ProcessRunner.RunAsync(_binaries.FfmpegPath, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            var error = ProcessRunner.SummarizeError(result.StandardError);
            throw new InvalidOperationException($"缩略图截取失败（退出码 {result.ExitCode}）：{error}");
        }

        return outputPath;
    }
}
