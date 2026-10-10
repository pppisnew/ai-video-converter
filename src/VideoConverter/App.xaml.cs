using Microsoft.UI.Xaml;

namespace VideoConverter;

public partial class App : Application
{
    private Window? _window;

    /// <summary>供文件选择器等需要窗口句柄的 WinRT API 使用。</summary>
    public static Window? MainWindow { get; private set; }

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
