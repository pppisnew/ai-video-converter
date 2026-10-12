using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using VideoConverter.ViewModels;
using VideoConverter.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace VideoConverter;

public sealed partial class MainWindow : Window
{
    private const int DesignWidth = 1180;
    private const int DesignHeight = 800;
    private const int MinimumWidth = 900;
    private const int MinimumHeight = 600;
    private const int ScreenMargin = 24;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".flv", ".wmv", ".ts",
        ".m2ts", ".mts", ".m4v", ".mpg", ".mpeg", ".3gp", ".rmvb", ".vob",
    };

    public MainWindow()
    {
        ViewModel = App.ViewModel;
        ViewModel.AttachQueueEvents();
        InitializeComponent();
        ConfigureWindow();
        ContentFrame.Navigate(typeof(ConvertPage));
    }

    public MainViewModel ViewModel { get; }

    private void ConfigureWindow()
    {
        // AppWindow.Resize 的单位是物理像素，而 PreferredMinimum* 是 DIP。
        // 固定写 1180x800 时，150% DPI 下实际只有 787x533 DIP，比最小宽度还窄。
        var hwnd = WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;

        var width = (int)Math.Round(DesignWidth * scale);
        var height = (int)Math.Round(DesignHeight * scale);

        // 不允许开出屏幕外：按当前显示器工作区夹取，并留出 24 DIP 边距。
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea is not null)
        {
            var margin = (int)Math.Round(ScreenMargin * scale);
            var minimumWidth = (int)Math.Round(MinimumWidth * scale);
            var minimumHeight = (int)Math.Round(MinimumHeight * scale);
            var maxWidth = Math.Max(displayArea.WorkArea.Width - margin, minimumWidth);
            var maxHeight = Math.Max(displayArea.WorkArea.Height - margin, minimumHeight);
            width = Math.Min(width, maxWidth);
            height = Math.Min(height, maxHeight);
        }

        AppWindow.Resize(new SizeInt32(width, height));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinimumWidth;
            presenter.PreferredMinimumHeight = MinimumHeight;
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            HeaderTitle.Text = tag switch
            {
                "Queue" => "队列",
                "Settings" => "设置",
                _ => "转换",
            };

            var pageType = tag switch
            {
                "Queue" => typeof(QueuePage),
                "Settings" => typeof(SettingsPage),
                _ => typeof(ConvertPage),
            };
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }

    /// <summary>
    /// 供页面按 Tag 切换导航项（如 ConvertPage 的"前往队列"）。
    /// 设置 SelectedItem 会走既有 SelectionChanged 路径，保证标题与 Frame 同步。
    /// </summary>
    internal static void NavigateTo(string tag)
    {
        if (App.MainWindow is not MainWindow window)
        {
            return;
        }

        foreach (var menuItem in window.NavView.MenuItems)
        {
            if (menuItem is NavigationViewItem item && item.Tag is string t && t == tag)
            {
                window.NavView.SelectedItem = item;
                return;
            }
        }
    }

    // 拖拽挂在窗口根 Grid 上：任何页面都可直接拖入视频文件
    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "释放以加入队列";
        e.Handled = true;
    }

    private void Root_DragLeave(object sender, DragEventArgs e) => e.Handled = true;

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) { return; }
        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.OfType<StorageFile>()
            .Where(item => VideoExtensions.Contains(System.IO.Path.GetExtension(item.Path), StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Path)
            .ToList();
        if (paths.Count > 0) { await ViewModel.LoadVideosCommand.ExecuteAsync(paths); }
    }

    internal static void InitializePickerWithWindow(object picker) =>
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
}
