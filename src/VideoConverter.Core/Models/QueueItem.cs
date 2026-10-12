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
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MetaLine)));
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

    /// <summary>
    /// 队列行元信息单行文案，如 "1.7 MB · 720 x 1280 · MP4 → MKV · 自动 · 高画质（已覆盖）"。
    /// 故意合成一个字符串而不是多个 TextBlock 横向排列：后者超宽时会把靠后的元素
    /// 直接挤出视野（实测加上"（继承全局）"后质量档位被吃掉），单个 TextBlock 只会
    /// 末端截断加省略号。时长不在这里——缩略图角标已经显示，避免同一行重复。
    /// 末尾的"（已覆盖）/（继承全局）"是参数来源标注：P5 起逐项覆盖生效，
    /// 用户必须能一眼看出哪些行不再跟随右侧面板。
    /// </summary>
    public string MetaLine
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

            // 行内只给"原格式 → 目标容器"：编码器标签（自动/H.265…）留给详情，
            // 否则 1180 DIP 下整行会在"（已覆盖）/（继承全局）"之前就被截断，
            // 用户看不到这个参数来源标注。
            parts.Add($"{SourceFormat} → {EffectiveContainerLabel}");
            parts.Add(QualityLabel);

            return string.Join(" · ", parts);
        }
    }

    /// <summary>生效容器的短标签（去点、大写；.mp3 显示为"仅音频"）。</summary>
    public string EffectiveContainerLabel => IsAudioOnlyTarget
        ? "仅音频"
        : EffectiveContainer.TrimStart('.').ToUpperInvariant();

    /// <summary>缩略图右下角时长角标（mm:ss；≥1 小时为 h:mm:ss），时长未知时为空串。</summary>
    public string DurationBadgeText => Info?.Duration is { } duration && duration > TimeSpan.Zero
        ? duration.TotalHours >= 1
            ? $"{duration.Hours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}"
        : string.Empty;

    /// <summary>质量档位文案，按**生效** CRF 推导（覆盖优先，见 <see cref="QualityLabelFor"/>）。</summary>
    public string QualityLabel => QualityLabelFor(EffectiveCrf);

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

    /// <summary>是否正在转换。任务列表页据此显示「取消」按钮。</summary>
    public bool IsConverting => State == QueueItemState.Converting;

    /// <summary>
    /// 该队列项的目标格式摘要，如 "MKV · H.265"。容器取生效值（逐项覆盖优先），
    /// 编码器不可逐项覆盖，仍来自该项入队/开跑时捕获的设置。
    /// 参数来源（已覆盖 / 继承全局）由 <see cref="MetaLine"/> 在行末统一标注。
    /// </summary>
    public string TargetSummary
    {
        get
        {
            if (IsAudioOnlyTarget)
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

            return $"{EffectiveContainer.TrimStart('.').ToUpperInvariant()} · {codec}";
        }
    }

    /// <summary>覆盖下拉里的"继承全局"选项，同时用作行末状态标注。</summary>
    public const string InheritLabel = "继承全局";

    /// <summary>覆盖容器的下拉选项：继承全局 + 应用支持的容器。</summary>
    public static IReadOnlyList<string> OverrideContainerChoices { get; } =
        new[] { InheritLabel }.Concat(ConversionSettingsPresets.Containers).ToArray();

    /// <summary>覆盖质量的下拉选项：继承全局 + 四个质量档位。</summary>
    public static IReadOnlyList<string> OverrideQualityChoices { get; } =
        new[] { InheritLabel, "原始画质", "高画质", "中等", "低画质" };

    private string? _overrideContainer;
    private int? _overrideCrf;

    /// <summary>
    /// 逐项覆盖：目标容器（null = 继承右侧面板的全局设置）。
    /// 覆盖是**字段级**的：只影响容器与质量，分辨率/帧率/音频码率始终跟随全局面板。
    /// </summary>
    public string? OverrideContainer
    {
        get => _overrideContainer;
        set
        {
            if (_overrideContainer == value)
            {
                return;
            }

            _overrideContainer = value;
            RaiseOverrideChanged();
        }
    }

    /// <summary>逐项覆盖：质量 CRF（null = 继承右侧面板）。</summary>
    public int? OverrideCrf
    {
        get => _overrideCrf;
        set
        {
            if (_overrideCrf == value)
            {
                return;
            }

            _overrideCrf = value;
            RaiseOverrideChanged();
        }
    }

    /// <summary>是否设置了任一逐项覆盖（驱动"恢复继承"按钮与行文案）。</summary>
    public bool HasOverride => OverrideContainer is not null || OverrideCrf is not null;

    /// <summary>行末参数来源标注：已覆盖 / 继承全局。</summary>
    public string OverrideStateLabel => HasOverride ? "已覆盖" : InheritLabel;

    /// <summary>回退：两项覆盖一起清掉，重新继承全局面板。</summary>
    public void ResetOverride()
    {
        if (!HasOverride)
        {
            return;
        }

        _overrideContainer = null;
        _overrideCrf = null;
        RaiseOverrideChanged();
    }

    /// <summary>
    /// 覆盖容器的下拉展示值（"继承全局" ⇄ null），供 ComboBox 双向绑定。
    /// 注意：ComboBox 在 ItemsSource 赋值后会把 SelectedItem 重置为 null 并回写，
    /// 若把这种 null 当作"清除覆盖"，用户设过的逐项参数会在重开浮出层/重启后静默丢失。
    /// 因此 null 写入一律忽略（清除只能走 ResetOverride），并刷新一次展示值。
    /// </summary>
    public string OverrideContainerSelection
    {
        get => OverrideContainer ?? InheritLabel;
        set
        {
            if (value is null)
            {
                RaiseOverrideChanged();
                return;
            }

            OverrideContainer = value == InheritLabel ? null : value;
        }
    }

    /// <summary>覆盖质量的下拉展示值（"继承全局" ⇄ null），供 ComboBox 双向绑定（null 写入同样忽略，理由见上）。</summary>
    public string OverrideQualitySelection
    {
        get => OverrideCrf is { } crf ? QualityLabelFor(crf) : InheritLabel;
        set
        {
            if (value is null)
            {
                RaiseOverrideChanged();
                return;
            }

            OverrideCrf = value == InheritLabel ? null : QualityCrfFor(value);
        }
    }

    /// <summary>生效容器：覆盖优先，否则该项当前设置（入队/开跑时捕获的全局值）。</summary>
    public string EffectiveContainer => OverrideContainer ?? Settings.Container;

    /// <summary>生效质量：覆盖优先，否则该项当前设置。</summary>
    public int EffectiveCrf => OverrideCrf ?? Settings.Crf;

    private bool IsAudioOnlyTarget => EffectiveContainer.Equals(".mp3", StringComparison.OrdinalIgnoreCase);

    /// <summary>质量档位文案（0 → 原始画质，≤18 → 高画质，≤26 → 中等，其余 → 低画质）。</summary>
    public static string QualityLabelFor(int crf) => crf switch
    {
        0 => "原始画质",
        <= 18 => "高画质",
        <= 26 => "中等",
        _ => "低画质",
    };

    /// <summary>质量下拉文案 → CRF（覆盖档位取值：原始 0 / 高 16 / 中等 23 / 低 30）。</summary>
    public static int QualityCrfFor(string label) => label switch
    {
        "原始画质" => 0,
        "高画质" => 16,
        "中等" => 23,
        _ => 30,
    };

    /// <summary>
    /// 把逐项覆盖叠加到全局面板上：只替换容器与质量，其余字段（分辨率/帧率/音频码率/编码器）
    /// 仍取任务开始时捕获的全局值。没有覆盖时原样返回入参。
    /// </summary>
    public ConversionSettings ApplyOverride(ConversionSettings baseSettings)
    {
        ArgumentNullException.ThrowIfNull(baseSettings);
        if (!HasOverride)
        {
            return baseSettings;
        }

        var container = OverrideContainer ?? baseSettings.Container;
        return baseSettings with
        {
            Container = container,
            Crf = OverrideCrf ?? baseSettings.Crf,
            // 容器决定是否纯音频：覆盖成 .mp3 即提取音频，覆盖回视频容器则恢复视频输出
            AudioOnly = container.Equals(".mp3", StringComparison.OrdinalIgnoreCase),
        };
    }

    private void RaiseOverrideChanged()
    {
        foreach (var name in new[]
        {
            nameof(OverrideContainer), nameof(OverrideCrf), nameof(HasOverride),
            nameof(OverrideContainerSelection), nameof(OverrideQualitySelection),
            nameof(OverrideStateLabel), nameof(EffectiveContainer), nameof(EffectiveCrf),
            nameof(TargetSummary), nameof(QualityLabel), nameof(MetaLine),
        })
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
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
