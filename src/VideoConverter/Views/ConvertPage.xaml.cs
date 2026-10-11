using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoConverter.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VideoConverter.Views;

public sealed partial class ConvertPage : Page
{
    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".flv", ".wmv", ".ts",
        ".m2ts", ".mts", ".m4v", ".mpg", ".mpeg", ".3gp", ".rmvb", ".vob",
    };

    public ConvertPage()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
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
}