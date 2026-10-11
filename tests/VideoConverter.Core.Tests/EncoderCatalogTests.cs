using VideoConverter.Core.Models;
using Xunit;

namespace VideoConverter.Core.Tests;

public class EncoderCatalogTests
{
    [Theory]
    [InlineData(".mp4", VideoEncoderChoice.H264, true)]
    [InlineData(".mp4", VideoEncoderChoice.H265, true)]
    [InlineData(".mp4", VideoEncoderChoice.Av1, true)]
    [InlineData(".mp4", VideoEncoderChoice.Vp9, false)]
    [InlineData(".webm", VideoEncoderChoice.Vp9, true)]
    [InlineData(".webm", VideoEncoderChoice.Av1, true)]
    [InlineData(".webm", VideoEncoderChoice.H264, false)]
    [InlineData(".webm", VideoEncoderChoice.H265, false)]
    [InlineData(".avi", VideoEncoderChoice.H264, true)]
    [InlineData(".avi", VideoEncoderChoice.Av1, false)]
    [InlineData(".mkv", VideoEncoderChoice.Vp9, true)]
    [InlineData(".mkv", VideoEncoderChoice.H264, true)]
    [InlineData(".mp3", VideoEncoderChoice.H264, false)]
    public void IsCompatible_MatchesContainerSupport(string container, VideoEncoderChoice choice, bool expected)
    {
        Assert.Equal(expected, EncoderCatalog.IsCompatible(container, choice));
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".webm")]
    [InlineData(".avi")]
    [InlineData(".mp3")]
    public void IsCompatible_AutoIsAlwaysAllowed(string container)
    {
        Assert.True(EncoderCatalog.IsCompatible(container, VideoEncoderChoice.Auto));
    }

    [Fact]
    public void Sanitize_IllegalCombination_FallsBackToAuto()
    {
        Assert.Equal(VideoEncoderChoice.Auto, EncoderCatalog.Sanitize(".webm", VideoEncoderChoice.H264));
    }

    [Fact]
    public void Sanitize_LegalCombination_Unchanged()
    {
        Assert.Equal(VideoEncoderChoice.H265, EncoderCatalog.Sanitize(".mkv", VideoEncoderChoice.H265));
    }

    [Theory]
    [InlineData(VideoCodecKind.H264, 23, 23)]
    [InlineData(VideoCodecKind.H265, 23, 23)]
    [InlineData(VideoCodecKind.Vp9, 23, 28)]      // 23 * 63 / 51 = 28.4 -> 28
    [InlineData(VideoCodecKind.Av1, 23, 28)]
    [InlineData(VideoCodecKind.Vp9, 51, 63)]      // 上限必须能调到 63
    [InlineData(VideoCodecKind.Vp9, 0, 0)]
    [InlineData(VideoCodecKind.H264, 51, 51)]
    public void MapQuality_NormalizesUiScaleToEncoderRange(VideoCodecKind kind, int uiValue, int expected)
    {
        var descriptor = EncoderCatalog.Software(kind);

        Assert.Equal(expected, descriptor.MapQuality(uiValue));
    }

    [Fact]
    public void MapQuality_Vp9CanReachFullRange()
    {
        // 回归：UI 滑杆 0-51 必须能覆盖 VP9/AV1 的 0-63，否则 52-63 段永远调不到
        var vp9 = EncoderCatalog.Software(VideoCodecKind.Vp9);

        Assert.Equal(0, vp9.MapQuality(0));
        Assert.Equal(63, vp9.MapQuality(51));
    }

    [Fact]
    public void NeedsQualityAnnotation_TrueWhenScaledOrDifferentFlag()
    {
        var vp9 = EncoderCatalog.Software(VideoCodecKind.Vp9);
        var x264 = EncoderCatalog.Software(VideoCodecKind.H264);
        var nvenc = EncoderCatalog.HardwareCandidates(VideoCodecKind.H264)[0];

        Assert.True(vp9.NeedsQualityAnnotation(23));   // 23 -> 28，数值被缩放
        Assert.False(x264.NeedsQualityAnnotation(23)); // crf 23 原样
        Assert.True(nvenc.NeedsQualityAnnotation(23)); // 参数名是 -cq 不是 -crf
    }

    [Fact]
    public void QualityFlagDisplay_StripsDash()
    {
        Assert.Equal("cq", EncoderCatalog.HardwareCandidates(VideoCodecKind.H264)[0].QualityFlagDisplay);
        Assert.Equal("crf", EncoderCatalog.Software(VideoCodecKind.H264).QualityFlagDisplay);
    }

    [Fact]
    public void HardwareCandidates_OrderedByVendorPriority()
    {
        var candidates = EncoderCatalog.HardwareCandidates(VideoCodecKind.H264);

        Assert.Equal(new[] { "h264_nvenc", "h264_qsv", "h264_amf" },
            candidates.Select(c => c.FfmpegName).ToArray());
    }

    [Fact]
    public void HardwareCandidates_Vp9HasOnlyQsv()
    {
        // ffmpeg 9 起移除了 VP9 的 NVENC 编码器，AMD 也从未提供
        var candidates = EncoderCatalog.HardwareCandidates(VideoCodecKind.Vp9);

        Assert.Single(candidates);
        Assert.Equal("vp9_qsv", candidates[0].FfmpegName);
    }

    [Fact]
    public void AutoPreference_CoversAllCodecs()
    {
        Assert.Equal(
            new[] { VideoCodecKind.H264, VideoCodecKind.H265, VideoCodecKind.Av1, VideoCodecKind.Vp9 },
            EncoderCatalog.AutoPreference.ToArray());
    }
}

