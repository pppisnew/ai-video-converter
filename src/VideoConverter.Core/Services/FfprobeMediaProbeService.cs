using System.Text;
using VideoConverter.Core.Internal;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

public interface IMediaProbeService
{
    Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>通过 ffprobe CLI 探测媒体信息。</summary>
public sealed class FfprobeMediaProbeService : IMediaProbeService
{
    private readonly FfmpegBinaries _binaries;

    public FfprobeMediaProbeService(FfmpegBinaries binaries)
    {
        _binaries = binaries;
    }

    public async Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("视频文件不存在", filePath);
        }

        var arguments = new List<string>
        {
            "-v", "quiet",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            filePath,
        };

        var result = await ProcessRunner.RunAsync(_binaries.FfprobePath, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var error = ProcessRunner.SummarizeError(result.StandardError);
            throw new InvalidOperationException($"ffprobe 探测失败（退出码 {result.ExitCode}）：{error}");
        }

        return MediaInfoMapper.FromJson(result.StandardOutput, filePath);
    }
}
