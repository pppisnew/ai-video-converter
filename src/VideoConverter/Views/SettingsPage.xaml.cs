using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoConverter.ViewModels;

namespace VideoConverter.Views;

public sealed partial class SettingsPage : Page
{
    private const double SingleColumnBreakpoint = 920;

    public SettingsPage()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
        Loaded += OnPageLoaded;
        SizeChanged += OnPageSizeChanged;
    }

    /// <summary>页面绑定源：应用级共享 ViewModel（x:Bind 需要页面自身暴露该属性）。</summary>
    public MainViewModel ViewModel { get; }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => ApplyBreakpoint();

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) => ApplyBreakpoint();

    /// <summary>分组断点：<920 DIP 从两列收成单列（不依赖 VisualStateManager）。</summary>
    private void ApplyBreakpoint()
    {
        var width = XamlRoot?.Size.Width ?? 0;
        if (width <= 0)
        {
            return;
        }

        var singleColumn = width < SingleColumnBreakpoint;
        SettingsSecondColumn.Width = singleColumn ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

        // 单列时消掉列间距，否则卡片右侧会留下一条空隙
        SettingsLayout.ColumnSpacing = singleColumn ? 0 : 20;

        Grid.SetRow(GroupEncoder, singleColumn ? 1 : 0);
        Grid.SetColumn(GroupEncoder, singleColumn ? 0 : 1);
        Grid.SetRow(GroupFfmpeg, singleColumn ? 2 : 1);
        Grid.SetColumn(GroupFfmpeg, 0);
        Grid.SetRow(GroupAbout, singleColumn ? 3 : 1);
        Grid.SetColumn(GroupAbout, singleColumn ? 0 : 1);
    }
}
