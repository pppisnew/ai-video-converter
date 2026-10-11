using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

/// <summary>用一个可编排的假转换服务测试队列行为，不依赖真实 ffmpeg。</summary>
public sealed class FakeConversionService : IConversionService
{
    private readonly Func<string, ConversionResult> _behavior;

    public FakeConversionService(Func<string, ConversionResult> behavior, Action<string>? onStart = null)
    {
        _behavior = behavior;
        OnStart = onStart;
    }

    /// <summary>转换开始时的回调，供测试在任务进行中触发取消。</summary>
    public Action<string>? OnStart { get; }

    public List<string> ConvertedInputs { get; } = new();

    public Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ConvertedInputs.Add(inputPath);
        cancellationToken.ThrowIfCancellationRequested();
        OnStart?.Invoke(inputPath);

        // 可取消的延迟：取消时抛出 OperationCanceledException，与真实 ffmpeg 行为一致
        return Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
            return _behavior(inputPath);
        }, cancellationToken);
    }
}

public class ConversionQueueTests
{
    private static ConversionSettings Settings() => ConversionSettingsPresets.New(".mp4");

    private static string TempInput(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "vc-queue-" + Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public async Task RunAsync_ProcessesItemsInOrder()
    {
        var service = new FakeConversionService(_ => ConversionResult.Ok("out.mp4", "libx264"));
        var queue = new ConversionQueue(service);
        var a = TempInput("a.mp4");
        var b = TempInput("b.mp4");
        var c = TempInput("c.mp4");
        queue.Enqueue(a, Settings());
        queue.Enqueue(b, Settings());
        queue.Enqueue(c, Settings());

        await queue.RunAsync();

        Assert.Equal(new[] { a, b, c }, service.ConvertedInputs);
        Assert.All(queue.Items, i => Assert.Equal(QueueItemState.Completed, i.State));
    }

    [Fact]
    public async Task RunAsync_OnlyOneItemConvertingAtATime()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var service = new FakeConversionService(_ =>
        {
            concurrent++;
            maxConcurrent = Math.Max(maxConcurrent, concurrent);
            Thread.Sleep(30);
            concurrent--;
            return ConversionResult.Ok("out.mp4");
        });
        var queue = new ConversionQueue(service);
        for (var i = 0; i < 4; i++)
        {
            queue.Enqueue(TempInput($"f{i}.mp4"), Settings());
        }

        await queue.RunAsync();

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task RunAsync_FailureDoesNotStopLaterItems()
    {
        var service = new FakeConversionService(input =>
            Path.GetFileName(input).StartsWith("bad")
                ? ConversionResult.Failed("out.mp4", 1, "boom")
                : ConversionResult.Ok("out.mp4", "libx264"));
        var queue = new ConversionQueue(service);
        var good1 = TempInput("good1.mp4");
        var bad = TempInput("bad.mp4");
        var good2 = TempInput("good2.mp4");
        queue.Enqueue(good1, Settings());
        queue.Enqueue(bad, Settings());
        queue.Enqueue(good2, Settings());

        QueueSummary? summary = null;
        queue.QueueFinished += s => summary = s;
        await queue.RunAsync();

        Assert.Equal(QueueItemState.Completed, queue.Items[0].State);
        Assert.Equal(QueueItemState.Failed, queue.Items[1].State);
        Assert.Equal(QueueItemState.Completed, queue.Items[2].State);
        Assert.Equal("boom", queue.Items[1].Error);
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.Completed);
        Assert.Equal(1, summary.Failed);
    }

    [Fact]
    public async Task RunAsync_ExceptionIsCapturedAsFailure()
    {
        var service = new FakeConversionService(_ => throw new InvalidOperationException("kaboom"));
        var queue = new ConversionQueue(service);
        queue.Enqueue(TempInput("a.mp4"), Settings());

        await queue.RunAsync();

        Assert.Equal(QueueItemState.Failed, queue.Items[0].State);
        Assert.Contains("kaboom", queue.Items[0].Error);
    }

