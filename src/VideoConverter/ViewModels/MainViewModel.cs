using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VideoConverter.Core.Models;
using VideoConverter.Core.Services;

namespace VideoConverter.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly Brush ReadyBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 65));
    private static readonly Brush WarningBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 197, 62, 22));
    private static readonly Brush NormalTextBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 230, 230));

    private const string FfmpegDownloadPage = "https://github.com/BtbN/FFmpeg-Builds/releases";

    private readonly IFfmpegLocator _locator;
    private readonly IMediaProbeService? _probeService;
    private readonly IThumbnailService? _thumbnailService;
    private readonly IConversionService? _conversionService;
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _cts;

    public MainViewModel(IFfmpegLocator locator, DispatcherQueue dispatcher)
    {
        _locator = locator;
        _dispatcher = dispatcher;
        Presets = new ObservableCollection<ConvertPreset>(ConvertPreset.All);
        SelectedPreset = ConvertPreset.FromFormat(TargetFormat.Mp4);
        StatusBrush = NormalTextBrush;

        var binaries = locator.Locate();
        if (binaries is not null)
        {
            _probeService = new FfprobeMediaProbeService(binaries);
            _thumbnailService = new FfmpegThumbnailService(binaries);
            _conversionService = new FfmpegConversionService(binaries, _probeService);

            var version = _locator.GetVersion(binaries);
            FfmpegStatus = $"ffmpeg 就绪 · {DescribeSource(binaries.Source)} · {ShortVersion(version)}";
            FfmpegStatusBrush = ReadyBrush;
            FfmpegWarningVisibility = Visibility.Collapsed;
            FfmpegReady = true;
        }
        else
        {
            FfmpegStatus = "未找到 ffmpeg · 转换功能不可用";
            FfmpegStatusBrush = WarningBrush;
            FfmpegWarningVisibility = Visibility.Visible;
        }
    }

    public ObservableCollection<ConvertPreset> Presets { get; }

    [ObservableProperty]
    private ConvertPreset _selectedPreset = null!;

    [ObservableProperty]
    private string? _selectedVideoPath;

    [ObservableProperty]
    private string _fileName = "尚未选择文件";

    [ObservableProperty]
    private string _fileSizeText = "-";

    [ObservableProperty]
    private string _containerText = "-";

    [ObservableProperty]
    private string _durationText = "-";

    [ObservableProperty]
    private string _resolutionText = "-";

    [ObservableProperty]
    private string _frameRateText = "-";

    [ObservableProperty]
    private string _videoCodecText = "-";

    [ObservableProperty]
    private string _audioCodecText = "-";

    [ObservableProperty]
    private string _audioChannelsText = "-";

    [ObservableProperty]
    private string _bitRateText = "-";

    [ObservableProperty]
    private ImageSource? _thumbnail;

    [ObservableProperty]
    private double _thumbnailPosition;

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private Brush _statusBrush = NormalTextBrush;

    [ObservableProperty]
    private Brush _ffmpegStatusBrush = NormalTextBrush;

    [ObservableProperty]
    private Visibility _ffmpegWarningVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private bool _ffmpegReady;

    [ObservableProperty]
    private string _ffmpegStatus = string.Empty;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    public bool HasVideo => !string.IsNullOrEmpty(SelectedVideoPath);

    partial void OnSelectedVideoPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasVideo));
        if (value is not null)
        {
            UpdateDefaultOutputPath();
        }
    }

    partial void OnSelectedPresetChanged(ConvertPreset value) => UpdateDefaultOutputPath();

    [RelayCommand]
    private async Task LoadVideoAsync(string path)
    {
        if (_probeService is null || !File.Exists(path))
        {
            SetStatus("文件不存在或 ffmpeg 未就绪", isError: true);
            return;
        }

        SelectedVideoPath = path;
        FileName = Path.GetFileName(path);
        FileSizeText = FormatSize(new FileInfo(path).Length);
        ClearMediaInfo();

        try
        {
            var info = await _probeService.ProbeAsync(path);
            ApplyMediaInfo(info);
            ThumbnailPosition = Math.Round((info.Duration?.TotalSeconds ?? 0) * 0.25, 2);
            await CaptureThumbnailAsync();
            SetStatus(string.Empty, isError: false);
        }
        catch (Exception ex)
        {
            Thumbnail = null;
            SetStatus($"加载失败：{ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    private async Task CaptureThumbnailAsync()
    {
        if (_thumbnailService is null || SelectedVideoPath is null)
        {
            return;
        }

        var at = TimeSpan.FromSeconds(Math.Max(0, ThumbnailPosition));
        var output = Path.Combine(Path.GetTempPath(), "VideoConverter", Guid.NewGuid().ToString("N") + ".jpg");

        try
        {
            await _thumbnailService.CaptureAsync(SelectedVideoPath, at, output);
            using var stream = File.OpenRead(output);
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            Thumbnail = bitmap;
        }
        catch (Exception ex)
        {
            SetStatus($"缩略图截取失败：{ex.Message}", isError: true);
        }
        finally
        {
            TryDelete(output);
        }
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (_conversionService is null || SelectedVideoPath is null)
        {
            return;
        }

        var output = string.IsNullOrWhiteSpace(OutputPath) ? BuildDefaultOutputPath() : OutputPath;

        _cts = new CancellationTokenSource();
        IsConverting = true;
        ProgressPercent = 0;
        ProgressText = "准备中…";

        var progress = new Progress<ConversionProgress>(OnProgress);

        try
        {
            var result = await _conversionService.ConvertAsync(
                SelectedVideoPath, output, SelectedPreset.Format, progress, _cts.Token);

            SetStatus(
                result.Success ? $"转换完成：{result.OutputPath}" : $"转换失败：{result.Error}",
                isError: !result.Success);
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消转换", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"转换失败：{ex.Message}", isError: true);
        }
        finally
        {
            IsConverting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelConvert() => _cts?.Cancel();

    [RelayCommand]
    private void OpenFfmpegDownloadPage()
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(FfmpegDownloadPage)
        {
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(startInfo);
    }

    private void OnProgress(ConversionProgress progress)
    {
        if (!_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() => ApplyProgress(progress));
            return;
        }

        ApplyProgress(progress);
    }

    private void ApplyProgress(ConversionProgress progress)
    {
        if (progress.Percent is { } percent)
        {
            ProgressPercent = percent;
            var speed = progress.Speed is { } value ? $" · {value:F1}x" : string.Empty;
            ProgressText = $"{percent:F1}% · {FormatTimeSpan(progress.Processed)} / {FormatTimeSpan(progress.Total)}{speed}";
        }
        else
        {
            var speedOnly = progress.Speed is { } s ? $" · {s:F1}x" : string.Empty;
            ProgressText = $"已处理 {FormatTimeSpan(progress.Processed)}{speedOnly}";
        }
    }

    private void ApplyMediaInfo(MediaInfo info)
    {
        ContainerText = info.ContainerFormat ?? "-";
        DurationText = info.Duration is { } duration ? FormatTimeSpan(duration) : "-";
        ResolutionText = info.Width is { } width && info.Height is { } height ? $"{width} x {height}" : "-";
        FrameRateText = info.FrameRate is { } frameRate ? $"{frameRate:0.###} fps" : "-";
        VideoCodecText = info.VideoCodec ?? "-";
        AudioCodecText = info.AudioCodec ?? "-";
        AudioChannelsText = info.AudioChannels?.ToString() ?? "-";
        BitRateText = info.BitRate is { } bitRate ? $"{bitRate / 1000} kbps" : "-";
    }

    private void ClearMediaInfo()
    {
        ContainerText = DurationText = ResolutionText = FrameRateText = "-";
        VideoCodecText = AudioCodecText = AudioChannelsText = BitRateText = "-";
        Thumbnail = null;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
        StatusBrush = isError ? WarningBrush : NormalTextBrush;
    }

    private void UpdateDefaultOutputPath()
    {
        if (!string.IsNullOrEmpty(SelectedVideoPath))
        {
            OutputPath = BuildDefaultOutputPath();
        }
    }

    private string BuildDefaultOutputPath()
    {
        if (string.IsNullOrEmpty(SelectedVideoPath))
        {
            return string.Empty;
        }

        var directory = Path.GetDirectoryName(SelectedVideoPath);
        var name = Path.GetFileNameWithoutExtension(SelectedVideoPath);
        return string.IsNullOrEmpty(directory)
            ? name + "_converted" + SelectedPreset.Extension
            : Path.Combine(directory, name + "_converted" + SelectedPreset.Extension);
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