public class ConversionSettingsTests
{
    [Fact]
    public void Defaults_MatchDocumentedDefaults()
    {
        var settings = ConversionSettingsPresets.New(".mp4");

        Assert.Equal(23, settings.Crf);
        Assert.Equal(ResolutionScale.Original, settings.Scale);
        Assert.Equal(128, settings.AudioBitrateKbps);
        Assert.Equal(VideoEncoderChoice.Auto, settings.Encoder);
        Assert.False(settings.AudioOnly);
        Assert.False(settings.ForceSoftware);
    }

    [Fact]
    public void Mp3Preset_IsAudioOnly()
    {
        var settings = ConversionSettingsPresets.Mp3();

        Assert.True(settings.AudioOnly);
        Assert.Equal(".mp3", settings.Container);
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mov")]
    [InlineData(".mkv")]
    [InlineData(".avi")]
    [InlineData(".webm")]
    public void ContainerPresets_CoverAllContainers(string container)
    {
        var settings = ConversionSettingsPresets.New(container);

        Assert.Equal(container, settings.Container);
    }
}

public class EncoderProbeSnapshotTests
{
    [Fact]
    public void IsValidFor_MatchingPathAndVersion_IsValid()
    {
        var snapshot = new EncoderProbeSnapshot
        {
            FfmpegPath = @"C:\tools\ffmpeg\ffmpeg.exe",
            FfmpegVersion = "n9.0.1",
            ProbedAtUtc = DateTime.UtcNow,
        };

        Assert.True(snapshot.IsValidFor(@"C:\tools\ffmpeg\ffmpeg.exe", "n9.0.1", DateTime.UtcNow));
    }

    [Fact]
    public void IsValidFor_DifferentPath_IsInvalid()
    {
        var snapshot = new EncoderProbeSnapshot
        {
            FfmpegPath = @"C:\old\ffmpeg.exe",
            FfmpegVersion = "n9.0.1",
            ProbedAtUtc = DateTime.UtcNow,
        };

        Assert.False(snapshot.IsValidFor(@"C:\new\ffmpeg.exe", "n9.0.1", DateTime.UtcNow));
    }

    [Fact]
    public void IsValidFor_DifferentVersion_IsInvalid()
    {
        var snapshot = new EncoderProbeSnapshot
        {
            FfmpegPath = @"C:\tools\ffmpeg\ffmpeg.exe",
            FfmpegVersion = "n9.0.1",
            ProbedAtUtc = DateTime.UtcNow,
        };

        Assert.False(snapshot.IsValidFor(@"C:\tools\ffmpeg\ffmpeg.exe", "n8.0.1", DateTime.UtcNow));
    }

    [Fact]
    public void IsValidFor_OlderThan30Days_IsInvalid()
    {
        var snapshot = new EncoderProbeSnapshot
        {
            FfmpegPath = @"C:\tools\ffmpeg\ffmpeg.exe",
            FfmpegVersion = "n9.0.1",
            ProbedAtUtc = DateTime.UtcNow.AddDays(-31),
        };

        Assert.False(snapshot.IsValidFor(@"C:\tools\ffmpeg\ffmpeg.exe", "n9.0.1", DateTime.UtcNow));
    }

    [Fact]
    public void IsValidFor_WrongSchemaVersion_IsInvalid()
    {
        var snapshot = new EncoderProbeSnapshot
        {
            SchemaVersion = 0,
            FfmpegPath = @"C:\tools\ffmpeg\ffmpeg.exe",
            FfmpegVersion = "n9.0.1",
            ProbedAtUtc = DateTime.UtcNow,
        };

        Assert.False(snapshot.IsValidFor(@"C:\tools\ffmpeg\ffmpeg.exe", "n9.0.1", DateTime.UtcNow));
    }
}
