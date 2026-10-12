using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Windows.ApplicationModel.DataTransfer;

namespace VideoConverter.ViewModels;

public sealed record EncoderOption(string Label, VideoEncoderChoice Value);

public sealed record ScaleOption(string Label, ResolutionScale Value);

/// <summary>帧率选项（Value 为 null 表示保持原始帧率）。</summary>
public sealed record FpsOption(string Label, int? Value);

/// <summary>设置页主题选项（标签 + 取值）。</summary>
public sealed record ThemeOption(string Label, AppTheme Value);

/// <summary>任务列表的筛选条件。</summary>
public enum TaskFilter
{
    All,
    Converting,
    Pending,
    Completed,
    Failed,
}

public sealed partial class MainViewModel : ObservableObject
{
    // 颜色一律取主题资源：应用跟随系统明暗主题，硬编码的浅色文字在浅色主题下几乎不可见。
    private static readonly Brush ReadyBrush =
        ThemeBrush("SystemFillColorSuccessBrush", Windows.UI.Color.FromArgb(255, 16, 124, 65));
    private static readonly Brush WarningBrush =
        ThemeBrush("SystemFillColorCriticalBrush", Windows.UI.Color.FromArgb(255, 197, 62, 22));
    private static readonly Brush NormalTextBrush =
        ThemeBrush("TextFillColorPrimaryBrush", Windows.UI.Color.FromArgb(255, 27, 27, 27));
    private static readonly Brush MutedTextBrush =
        ThemeBrush("TextFillColorSecondaryBrush", Windows.UI.Color.FromArgb(255, 93, 93, 93));

