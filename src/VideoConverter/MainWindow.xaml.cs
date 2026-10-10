using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using VideoConverter.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
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
    }

    public MainViewModel ViewModel { get; }

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(1180, 780));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 620;
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

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await ViewModel.LoadVideoCommand.ExecuteAsync(file.Path);
        }
    }

    private async void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            SuggestedFileName = System.IO.Path.GetFileName(ViewModel.OutputPath),
        };
        InitializePickerWithWindow(picker);
        picker.FileTypeChoices.Add("转换输出", new List<string> { ViewModel.SelectedPreset.Extension });

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            ViewModel.OutputPath = file.Path;
        }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "释放以加载视频";
        e.Handled = true;
        DropZone.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
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
        var file = items.OfType<StorageFile>()
            .FirstOrDefault(item => VideoExtensions.Contains(
                System.IO.Path.GetExtension(item.Path), StringComparer.OrdinalIgnoreCase));

        if (file is not null)
        {
            await ViewModel.LoadVideoCommand.ExecuteAsync(file.Path);
        }
    }

    private void ResetDropZoneAppearance() =>
        DropZone.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

    private static void InitializePickerWithWindow(object picker) =>
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
}
