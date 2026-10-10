using VideoConverter.Core.Internal;
using VideoConverter.Core.Services;

namespace VideoConverter.Core.Tests;

/// <summary>定位仓库 tools/ffmpeg 下的二进制，并用 lavfi 合成测试用短视频。</summary>
internal static class TestMediaFactory
{
    public static string FfmpegPath
    {
        get
        {
            var dir = LocateFfmpegDirectory();
            return Path.Combine(dir, "ffmpeg.exe");
        }
    }

    public static string FfprobePath
    {
        get
        {
            var dir = LocateFfmpegDirectory();
            return Path.Combine(dir, "ffprobe.exe");
        }
    }

    private static string LocateFfmpegDirectory()
    {
        foreach (var candidate in FfmpegLocator.WalkUpForToolsDirectory(AppContext.BaseDirectory))
        {
            if (File.Exists(Path.Combine(candidate, "ffmpeg.exe")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "未找到 ffmpeg 二进制。请先执行 tools/fetch-ffmpeg.ps1（或安装 ffmpeg 并加入 PATH）。");
    }

    /// <summary>合成一段 5 秒 320x240、15fps、带 440Hz 正弦音轨的测试视频。</summary>
    public static async Task<string> CreateSampleVideoAsync(string outputPath, int durationSeconds = 5, int width = 320, int height = 240, CancellationToken cancellationToken = default)
    {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-f", "lavfi", "-i", $"testsrc2=duration={durationSeconds}:size={width}x{height}:rate=15",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={durationSeconds}",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-ac", "2",
            "-shortest",
            outputPath,
        };

        var result = await ProcessRunner.RunAsync(FfmpegPath, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException("合成测试视频失败：" + ProcessRunner.SummarizeError(result.StandardError));
        }

        return outputPath;
    }

    public static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
