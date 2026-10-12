using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

/// <summary>
/// 逐项覆盖（P5）：覆盖是**字段级**的，只作用于容器与质量，
/// 其余参数始终跟随任务开始时捕获的全局面板。
/// </summary>
public class QueueItemOverrideTests
{
    private static readonly string InputPath =
        Path.Combine(Path.GetTempPath(), "vc-override", "clip.mp4");

    private static QueueItem NewItem(MediaInfo? info = null) => new(
        InputPath,
        Path.Combine(Path.GetTempPath(), "vc-override", "clip_converted.mp4"),
        ConversionSettingsPresets.New(".mp4"),
        info);

    private static MediaInfo Info() => new()
    {
        FilePath = InputPath,
        SizeBytes = 1_572_864,
        Width = 1280,
        Height = 720,
        Duration = TimeSpan.FromSeconds(90),
        VideoCodec = "h264",
    };

    [Fact]
    public void ApplyOverride_ContainerOnly_KeepsGlobalQualityAndOtherFields()
    {
        var globalSettings = ConversionSettingsPresets.New(".webm") with
        {
            Encoder = VideoEncoderChoice.Vp9,
            Crf = 23,
            Scale = ResolutionScale.P720,
            Fps = 30,
            AudioBitrateKbps = 192,
        };
        var item = NewItem();
        item.OverrideContainer = ".mkv";

        var effective = item.ApplyOverride(globalSettings);

        Assert.Equal(".mkv", effective.Container);
        Assert.Equal(23, effective.Crf);
        Assert.Equal(ResolutionScale.P720, effective.Scale);
        Assert.Equal(30, effective.Fps);
        Assert.Equal(192, effective.AudioBitrateKbps);
        Assert.Equal(VideoEncoderChoice.Vp9, effective.Encoder);
        Assert.False(effective.AudioOnly);
    }

    [Fact]
    public void ApplyOverride_QualityOnly_KeepsGlobalContainer()
    {
        var item = NewItem();
        item.OverrideCrf = 16;

        var effective = item.ApplyOverride(ConversionSettingsPresets.New(".webm") with { Crf = 23 });

        Assert.Equal(".webm", effective.Container);
        Assert.Equal(16, effective.Crf);
    }

    [Fact]
    public void ApplyOverride_Mp3Container_SwitchesToAudioOnly()
    {
        var item = NewItem();
        item.OverrideContainer = ".mp3";

        var effective = item.ApplyOverride(ConversionSettingsPresets.New(".mkv"));

        Assert.Equal(".mp3", effective.Container);
        Assert.True(effective.AudioOnly);
    }

    [Fact]
    public void ApplyOverride_VideoContainer_ClearsInheritedAudioOnly()
    {
        // 面板停在"仅音频"时，单独覆盖回 MKV 必须回到视频输出
        var item = NewItem();
        item.OverrideContainer = ".mkv";

        var effective = item.ApplyOverride(ConversionSettingsPresets.Mp3());

        Assert.Equal(".mkv", effective.Container);
        Assert.False(effective.AudioOnly);
    }

    [Fact]
    public void ApplyOverride_WithoutOverride_ReturnsBaseUnchanged()
    {
        var globalSettings = ConversionSettingsPresets.Mp3();
        var item = NewItem();

        var effective = item.ApplyOverride(globalSettings);

        Assert.Same(globalSettings, effective);
        Assert.True(effective.AudioOnly);
    }

    [Fact]
    public void MetaLine_InheritedItem_MarksInheritGlobal()
    {
        var item = NewItem(Info());

        Assert.Equal("1.5 MB · 1280 x 720 · MP4 → MP4 · 中等", item.MetaLine);
    }

    [Fact]
    public void MetaLine_OverriddenItem_ShowsOverrideAndCovered()
    {
        var item = NewItem(Info());
        item.OverrideContainer = ".mkv";
        item.OverrideCrf = 16;

        Assert.Equal("1.5 MB · 1280 x 720 · MP4 → MKV · 高画质", item.MetaLine);
        Assert.Contains("MKV", item.TargetSummary);
        Assert.Equal("高画质", item.QualityLabel);
    }

