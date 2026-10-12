using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

/// <summary>
/// ffmpeg 命令行拼装的单测：不启动进程，只断言 BuildArguments 的产物。
/// 帧率是"可空参数"，最容易写成"null 也传了 -r"或"传了却没落到参数里"，
/// 两条路径都要钉住。
/// </summary>
public class FfmpegArgumentsTests
{
    private static EncoderDescriptor SoftwareH264() => EncoderCatalog.Software(VideoCodecKind.H264);

    private static List<string> BuildArguments(ConversionSettings settings) =>
        FfmpegConversionService.BuildArguments("in.mp4", "out.mp4", settings, SoftwareH264());

    [Fact]
    public void BuildArguments_WithFps_EmitsRateFlag()
    {
        var arguments = BuildArguments(ConversionSettingsPresets.New(".mp4") with { Fps = 30 });

        var index = arguments.IndexOf("-r");
        Assert.True(index >= 0, string.Join(' ', arguments));
        Assert.Equal("30", arguments[index + 1]);
    }

    [Fact]
    public void BuildArguments_WithoutFps_OmitsRateFlag()
    {
        var arguments = BuildArguments(ConversionSettingsPresets.New(".mp4"));

        Assert.DoesNotContain("-r", arguments);
    }

    [Fact]
    public void BuildArguments_AudioOnly_IgnoresFps()
    {
        // 音频提取走 -vn 分支，视频参数（含帧率）一个都不该进命令行
        var arguments = FfmpegConversionService.BuildArguments(
            "in.mp4",
            "out.mp3",
            ConversionSettingsPresets.Mp3() with { Fps = 60 },
            descriptor: null);

        Assert.Contains("-vn", arguments);
        Assert.DoesNotContain("-r", arguments);
    }
}
