using VideoConverter.Core.Internal;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

public interface IConversionService
{
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        TargetFormat format,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>通过 ffmpeg CLI 执行格式转换。</summary>
public sealed class FfmpegConversionService : IConversionService
{
    private readonly FfmpegBinaries _binaries;
    private readonly IMediaProbeService _probeService;

    public FfmpegConversionService(FfmpegBinaries binaries, IMediaProbeService probeService)
    {
        _binaries = binaries;
        _probeService = probeService;
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        TargetFormat format,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("视频文件不存在", inputPath);
        }

        var preset = ConvertPreset.FromFormat(format);

        var total = TimeSpan.Zero;
        try
        {
            var info = await _probeService.ProbeAsync(inputPath, cancellationToken).ConfigureAwait(false);
            total = info.Duration ?? TimeSpan.Zero;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 探测失败不阻塞转换：进度将只展示已处理时长
            total = TimeSpan.Zero;
        }

        var arguments = new List<string>
        {
            "-hide_banner",
            "-nostats",
            "-progress", "pipe:1",
            "-y",
            "-i", inputPath,
        };

        if (preset.AudioOnly)
        {
            arguments.Add("-vn");
        }
        else
        {
            arguments.Add("-c:v");
            arguments.Add(preset.VideoCodec);
        }

        arguments.Add("-c:a");
        arguments.Add(preset.AudioCodec);
        arguments.Add(outputPath);

        var state = new ProgressState();

        try
        {
            var result = await ProcessRunner.RunAsync(
                _binaries.FfmpegPath,
                arguments,
                onOutputLine: line =>
                {
                    if (state.Apply(line))
                    {
                        progress?.Report(ConversionProgress.Create(state.Processed, total, state.Speed));
                    }
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0 && File.Exists(outputPath))
            {
                // ffmpeg 自报的结束时间可能与探测到的总时长有微小差异，取较大值保证进度不倒退
                var finalProcessed = state.Processed > total ? state.Processed : total;
                progress?.Report(ConversionProgress.Create(finalProcessed, finalProcessed, state.Speed));
                return ConversionResult.Ok(outputPath);
            }

            DeletePartialOutput(outputPath);
            return ConversionResult.Failed(outputPath, result.ExitCode, ProcessRunner.SummarizeError(result.StandardError));
        }
        catch (OperationCanceledException)
        {
            DeletePartialOutput(outputPath);
            throw;
        }
    }

    /// <summary>失败或取消时删除不完整的输出，避免用户误当成品播放。</summary>
    private static void DeletePartialOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
        catch (IOException)
        {
            // 文件被占用等场景下删除失败可接受
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}
