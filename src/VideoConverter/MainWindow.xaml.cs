using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI.Xaml.Media;
using VideoConverter.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
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
        ViewModel = new MainViewModel(new Core.Services.FfmpegLocator(), DispatcherQueue);
        InitializeComponent();
        ConfigureWindow();
        ViewModel.AttachQueueEvents();
    }

    public MainViewModel ViewModel { get; }

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(1280, 820));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 1040;
            presenter.PreferredMinimumHeight = 660;
        }
    }

    private async void PickFileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
        };
        InitializePickerWithWindow(picker);

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

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "释放以加入队列";
        e.Handled = true;
        DropZone.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    private void Root_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropZoneAppearance();
        e.Handled = true;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        ResetDropZoneAppearance();

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.OfType<StorageFile>()
            .Where(item => VideoExtensions.Contains(
                System.IO.Path.GetExtension(item.Path), StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Path)
            .ToList();

        if (paths.Count > 0)
        {
            await ViewModel.LoadVideosCommand.ExecuteAsync(paths);
        }
    }

    private void RevealOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Core.Models.QueueItem item })
        {
            ViewModel.RevealOutputFor(item);
        }
    }

    private void ResetDropZoneAppearance() =>
        DropZone.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

    internal static void InitializePickerWithWindow(object picker) =>
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
}
