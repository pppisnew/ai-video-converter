using VideoConverter.Core.Internal;
using Xunit;

namespace VideoConverter.Core.Tests;

public class ProgressStateTests
{
    [Fact]
    public void Apply_OutTimeUs_UpdatesProcessedTime()
    {
        var state = new ProgressState();

        var changed = state.Apply("out_time_us=1500000");

        Assert.True(changed);
        Assert.Equal(TimeSpan.FromSeconds(1.5), state.Processed);
    }

    [Fact]
    public void Apply_OutTimeMs_IsIgnored()
    {
        // ffmpeg 的 out_time_ms 字段名有误导性：实测它与 out_time_us 输出相同的微秒值
        // （17.2s 时两者都是 17200000）。按毫秒解析会把进度放大 1000 倍，故只认 out_time_us。
        var state = new ProgressState();

        var changed = state.Apply("out_time_ms=17200000");

        Assert.False(changed);
        Assert.Equal(TimeSpan.Zero, state.Processed);
    }

    [Fact]
    public void Apply_RealFfmpegOutput_ProducesSaneprogress()
    {
        // 复现用户反馈的 bug：17 秒视频显示成 01:49:54
        var state = new ProgressState();
        foreach (var line in new[]
        {
            "frame=123", "fps=45.2", "out_time_us=4551104", "out_time_ms=4551104",
            "out_time=00:00:04.55", "speed=8.74x", "progress=continue",
            "frame=456", "out_time_us=17200000", "out_time_ms=17200000",
            "out_time=00:00:17.20", "speed=3.07x", "progress=end",
        })
        {
            state.Apply(line);
        }

        Assert.Equal(TimeSpan.FromSeconds(17.2), state.Processed);
        Assert.True(state.Finished);
        Assert.Equal(3.07, state.Speed);
    }

    [Fact]
    public void Apply_Speed_ParsesTrailingX()
    {
        var state = new ProgressState();

        state.Apply("speed=2.5x");

        Assert.Equal(2.5d, state.Speed);
    }

    [Fact]
    public void Apply_SpeedNotAvailable_LeavesSpeedNull()
    {
        var state = new ProgressState();

        state.Apply("speed=N/A");

        Assert.Null(state.Speed);
    }

    [Fact]
    public void Apply_ProgressEnd_SetsFinished()
    {
        var state = new ProgressState();

        state.Apply("progress=end");

        Assert.True(state.Finished);
    }

    [Fact]
    public void Apply_ProgressContinue_DoesNotFinish()
    {
        var state = new ProgressState();

        state.Apply("progress=continue");

        Assert.False(state.Finished);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-separator-here")]
    [InlineData("=leading-empty-key")]
    [InlineData("frame=123")]
    [InlineData("bitrate=N/A")]
    public void Apply_IrrelevantOrMalformedLines_AreIgnored(string line)
    {
        var state = new ProgressState();

        Assert.False(state.Apply(line));
        Assert.Equal(TimeSpan.Zero, state.Processed);
        Assert.False(state.Finished);
    }

    [Fact]
    public void Apply_SmallerSubsequentValue_DoesNotMoveBackwards()
    {
        var state = new ProgressState();

        state.Apply("out_time_us=1000000");
        var changed = state.Apply("out_time_us=500000");

        Assert.False(changed, "回退的时间不应产生状态变化");
        Assert.Equal(TimeSpan.FromSeconds(1), state.Processed);
    }

    [Fact]
    public void Apply_UsUpdatesWhenLarger()
    {
        var state = new ProgressState();

        state.Apply("out_time_ms=1000");
        var changed = state.Apply("out_time_us=1500000");

        Assert.True(changed);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), state.Processed);
    }

    [Fact]
    public void Apply_NonNumericValue_IsIgnored()
    {
        var state = new ProgressState();

        Assert.False(state.Apply("out_time_us=abc"));
        Assert.Equal(TimeSpan.Zero, state.Processed);
    }
}
