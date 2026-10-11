using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using VideoConverter.ViewModels;

namespace VideoConverter;

public partial class App : Application
{
    private static Window? _window;
    private static MainViewModel? _viewModel;

    public static Window? MainWindow { get; private set; }

    /// <summary>全应用共享的 ViewModel，MainWindow 与各 Page 共用（首次访问时在 UI 线程创建）。</summary>
    public static MainViewModel ViewModel =>
        _viewModel ??= new MainViewModel(new Core.Services.FfmpegLocator(), DispatcherQueue.GetForCurrentThread());

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }
}
