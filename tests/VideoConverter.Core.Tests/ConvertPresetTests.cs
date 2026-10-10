using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

public class ConvertPresetTests
{
    [Fact]
    public void All_PresetsHaveUniqueExtensions()
    {
        var extensions = ConvertPreset.All.Select(p => p.Extension).ToList();

        Assert.Equal(extensions.Count, extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void All_PresetsHaveAudioCodec()
    {
        Assert.All(ConvertPreset.All, preset => Assert.False(string.IsNullOrWhiteSpace(preset.AudioCodec)));
    }

    [Fact]
    public void AudioOnly_PresetHasNoVideoCodec()
    {
        var mp3 = ConvertPreset.FromFormat(TargetFormat.Mp3);

        Assert.True(mp3.AudioOnly);
        Assert.Equal(string.Empty, mp3.VideoCodec);
    }

    [Fact]
    public void NonAudioOnly_PresetsHaveVideoCodec()
    {
        Assert.All(
            ConvertPreset.All.Where(p => !p.AudioOnly),
            preset => Assert.False(string.IsNullOrWhiteSpace(preset.VideoCodec)));
    }

    [Theory]
    [InlineData(TargetFormat.Mp4, ".mp4", "libx264", "aac")]
    [InlineData(TargetFormat.Mov, ".mov", "libx264", "aac")]
    [InlineData(TargetFormat.Mkv, ".mkv", "libx264", "aac")]
    [InlineData(TargetFormat.Avi, ".avi", "mpeg4", "libmp3lame")]
    [InlineData(TargetFormat.WebM, ".webm", "libvpx-vp9", "libopus")]
    [InlineData(TargetFormat.Mp3, ".mp3", "", "libmp3lame")]
    public void Preset_MatchesDesign(TargetFormat format, string extension, string videoCodec, string audioCodec)
    {
        var preset = ConvertPreset.FromFormat(format);

        Assert.Equal(extension, preset.Extension);
        Assert.Equal(videoCodec, preset.VideoCodec);
        Assert.Equal(audioCodec, preset.AudioCodec);
    }

    [Fact]
    public void FromFormat_UnknownValue_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ConvertPreset.FromFormat((TargetFormat)999));
    }
}

public class ConversionProgressTests
{
    [Fact]
    public void Create_ComputesPercentFromTotal()
    {
        var progress = ConversionProgress.Create(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), 1.5);

        Assert.Equal(20d, progress.Percent);
        Assert.Equal(TimeSpan.FromSeconds(2), progress.Processed);
        Assert.Equal(1.5, progress.Speed);
    }

    [Fact]
    public void Create_ZeroTotal_PercentIsNull()
    {
        var progress = ConversionProgress.Create(TimeSpan.FromSeconds(2), TimeSpan.Zero, null);

        Assert.Null(progress.Percent);
    }

    [Fact]
    public void Create_OverHundredPercent_IsClamped()
    {
        var progress = ConversionProgress.Create(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(10), null);

        Assert.Equal(100d, progress.Percent);
    }
}