    private static Brush ThemeBrush(string key, Windows.UI.Color fallback)
    {
        if (Application.Current?.Resources is { } resources
            && resources.TryGetValue(key, out var value)
            && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private readonly IFfmpegLocator _locator;
    private readonly IMediaProbeService? _probeService;
    private readonly IThumbnailService? _thumbnailService;
    private readonly IConversionQueue _queue;
    private readonly IEncoderProbeService? _probe;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _statusTimer;
    private readonly AppSettingsStore _settingsStore;
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly QueueStore _queueStore;
    private readonly DispatcherQueueTimer _queueSaveTimer;

    public MainViewModel(IFfmpegLocator locator, DispatcherQueue dispatcher)
    {
        _locator = locator;
        _dispatcher = dispatcher;

        // 右下浮层提示：显示后 4 秒自动消失，每条新消息都重新计时（不占布局高度）
        _statusTimer = _dispatcher.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(4);
        _statusTimer.IsRepeating = false;
        _statusTimer.Tick += (_, _) => StatusVisible = Visibility.Collapsed;

        _settingsStore = new AppSettingsStore();
        _queueStore = new QueueStore();

        // 设置落盘防抖：拖质量滑杆会连续触发，合并成一次写盘
        _saveTimer = _dispatcher.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(250);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settingsStore.Save(CaptureSettings());
        };

        var binaries = locator.Locate();
        if (binaries is not null)
        {
            _probeService = new FfprobeMediaProbeService(binaries);
            _thumbnailService = new FfmpegThumbnailService(binaries);
            _probe = new EncoderProbeService(binaries);
            _queue = new ConversionQueue(new FfmpegConversionService(binaries, _probeService, _probe))
        {
            SettingsProvider = BuildSettings,
        };
            FfmpegStatus = $"ffmpeg 就绪 · {DescribeSource(binaries.Source)} · {ShortVersion(locator.GetVersion(binaries))}";
            FfmpegStatusBrush = ReadyBrush;
            FfmpegStatusGlyph = "\uE930";
            FfmpegReady = true;
            FfmpegShortStatus = "ffmpeg 就绪";
        }
        else
        {
            _queue = new ConversionQueue(new FfmpegConversionService(null!, new FfprobeMediaProbeService(null!)));
            FfmpegStatus = "未找到 ffmpeg · 转换功能不可用";
            FfmpegStatusBrush = WarningBrush;
            FfmpegStatusGlyph = "\uEA39";
            FfmpegWarningVisibility = Visibility.Visible;
            FfmpegShortStatus = "未找到 ffmpeg";
        }

        Queue = new ObservableCollection<QueueItem>();
        // 与逐项覆盖下拉同源：支持的容器只在 ConversionSettingsPresets 里定义一次
        Containers = ConversionSettingsPresets.Containers;

        // 队列快照防抖：批量增删/连续状态变更合并成一次写盘
        // （必须在 Queue 建好之后创建：lambda 里要读它）
        _queueSaveTimer = _dispatcher.CreateTimer();
        _queueSaveTimer.Interval = TimeSpan.FromMilliseconds(250);
        _queueSaveTimer.IsRepeating = false;
        _queueSaveTimer.Tick += (_, _) =>
        {
            _queueSaveTimer.Stop();
            _queueStore.Save(QueueSnapshot.FromItems(Queue));
        };

        // 选项集合必须先填充：OnScaleChanged / RebuildEncoderOptions 都依赖它们
        ScaleOptions.Add(new ScaleOption("原始分辨率", ResolutionScale.Original));
        ScaleOptions.Add(new ScaleOption("1080p", ResolutionScale.P1080));
        ScaleOptions.Add(new ScaleOption("720p", ResolutionScale.P720));
        ScaleOptions.Add(new ScaleOption("480p", ResolutionScale.P480));

        FpsOptions.Add(new FpsOption("保持原始", null));
        FpsOptions.Add(new FpsOption("60 fps", 60));
        FpsOptions.Add(new FpsOption("30 fps", 30));
        FpsOptions.Add(new FpsOption("24 fps", 24));

        // 再读回上次的偏好；顺序反了会在启动瞬间把存下来的设置覆盖掉
        var saved = _settingsStore.Load();
        SelectedContainer = saved.Container;
        EncoderChoice = VideoEncoderChoice.Auto;
        Crf = saved.Crf;
        Scale = saved.Scale;
        Fps = saved.Fps;
        AudioBitrate = saved.AudioBitrateKbps;
        Theme = saved.Theme;
        OpenFolderAfterConversion = saved.OpenFolderAfterConversion;
        RestoreQueueOnStartup = saved.RestoreQueueOnStartup;

        // Scale 变更已把 SelectedScaleOption 同步过去；兜底防空（枚举被收敛过不该发生）
        SelectedScaleOption ??= ScaleOptions[0];
        SelectedFpsOption ??= FpsOptions[0];
        RebuildEncoderOptions();
        SelectedEncoderOption = EncoderOptions.FirstOrDefault();
        StatusBrush = NormalTextBrush;
        RestoreQueueFromSnapshot();
        _ = ProbeEncodersAsync();
        RefreshQueueSummary();
    }

    public ObservableCollection<QueueItem> Queue { get; }

    public IReadOnlyList<string> Containers { get; }

    /// <summary>关于区展示的应用版本（取自程序集版本）。</summary>
    public string AppVersionText { get; } = ResolveAppVersion();

    private static string ResolveAppVersion()
    {
        var version = typeof(MainViewModel).Assembly.GetName().Version;
        return version is null
            ? "版本未知 · WinUI 3 / Windows App SDK"
            : $"版本 {version.ToString(3)} · WinUI 3 / Windows App SDK";
    }

    /// <summary>主题偏好变更：由 MainWindow 订阅后施加到窗口根元素。</summary>
    public event Action<AppTheme>? ThemeChanged;

    [ObservableProperty]
    private AppTheme _theme = AppTheme.System;

    partial void OnThemeChanged(AppTheme value)
    {
        ThemeChanged?.Invoke(value);
        ScheduleSettingsSave();
    }

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

    /// <summary>Queue header select-all checkbox. Setting it applies to every item.</summary>
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

    /// <summary>
    /// 只回写全选复选框的展示态，不触发 SetAllSelected，
    /// 避免把用户刚取消的单项又默默全选上。
    /// </summary>
    private void SyncAllSelected()
    {
        var all = Queue.Count > 0 && Queue.All(i => i.IsSelected);
        if (_allSelected != all)
        {
            SetProperty(ref _allSelected, all, nameof(AllSelected));
        }
    }

    /// <summary>队列是否为空（驱动空态提示）。</summary>
    public bool QueueEmpty => Queue.Count == 0;

    /// <summary>队列头右侧的计数文案，例如 "3 个文件"。</summary>
    public string QueueCountText => $"{Queue.Count} 个文件";

    /// <summary>是否存在失败任务（驱动重试失败项按钮）。</summary>
    public bool HasFailedItems => Queue.Any(i => i.State == QueueItemState.Failed);

    /// <summary>开始转换按钮的可用条件：未在运行且有勾选的待转换任务。</summary>
    public bool CanStartConvert => !IsRunning && SelectedCount > 0;

    /// <summary>操作条左侧的批次提示，例如：参数将应用到 3 个已勾选任务。</summary>
    public string BatchHintText
    {
        get
        {
            if (!Queue.Any(i => i.State == QueueItemState.Pending))
            {
                return "队列中没有待转换任务";
            }

            return SelectedCount > 0
                ? $"参数将应用到 {SelectedCount} 个已勾选任务"
                : "队列中没有勾选任务";
        }
    }

    /// <summary>Checked and still-pending items.</summary>
    public int SelectedCount => Queue.Count(i => i.IsSelected && i.State == QueueItemState.Pending);

    /// <summary>Queue page header summary, e.g. "4 items - 1 running, 3 waiting".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCount))]
    private string _queueSummaryText = "队列为空";

    /// <summary>任务列表统计卡：全部 / 转换中 / 已完成 / 失败。</summary>
    public int TotalTaskCount => Queue.Count;
    public int RunningTaskCount => Queue.Count(i => i.State == QueueItemState.Converting);
    public int CompletedTaskCount => Queue.Count(i => i.State == QueueItemState.Completed);
    public int FailedTaskCount => Queue.Count(i => i.State == QueueItemState.Failed);

    /// <summary>按当前筛选条件过滤后的任务列表（与 Queue 共享同一批 QueueItem 实例）。</summary>
    public ObservableCollection<QueueItem> FilteredTasks { get; } = new();

    [ObservableProperty]
    private TaskFilter _taskFilter = TaskFilter.All;

    partial void OnTaskFilterChanged(TaskFilter value) => ApplyTaskFilter();

    /// <summary>筛选后列表为空（驱动任务列表空态）。</summary>
    public bool TaskListEmpty => FilteredTasks.Count == 0;

    [RelayCommand]
    private void SetTaskFilter(string? filter)
    {
        if (!string.IsNullOrEmpty(filter) && Enum.TryParse<TaskFilter>(filter, ignoreCase: true, out var parsed))
        {
            TaskFilter = parsed;
        }
    }

    private bool MatchesTaskFilter(QueueItem item) => TaskFilter switch
    {
        TaskFilter.Converting => item.State == QueueItemState.Converting,
        TaskFilter.Pending => item.State == QueueItemState.Pending,
        TaskFilter.Completed => item.State == QueueItemState.Completed,
        TaskFilter.Failed => item.State == QueueItemState.Failed,
        _ => true,
    };

    /// <summary>
    /// 增量同步筛选结果：只增删/移动变化项，避免每次进度刷新都重建集合
    /// （重建会让任务列表滚动位置与选中态跳回开头）。
    /// </summary>
    private void ApplyTaskFilter()
    {
        var wanted = Queue.Where(MatchesTaskFilter).ToList();

        for (var i = FilteredTasks.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(FilteredTasks[i]))
            {
                FilteredTasks.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var item = wanted[i];
            var existing = FilteredTasks.IndexOf(item);
            if (existing < 0)
            {
                FilteredTasks.Insert(i, item);
            }
            else if (existing != i)
            {
                FilteredTasks.Move(existing, i);
            }
        }

        OnPropertyChanged(nameof(TaskListEmpty));
    }

    private int _activeQueueCount;

    /// <summary>运行中 + 等待中 的任务数，驱动导航项 InfoBadge。</summary>
    public int ActiveQueueCount
    {
        get => _activeQueueCount;
        private set => SetProperty(ref _activeQueueCount, value);
    }

    /// <summary>队列徽标可见性：有活动任务时才显示，避免空闲时挂着 0。</summary>
    public Visibility QueueBadgeVisibility => ActiveQueueCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void RefreshQueueSummary()
    {
        var total = Queue.Count;
        if (total == 0)
        {
            QueueSummaryText = "队列为空";
            ActiveQueueCount = 0;
        }
        else
        {
            var parts = new List<string>();
            var running = Queue.Count(i => i.State == QueueItemState.Converting);
            var waiting = Queue.Count(i => i.State == QueueItemState.Pending);
            var done = Queue.Count(i => i.State == QueueItemState.Completed);
            var failed = Queue.Count(i => i.State == QueueItemState.Failed && i.Error?.Contains("已取消") != true);

            if (running > 0) parts.Add($"转换中 {running}");
            if (waiting > 0) parts.Add($"等待中 {waiting}");
            if (done > 0) parts.Add($"已完成 {done}");
            if (failed > 0) parts.Add($"失败 {failed}");
            QueueSummaryText = $"共 {total} 项 · " + string.Join(" · ", parts);
            ActiveQueueCount = running + waiting;
        }

        OnPropertyChanged(nameof(QueueBadgeVisibility));

        SyncAllSelected();
        OnPropertyChanged(nameof(QueueEmpty));
        OnPropertyChanged(nameof(QueueCountText));
        OnPropertyChanged(nameof(TotalTaskCount));
        OnPropertyChanged(nameof(RunningTaskCount));
        OnPropertyChanged(nameof(CompletedTaskCount));
        OnPropertyChanged(nameof(FailedTaskCount));
        OnPropertyChanged(nameof(HasFailedItems));
        OnPropertyChanged(nameof(CanStartConvert));
        OnPropertyChanged(nameof(BatchHintText));
        ApplyTaskFilter();
    }

    [ObservableProperty]
    private int _crf = 23;

    [ObservableProperty]
    private ResolutionScale _scale = ResolutionScale.Original;

    /// <summary>目标帧率；null = 保持原始帧率。</summary>
    [ObservableProperty]
    private int? _fps;

    [ObservableProperty]
    private int _audioBitrate = 128;

    [ObservableProperty]
    private string _parameterSummary = string.Empty;
    /// <summary>Global output folder; empty means next to each source file.</summary>
    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    /// <summary>输出目录的展示文案：为空时说明会与源文件同目录。</summary>
    public string OutputDirectoryDisplay =>
        string.IsNullOrWhiteSpace(OutputDirectory) ? "与源文件同目录" : OutputDirectory;

    partial void OnOutputDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(OutputDirectoryDisplay));
        _queue.OutputDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
        var settings = BuildSettings();
        foreach (var item in Queue.Where(i => i.State == QueueItemState.Pending))
        {
            item.UpdateSettings(settings, _queue.ResolveOutputPathFor(item.InputPath, settings, item.Id));
            if (SelectedItem == item)
            {
                OutputPathText = item.OutputPath;
            }
        }
    }

    [RelayCommand]
    private async Task BrowseOutputDirectoryAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary,
        };
        MainWindow.InitializePickerWithWindow(picker);
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            OutputDirectory = folder.Path;
        }
    }

    /// <summary>整队成功结束后在资源管理器中定位产物（默认开，可在设置页关掉）。</summary>
    [ObservableProperty]
    private bool _openFolderAfterConversion = true;

    partial void OnOpenFolderAfterConversionChanged(bool value) => ScheduleSettingsSave();

    /// <summary>启动时恢复上次未完成的任务（默认开）。</summary>
    [ObservableProperty]
    private bool _restoreQueueOnStartup = true;

    partial void OnRestoreQueueOnStartupChanged(bool value) => ScheduleSettingsSave();


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

    /// <summary>导航底部状态脚图标：就绪=对勾，缺失=错误。</summary>
    [ObservableProperty]
    private string _ffmpegStatusGlyph = "\uE930";

    [ObservableProperty]
    private Visibility _ffmpegWarningVisibility = Visibility.Collapsed;

    /// <summary>顶部药丸里的短状态；完整版本/来源/路径放在 FfmpegStatus 走 ToolTip。</summary>
    [ObservableProperty]
    private string _ffmpegShortStatus = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartConvert))]
    [NotifyPropertyChangedFor(nameof(BatchHintText))]
    private bool _isRunning;

    [ObservableProperty]
    private string _progressSummary = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    public ObservableCollection<EncoderOption> EncoderOptions { get; } = new();

    public ObservableCollection<ScaleOption> ScaleOptions { get; } = new();

    /// <summary>帧率选项：保持原始 / 60 / 30 / 24。</summary>
    public ObservableCollection<FpsOption> FpsOptions { get; } = new();

    /// <summary>主题选项：浅色 / 深色 / 跟随系统。</summary>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption("浅色", AppTheme.Light),
        new ThemeOption("深色", AppTheme.Dark),
        new ThemeOption("跟随系统", AppTheme.System),
    };


    [ObservableProperty]
    private EncoderOption? _selectedEncoderOption;

    [ObservableProperty]
    private ScaleOption _selectedScaleOption = null!;

    [ObservableProperty]
    private FpsOption _selectedFpsOption = null!;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private Brush _statusBrush = NormalTextBrush;

    /// <summary>
    /// 右下浮层提示的可见性：有消息即显示，4 秒后自动收起。
    /// 类型为 Visibility 而非 bool，因为 MainWindow 是 Window 根，不能走 x:Bind + 转换器。
    /// </summary>
    [ObservableProperty]
    private Visibility _statusVisible = Visibility.Collapsed;

    /// <summary>提示图标字形（成功/信息与错误各一），与 StatusBrush 同色。</summary>
    [ObservableProperty]
    private string _statusGlyph = "\uE930";

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
        ScheduleSettingsSave();
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

    partial void OnCrfChanged(int value)
    {
        UpdateSummary();
        ScheduleSettingsSave();
    }

    partial void OnSelectedScaleOptionChanged(ScaleOption value)
    {
        if (value is not null && Scale != value.Value)
        {
            Scale = value.Value;
        }
    }

    partial void OnScaleChanged(ResolutionScale value)
    {
        ScheduleSettingsSave();
        var match = ScaleOptions.FirstOrDefault(o => o.Value == value);
        if (match is not null && SelectedScaleOption != match)
        {
            SelectedScaleOption = match;
        }

        UpdateSummary();
    }

    partial void OnAudioBitrateChanged(int value)
    {
        UpdateSummary();
        ScheduleSettingsSave();
    }

    partial void OnSelectedFpsOptionChanged(FpsOption value)
    {
        if (value is not null && Fps != value.Value)
        {
            Fps = value.Value;
        }
    }

    partial void OnFpsChanged(int? value)
    {
        ScheduleSettingsSave();
        var match = FpsOptions.FirstOrDefault(o => o.Value == value);
        if (match is not null && SelectedFpsOption != match)
        {
            SelectedFpsOption = match;
        }

        UpdateSummary();
    }

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
        Fps = Fps,
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
            var output = _queue.ResolveOutputPathFor(item.InputPath, settings, item.Id);
            item.UpdateSettings(settings, output);
            if (SelectedItem == item)
            {
                OutputPathText = item.OutputPath;
            }
        }
    }

    private void UpdateSummary()
    {
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

        var frameRate = settings.Fps is { } fps ? $" · {fps}fps" : string.Empty;
        ParameterSummary = $"{SelectedContainer.TrimStart('.').ToUpperInvariant()} · {codecName}{hwSuffix} · {quality} · {scale}{frameRate} · 音频 {settings.AudioBitrateKbps}kbps";
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
            TrackItem(item);
            Queue.Add(item);
            SelectedItem ??= item;
            added++;
        }

        if (added > 0)
        {
            OnPropertyChanged(nameof(SelectedCount));
            ScheduleQueueSave();
        }

        // Report even when everything was a duplicate (added == 0), otherwise the user
        // gets no feedback at all and assumes the click did nothing.
        if (added > 0 || skipped > 0)
        {
            var message = skipped > 0
                ? $"已加入 {added} 个文件，跳过 {skipped} 个已在队列中的文件"
                : $"已加入 {added} 个文件到队列";
            SetStatus(message, false);
        }

        RefreshQueueSummary();
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
            // 缩略图按 220 DIP 显示，高 DPI 下按 440 物理像素解码才不会发虚
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 440 };
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
        if (SelectedCount == 0)
        {
            SetStatus(Queue.Any(i => i.State == QueueItemState.Pending)
                ? "没有勾选任何待转换任务，请先在队列中勾选"
                : "队列中没有待转换的任务", true);
            return;
        }

        IsRunning = true;
        ProgressPercent = 0;
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
        if (IsRunning)
        {
            SetStatus("队列运行中，请先停止再清空", true);
            return;
        }

        foreach (var item in Queue)
        {
            UntrackItem(item);
        }

        var count = _queue.Clear();
        Queue.Clear();
        SelectedItem = null;
        RefreshQueueSummary();
        ScheduleQueueSave();
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
        ScheduleQueueSave();
        RefreshQueueSummary();
    }

    [RelayCommand]
    private void RemoveSelected() => RemoveItem(SelectedItem);

    /// <summary>行内上移 / 下移：直接按队列项 Id 调整顺序，不依赖当前选中项。</summary>
    public void MoveItemUp(QueueItem? item)
    {
        if (item is not null && _queue.MoveUp(item.Id))
        {
            RebuildQueueFromCore();
        }
    }

    public void MoveItemDown(QueueItem? item)
    {
        if (item is not null && _queue.MoveDown(item.Id))
        {
            RebuildQueueFromCore();
        }
    }

    /// <summary>行内重试单个失败项：重置为等待中，随后由"开始转换"统一执行。</summary>
    public void RetryItem(QueueItem? item)
    {
        if (item is null || item.State != QueueItemState.Failed)
        {
            return;
        }

        item.ResetForRetry();
        RefreshQueueSummary();
        ScheduleQueueSave();
        SetStatus($"已重新加入 {item.FileName}", false);
    }

    /// <summary>
    /// 行内取消单个任务。队列严格顺序，同一时刻只有一个任务在转换，
    /// 因此"取消这一项"实际等价于取消当前项；点的不是当前项时给出提示。
    /// </summary>
    public void CancelItem(QueueItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.State != QueueItemState.Converting)
        {
            SetStatus("只能取消正在转换的任务", true);
            return;
        }

        _queue.CancelCurrent();
    }

    /// <summary>把当前偏好打包成设置对象。</summary>
    private AppSettings CaptureSettings() => new()
    {
        Theme = Theme,
        Container = SelectedContainer,
        Crf = Crf,
        Scale = Scale,
        Fps = Fps,
        AudioBitrateKbps = AudioBitrate,
        OpenFolderAfterConversion = OpenFolderAfterConversion,
        RestoreQueueOnStartup = RestoreQueueOnStartup,
    };

    /// <summary>排一次防抖落盘；构造期的高频赋值只会写一次。</summary>
    private void ScheduleSettingsSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>排一次队列快照落盘；批量增删与连续状态变更合并成一次写盘。</summary>
    private void ScheduleQueueSave()
    {
        _queueSaveTimer.Stop();
        _queueSaveTimer.Start();
    }

    /// <summary>
    /// 启动恢复：把上次会话未完成的任务放回队列，一律"等待中"，绝不自动开跑。
    /// 媒体信息摘要来自快照，启动时不需要再跑 ffprobe。
    /// </summary>
    private void RestoreQueueFromSnapshot()
    {
        if (!RestoreQueueOnStartup || _queueStore.Load() is not { } snapshot)
        {
            return;
        }

        var (items, skipped) = snapshot.PlanRestore();
        var restored = 0;

        foreach (var entry in items)
        {
            var item = _queue.Enqueue(entry.InputPath, entry.Settings, entry.Info);
            // 快照里的输出路径优先：用户在设置页选过输出目录时，恢复出来的目标不能变回源目录
            item.OutputPath = entry.OutputPath;
            item.OverrideContainer = entry.OverrideContainer;
            item.OverrideCrf = entry.OverrideCrf;
            TrackItem(item);
            Queue.Add(item);
            restored++;
        }

        if (restored == 0 && skipped == 0)
        {
            return;
        }

        SetStatus(skipped > 0
            ? $"已恢复 {restored} 个未完成任务，跳过 {skipped} 个已缺失文件"
            : $"已恢复 {restored} 个未完成任务", false);
    }

    /// <summary>移除单个队列项（列表项上的删除按钮直接调用）。转换中的项不可移除。</summary>
    public void RemoveItem(QueueItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (!_queue.Remove(item.Id))
        {
            SetStatus("正在转换的任务无法移除，可先取消当前任务", true);
            return;
        }

        UntrackItem(item);
        Queue.Remove(item);
        if (SelectedItem == item)
        {
            SelectedItem = Queue.FirstOrDefault();
        }

        RefreshQueueSummary();
        ScheduleQueueSave();
        SetStatus($"已移除 {item.FileName}", false);
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        var completed = Queue.Where(i => i.State == QueueItemState.Completed).ToList();
        foreach (var item in completed)
        {
            UntrackItem(item);
            Queue.Remove(item);
        }

        _queue.ClearCompleted();
        SelectedItem = Queue.FirstOrDefault();
        RefreshQueueSummary();
        if (completed.Count > 0)
        {
            ScheduleQueueSave();
        }

        SetStatus($"已清除 {completed.Count} 个已完成任务", false);
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        var retried = _queue.RetryFailed();
        if (retried == 0)
        {
            SetStatus("没有失败的任务", true);
            return;
        }

        RefreshQueueSummary();
        ScheduleQueueSave();
        SetStatus($"已重新加入 {retried} 个失败任务", false);
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
        Fps = null;
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

        if (!TryRevealInExplorer(item.OutputPath))
        {
            SetStatus("无法打开资源管理器", true);
        }
    }

    [RelayCommand]
    private void OpenFfmpegDownloadPage() =>
        OpenExternalUrl(string.Concat("https:", "//github.com/", "BtbN/FFmpeg-Builds/releases"));

    /// <summary>
    /// 打开发布页。应用没有可查询的更新接口，所以这里不做成"检查更新"——
    /// 文案与真实行为一致，用户点下去看到的就是版本说明页。
    /// </summary>
    [RelayCommand]
    private void OpenReleasesPage() =>
        OpenExternalUrl(string.Concat("https:", "//github.com/", "pppisnew/ai-video-converter/releases"));

    /// <summary>把 ffmpeg 版本/来源信息复制到剪贴板，便于排查环境问题。</summary>
    [RelayCommand]
    private void CopyFfmpegStatus()
    {
        var data = new DataPackage();
        data.SetText(FfmpegStatus);
        Clipboard.SetContent(data);
        SetStatus("已复制 ffmpeg 版本信息", false);
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
        // 进度条始终跟踪正在转换的那一项，与用户当前选中哪一项无关
        if (item.State == QueueItemState.Converting && item.Progress is { } p && p.Percent is { } percent)
        {
            ProgressPercent = percent;
            ProgressSummary = $"{item.FileName} · {percent:F0}% · {FormatTimeSpan(p.Processed)} / {FormatTimeSpan(p.Total)}";
        }

        if (SelectedItem == item)
        {
            OutputPathText = item.OutputPath;
        }

        OnPropertyChanged(nameof(SelectedCount));

        // QueueItem 已实现 INotifyPropertyChanged，无需替换集合项即可刷新列表
        RefreshQueueSummary();
    }

    private void OnQueueFinished(QueueSummary summary)
    {
        // 结果只走底部状态栏；进度区回到空闲态，避免与队列摘要重复或自相矛盾
        var result = summary.WasCancelled
            ? "队列已取消"
            : $"完成 {summary.Completed} 个，失败 {summary.Failed} 个，取消 {summary.Cancelled} 个";
        SetStatus(result, summary.Failed > 0);
        ProgressPercent = 0;
        ProgressSummary = string.Empty;
        RefreshQueueSummary();
        ScheduleQueueSave();

        // 整队成功（有产物且没有失败）时才自动定位到第一个产物。
        // shell 调用失败只当没发生：转换本身已经成功，不该因为打不开资源管理器报错。
        if (OpenFolderAfterConversion
            && summary.Completed > 0
            && summary.Failed == 0
            && Queue.FirstOrDefault(i => i.State == QueueItemState.Completed) is { } firstCompleted)
        {
            TryRevealInExplorer(firstCompleted.OutputPath);
        }
    }

    // ---- 队列项属性订阅（复选框勾选态变化需要回写全选态与计数） ----

    private void TrackItem(QueueItem item) => item.PropertyChanged += OnQueueItemPropertyChanged;

    private void UntrackItem(QueueItem item) => item.PropertyChanged -= OnQueueItemPropertyChanged;

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 逐项覆盖也要落盘，否则关掉应用后"已覆盖"会悄悄丢失
        if (e.PropertyName is nameof(QueueItem.OverrideContainer) or nameof(QueueItem.OverrideCrf))
        {
            if (!_dispatcher.HasThreadAccess)
            {
                _dispatcher.TryEnqueue(ScheduleQueueSave);
                return;
            }

            ScheduleQueueSave();
            return;
        }

        if (e.PropertyName != nameof(QueueItem.IsSelected))
        {
            return;
        }

        if (!_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() =>
            {
                SyncAllSelected();
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(BatchHintText));
            });
            return;
        }

        SyncAllSelected();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(BatchHintText));
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
        StatusGlyph = isError ? "\uEA39" : "\uE930";
        StatusVisible = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;

        _statusTimer.Stop();
        _statusTimer.Start();
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

    /// <summary>
    /// 用系统默认程序打开网页。非打包环境下没有浏览器关联、被策略拦下都很常见，
    /// 打不开只是拿不到外部资源，不该以异常形式冒到应用层。
    /// </summary>
    private static void OpenExternalUrl(string url)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex) when (IsShellFailure(ex))
        {
        }
    }

    /// <summary>在资源管理器中选中文件；返回是否成功，shell 失败一律不抛。</summary>
    private static bool TryRevealInExplorer(string path)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                Arguments = $"/select,\"{path}\"",
            };
            System.Diagnostics.Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (IsShellFailure(ex))
        {
            return false;
        }
    }

    /// <summary>Process.Start 在 shell 层能抛出的异常集合（含无关联程序与权限不足）。</summary>
    private static bool IsShellFailure(Exception ex) =>
        ex is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException;
}
