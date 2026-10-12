namespace VideoConverter.Core.Models;

/// <summary>
/// 队列中一个未完成任务的持久化形态。字段与 QueueItem 一一对应，
/// 恢复时不用重新 ffprobe：媒体信息摘要（时长/大小/分辨率/封装/编码/码率）原样带回。
/// </summary>
public sealed class QueueItemSnapshot
{
    public required string InputPath { get; set; }

    public required string OutputPath { get; set; }

    public required ConversionSettings Settings { get; set; }

    public MediaInfo? Info { get; set; }
}

/// <summary>一次队列持久化的完整快照，落盘在 <c>%LOCALAPPDATA%\VideoConverter\queue.json</c>。</summary>
public sealed class QueueSnapshot
{
    public List<QueueItemSnapshot> Items { get; set; } = new();

    /// <summary>
    /// 只留"未完成"的任务：等待中与失败。已完成的不恢复——
    /// 产物已经在磁盘上，塞回队列只会让人以为要重跑一遍。
    /// </summary>
    public static QueueSnapshot FromItems(IEnumerable<QueueItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new QueueSnapshot
        {
            Items = items
                .Where(i => i.State is QueueItemState.Pending or QueueItemState.Failed)
                .Select(i => new QueueItemSnapshot
                {
                    InputPath = i.InputPath,
                    OutputPath = i.OutputPath,
                    Settings = i.Settings,
                    Info = i.Info,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// 算出这次启动实际能恢复什么：源文件已经不存在的项直接跳过
    /// （恢复出来必然是一个失败任务），其余保持原顺序。
    /// <paramref name="fileExists"/> 供测试注入，默认真实文件系统。
    /// </summary>
    public QueueRestorePlan PlanRestore(Func<string, bool>? fileExists = null)
    {
        var exists = fileExists ?? File.Exists;
        var restorable = new List<QueueItemSnapshot>();
        var skipped = 0;

        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.InputPath) || !exists(item.InputPath))
            {
                skipped++;
                continue;
            }

            restorable.Add(item);
        }

        return new QueueRestorePlan(restorable, skipped);
    }
}

/// <summary>恢复计划：可恢复的项，以及因源文件缺失被跳过的数量。</summary>
public sealed record QueueRestorePlan(IReadOnlyList<QueueItemSnapshot> Items, int SkippedMissing);