    [Fact]
    public async Task RunAsync_CancelCurrent_LeavesLaterItemsPending()
    {
        var firstSeen = new TaskCompletionSource();
        var service = new FakeConversionService(
            _ => ConversionResult.Ok("out.mp4"),
            _ => firstSeen.TrySetResult());
        var queue = new ConversionQueue(service);
        queue.Enqueue(TempInput("a.mp4"), Settings());
        queue.Enqueue(TempInput("b.mp4"), Settings());
        queue.Enqueue(TempInput("c.mp4"), Settings());

        var run = queue.RunAsync();
        await firstSeen.Task;
        queue.CancelCurrent();
        await run;

        // "取消当前" = 跳过这个文件，队列继续跑完后续项
        Assert.Equal(QueueItemState.Failed, queue.Items[0].State);
        Assert.Contains("已取消", queue.Items[0].Error);
        Assert.Equal(QueueItemState.Completed, queue.Items[1].State);
        Assert.Equal(QueueItemState.Completed, queue.Items[2].State);
    }

    [Fact]
    public async Task RunAsync_CancelAll_StopsQueueEntirely()
    {
        var firstSeen = new TaskCompletionSource();
        var service = new FakeConversionService(
            _ => ConversionResult.Ok("out.mp4"),
            _ => firstSeen.TrySetResult());
        var queue = new ConversionQueue(service);
        queue.Enqueue(TempInput("a.mp4"), Settings());
        queue.Enqueue(TempInput("b.mp4"), Settings());
        queue.Enqueue(TempInput("c.mp4"), Settings());

        var run = queue.RunAsync();
        await firstSeen.Task;
        queue.CancelAll();
        await run;

        Assert.Equal(QueueItemState.Failed, queue.Items[0].State);
        Assert.Equal(QueueItemState.Failed, queue.Items[0].State);
        // never-started items are marked cancelled too, so no phantom waiting rows remain
        Assert.All(queue.Items.Skip(1), i =>
        {
            Assert.Equal(QueueItemState.Failed, i.State);
            Assert.Contains("已取消", i.Error);
        });
    }

    [Fact]
    public async Task RetryFailed_ResetsFailedItemsOnly()
    {
        var attempts = 0;
        var service = new FakeConversionService(_ =>
        {
            attempts++;
            return attempts == 1
                ? ConversionResult.Failed("out.mp4", 1, "first fails")
                : ConversionResult.Ok("out.mp4", "libx264");
        });
        var queue = new ConversionQueue(service);
        queue.Enqueue(TempInput("a.mp4"), Settings());
        await queue.RunAsync();

        Assert.Equal(QueueItemState.Failed, queue.Items[0].State);
        var retried = queue.RetryFailed();
        await queue.RunAsync();

        Assert.Equal(1, retried);
        Assert.Equal(QueueItemState.Completed, queue.Items[0].State);
        Assert.Null(queue.Items[0].Error);
    }

