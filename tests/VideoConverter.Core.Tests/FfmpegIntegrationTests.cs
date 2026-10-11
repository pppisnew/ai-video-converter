using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

/// <summary>依赖真实 ffmpeg 二进制的集成测试（素材由 lavfi 现场合成）。</summary>
public class FfmpegIntegrationTests : IAsyncLifetime
{
    private string _tempDir = null!;
    private string _sampleVideo = null!;
    private FfmpegBinaries _binaries = null!;

    public async Task InitializeAsync()
    {
        _tempDir = TestMediaFactory.CreateTempDirectory();
        _binaries = new FfmpegBinaries(TestMediaFactory.FfmpegPath, TestMediaFactory.FfprobePath, FfmpegSource.SystemPath);
        _sampleVideo = await TestMediaFactory.CreateSampleVideoAsync(Path.Combine(_tempDir, "sample.mp4"));
    }

    public Task DisposeAsync()
    {
        TryDeleteDirectory(_tempDir);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Probe_ReturnsExpectedMetadata()
    {
        var service = new FfprobeMediaProbeService(_binaries);

        var info = await service.ProbeAsync(_sampleVideo);

        Assert.Equal("MP4", info.ContainerFormat);
        Assert.Equal("h264", info.VideoCodec);
        Assert.Equal(320, info.Width);
        Assert.Equal(240, info.Height);
        Assert.Equal(15d, info.FrameRate);
        Assert.Equal("aac", info.AudioCodec);
        Assert.Equal(2, info.AudioChannels);
        Assert.NotNull(info.Duration);
        Assert.InRange(info.Duration!.Value.TotalSeconds, 4.5, 5.5);
        Assert.True(info.SizeBytes > 0);
    }

    [Fact]
    public async Task Probe_MissingFile_ThrowsFileNotFound()
    {
        var service = new FfprobeMediaProbeService(_binaries);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.ProbeAsync(Path.Combine(_tempDir, "nope.mp4")));
    }

