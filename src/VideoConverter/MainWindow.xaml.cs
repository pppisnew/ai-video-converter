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

namespace VideoConverter;

public sealed partial class MainWindow : Window
{
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
        AppWindow.Resize(new SizeInt32(1180, 800));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 900;
            presenter.PreferredMinimumHeight = 600;
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
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