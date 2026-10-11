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
public sealed class QueueItem : System.ComponentModel.INotifyPropertyChanged
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

    private bool _isSelected = true;

    /// <summary>是否参与本次转换（队列项复选框）。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public ConversionProgress? Progress { get; private set; }
    public string? Error { get; private set; }
    public string? UsedEncoder { get; private set; }
    public CancellationTokenSource? Cancellation { get; private set; }

    public string FileName => System.IO.Path.GetFileName(InputPath);

    /// <summary>队列项状态的中文展示文本（UI 无关，供列表直接绑定）。</summary>
    public string StateText => State switch
    {
        QueueItemState.Pending => "等待中",
        QueueItemState.Converting => Progress is { Percent: { } percent }
            ? $"转换中 {percent:F0}%"
            : "转换中",
        QueueItemState.Completed => $"已完成（{UsedEncoder}）",
        QueueItemState.Failed => string.IsNullOrEmpty(Error) ? "失败" : $"失败：{Error}",
        _ => string.Empty,
    };

    public bool IsCompleted => State == QueueItemState.Completed;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void MarkConverting(CancellationTokenSource cts)
    {
        State = QueueItemState.Converting;
        Cancellation = cts;
        Progress = null;
        Error = null;
        RaiseAll();
    }

    public void UpdateProgress(ConversionProgress progress)
    {
        Progress = progress;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Progress)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(StateText)));
    }

    public void MarkCompleted(string usedEncoder)
    {
        State = QueueItemState.Completed;
        UsedEncoder = usedEncoder;
        Progress = null;
        Cancellation?.Dispose();
        Cancellation = null;
        RaiseAll();
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
        RaiseAll();
    }

    public void ResetForRetry()
    {
        State = QueueItemState.Pending;
        Progress = null;
        Error = null;
        UsedEncoder = null;
        RaiseAll();
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
        {
            nameof(State), nameof(StateText), nameof(IsCompleted), nameof(Progress), nameof(Error), nameof(UsedEncoder),
        })
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
