using VideoConverter.Core.Internal;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

/// <summary>通过 ffmpeg CLI 执行格式转换，支持编码器自动解析与硬件失败回退。</summary>
public sealed class FfmpegConversionService : IConversionService
{
    private readonly FfmpegBinaries _binaries;
    private readonly IMediaProbeService _probeService;
    private readonly IEncoderProbeService? _probeService2;

    public FfmpegConversionService(
        FfmpegBinaries binaries,
        IMediaProbeService probeService,
        IEncoderProbeService? encoderProbeService = null)
    {
        _binaries = binaries;
        _probeService = probeService;
        _probeService2 = encoderProbeService;
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("视频文件不存在", inputPath);
        }

        var total = await ProbeDurationAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var descriptor = ResolveDescriptor(settings);

        var result = await RunFfmpegAsync(inputPath, outputPath, settings, descriptor, total, progress, cancellationToken)
            .ConfigureAwait(false);

        // 硬件编码器运行时失败：标记不可用（自愈），并用软件编码重试一次
        if (!result.Success && descriptor is { IsHardware: true } && !cancellationToken.IsCancellationRequested)
        {
            _probeService2?.MarkUnavailable(descriptor.FfmpegName, result.Error ?? "运行时编码失败");
            var software = EncoderCatalog.Software(descriptor.Codec);
            result = await RunFfmpegAsync(inputPath, outputPath, settings, software, total, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private async Task<TimeSpan> ProbeDurationAsync(string inputPath, CancellationToken cancellationToken)
    {
        try
        {
            var info = await _probeService.ProbeAsync(inputPath, cancellationToken).ConfigureAwait(false);
            return info.Duration ?? TimeSpan.Zero;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return TimeSpan.Zero;
        }
    }

    private EncoderDescriptor? ResolveDescriptor(ConversionSettings settings)
    {
        if (settings.AudioOnly)
        {
            return null;
        }

        if (_probeService2 is null)
        {
            return settings.ForceSoftware
                ? SoftwareOrNull(settings.Encoder)
                : SoftwareOrNull(settings.Encoder);
        }

        return _probeService2.ResolveEncoder(settings);
    }

    private static EncoderDescriptor? SoftwareOrNull(VideoEncoderChoice choice)
    {
        try
        {
            return EncoderCatalog.Software(EncoderCatalog.ToKind(choice));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private async Task<ConversionResult> RunFfmpegAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        EncoderDescriptor? descriptor,
        TimeSpan total,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = BuildArguments(inputPath, outputPath, settings, descriptor);
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
                var finalProcessed = state.Processed > total ? state.Processed : total;
                progress?.Report(ConversionProgress.Create(finalProcessed, finalProcessed, state.Speed));
                return ConversionResult.Ok(outputPath, descriptor?.FfmpegName);
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

    internal static List<string> BuildArguments(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        EncoderDescriptor? descriptor)
    {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-nostats",
            "-progress", "pipe:1",
            "-y",
            "-i", inputPath,
        };

        if (settings.AudioOnly || descriptor is null)
        {
            arguments.Add("-vn");
            arguments.Add("-c:a");
            arguments.Add("libmp3lame");
            arguments.Add("-b:a");
            arguments.Add($"{settings.AudioBitrateKbps}k");
            arguments.Add(outputPath);
            return arguments;
        }

        arguments.Add("-c:v");
        arguments.Add(descriptor.FfmpegName);

        var quality = descriptor.MapQuality(settings.Crf);
        arguments.Add(descriptor.QualityFlag);
        arguments.Add(quality.ToString(System.Globalization.CultureInfo.InvariantCulture));

        foreach (var extra in descriptor.ExtraVideoArgs)
        {
            arguments.Add(extra);
        }

        if (settings.Scale != ResolutionScale.Original)
        {
            var height = settings.Scale switch
            {
                ResolutionScale.P1080 => 1080,
                ResolutionScale.P720 => 720,
                ResolutionScale.P480 => 480,
                _ => 0,
            };

            if (height > 0)
            {
                arguments.Add("-vf");
                arguments.Add($"scale=-2:{height}");
            }
        }

        // 帧率：null 表示保持原始，此时不传 -r，避免 ffmpeg 无谓地重采样
        if (settings.Fps is { } fps)
        {
            arguments.Add("-r");
            arguments.Add(fps.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // WebM 只支持 Vorbis/Opus 音频，AAC 会被 mkv/webm muxer 拒绝
        var audioCodec = settings.Container.Equals(".webm", StringComparison.OrdinalIgnoreCase)
            ? "libopus"
            : "aac";

        arguments.Add("-c:a");
        arguments.Add(audioCodec);
        arguments.Add("-b:a");
        arguments.Add($"{settings.AudioBitrateKbps}k");
        arguments.Add(outputPath);
        return arguments;
    }

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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