    [Fact]
    public void MetaLine_QualityOnlyOverride_KeepsInheritedContainer()
    {
        var item = NewItem(Info());
        item.OverrideCrf = 30;

        Assert.Equal("1.5 MB · 1280 x 720 · MP4 → MP4 · 低画质", item.MetaLine);
    }

    [Fact]
    public void ResetOverride_RestoresInheritedCopy()
    {
        var item = NewItem(Info());
        item.OverrideContainer = ".mkv";
        item.OverrideCrf = 16;
        Assert.True(item.HasOverride);

        item.ResetOverride();

        Assert.False(item.HasOverride);
        Assert.Null(item.OverrideContainer);
        Assert.Null(item.OverrideCrf);
        Assert.Equal("MP4 → MP4 · 中等",
            item.MetaLine[item.MetaLine.IndexOf("MP4 →", StringComparison.Ordinal)..]);
        Assert.Equal(QueueItem.InheritLabel, item.OverrideContainerSelection);
        Assert.Equal(QueueItem.InheritLabel, item.OverrideQualitySelection);
    }

    [Fact]
    public void SelectionBridge_MapsInheritLabelToNull()
    {
        var item = NewItem();

        item.OverrideContainerSelection = ".mkv";
        item.OverrideQualitySelection = "高画质";

        Assert.Equal(".mkv", item.OverrideContainer);
        Assert.Equal(16, item.OverrideCrf);

        item.OverrideContainerSelection = QueueItem.InheritLabel;
        item.OverrideQualitySelection = QueueItem.InheritLabel;

        Assert.Null(item.OverrideContainer);
        Assert.Null(item.OverrideCrf);
        Assert.False(item.HasOverride);
    }

    [Fact]
    public void OverrideChange_NotifiesEveryRowCopyProperty()
    {
        var item = NewItem();
        var seen = new List<string?>();
        item.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

        item.OverrideContainer = ".mkv";
        item.OverrideCrf = 16;
        item.ResetOverride();

        Assert.Contains(nameof(QueueItem.MetaLine), seen);
        Assert.Contains(nameof(QueueItem.TargetSummary), seen);
        Assert.Contains(nameof(QueueItem.QualityLabel), seen);
        Assert.Contains(nameof(QueueItem.HasOverride), seen);
        Assert.Contains(nameof(QueueItem.OverrideContainerSelection), seen);
        Assert.Contains(nameof(QueueItem.OverrideQualitySelection), seen);
    }

    /// <summary>
    /// 回归：ComboBox 赋 ItemsSource 后会把 SelectedItem 置空并回写到双向绑定，
    /// 若把这种 null 当"清除覆盖"，用户设过的逐项参数会在重开浮出层或重启后静默丢失。
    /// </summary>
    [Fact]
    public void SelectionBridge_NullWrite_DoesNotClearOverride()
    {
        var item = NewItem(Info());
        item.OverrideContainer = ".mkv";
        item.OverrideCrf = 16;

        item.OverrideContainerSelection = null!;
        item.OverrideQualitySelection = null!;

        Assert.Equal(".mkv", item.OverrideContainer);
        Assert.Equal(16, item.OverrideCrf);
    }
}

/// <summary>同批执行时，覆盖项用自己的两项参数，继承项仍用全局提供者。</summary>
public class ConversionQueueOverrideTests
{
    private static string TempInput(string name) =>
        Path.Combine(Path.GetTempPath(), "vc-ovr-" + Guid.NewGuid().ToString("N"), name);

    private sealed class CapturingService : IConversionService
    {
        public List<ConversionSettings> Seen { get; } = new();

        public List<string> OutputPaths { get; } = new();

        public Task<ConversionResult> ConvertAsync(
            string inputPath,
            string outputPath,
            ConversionSettings settings,
            IProgress<ConversionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Seen.Add(settings);
            OutputPaths.Add(outputPath);
            return Task.FromResult(ConversionResult.Ok(outputPath, "libx264"));
        }
    }

