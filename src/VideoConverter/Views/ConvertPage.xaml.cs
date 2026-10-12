using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".flv", ".wmv", ".ts",
        ".m2ts", ".mts", ".m4v", ".mpg", ".mpeg", ".3gp", ".rmvb", ".vob",
    };

    private static readonly Brush DragStroke = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
    private static readonly Brush DragFill = new SolidColorBrush(Color.FromArgb(26, 0, 120, 212));

    private Brush? _restingStroke;
    private Brush? _restingFill;

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
    /// </summary>
    private void ApplyBreakpoint()
    {
        var width = XamlRoot?.Size.Width ?? 0;
        if (width <= 0)
        {
            return;
        }

        var singleColumn = width < SettingsBreakpoint;
        SettingsColumn.Width = singleColumn ? new GridLength(0) : new GridLength(SettingsColumnWidth);
        Grid.SetColumn(SettingsPanel, singleColumn ? 0 : 1);
        Grid.SetRow(SettingsPanel, singleColumn ? 2 : 1);

        // 单列时消掉列间距，否则左列右侧会留下一条空隙
        ConvertLayout.ColumnSpacing = singleColumn ? 0 : 20;
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
}
