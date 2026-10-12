using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

/// <summary>队列快照的读写与恢复筛选：这是"重启后未完成任务还在"的唯一依据。</summary>
public class QueueStoreTests
{
    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), "vc-queue-store-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>建一个真实存在的假输入文件，返回其路径。</summary>
    private static string TempInput(string name)
    {
        var path = Path.Combine(NewTempDir(), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private static MediaInfo SampleInfo(string path) => new()
    {
        FilePath = path,
        ContainerFormat = "mov,mp4,m4a,3gp,3g2,mj2",
        Duration = TimeSpan.FromSeconds(42),
        SizeBytes = 1_500_000,
        BitRate = 320_000,
        VideoCodec = "h264",
        Width = 1280,
        Height = 720,
        FrameRate = 29.97,
        AudioCodec = "aac",
        AudioChannels = 2,
        SampleRate = 44100,
    };

    private static QueueItemSnapshot Snapshot(string inputPath) => new()
    {
        InputPath = inputPath,
        OutputPath = inputPath + ".out.mp4",
        Settings = ConversionSettingsPresets.New(".mp4"),
    };

    [Fact]
    public void SaveThenLoad_RoundTripsUnfinishedItems()
    {
        var dir = NewTempDir();
        var pendingInput = TempInput("a.mp4");
        var failedInput = TempInput("b.mp4");
        try
        {
            var settings = ConversionSettingsPresets.New(".mkv") with
            {
                Encoder = VideoEncoderChoice.H265,
                Crf = 20,
                Scale = ResolutionScale.P720,
                Fps = 30,
                AudioBitrateKbps = 192,
            };

            var pending = new QueueItem(pendingInput, pendingInput + ".mkv", settings, SampleInfo(pendingInput));
            var failed = new QueueItem(failedInput, failedInput + ".mkv", settings, SampleInfo(failedInput));
            failed.MarkFailed("编码器不可用");

            var store = new QueueStore(dir);
            store.Save(QueueSnapshot.FromItems(new[] { pending, failed }));

            var loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Items.Count);

            var first = loaded.Items[0];
            Assert.Equal(pendingInput, first.InputPath);
            Assert.Equal(pendingInput + ".mkv", first.OutputPath);
            Assert.Equal(".mkv", first.Settings.Container);
            Assert.Equal(VideoEncoderChoice.H265, first.Settings.Encoder);
            Assert.Equal(20, first.Settings.Crf);
            Assert.Equal(ResolutionScale.P720, first.Settings.Scale);
            Assert.Equal(30, first.Settings.Fps);
            Assert.Equal(192, first.Settings.AudioBitrateKbps);

            // 媒体信息摘要原样带回：恢复出来的行不用重新跑 ffprobe
            Assert.NotNull(first.Info);
            Assert.Equal(TimeSpan.FromSeconds(42), first.Info!.Duration);
            Assert.Equal(1_500_000, first.Info.SizeBytes);
            Assert.Equal(1280, first.Info.Width);
            Assert.Equal(720, first.Info.Height);
            Assert.Equal("h264", first.Info.VideoCodec);
            Assert.Equal(320_000, first.Info.BitRate);

            Assert.Equal(failedInput, loaded.Items[1].InputPath);
        }
        finally
        {
            Cleanup(dir);
            Cleanup(Path.GetDirectoryName(pendingInput)!);
            Cleanup(Path.GetDirectoryName(failedInput)!);
        }
    }

    [Fact]
    public void FromItems_KeepsOnlyPendingAndFailed()
    {
        var settings = ConversionSettingsPresets.New(".mp4");
        var pending = new QueueItem("pending.mp4", "pending_out.mp4", settings);
        var converting = new QueueItem("converting.mp4", "converting_out.mp4", settings);
        var completed = new QueueItem("completed.mp4", "completed_out.mp4", settings);
        var failed = new QueueItem("failed.mp4", "failed_out.mp4", settings);

        converting.MarkConverting(new CancellationTokenSource());
        completed.MarkCompleted("libx264");
        failed.MarkFailed("boom");

        var snapshot = QueueSnapshot.FromItems(new[] { pending, converting, completed, failed });

        Assert.Equal(
            new[] { "pending.mp4", "failed.mp4" },
            snapshot.Items.Select(i => i.InputPath).ToArray());
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var dir = NewTempDir();
        try
        {
            // 目录根本不存在：按"没有可恢复的任务"处理，不能抛
            Assert.Null(new QueueStore(dir).Load());
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Load_CorruptJson_ReturnsNull()
    {
        var dir = NewTempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "queue.json"), "{ this is not json");

            Assert.Null(new QueueStore(dir).Load());
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Save_CreatesDirectoryWhenMissing()
    {
        var root = NewTempDir();
        var dir = Path.Combine(root, "nested", "deeper");
        try
        {
            new QueueStore(dir).Save(new QueueSnapshot { Items = { Snapshot("in.mp4") } });

            Assert.True(File.Exists(Path.Combine(dir, "queue.json")));
            Assert.Single(new QueueStore(dir).Load()!.Items);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void PlanRestore_SkipsInputFilesThatAreGone()
    {
        var present = TempInput("keep.mp4");
        var missing = Path.Combine(Path.GetDirectoryName(present)!, "gone.mp4");
        try
        {
            var snapshot = new QueueSnapshot
            {
                Items = { Snapshot(present), Snapshot(missing) },
            };

            var plan = snapshot.PlanRestore();

            Assert.Single(plan.Items);
            Assert.Equal(present, plan.Items[0].InputPath);
            Assert.Equal(1, plan.SkippedMissing);
        }
        finally
        {
            Cleanup(Path.GetDirectoryName(present)!);
        }
    }

    [Fact]
    public void PlanRestore_SkipsBlankInputPath()
    {
        var snapshot = new QueueSnapshot
        {
            Items =
            {
                new QueueItemSnapshot
                {
                    InputPath = "   ",
                    OutputPath = "out.mp4",
                    Settings = ConversionSettingsPresets.New(".mp4"),
                },
            },
        };

        var plan = snapshot.PlanRestore(_ => true);

        Assert.Empty(plan.Items);
        Assert.Equal(1, plan.SkippedMissing);
    }
}
