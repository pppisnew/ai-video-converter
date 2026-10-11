using Microsoft.UI.Xaml.Controls;
using VideoConverter.ViewModels;

namespace VideoConverter.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
    }

    /// <summary>页面绑定源：应用级共享 ViewModel（x:Bind 需要页面自身暴露该属性）。</summary>
    public MainViewModel ViewModel { get; }
}