    [Fact]
    public async Task Thumbnail_CreatesJpegFile()
    {
        var service = new FfmpegThumbnailService(_binaries);
        var output = Path.Combine(_tempDir, "thumb.jpg");

        await service.CaptureAsync(_sampleVideo, TimeSpan.FromSeconds(2), output);

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, (await File.ReadAllBytesAsync(output))[..2]);
    }

    [Fact]
    public async Task Thumbnail_AtTimeZero_Works()
    {
        var service = new FfmpegThumbnailService(_binaries);
        var output = Path.Combine(_tempDir, "thumb0.jpg");

        await service.CaptureAsync(_sampleVideo, TimeSpan.Zero, output);

        Assert.True(File.Exists(output));
    }

    [Theory]
    [InlineData(".mp4", "mp4", "h264", "aac")]
    [InlineData(".mov", "mov,mp4,m4a,3gp,3g2,mj2", "h264", "aac")]
    [InlineData(".mkv", "matroska,webm", "h264", "aac")]
    [InlineData(".avi", "avi", "h264", "aac")]
    [InlineData(".webm", "matroska,webm", "vp9", "opus")]
    [InlineData(".mp3", "mp3", null, "mp3")]
    public async Task Convert_ProducesPlayableOutput(string container, string expectedContainer, string? expectedVideoCodec, string expectedAudioCodec)
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        // WebM 只支持 VP8/VP9/AV1 视频，H.264 放进去会被 muxer 拒绝
        var settings = container == ".mp3"
            ? ConversionSettingsPresets.Mp3()
            : ConversionSettingsPresets.New(container) with
            {
                Encoder = container == ".webm" ? VideoEncoderChoice.Vp9 : VideoEncoderChoice.H264,
            };
        var output = Path.Combine(_tempDir, "out" + container);

        var result = await service.ConvertAsync(_sampleVideo, output, settings);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 0);

        var info = await probe.ProbeAsync(output);
        Assert.Contains(expectedContainer.Split(',', StringSplitOptions.RemoveEmptyEntries),
            token => string.Equals(token, info.ContainerFormat, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedVideoCodec, info.VideoCodec);
        Assert.Equal(expectedAudioCodec, info.AudioCodec);
    }

    [Theory]
    [InlineData(VideoEncoderChoice.H264, "h264", "libx264")]
    [InlineData(VideoEncoderChoice.H265, "hevc", "libx265")]
    [InlineData(VideoEncoderChoice.Vp9, "vp9", "libvpx-vp9")]
    public async Task Convert_RespectsEncoderChoice(VideoEncoderChoice choice, string expectedCodec, string expectedFfmpegEncoder)
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        var settings = ConversionSettingsPresets.New(".mkv") with { Encoder = choice };
        var output = Path.Combine(_tempDir, $"enc-{choice}.mkv");

        var result = await service.ConvertAsync(_sampleVideo, output, settings);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expectedFfmpegEncoder, result.UsedEncoder);
        var info = await probe.ProbeAsync(output);
        Assert.Equal(expectedCodec, info.VideoCodec);
    }

    [Theory]
    [InlineData(ResolutionScale.P1080, 1080, 1440)]
    [InlineData(ResolutionScale.P720, 720, 960)]
    [InlineData(ResolutionScale.P480, 480, 640)]
    public async Task Convert_AppliesResolutionScale(ResolutionScale scale, int expectedHeight, int expectedWidth)
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        var settings = ConversionSettingsPresets.New(".mp4") with { Encoder = VideoEncoderChoice.H264, Scale = scale };
        var output = Path.Combine(_tempDir, $"scaled-{scale}.mp4");

        var result = await service.ConvertAsync(_sampleVideo, output, settings);

        Assert.True(result.Success, result.Error);
        var info = await probe.ProbeAsync(output);
        Assert.Equal(expectedHeight, info.Height);
        Assert.Equal(expectedWidth, info.Width);
    }

    [Fact]
    public async Task Convert_OriginalScale_KeepsSourceResolution()
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        var settings = ConversionSettingsPresets.New(".mp4") with { Encoder = VideoEncoderChoice.H264, Scale = ResolutionScale.Original };
        var output = Path.Combine(_tempDir, "original-scale.mp4");

        var result = await service.ConvertAsync(_sampleVideo, output, settings);

        Assert.True(result.Success, result.Error);
        var info = await probe.ProbeAsync(output);
        Assert.Equal(320, info.Width);
        Assert.Equal(240, info.Height);
    }

    [Fact]
    public async Task Convert_ReportsMonotonicProgressAndCompletes()
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        var output = Path.Combine(_tempDir, "progress.mp4");
        var reports = new List<ConversionProgress>();

        var progress = new Progress<ConversionProgress>(reports.Add);
        var result = await service.ConvertAsync(_sampleVideo, output, ConversionSettingsPresets.Mp4(), progress);

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(reports);
        Assert.NotNull(reports[0].Percent);
        Assert.True(reports.Last().Percent >= 99d, "最后一次进度应达到 100%");
        Assert.True(reports.Zip(reports.Skip(1)).All(pair => pair.Second.Processed >= pair.First.Processed),
            "已处理时长应单调不减");

        // 回归防护：曾因把 ffmpeg 的 out_time_ms（实为微秒）按毫秒解析，
        // 导致 17 秒视频显示成 01:49:54。已处理时长不允许远超总时长。
        var final = reports.Last();
        Assert.True(final.Processed <= TimeSpan.FromTicks(final.Total.Ticks * 2),
            $"已处理时长 {final.Processed} 不应远超总时长 {final.Total}");
        Assert.All(reports, r => Assert.True(r.Processed <= TimeSpan.FromTicks(r.Total.Ticks * 2),
            $"进度 {r.Processed}/{r.Total} 超出合理范围"));
    }

    [Fact]
    public async Task Convert_Cancellation_ThrowsAndDeletesPartialOutput()
    {
        var longVideo = await TestMediaFactory.CreateSampleVideoAsync(
            Path.Combine(_tempDir, "long.mp4"), durationSeconds: 30, width: 640, height: 480);
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        var output = Path.Combine(_tempDir, "cancelled.mp4");
        using var cts = new CancellationTokenSource();
        var progress = new SynchronousProgress(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ConvertAsync(longVideo, output, ConversionSettingsPresets.Mp4(), progress, cts.Token));

        Assert.False(File.Exists(output), "取消后应删除不完整的输出文件");
    }

    [Fact]
    public async Task Convert_HandlesPathsWithSpacesAndChineseCharacters()
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);
        var directory = Path.Combine(_tempDir, "带空格 的目录");
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "测试 视频.mp4");
        File.Copy(_sampleVideo, input);
        var output = Path.Combine(directory, "输出 文件.mkv");

        var result = await service.ConvertAsync(input, output, ConversionSettingsPresets.Mkv());

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task Convert_MissingInput_ThrowsFileNotFound()
    {
        var probe = new FfprobeMediaProbeService(_binaries);
        var service = new FfmpegConversionService(_binaries, probe);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.ConvertAsync(
                Path.Combine(_tempDir, "missing.mp4"),
                Path.Combine(_tempDir, "out.mp4"),
                ConversionSettingsPresets.Mp4()));
    }

    [Fact]
    public async Task Locate_ReturnsUsableBinaries()
    {
        var locator = new FfmpegLocator();

        var found = locator.Locate();

        Assert.NotNull(found);
        Assert.True(File.Exists(found!.FfmpegPath));
        Assert.True(File.Exists(found.FfprobePath));
        Assert.Contains("ffmpeg version", locator.GetVersion(found));
    }

    /// <summary>同步回调的 IProgress，确保取消发生在转换线程上而不是被抛到线程池。</summary>
    private sealed class SynchronousProgress(Action<ConversionProgress> handler) : IProgress<ConversionProgress>
    {
        public void Report(ConversionProgress value) => handler(value);
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
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