    [Fact]
    public void Enqueue_QueueLevelCollision_DoesNotReuseAnotherItemsOutput()
    {
        // 场景：两个不同目录的同名文件，输出被指定到同一目录时不能撞车。
        // 用 settings.Container 指向同一目录来模拟（把输入放到同一目录即可复现）。
        var service = new FakeConversionService(_ => ConversionResult.Ok("out"));
        var queue = new ConversionQueue(service);
        var dir = Path.Combine(Path.GetTempPath(), "vc-dup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var inputA = Path.Combine(dir, "clip.mp4");
        File.WriteAllText(inputA, "x");
        var itemA = queue.Enqueue(inputA, Settings());
        Assert.EndsWith("clip_converted.mp4", itemA.OutputPath);

        // 同名文件再次入队：即使磁盘上还没生成第一个输出，也不能复用同一路径
        var inputB = Path.Combine(dir, "clip.mp4");
        var itemB = queue.Enqueue(inputB, Settings());
        Assert.NotEqual(itemA.OutputPath, itemB.OutputPath);
        Assert.EndsWith("clip_converted_1.mp4", itemB.OutputPath);
    }

    [Fact]
    public void Enqueue_OutputCollision_AppendsNumericSuffix()
    {
        var service = new FakeConversionService(_ => ConversionResult.Ok("out"));
        var queue = new ConversionQueue(service);
        var dir = Path.Combine(Path.GetTempPath(), "vc-suffix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, "clip.mp4");
        File.WriteAllText(input, "x");

        var first = queue.Enqueue(input, Settings());
        Assert.EndsWith("clip_converted.mp4", first.OutputPath);

        // 制造磁盘上已存在的同名输出
        File.WriteAllText(first.OutputPath, "existing");
        var second = queue.Enqueue(input, Settings());
        Assert.EndsWith("clip_converted_1.mp4", second.OutputPath);

        File.WriteAllText(second.OutputPath, "existing");
        var third = queue.Enqueue(input, Settings());
        Assert.EndsWith("clip_converted_2.mp4", third.OutputPath);
    }

    [Fact]
    public void Remove_ConvertingItem_IsRejected()
    {
        var service = new FakeConversionService(_ => ConversionResult.Ok("out"));
        var queue = new ConversionQueue(service);
        var item = queue.Enqueue(TempInput("a.mp4"), Settings());
        item.MarkConverting(new CancellationTokenSource());

        Assert.False(queue.Remove(item.Id));
        Assert.Single(queue.Items);
    }

    [Fact]
    public void ClearCompleted_RemovesOnlyCompleted()
    {
        var service = new FakeConversionService(_ => ConversionResult.Ok("out"));
        var queue = new ConversionQueue(service);
        var done = queue.Enqueue(TempInput("done.mp4"), Settings());
        var pending = queue.Enqueue(TempInput("pending.mp4"), Settings());
        done.MarkCompleted("libx264");

        var removed = queue.ClearCompleted();

        Assert.Equal(1, removed);
        Assert.Single(queue.Items);
        Assert.Equal(pending.Id, queue.Items[0].Id);
    }
}

public class ConversionQueueSelectionTests
{
    private static ConversionSettings Settings() => ConversionSettingsPresets.New(".mp4");

    private static string TempInput(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "vc-sel-" + Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public async Task RunAsync_SkipsUncheckedItems()
    {
        var service = new FakeConversionService(_ => ConversionResult.Ok("out", "libx264"));
        var queue = new ConversionQueue(service);
        var a = queue.Enqueue(TempInput("a.mp4"), Settings());
        var b = queue.Enqueue(TempInput("b.mp4"), Settings());
        var c = queue.Enqueue(TempInput("c.mp4"), Settings());
        b.IsSelected = false;

        await queue.RunAsync();

        Assert.Equal(new[] { a.InputPath, c.InputPath }, service.ConvertedInputs);
        Assert.Equal(QueueItemState.Completed, a.State);
        Assert.Equal(QueueItemState.Pending, b.State);
        Assert.Equal(QueueItemState.Completed, c.State);
    }

    [Fact]
    public void SetAllSelected_TogglesEveryItem()
    {
        var queue = new ConversionQueue(new FakeConversionService(_ => ConversionResult.Ok("out")));
        queue.Enqueue(TempInput("a.mp4"), Settings());
        queue.Enqueue(TempInput("b.mp4"), Settings());

        queue.SetAllSelected(false);
        Assert.All(queue.Items, i => Assert.False(i.IsSelected));

        queue.SetAllSelected(true);
        Assert.All(queue.Items, i => Assert.True(i.IsSelected));
    }

    [Fact]
    public void MoveUpMoveDown_ReordersItems()
    {
        var queue = new ConversionQueue(new FakeConversionService(_ => ConversionResult.Ok("out")));
        var a = queue.Enqueue(TempInput("a.mp4"), Settings());
        var b = queue.Enqueue(TempInput("b.mp4"), Settings());
        var c = queue.Enqueue(TempInput("c.mp4"), Settings());

        Assert.True(queue.MoveUp(c.Id));
        Assert.Equal(new[] { a.Id, c.Id, b.Id }, queue.Items.Select(i => i.Id).ToArray());

        Assert.True(queue.MoveDown(a.Id));
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, queue.Items.Select(i => i.Id).ToArray());

        Assert.False(queue.MoveUp(c.Id), "already at top");
        Assert.False(queue.MoveDown(b.Id), "already at bottom");
        Assert.False(queue.MoveUp("nope"), "unknown id");
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var queue = new ConversionQueue(new FakeConversionService(_ => ConversionResult.Ok("out")));
        queue.Enqueue(TempInput("a.mp4"), Settings());
        queue.Enqueue(TempInput("b.mp4"), Settings());

        Assert.Equal(2, queue.Clear());
        Assert.Empty(queue.Items);
    }

    [Fact]
    public async Task RunAsync_NotifiesOnCallerSynchronizationContext()
    {
        // Regression guard for the state-machine desync: MarkCompleted used to run on a
        // thread-pool thread (ConfigureAwait(false)), so WinUI bindings never saw the
        // Completed state and queue items stayed stuck on "Converting".
        var contexts = new List<string>();
        var queue = new ConversionQueue(new FakeConversionService(_ => ConversionResult.Ok("out", "libx264")));
        queue.ItemChanged += _ => contexts.Add(SynchronizationContext.Current?.GetType().Name ?? "null");
        queue.QueueFinished += _ => contexts.Add("finished:" + (SynchronizationContext.Current?.GetType().Name ?? "null"));

        var singleThread = new SingleThreadSynchronizationContext();
        System.Threading.SynchronizationContext.SetSynchronizationContext(singleThread);
        try
        {
            var run = queue.RunAsync();
            singleThread.RunOnCurrentThread();
            await run;
        }
        finally
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
        }

        Assert.NotEmpty(contexts);
        Assert.All(contexts, c => Assert.Contains("SingleThread", c));
    }

    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunOnCurrentThread()
        {
            while (_queue.TryTake(out var item, 50))
            {
                item.Item1(item.Item2);
            }
        }
    }
}

