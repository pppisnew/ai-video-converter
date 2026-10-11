using System.Text.Json;
using VideoConverter.Core.Internal;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

public interface IEncoderProbeService
{
    /// <null>尚未探测（正在探测中或探测失败）。</null>
    EncoderProbeSnapshot? Snapshot { get; }

    bool IsProbing { get; }

    /// <summary>确保已有可用探测结果：命中有效缓存直接用，否则执行探测。</summary>
    Task<EncoderProbeSnapshot> EnsureProbedAsync(CancellationToken cancellationToken = default);

    /// <summary>强制重新探测（忽略缓存）。</summary>
    Task<EncoderProbeSnapshot> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>运行时自愈：某个硬件编码器实际转换失败，标记不可用并写回缓存。</summary>
    void MarkUnavailable(string encoderName, string reason);

    /// <summary>按设置解析实际使用的编码器；返回 null 表示无可用编码器。</summary>
    EncoderDescriptor? ResolveEncoder(ConversionSettings settings);
}

/// <summary>
/// 通过真实试编码探测硬件编码器可用性，并缓存结果。
/// 只查 `-encoders` 列表是不够的：编码器可能存在但驱动版本过旧 / GPU 不在位 / 运行时 DLL 缺失。
/// </summary>
public sealed class EncoderProbeService : IEncoderProbeService
{
    private const string CacheFileName = "encoder-probe.json";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly FfmpegBinaries _binaries;
    private readonly string _cachePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public EncoderProbeService(FfmpegBinaries binaries, string? cacheDirectory = null)
    {
        _binaries = binaries;
        var dir = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoConverter");
        _cachePath = Path.Combine(dir, CacheFileName);
    }

    public EncoderProbeSnapshot? Snapshot { get; private set; }

    public bool IsProbing { get; private set; }

    public async Task<EncoderProbeSnapshot> EnsureProbedAsync(CancellationToken cancellationToken = default)
    {
        if (Snapshot is not null)
        {
            return Snapshot;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Snapshot is not null)
            {
                return Snapshot;
            }

            var version = ReadVersion();
            var cached = LoadCache();
            if (cached is not null && cached.IsValidFor(_binaries.FfmpegPath, version, DateTime.UtcNow))
            {
                Snapshot = cached;
                return Snapshot;
            }

            return await ProbeInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EncoderProbeSnapshot> ProbeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ProbeInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void MarkUnavailable(string encoderName, string reason)
    {
        var snapshot = Snapshot;
        if (snapshot is null || !snapshot.Results.TryGetValue(encoderName, out var result))
        {
            return;
        }

        if (!result.IsAvailable)
        {
            return;
        }

        result.IsAvailable = false;
        result.FailureReason = reason;
        SaveCache(snapshot);
    }

    public EncoderDescriptor? ResolveEncoder(ConversionSettings settings)
    {
        var snapshot = Snapshot;
        if (settings.AudioOnly)
        {
            return null;
        }

        if (settings.ForceSoftware)
        {
            return SoftwareEncoderFor(settings.Encoder);
        }

        var preferred = settings.Encoder == VideoEncoderChoice.Auto
            ? EncoderCatalog.AutoPreference
            : new[] { EncoderCatalog.ToKind(settings.Encoder) };

        foreach (var kind in preferred)
        {
            if (snapshot is not null)
            {
                foreach (var candidate in EncoderCatalog.HardwareCandidates(kind))
                {
                    if (snapshot.Results.TryGetValue(candidate.FfmpegName, out var r) && r.IsAvailable)
                    {
                        return candidate;
                    }
                }
            }

            // Auto 模式：只在没有可用硬件编码器时回落到软件；
            // 指定了具体编码格式时，该格式的软件编码器是最终保障
            if (settings.Encoder == VideoEncoderChoice.Auto)
            {
                continue;
            }

            return SoftwareEncoderFor(settings.Encoder);
        }

        return SoftwareEncoderFor(VideoEncoderChoice.H264);
    }

    private static EncoderDescriptor? SoftwareEncoderFor(VideoEncoderChoice choice)
    {
        var kind = choice == VideoEncoderChoice.Auto ? VideoCodecKind.H264 : EncoderCatalog.ToKind(choice);
        try
        {
            return EncoderCatalog.Software(kind);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private async Task<EncoderProbeSnapshot> ProbeInternalAsync(CancellationToken cancellationToken)
    {
        IsProbing = true;
        try
        {
            var snapshot = new EncoderProbeSnapshot
            {
                FfmpegPath = _binaries.FfmpegPath,
                FfmpegVersion = ReadVersion(),
                ProbedAtUtc = DateTime.UtcNow,
            };

            foreach (var descriptor in AllCandidateEncoders())
            {
                var result = await ProbeOneAsync(descriptor.FfmpegName, cancellationToken).ConfigureAwait(false);
                snapshot.Results[descriptor.FfmpegName] = result;
            }

            Snapshot = snapshot;
            SaveCache(snapshot);
            return snapshot;
        }
        finally
        {
            IsProbing = false;
        }
    }

    private static IEnumerable<EncoderDescriptor> AllCandidateEncoders()
    {
        foreach (var kind in EncoderCatalog.AutoPreference)
        {
            foreach (var hardware in EncoderCatalog.HardwareCandidates(kind))
            {
                yield return hardware;
            }
        }
    }

    private async Task<EncoderProbeResult> ProbeOneAsync(string encoderName, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-f", "lavfi", "-i", "testsrc=duration=1:size=320x240:rate=1",
            "-c:v", encoderName,
            "-f", "null",
            "-",
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProbeTimeout);

            var result = await ProcessRunner.RunAsync(_binaries.FfmpegPath, arguments, cancellationToken: cts.Token)
                .ConfigureAwait(false);

            return result.ExitCode == 0
                ? new EncoderProbeResult { EncoderName = encoderName, IsAvailable = true }
                : new EncoderProbeResult
                {
                    EncoderName = encoderName,
                    IsAvailable = false,
                    FailureReason = FirstLine(ProcessRunner.SummarizeError(result.StandardError, 1)),
                };
        }
        catch (Exception ex)
        {
            return new EncoderProbeResult
            {
                EncoderName = encoderName,
                IsAvailable = false,
                FailureReason = ex.Message,
            };
        }
    }

    private string? ReadVersion()
    {
        try
        {
            var result = ProcessRunner.RunAsync(_binaries.FfmpegPath, new[] { "-version" }).GetAwaiter().GetResult();
            var first = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return first?.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "未知原因";
        }

        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        return line.Length > 200 ? line[..200] : line;
    }

    private EncoderProbeSnapshot? LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<EncoderProbeSnapshot>(json, JsonOptions);
        }
        catch (Exception)
        {
            // 缓存损坏：视为不存在，重新探测
            return null;
        }
    }

    private void SaveCache(EncoderProbeSnapshot snapshot)
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_cachePath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception)
        {
            // 缓存写失败不影响使用（下次重新探测）
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
}