    [Fact]
    public async Task RunAsync_OverriddenItem_UsesItsOwnContainerAndQualityOnly()
    {
        var service = new CapturingService();
        var globalSettings = ConversionSettingsPresets.New(".mp4") with
        {
            Crf = 23,
            Scale = ResolutionScale.P720,
            Fps = 30,
            AudioBitrateKbps = 192,
        };
        var queue = new ConversionQueue(service) { SettingsProvider = () => globalSettings };
        var overridden = queue.Enqueue(TempInput("a.mp4"), ConversionSettingsPresets.New(".mp4"));
        var inherited = queue.Enqueue(TempInput("b.mp4"), ConversionSettingsPresets.New(".mp4"));
        overridden.OverrideContainer = ".mkv";
        overridden.OverrideCrf = 16;

        await queue.RunAsync();

        Assert.Equal(2, service.Seen.Count);

        var first = service.Seen[0];
        Assert.Equal(".mkv", first.Container);
        Assert.Equal(16, first.Crf);
        // 只覆盖两项：分辨率/帧率/音频码率仍取任务开始时的全局值
        Assert.Equal(ResolutionScale.P720, first.Scale);
        Assert.Equal(30, first.Fps);
        Assert.Equal(192, first.AudioBitrateKbps);

        var second = service.Seen[1];
        Assert.Equal(".mp4", second.Container);
        Assert.Equal(23, second.Crf);
        Assert.Equal(ResolutionScale.P720, second.Scale);

        Assert.EndsWith(".mkv", overridden.OutputPath);
        Assert.EndsWith(".mp4", inherited.OutputPath);
    }

    [Fact]
    public async Task RunAsync_AudioOnlyOverride_ProducesAudioOnlyJob()
    {
        // 全局是视频输出，单独覆盖成 MP3：这一项按"仅音频"执行
        var service = new CapturingService();
        var queue = new ConversionQueue(service)
        {
            SettingsProvider = () => ConversionSettingsPresets.New(".mkv"),
        };
        var item = queue.Enqueue(TempInput("a.mp4"), ConversionSettingsPresets.New(".mp4"));
        item.OverrideContainer = ".mp3";

        await queue.RunAsync();

        Assert.True(service.Seen[0].AudioOnly);
        Assert.EndsWith(".mp3", item.OutputPath);
    }

    [Fact]
    public async Task RunAsync_ItemKeepsItsOwnOutputPath_NoSpuriousSuffix()
    {
        // 恢复出来的任务输出路径就是上次算好的那个：重新解析时不能把它自己算成占用，
        // 否则用户每次都会多拿到一个 _1 文件。
        var service = new CapturingService();
        var queue = new ConversionQueue(service)
        {
            SettingsProvider = () => ConversionSettingsPresets.New(".mp4"),
        };
        var item = queue.Enqueue(TempInput("clip.mp4"), ConversionSettingsPresets.New(".mp4"));
        var original = item.OutputPath;
        Assert.EndsWith("clip_converted.mp4", original);

        await queue.RunAsync();

        Assert.Equal(original, item.OutputPath);
        Assert.EndsWith("clip_converted.mp4", service.OutputPaths[0]);
    }

    [Fact]
    public async Task RunAsync_StoresPanelSettings_WhileOverridesStayInOverrideFields()
    {
        // 覆盖只体现在"这次执行用的参数"上；项里留面板参数，
        // 于是清掉覆盖后行文案立刻回到面板值，不会把上次的覆盖值当成继承值展示。
        var service = new CapturingService();
        var queue = new ConversionQueue(service)
        {
            SettingsProvider = () => ConversionSettingsPresets.New(".mp4") with { Crf = 18 },
        };
        var item = queue.Enqueue(TempInput("a.mp4"), ConversionSettingsPresets.New(".mp4"));
        item.OverrideContainer = ".mkv";
        item.OverrideCrf = 23;

        await queue.RunAsync();

        Assert.Equal(".mkv", service.Seen[0].Container);
        Assert.Equal(23, service.Seen[0].Crf);
        Assert.Equal(".mp4", item.Settings.Container);
        Assert.Equal(18, item.Settings.Crf);
        Assert.EndsWith(".mkv", item.OutputPath);

        item.ResetOverride();

        Assert.Contains("MP4", item.TargetSummary);
        Assert.Equal("高画质", item.QualityLabel);
        Assert.Contains("高画质", item.MetaLine);   // 清掉覆盖后回到面板值（CRF 18），而不是残留的覆盖值（中等）
    }
}