public class EncoderContainerCompatibilityTests
{
    private static FfmpegBinaries Binaries() =>
        new(TestMediaFactory.FfmpegPath, TestMediaFactory.FfprobePath, FfmpegSource.SystemPath);

    [Theory]
    [InlineData(".webm", "libvpx-vp9")]   // Auto + WebM must NOT resolve to libx264
    [InlineData(".mp4", "libx264")]
    [InlineData(".mkv", "libx264")]
    [InlineData(".avi", "libx264")]
    public async Task ResolveEncoder_AutoRespectsContainerRestrictions(string container, string expectedEncoder)
    {
        var probe = new EncoderProbeService(Binaries(), Path.Combine(Path.GetTempPath(), "vc-probe-" + Guid.NewGuid().ToString("N")));
        await probe.EnsureProbedAsync();
        var settings = ConversionSettingsPresets.New(container) with { Encoder = VideoEncoderChoice.Auto };

        var descriptor = probe.ResolveEncoder(settings);

        Assert.NotNull(descriptor);
        Assert.Equal(expectedEncoder, descriptor!.FfmpegName);
    }

    [Fact]
    public async Task ResolveEncoder_AutoForWebM_NeverReturnsSoftwareH264()
    {
        // Regression: Auto used to scan H264 -> H265 -> AV1 -> VP9 and fall back to libx264,
        // which the WebM muxer rejects ("Only VP8 or VP9 or AV1 video ... supported for WebM").
        var probe = new EncoderProbeService(Binaries(), Path.Combine(Path.GetTempPath(), "vc-probe-" + Guid.NewGuid().ToString("N")));
        await probe.EnsureProbedAsync();
        var settings = ConversionSettingsPresets.New(".webm") with { Encoder = VideoEncoderChoice.Auto };

        var descriptor = probe.ResolveEncoder(settings);

        Assert.NotNull(descriptor);
        Assert.Equal(VideoCodecKind.Vp9, descriptor!.Codec);
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    [InlineData(".webm")]
    public async Task ConvertAsync_AutoEncoder_ProducesPlayableOutput(string container)
    {
        var binaries = Binaries();
        var probeService = new FfprobeMediaProbeService(binaries);
        var probe = new EncoderProbeService(binaries, Path.Combine(Path.GetTempPath(), "vc-probe-" + Guid.NewGuid().ToString("N")));
        await probe.EnsureProbedAsync();
        var service = new FfmpegConversionService(binaries, probeService, probe);
        var dir = TestMediaFactory.CreateTempDirectory();
        var input = await TestMediaFactory.CreateSampleVideoAsync(Path.Combine(dir, "in.mp4"));
        var settings = ConversionSettingsPresets.New(container) with { Encoder = VideoEncoderChoice.Auto };
        var output = Path.Combine(dir, "out" + container);

        var result = await service.ConvertAsync(input, output, settings);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(output));
        var info = await probeService.ProbeAsync(output);
        Assert.True(EncoderCatalog.IsCompatible(container,
            info.VideoCodec == "h264" ? VideoEncoderChoice.H264
            : info.VideoCodec == "hevc" ? VideoEncoderChoice.H265
            : info.VideoCodec == "vp9" ? VideoEncoderChoice.Vp9
            : VideoEncoderChoice.Av1));
    }
}

