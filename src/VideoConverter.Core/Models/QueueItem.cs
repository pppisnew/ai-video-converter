using VideoConverter.Core.Models;

namespace VideoConverter.Core.Models;

public enum QueueItemState
{
    Pending,
    Converting,
    Completed,
    Failed,
}

/// <summary>队列中的一个转换任务。</summary>
public sealed class QueueItem
{
    public QueueItem(string inputPath, string outputPath, ConversionSettings settings, MediaInfo? info = null)
    {
        Id = Guid.NewGuid().ToString("N");
        InputPath = inputPath;
        OutputPath = outputPath;
        Settings = settings;
        Info = info;
    }

    public string Id { get; }
    public string InputPath { get; }
    public string OutputPath { get; set; }
    public ConversionSettings Settings { get; }
    public MediaInfo? Info { get; init; }

    public QueueItemState State { get; private set; } = QueueItemState.Pending;
    public ConversionProgress? Progress { get; private set; }
    public string? Error { get; private set; }
    public string? UsedEncoder { get; private set; }
    public CancellationTokenSource? Cancellation { get; private set; }

    public string FileName => System.IO.Path.GetFileName(InputPath);

    public void MarkConverting(CancellationTokenSource cts)
    {
        State = QueueItemState.Converting;
        Cancellation = cts;
        Progress = null;
        Error = null;
    }

    public void UpdateProgress(ConversionProgress progress) => Progress = progress;

    public void MarkCompleted(string usedEncoder)
    {
        State = QueueItemState.Completed;
        UsedEncoder = usedEncoder;
        Progress = null;
        Cancellation?.Dispose();
        Cancellation = null;
    }

    public void MarkFailed(string error, bool cancelled = false)
    {
        State = QueueItemState.Failed;
        Error = error;
        if (cancelled)
        {
            UsedEncoder = null;
        }

        Cancellation?.Dispose();
        Cancellation = null;
    }

    public void ResetForRetry()
    {
        State = QueueItemState.Pending;
        Progress = null;
        Error = null;
        UsedEncoder = null;
    }
}
