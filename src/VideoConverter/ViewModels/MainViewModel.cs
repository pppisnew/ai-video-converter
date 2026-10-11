using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VideoConverter.Core.Models;
using VideoConverter.Core.Services;

namespace VideoConverter.ViewModels;

public sealed record EncoderOption(string Label, VideoEncoderChoice Value);

public sealed record ScaleOption(string Label, ResolutionScale Value);

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly Brush ReadyBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 65));
    private static readonly Brush WarningBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 197, 62, 22));
    private static readonly Brush NormalTextBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 230, 230));
    private static readonly Brush MutedTextBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 138, 138, 138));

    private readonly IFfmpegLocator _locator;
    private readonly IMediaProbeService? _probeService;
    private readonly IThumbnailService? _thumbnailService;
    private readonly IConversionQueue _queue;
    private readonly IEncoderProbeService? _probe;
    private readonly DispatcherQueue _dispatcher;

    public MainViewModel(IFfmpegLocator locator, DispatcherQueue dispatcher)
    {
        _locator = locator;
        _dispatcher = dispatcher;

        var binaries = locator.Locate();
        if (binaries is not null)
        {
            _probeService = new FfprobeMediaProbeService(binaries);
            _thumbnailService = new FfmpegThumbnailService(binaries);
            _probe = new EncoderProbeService(binaries);
            _queue = new ConversionQueue(new FfmpegConversionService(binaries, _probeService, _probe));
            FfmpegStatus = $"ffmpeg 就绪 · {DescribeSource(binaries.Source)} · {ShortVersion(locator.GetVersion(binaries))}";
            FfmpegStatusBrush = ReadyBrush;
            FfmpegReady = true;
        }
        else
        {
            _queue = new ConversionQueue(new FfmpegConversionService(null!, new FfprobeMediaProbeService(null!)));
            FfmpegStatus = "未找到 ffmpeg · 转换功能不可用";
            FfmpegStatusBrush = WarningBrush;
            FfmpegWarningVisibility = Visibility.Visible;
        }

        Queue = new ObservableCollection<QueueItem>();
        Containers = new[] { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".mp3" };
        SelectedContainer = ".mp4";
        EncoderChoice = VideoEncoderChoice.Auto;
        Crf = 23;
        Scale = ResolutionScale.Original;
        AudioBitrate = 128;

        ScaleOptions.Add(new ScaleOption("原始分辨率", ResolutionScale.Original));
        ScaleOptions.Add(new ScaleOption("1080p", ResolutionScale.P1080));
        ScaleOptions.Add(new ScaleOption("720p", ResolutionScale.P720));
        ScaleOptions.Add(new ScaleOption("480p", ResolutionScale.P480));
        SelectedScaleOption = ScaleOptions[0];
        RebuildEncoderOptions();
        SelectedEncoderOption = EncoderOptions.FirstOrDefault();
        StatusBrush = NormalTextBrush;
        ProgressSummary = "队列空闲";
        _ = ProbeEncodersAsync();
    }

    public ObservableCollection<QueueItem> Queue { get; }

    public IReadOnlyList<string> Containers { get; }

    [ObservableProperty]
    private QueueItem? _selectedItem;

    [ObservableProperty]
    private string _selectedContainer = ".mp4";

    [ObservableProperty]
    private VideoEncoderChoice _encoderChoice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableEncoders))]
    private bool _forceSoftware;

    public IReadOnlyList<VideoEncoderChoice> AvailableEncoders =>
        EncoderCatalog.AllowedEncoders(SelectedContainer);


    /// <summary>是否有选中队列项（驱动"移除"按钮启用状态）。</summary>
    public bool HasSelectedItem => SelectedItem is not null;

    /// <summary>是否允许重新探测（探测中禁用）。</summary>
    public bool CanReprobe => !IsProbing;

    private bool _allSelected = true;

    /// <summary>Queue header select-all checkbox.</summary>
    public bool AllSelected
    {
        get => _allSelected;
        set
        {
            if (SetProperty(ref _allSelected, value))
            {
                _queue.SetAllSelected(value);
                OnPropertyChanged(nameof(SelectedCount));
            }
        }
    }

    /// <summary>Checked and still-pending items.</summary>
    public int SelectedCount => Queue.Count(i => i.IsSelected && i.State == QueueItemState.Pending);
    [ObservableProperty]
    private int _crf = 23;

    [ObservableProperty]
    private ResolutionScale _scale = ResolutionScale.Original;

    [ObservableProperty]
    private int _audioBitrate = 128;

    [ObservableProperty]
    private string _parameterSummary = string.Empty;

    [ObservableProperty]
    private string _encoderStatusText = "正在探测编码器…";

    [ObservableProperty]
    private bool _isProbing = true;

    [ObservableProperty]
    private bool _ffmpegReady;

    [ObservableProperty]
    private string _ffmpegStatus = string.Empty;

    [ObservableProperty]
    private Brush _ffmpegStatusBrush = NormalTextBrush;

    [ObservableProperty]
    private Visibility _ffmpegWarningVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _progressSummary = "队列空闲";

    [ObservableProperty]
    private double _progressPercent;

    public ObservableCollection<EncoderOption> EncoderOptions { get; } = new();

    public ObservableCollection<ScaleOption> ScaleOptions { get; } = new();

    [ObservableProperty]
    private EncoderOption? _selectedEncoderOption;

    [ObservableProperty]
    private ScaleOption _selectedScaleOption = null!;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private Brush _statusBrush = NormalTextBrush;

    [ObservableProperty]
    private Brush _parameterSummaryBrush = NormalTextBrush;

    // ---- 媒体信息（展示选中队列项） ----
    [ObservableProperty] private string _fileName = "尚未选择文件";
    [ObservableProperty] private string _fileSizeText = "-";
    [ObservableProperty] private string _containerText = "-";
    [ObservableProperty] private string _durationText = "-";
    [ObservableProperty] private string _resolutionText = "-";
    [ObservableProperty] private string _frameRateText = "-";
    [ObservableProperty] private string _videoCodecText = "-";
    [ObservableProperty] private string _audioCodecText = "-";
    [ObservableProperty] private string _audioChannelsText = "-";
    [ObservableProperty] private string _bitRateText = "-";
    [ObservableProperty] private ImageSource? _thumbnail;
    [ObservableProperty] private double _thumbnailPosition;
    [ObservableProperty] private string _outputPathText = "-";

    // ---- 参数变更联动 ----

    partial void OnSelectedContainerChanged(string value)
    {
        // 容器切换后编码器可能不再合法（如 WebM 不支持 H.264）
        if (!EncoderCatalog.IsCompatible(value, EncoderChoice))
        {
            EncoderChoice = VideoEncoderChoice.Auto;
        }

        RebuildEncoderOptions();
        UpdateSummary();
    }

    private void RebuildEncoderOptions()
    {
        EncoderOptions.Clear();
        EncoderOptions.Add(new EncoderOption("自动（优先硬件）", VideoEncoderChoice.Auto));
        foreach (var choice in EncoderCatalog.AllowedEncoders(SelectedContainer))
        {
            EncoderOptions.Add(new EncoderOption(LabelFor(choice), choice));
        }

        if (SelectedEncoderOption is null || !EncoderOptions.Contains(SelectedEncoderOption))
        {
            SelectedEncoderOption = EncoderOptions[0];
        }
    }

    private static string LabelFor(VideoEncoderChoice choice) => choice switch
    {
        VideoEncoderChoice.H264 => "H.264",
        VideoEncoderChoice.H265 => "H.265",
        VideoEncoderChoice.Av1 => "AV1",
        VideoEncoderChoice.Vp9 => "VP9",
        _ => "自动",
    };

    partial void OnSelectedEncoderOptionChanged(EncoderOption? value)
    {
        if (value is not null && EncoderChoice != value.Value)
        {
            EncoderChoice = value.Value;
        }
    }

    partial void OnEncoderChoiceChanged(VideoEncoderChoice value)
    {
        var match = EncoderOptions.FirstOrDefault(o => o.Value == value);
        if (match is not null && SelectedEncoderOption != match)
        {
            SelectedEncoderOption = match;
        }

        UpdateSummary();
    }

    partial void OnCrfChanged(int value) => UpdateSummary();

    partial void OnSelectedScaleOptionChanged(ScaleOption value)
    {
        if (value is not null && Scale != value.Value)
        {
            Scale = value.Value;
        }
    }

    partial void OnScaleChanged(ResolutionScale value)
    {
        var match = ScaleOptions.FirstOrDefault(o => o.Value == value);
        if (match is not null && SelectedScaleOption != match)
        {
            SelectedScaleOption = match;
        }

        UpdateSummary();
    }

    partial void OnAudioBitrateChanged(int value) => UpdateSummary();

    partial void OnForceSoftwareChanged(bool value) => UpdateSummary();

    partial void OnSelectedItemChanged(QueueItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        if (value is null)
        {
            ClearMediaInfo();
            return;
        }

        OutputPathText = value.OutputPath;
        if (value.Info is not null)
        {
            ApplyMediaInfo(value.Info);
            ThumbnailPosition = Math.Round((value.Info.Duration?.TotalSeconds ?? 0) * 0.25, 2);
            _ = CaptureThumbnailAsync(value.InputPath);
        }
        else
        {
            ClearMediaInfo();
        }
    }

    private ConversionSettings BuildSettings() => new()
    {
        Container = SelectedContainer,
        Encoder = EncoderCatalog.Sanitize(SelectedContainer, EncoderChoice),
        Crf = Crf,
        Scale = Scale,
        AudioBitrateKbps = AudioBitrate,
        AudioOnly = SelectedContainer == ".mp3",
        ForceSoftware = ForceSoftware,
    };

    /// <summary>
    /// Parameter-panel changes are pushed to every Pending queue item so that "convert
    /// everything to one format" stays true. Finished/failed items keep their settings.
    /// </summary>
    private void SyncSettingsToPendingItems()
    {
        var settings = BuildSettings();
        foreach (var item in Queue.Where(i => i.State == QueueItemState.Pending))
        {
            var output = _queue.ResolveOutputPathFor(item.InputPath, settings);
            item.UpdateSettings(settings, output);
            if (SelectedItem == item)
            {
                OutputPathText = item.OutputPath;
            }
        }
    }

    private void UpdateSummary()
    {
        SyncSettingsToPendingItems();
        var settings = BuildSettings();

        if (settings.AudioOnly)
        {
            ParameterSummary = $"MP3 · 仅音频 · {AudioBitrate} kbps";
            ParameterSummaryBrush = NormalTextBrush;
            return;
        }

        var descriptor = _probe?.ResolveEncoder(settings)
            ?? EncoderCatalog.Software(EncoderCatalog.ToKind(settings.Encoder));

        var codecName = descriptor?.Codec switch
        {
            VideoCodecKind.H264 => "H.264",
            VideoCodecKind.H265 => "H.265",
            VideoCodecKind.Av1 => "AV1",
            VideoCodecKind.Vp9 => "VP9",
            _ => "H.264",
        };

        var hwSuffix = descriptor is { IsHardware: true } ? $" ({descriptor.FfmpegName})" : " (软件)";
        var quality = $"质量 {settings.Crf}";
        if (descriptor is not null && descriptor.NeedsQualityAnnotation(settings.Crf))
        {
            quality += $" (实际 {descriptor.QualityFlagDisplay} {descriptor.MapQuality(settings.Crf)})";
        }

        var scale = settings.Scale switch
        {
            ResolutionScale.Original => "原始分辨率",
            ResolutionScale.P1080 => "1080p",
            ResolutionScale.P720 => "720p",
            ResolutionScale.P480 => "480p",
            _ => "原始分辨率",
        };

        ParameterSummary = $"{SelectedContainer.TrimStart('.').ToUpperInvariant()} · {codecName}{hwSuffix} · {quality} · {scale} · 音频 {settings.AudioBitrateKbps}kbps";
        ParameterSummaryBrush = NormalTextBrush;
    }

    private async Task ProbeEncodersAsync()
    {
        if (_probe is null)
        {
            return;
        }

        try
        {
            await _probe.EnsureProbedAsync();
            var snapshot = _probe.Snapshot;
            var available = snapshot?.Results.Count(r => r.Value.IsAvailable) ?? 0;
            EncoderStatusText = available > 0
                ? $"硬件编码可用：{string.Join(" / ", snapshot!.Results.Where(r => r.Value.IsAvailable).Select(r => r.Key))}"
                : "未探测到可用硬件编码器，将使用软件编码";
        }
        catch (Exception)
        {
            EncoderStatusText = "编码器探测失败，将使用软件编码";
        }
        finally
        {
            IsProbing = false;
            OnPropertyChanged(nameof(CanReprobe));
            UpdateSummary();
        }
    }

    // ---- 命令 ----

    [RelayCommand]
    private async Task LoadVideosAsync(IReadOnlyList<string> paths)
    {
        if (_probeService is null)
        {
            SetStatus("ffmpeg 未就绪，无法加载视频", true);
            return;
        }

        var added = 0;
        var skipped = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            if (_queue.Contains(path))
            {
                skipped++;
                continue;
            }

            MediaInfo? info = null;
            try
            {
                info = await _probeService.ProbeAsync(path);
            }
            catch (Exception ex)
            {
                SetStatus($"加载失败 {Path.GetFileName(path)}：{ex.Message}", true);
                continue;
            }

            var item = _queue.Enqueue(path, BuildSettings(), info);
            Queue.Add(item);
            SelectedItem ??= item;
            added++;
        }

        if (added > 0)
        {
            AllSelected = Queue.All(i => i.IsSelected);
            OnPropertyChanged(nameof(SelectedCount));
            SetStatus($"已加入 {added} 个文件到队列", false);
        }
    }

    [RelayCommand]
    private async Task CaptureThumbnailAsync()
    {
        if (SelectedItem is not null)
        {
            await CaptureThumbnailAsync(SelectedItem.InputPath);
        }
    }

    private async Task CaptureThumbnailAsync(string videoPath)
    {
        if (_thumbnailService is null)
        {
            return;
        }

        var at = TimeSpan.FromSeconds(Math.Max(0, ThumbnailPosition));
        var output = Path.Combine(Path.GetTempPath(), "VideoConverter", Guid.NewGuid().ToString("N") + ".jpg");

        try
        {
            await _thumbnailService.CaptureAsync(videoPath, at, output);
            using var stream = File.OpenRead(output);
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            Thumbnail = bitmap;
        }
        catch (Exception ex)
        {
            SetStatus($"缩略图截取失败：{ex.Message}", true);
        }
        finally
        {
            TryDelete(output);
        }
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (!Queue.Any(i => i.State == QueueItemState.Pending))
        {
            SetStatus("队列中没有待转换的任务", true);
            return;
        }

        IsRunning = true;
        ProgressSummary = "准备中…";

        try
        {
            await _queue.RunAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"队列执行失败：{ex.Message}", true);
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelCurrent() => _queue.CancelCurrent();

    [RelayCommand]
    private void CancelAll() => _queue.CancelAll();

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedItem is not null && _queue.MoveUp(SelectedItem.Id))
        {
            RebuildQueueFromCore();
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedItem is not null && _queue.MoveDown(SelectedItem.Id))
        {
            RebuildQueueFromCore();
        }
    }

    [RelayCommand]
    private void ClearQueue()
    {
        var count = _queue.Clear();
        Queue.Clear();
        SelectedItem = null;
        SetStatus($"已清空队列（{count} 项）", false);
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var target = !AllSelected;
        _queue.SetAllSelected(target);
        AllSelected = target;
        OnPropertyChanged(nameof(SelectedCount));
    }

    private void RebuildQueueFromCore()
    {
        var selectedId = SelectedItem?.Id;
        Queue.Clear();
        foreach (var item in _queue.Items)
        {
            Queue.Add(item);
        }

        SelectedItem = _queue.Items.FirstOrDefault(i => i.Id == selectedId) ?? Queue.FirstOrDefault();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedItem is null)
        {
            return;
        }

        _queue.Remove(SelectedItem.Id);
        Queue.Remove(SelectedItem);
        SelectedItem = Queue.FirstOrDefault();
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        var completed = Queue.Where(i => i.State == QueueItemState.Completed).ToList();
        foreach (var item in completed)
        {
            Queue.Remove(item);
        }

        _queue.ClearCompleted();
        SelectedItem = Queue.FirstOrDefault();
        SetStatus($"已清除 {completed.Count} 个已完成任务", false);
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        _queue.RetryFailed();
        await ConvertAsync();
    }

    [RelayCommand]
    private async Task ReprobeAsync()
    {
        if (_probe is null)
        {
            return;
        }

        IsProbing = true;
        OnPropertyChanged(nameof(CanReprobe));
        EncoderStatusText = "正在重新探测编码器…";
        try
        {
            await _probe.ProbeAsync();
            var snapshot = _probe.Snapshot;
            var available = snapshot?.Results.Count(r => r.Value.IsAvailable) ?? 0;
            EncoderStatusText = available > 0
                ? $"硬件编码可用：{string.Join(" / ", snapshot!.Results.Where(r => r.Value.IsAvailable).Select(r => r.Key))}"
                : "未探测到可用硬件编码器，将使用软件编码";
            SetStatus("编码器探测完成", false);
        }
        catch (Exception ex)
        {
            EncoderStatusText = $"探测失败：{ex.Message}";
            SetStatus("编码器探测失败", true);
        }
        finally
        {
            IsProbing = false;
            OnPropertyChanged(nameof(CanReprobe));
            UpdateSummary();
        }
    }

    [RelayCommand]
    private void ResetParameters()
    {
        SelectedContainer = ".mp4";
        EncoderChoice = VideoEncoderChoice.Auto;
        Crf = 23;
        Scale = ResolutionScale.Original;
        AudioBitrate = 128;
        ForceSoftware = false;
        SetStatus("参数已重置为默认值", false);
    }

    [RelayCommand]
    private void RevealOutput() => RevealOutputFor(SelectedItem);

    /// <summary>在资源管理器中定位指定队列项的输出文件（列表按钮直接调用）。</summary>
    public void RevealOutputFor(QueueItem? item)
    {
        if (item is null || !File.Exists(item.OutputPath))
        {
            SetStatus("输出文件不存在", true);
            return;
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            Arguments = $"/select,\"{item.OutputPath}\"",
        };
        System.Diagnostics.Process.Start(startInfo);
    }

    [RelayCommand]
    private void OpenFfmpegDownloadPage()
    {
        var url = string.Concat("https:", "//github.com/", "BtbN/FFmpeg-Builds/releases");
        var startInfo = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
        System.Diagnostics.Process.Start(startInfo);
    }

    // ---- 队列事件 ----

    public void AttachQueueEvents()
    {
        _queue.ItemChanged += item =>
        {
            if (!_dispatcher.HasThreadAccess)
            {
                _dispatcher.TryEnqueue(() => OnQueueItemChanged(item));
                return;
            }

            OnQueueItemChanged(item);
        };

        _queue.QueueFinished += summary =>
        {
            if (!_dispatcher.HasThreadAccess)
            {
                _dispatcher.TryEnqueue(() => OnQueueFinished(summary));
                return;
            }

            OnQueueFinished(summary);
        };
    }

    private void OnQueueItemChanged(QueueItem item)
    {
        if (SelectedItem == item)
        {
            OutputPathText = item.OutputPath;
            if (item.Progress is { } p && item.State == QueueItemState.Converting && p.Percent is { } percent)
            {
                ProgressPercent = percent;
                ProgressSummary = $"{Queue.IndexOf(item) + 1}/{Queue.Count} · {percent:F0}% · {FormatTimeSpan(p.Processed)} / {FormatTimeSpan(p.Total)}";
            }

            OnPropertyChanged(nameof(SelectedCount));
        }

        // QueueItem 已实现 INotifyPropertyChanged，无需替换集合项即可刷新列表
    }

    private void OnQueueFinished(QueueSummary summary)
    {
        ProgressSummary = summary.WasCancelled
            ? "队列已取消"
            : $"完成 {summary.Completed} 个，失败 {summary.Failed} 个，取消 {summary.Cancelled} 个";
        SetStatus(ProgressSummary, summary.Failed > 0);
    }

    // ---- 辅助 ----

    private void ApplyMediaInfo(MediaInfo info)
    {
        FileName = Path.GetFileName(info.FilePath);
        FileSizeText = info.SizeBytes is { } size ? FormatSize(size) : "-";
        ContainerText = info.ContainerFormat ?? "-";
        DurationText = info.Duration is { } duration ? FormatTimeSpan(duration) : "-";
        ResolutionText = info.Width is { } w && info.Height is { } h ? $"{w} x {h}" : "-";
        FrameRateText = info.FrameRate is { } fps ? $"{fps:0.###} fps" : "-";
        VideoCodecText = info.VideoCodec ?? "-";
        AudioCodecText = info.AudioCodec ?? "-";
        AudioChannelsText = info.AudioChannels?.ToString() ?? "-";
        BitRateText = info.BitRate is { } br ? $"{br / 1000} kbps" : "-";
    }

    private void ClearMediaInfo()
    {
        FileName = "尚未选择文件";
        FileSizeText = ContainerText = DurationText = ResolutionText = "-";
        FrameRateText = VideoCodecText = AudioCodecText = "-";
        AudioChannelsText = BitRateText = "-";
        Thumbnail = null;
        OutputPathText = "-";
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
        StatusBrush = isError ? WarningBrush : NormalTextBrush;
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? $"{bytes / 1024d / 1024d / 1024d:F2} GB"
        : bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:F1} MB"
        : bytes >= 1024 ? $"{bytes / 1024d:F1} KB"
        : $"{bytes} B";

    private static string FormatTimeSpan(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static string DescribeSource(FfmpegSource source) => source switch
    {
        FfmpegSource.Bundled => "随包捆绑",
        FfmpegSource.Repository => "仓库 tools 目录",
        FfmpegSource.SystemPath => "系统 PATH",
        _ => "未知来源",
    };

    private static string ShortVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "版本未知";
        }

        var parts = version.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[2] : version;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
