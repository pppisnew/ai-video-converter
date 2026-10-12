using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VideoConverter.Core.Models;
using VideoConverter.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

namespace VideoConverter.Views;

public sealed partial class ConvertPage : Page
{
    private const double SettingsBreakpoint = 1100;
    private const double SettingsColumnWidth = 372;
    private const int SettingsRowIndex = 4;

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".flv", ".wmv", ".ts",
        ".m2ts", ".mts", ".m4v", ".mpg", ".mpeg", ".3gp", ".rmvb", ".vob",
    };

    private static readonly Brush DragStroke = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
    private static readonly Brush DragFill = new SolidColorBrush(Color.FromArgb(26, 0, 120, 212));

    private Brush? _restingStroke;
    private Brush? _restingFill;
    private bool _thumbnailDialogOpen;

    public ConvertPage()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        SizeChanged += OnPageSizeChanged;
    }

    /// <summary>页面绑定源：应用级共享 ViewModel（x:Bind 需要页面自身暴露该属性）。</summary>
    public MainViewModel ViewModel { get; }

    private async void PickFileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
        MainWindow.InitializePickerWithWindow(picker);
        foreach (var extension in VideoExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        var files = await picker.PickMultipleFilesAsync();
        if (files is { Count: > 0 })
        {
            await ViewModel.LoadVideosCommand.ExecuteAsync(files.Select(f => f.Path).ToList());
        }
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // 拖放逻辑仍在窗口层；这里只订阅"拖入/离开"状态做高亮
        MainWindow.DragOverChanged -= OnDragOverChanged;
        MainWindow.DragOverChanged += OnDragOverChanged;
        _restingStroke = DropzoneStroke.Stroke;
        _restingFill = DropzoneStroke.Fill;
        ApplyBreakpoint();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e) =>
        MainWindow.DragOverChanged -= OnDragOverChanged;

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) => ApplyBreakpoint();

    /// <summary>
    /// 断点不能靠 VisualStateManager/AdaptiveTrigger 放在 ScrollViewer 内的网格上（实测不触发），
    /// 这里直接读 XamlRoot 宽度（DIP、窗口基准），每次尺寸变化都重算。
    ///
    /// 宽度只决定"谁来滚"：
    /// - 宽窗：整页不滚，队列列表与设置面板参数区各自滚动，主操作常驻面板底部；
    /// - 窄窗：整页滚，两处内部滚动关掉（避免嵌套滚动抢滚轮），设置面板落到第 5 行整宽展示。
    /// </summary>
    private void ApplyBreakpoint()
    {
        var width = XamlRoot?.Size.Width ?? 0;
        if (width <= 0)
        {
            return;
        }

        var singleColumn = width < SettingsBreakpoint;

        // 列：窄窗收掉右列，面板换到左列第 5 行整宽展示
        SettingsColumn.Width = singleColumn ? new GridLength(0) : new GridLength(SettingsColumnWidth);
        Grid.SetColumn(SettingsPanel, singleColumn ? 0 : 1);
        Grid.SetRow(SettingsPanel, singleColumn ? SettingsRowIndex : 1);
        Grid.SetRowSpan(SettingsPanel, singleColumn ? 1 : 3);

        // 单列时消掉列间距，否则左列右侧会留下一条空隙
        ConvertLayout.ColumnSpacing = singleColumn ? 0 : 20;

        // 行：宽窗队列区吃满剩余高度（列表自己滚），窄窗回到 Auto 交给整页滚动
        QueueRow.Height = singleColumn ? GridLength.Auto : new GridLength(1, GridUnitType.Star);

        PageScroll.VerticalScrollMode = singleColumn ? ScrollMode.Enabled : ScrollMode.Disabled;
        PageScroll.VerticalScrollBarVisibility =
            singleColumn ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

        var innerMode = singleColumn ? ScrollMode.Disabled : ScrollMode.Auto;
        var innerBar = singleColumn ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        ScrollViewer.SetVerticalScrollMode(QueueList, innerMode);
        ScrollViewer.SetVerticalScrollBarVisibility(QueueList, innerBar);
        ScrollViewer.SetVerticalScrollMode(SettingsScroll, innerMode);
        ScrollViewer.SetVerticalScrollBarVisibility(SettingsScroll, innerBar);
    }

    private void OnDragOverChanged(bool active)
    {
        DropzoneStroke.Stroke = active ? DragStroke : _restingStroke;
        DropzoneStroke.Fill = active ? DragFill : _restingFill;
    }

    private void DropzoneButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        DropzoneStroke.Stroke = DragStroke;

    private void DropzoneButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        DropzoneStroke.Stroke = _restingStroke;

    private void RemoveQueueItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.RemoveItem(item);
        }
    }

    private void MoveQueueItemUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.MoveItemUp(item);
        }
    }

    private void MoveQueueItemDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.MoveItemDown(item);
        }
    }

    /// <summary>
    /// 行尾 ▼/▲：只翻这一行的展开状态，不碰选中项——用户可能同时展开好几行做对比。
    /// 选中与展开的联动只发生在"切换选中项"时（见 MainViewModel.OnSelectedItemChanged）。
    /// </summary>
    private void ExpandToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueueItem item })
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    /// <summary>行内"恢复继承"：清掉该项的两项覆盖，重新跟随右侧面板。</summary>
    private void ResetOverrideButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueueItem item })
        {
            item.ResetOverride();
        }
    }

    /// <summary>
    /// 点击行内小图 / 详情区大图：把同一张缓存帧放大展示（640x360，下方一行文件名）。
    /// 只读缓存文件，不重新截帧；还没有缓存帧（ThumbnailPath 为空）或文件已被清理时什么都不做。
    /// </summary>
    private async void ShowThumbnailPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_thumbnailDialogOpen || sender is not FrameworkElement { DataContext: QueueItem item })
        {
            return;
        }

        // 缩略图是按钮，会把点击从行选中上截走；这里先补上选中，
        // 让行高亮与右侧面板「缩略图时间点」都切到这一项，再打开放大视图。
        ViewModel.SelectedItem = item;

        var path = item.ThumbnailPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var bitmap = new BitmapImage { DecodePixelWidth = 640 };
        try
        {
            using var stream = File.OpenRead(path);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
        }
        catch (Exception)
        {
            // 文件被清理或损坏：静默放弃，不弹一个空白预览
            return;
        }

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new Image
        {
            Width = 640,
            Height = 360,
            Stretch = Stretch.Uniform,
            Source = bitmap,
        });
        content.Children.Add(new TextBlock
        {
            Text = item.FileName,
            FontSize = 13,
            MaxWidth = 640,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var dialog = new ContentDialog
        {
            // WinUI 3 桌面端没有隐式窗口根，缺 XamlRoot 时 ShowAsync 直接抛异常
            XamlRoot = XamlRoot,
            Title = "缩略图预览",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            Content = content,
        };

        // 连点会撞上「同一时间只能开一个 ContentDialog」，先挡住
        _thumbnailDialogOpen = true;
        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // 对话框偶尔起不来（窗口正在关闭等），不值得把应用带崩
        }
        finally
        {
            _thumbnailDialogOpen = false;
        }
    }
}