public class ConversionQueueDuplicateTests
{
    private static ConversionSettings Settings() => ConversionSettingsPresets.New(".mp4");

    private static string TempInput(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "vc-dup2-" + Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Contains_MatchesByFullPathIgnoringCase()
    {
        var queue = new ConversionQueue(new FakeConversionService(_ => ConversionResult.Ok("out")));
        var path = TempInput("a.mp4");
        queue.Enqueue(path, Settings());

        Assert.True(queue.Contains(path));
        Assert.True(queue.Contains(path.ToUpperInvariant()));
        Assert.False(queue.Contains(TempInput("b.mp4")));
    }

    [Fact]
    public async Task CancelAll_MarksNeverStartedItemsAsCancelled()
    {
        // After an explicit stop, Pending items must not keep claiming to be waiting.
        var firstSeen = new TaskCompletionSource();
        var service = new FakeConversionService(_ => ConversionResult.Ok("out"), _ => firstSeen.TrySetResult());
        var queue = new ConversionQueue(service);
        queue.Enqueue(TempInput("a.mp4"), Settings());
        queue.Enqueue(TempInput("b.mp4"), Settings());
        queue.Enqueue(TempInput("c.mp4"), Settings());

        var run = queue.RunAsync();
        await firstSeen.Task;
        queue.CancelAll();
        await run;

        Assert.Equal(QueueItemState.Failed, queue.Items[0].State);
        Assert.Contains("已取消", queue.Items[0].Error);
        Assert.All(queue.Items.Skip(1), i =>
        {
            Assert.Equal(QueueItemState.Failed, i.State);
            Assert.Contains("已取消", i.Error);
        });
    }

    [Fact]
    public void UpdateSettings_ChangesTargetAndOutputPath()
    {
        var queue = new ConversionQueue(new FakeConversionService(_ => ConversionResult.Ok("out")));
        var item = queue.Enqueue(TempInput("clip.mp4"), ConversionSettingsPresets.New(".mp4"));
        Assert.Contains("MP4", item.TargetSummary);

        var newSettings = ConversionSettingsPresets.New(".mkv") with { Encoder = VideoEncoderChoice.H265 };
        var newOutput = queue.ResolveOutputPathFor(item.InputPath, newSettings);
        item.UpdateSettings(newSettings, newOutput);

        Assert.Contains("MKV", item.TargetSummary);
        Assert.Contains("H.265", item.TargetSummary);
        Assert.EndsWith(".mkv", item.OutputPath);
    }
}
