using System.Text.Json;
using VideoConverter.Core.Models;
using Xunit;

namespace VideoConverter.Core.Tests;

/// <summary>
/// 队列行展开区（媒体信息详情）：6 个只读技术字段 + <see cref="QueueItem.IsExpanded"/> 展开态。
/// 字段必须挂在 QueueItem 上——展开区可以多行同时打开，绑"当前选中项"的 ViewModel 属性
/// 会让所有展开行显示同一份数据；展开态则是纯 UI 状态，不写进队列快照。
/// </summary>
public class QueueItemDetailTests
{
    private static readonly string InputPath =
        Path.Combine(Path.GetTempPath(), "vc-detail", "clip.mp4");

    private static QueueItem NewItem(MediaInfo? info = null) => new(
        InputPath,
        Path.Combine(Path.GetTempPath(), "vc-detail", "clip_converted.mp4"),
        ConversionSettingsPresets.New(".mp4"),
        info);

    private static MediaInfo Info() => new()
    {
        FilePath = InputPath,
        ContainerFormat = "mov,mp4,m4a,3gp,3g2,mj2",
        Duration = TimeSpan.FromSeconds(17.27),
        SizeBytes = 1_776_068,
        BitRate = 822_888,
        VideoCodec = "hevc",
        Width = 720,
        Height = 1280,
        FrameRate = 30,
        AudioCodec = "aac",
        AudioChannels = 1,
        SampleRate = 48_000,
    };

    [Fact]
    public void DetailTexts_WithInfo_MirrorProbeValues()
    {
        var item = NewItem(Info());

        Assert.Equal("mov,mp4,m4a,3gp,3g2,mj2", item.ContainerDetailText);
        Assert.Equal("hevc", item.VideoCodecDetailText);
        Assert.Equal("aac", item.AudioCodecDetailText);
        Assert.Equal("30 fps", item.FrameRateDetailText);
        Assert.Equal("1", item.AudioChannelsDetailText);
        Assert.Equal("822 kbps", item.BitRateDetailText);
    }

    [Fact]
    public void DetailTexts_WithoutInfo_FallBackToDash()
    {
        var item = NewItem();

        Assert.Equal("-", item.ContainerDetailText);
        Assert.Equal("-", item.VideoCodecDetailText);
        Assert.Equal("-", item.AudioCodecDetailText);
        Assert.Equal("-", item.FrameRateDetailText);
        Assert.Equal("-", item.AudioChannelsDetailText);
        Assert.Equal("-", item.BitRateDetailText);
    }

    [Fact]
    public void DetailTexts_WithInfoButMissingFields_FallBackToDash()
    {
        // ffprobe 缺字段（纯音频 / 探测失败的部分信息）时不能露出空串或 "0"
        var item = NewItem(new MediaInfo { FilePath = InputPath });

        Assert.Equal("-", item.ContainerDetailText);
        Assert.Equal("-", item.VideoCodecDetailText);
        Assert.Equal("-", item.AudioCodecDetailText);
        Assert.Equal("-", item.FrameRateDetailText);
        Assert.Equal("-", item.AudioChannelsDetailText);
        Assert.Equal("-", item.BitRateDetailText);
    }

    [Fact]
    public void FrameRateDetailText_KeepsUpToThreeDecimals()
    {
        var item = NewItem(Info() with { FrameRate = 29.97 });

        Assert.Equal("29.97 fps", item.FrameRateDetailText);
    }

    [Fact]
    public void BitRateDetailText_UsesKbps()
    {
        var item = NewItem(Info() with { BitRate = 1_306_011 });

        Assert.Equal("1306 kbps", item.BitRateDetailText);
    }

    [Fact]
    public void IsExpanded_DefaultsToFalse()
    {
        var item = NewItem(Info());

        Assert.False(item.IsExpanded);
        Assert.Equal("展开媒体信息", item.ExpandToggleLabel);
    }

    [Fact]
    public void IsExpanded_TogglesLabelAndNotifies()
    {
        var item = NewItem(Info());
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        item.IsExpanded = true;

        Assert.True(item.IsExpanded);
        Assert.Equal("收起媒体信息", item.ExpandToggleLabel);
        Assert.Contains(nameof(QueueItem.IsExpanded), changed);
        Assert.Contains(nameof(QueueItem.ExpandToggleLabel), changed);

        changed.Clear();
        item.IsExpanded = true;
        Assert.Empty(changed);   // 同值不重复通知

        item.IsExpanded = false;
        Assert.Equal("展开媒体信息", item.ExpandToggleLabel);
    }

    [Fact]
    public void IsExpanded_DoesNotLeakIntoQueueSnapshot()
    {
        var item = NewItem(Info());
        item.IsExpanded = true;

        var snapshot = QueueSnapshot.FromItems(new[] { item });
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Single(snapshot.Items);
        Assert.DoesNotContain("IsExpanded", json, StringComparison.Ordinal);
        // 恢复出来的新项（= 重启后的队列）一律收起
        Assert.False(NewItem(Info()).IsExpanded);
    }
}
