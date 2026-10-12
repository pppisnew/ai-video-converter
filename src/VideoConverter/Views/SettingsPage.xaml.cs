using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoConverter.Core.Models;
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

        ThemeRadios.Add(ThemeLightRadio);
        ThemeRadios.Add(ThemeDarkRadio);
        ThemeRadios.Add(ThemeSystemRadio);
    }

    /// <summary>页面绑定源：应用级共享 ViewModel（x:Bind 需要页面自身暴露该属性）。</summary>
    public MainViewModel ViewModel { get; }

    private System.Collections.Generic.List<RadioButton> ThemeRadios { get; } = new();

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

        // 界面卡片：两列时占第 2 行右列，单列时排到第 4 行
        Grid.SetRow(GroupAppearance, singleColumn ? 4 : 2);
        Grid.SetColumn(GroupAppearance, 0);
    }

    /// <summary>主题单选：Tag 直接给枚举名，解析失败就忽略，不写坏设置。</summary>
    private void ThemeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse<AppTheme>(tag, out var theme))
        {
            ViewModel.Theme = theme;
        }
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // 页面每次导航都会重建，单选态要从 ViewModel 回填（而非只靠用户点击）
        var theme = ViewModel.Theme;
        foreach (var radio in ThemeRadios)
        {
            radio.IsChecked = radio.Tag as string == theme.ToString();
        }

        ApplyBreakpoint();
    }
}
