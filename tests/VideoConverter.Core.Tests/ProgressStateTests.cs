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
    public void Apply_OutTimeMs_UpdatesProcessedTime()
    {
        var state = new ProgressState();

        state.Apply("out_time_ms=2500");

        Assert.Equal(TimeSpan.FromMilliseconds(2500), state.Processed);
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

        state.Apply("out_time_ms=1000");
        var changed = state.Apply("out_time_us=500000");

        Assert.False(changed, "回退的时间不应产生状态变化");
        Assert.Equal(TimeSpan.FromMilliseconds(1000), state.Processed);
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
