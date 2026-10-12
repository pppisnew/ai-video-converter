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
    public ConversionSettings Settings { get; private set; }

    /// <summary>更新参数并重算输出路径（参数面板变更时同步到待转换项）。</summary>
    public void UpdateSettings(ConversionSettings settings, string outputPath)
    {
        Settings = settings;
        OutputPath = outputPath;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Settings)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TargetSummary)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(QualityLabel)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(OutputPath)));
    }
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

    /// <summary>源文件扩展名（大写、无点），用于列表行的 "原格式 → 目标格式"。</summary>
    public string SourceFormat =>
        System.IO.Path.GetExtension(InputPath).TrimStart('.').ToUpperInvariant();

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

    /// <summary>徽章用的短状态文案（等待中 / 转换中 / 已完成 / 失败）。</summary>
    public string StateLabel => State switch
    {
        QueueItemState.Pending => "等待中",
        QueueItemState.Converting => "转换中",
        QueueItemState.Completed => "已完成",
        QueueItemState.Failed => "失败",
        _ => string.Empty,
    };

    /// <summary>列表行元信息前缀：大小 · 时长 · 分辨率（信息缺失时给出提示而非空白）。</summary>
    public string MetaPrefix
    {
        get
        {
            var parts = new List<string>();
            if (Info?.SizeBytes is { } size)
            {
                parts.Add(FormatSize(size));
            }

            if (Info?.Duration is { } duration)
            {
                parts.Add(FormatTimeSpan(duration));
            }

            if (Info?.Width is { } width && Info?.Height is { } height)
            {
                parts.Add($"{width} x {height}");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "信息不可用";
        }
    }

    /// <summary>任务行元信息前缀：大小 · 分辨率（原型任务行不带时长，时长走缩略图角标）。</summary>
    public string TaskMetaPrefix
    {
        get
        {
            var parts = new List<string>();
            if (Info?.SizeBytes is { } size)
            {
                parts.Add(FormatSize(size));
            }

            if (Info?.Width is { } width && Info?.Height is { } height)
            {
                parts.Add($"{width} x {height}");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : "信息不可用";
        }
    }

    /// <summary>缩略图右下角时长角标（mm:ss；≥1 小时为 h:mm:ss），时长未知时为空串。</summary>
    public string DurationBadgeText => Info?.Duration is { } duration && duration > TimeSpan.Zero
        ? duration.TotalHours >= 1
            ? $"{duration.Hours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}"
        : string.Empty;

    /// <summary>质量档位文案，按真实 CRF 推导（0 → 原始画质，≤18 → 高画质，≤26 → 中等，其余 → 低画质）。</summary>
    public string QualityLabel => Settings.Crf switch
    {
        0 => "原始画质",
        <= 18 => "高画质",
        <= 26 => "中等",
        _ => "低画质",
    };

    public bool IsCompleted => State == QueueItemState.Completed;

    /// <summary>队列项进度条取值：等待中 0%、转换中实时百分比、已完成 100%、失败保持最后值。</summary>
    public double ProgressValue => State switch
    {
        QueueItemState.Pending => 0,
        QueueItemState.Converting => Progress?.Percent ?? 0,
        QueueItemState.Completed => 100,
        QueueItemState.Failed => Progress?.Percent ?? 0,
        _ => 0,
    };

    /// <summary>任务行进度百分比文案（"42%"），非转换中为空。</summary>
    public string ProgressPercentText =>
        State == QueueItemState.Converting && Progress?.Percent is { } percent
            ? $"{percent:F0}%"
            : string.Empty;

    /// <summary>任务行右侧说明：转换中显示已处理/总时长，失败显示错误原因。</summary>
    public string ProgressDetail => State switch
    {
        QueueItemState.Converting => Progress is { } progress
            ? $"{FormatTimeSpan(progress.Processed)} / {FormatTimeSpan(progress.Total)}"
            : string.Empty,
        QueueItemState.Failed => Error ?? "失败",
        _ => string.Empty,
    };

    /// <summary>是否失败（驱动行内重试按钮的可见性）。</summary>
    public bool IsFailed => State == QueueItemState.Failed;

    /// <summary>是否未在转换中（驱动单项删除按钮的可见性）。</summary>
    public bool IsNotConverting => State != QueueItemState.Converting;

    /// <summary>该队列项的目标格式摘要，如 "MKV · H.265"。</summary>
    public string TargetSummary
    {
        get
        {
            if (Settings.AudioOnly)
            {
                return "MP3 · 仅音频";
            }

            var codec = Settings.Encoder switch
            {
                VideoEncoderChoice.Auto => "自动",
                VideoEncoderChoice.H264 => "H.264",
                VideoEncoderChoice.H265 => "H.265",
                VideoEncoderChoice.Av1 => "AV1",
                VideoEncoderChoice.Vp9 => "VP9",
                _ => "自动",
            };

            return $"{Settings.Container.TrimStart('.').ToUpperInvariant()} · {codec}";
        }
    }

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
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ProgressValue)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ProgressPercentText)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ProgressDetail)));
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
            nameof(State), nameof(StateText), nameof(StateLabel), nameof(IsCompleted), nameof(IsFailed),
            nameof(IsNotConverting), nameof(Progress), nameof(ProgressValue), nameof(ProgressPercentText),
            nameof(ProgressDetail), nameof(Error), nameof(UsedEncoder),
        })
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024d / 1024d:F2} GB"
        : bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:F1} MB"
        : bytes >= 1024 ? $"{bytes / 1024d:F1} KB"
        : $"{bytes} B";

    private static string FormatTimeSpan(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
}
