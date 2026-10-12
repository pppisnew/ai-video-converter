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

        // 两列：左列 ffmpeg / 常规，右列 关于 / 界面，编码器独占最后一行。
        // 单列顺序：ffmpeg -> 关于 -> 常规 -> 界面 -> 编码器（编码器永远在最后）。
        Grid.SetRow(GroupFfmpeg, 0);
        Grid.SetColumn(GroupFfmpeg, 0);
        Grid.SetRow(GroupAbout, singleColumn ? 1 : 0);
        Grid.SetColumn(GroupAbout, singleColumn ? 0 : 1);
        Grid.SetRow(GroupRegular, singleColumn ? 2 : 1);
        Grid.SetColumn(GroupRegular, 0);
        Grid.SetRow(GroupAppearance, singleColumn ? 3 : 1);
        Grid.SetColumn(GroupAppearance, singleColumn ? 0 : 1);
        Grid.SetRow(GroupEncoder, singleColumn ? 4 : 2);
        Grid.SetColumn(GroupEncoder, 0);
    }

    /// <summary>主题单选：Tag 直接给枚举名，解析失败就忽略，不写坏设置。</summary>
    private void ThemeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse<AppTheme>(tag, out var theme))
        {
            ViewModel.Theme = theme;
        }
    }

    /// <summary>
    /// 开源许可对话框。XamlRoot 必须显式赋值：WinUI 3 桌面端没有隐式的窗口根，
    /// 少了这一行 ShowAsync 直接抛异常（经典崩溃）。
    /// </summary>
    private async void ShowLicensesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "开源许可",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            Content = BuildLicenseContent(),
        };

        await dialog.ShowAsync();
    }

    /// <summary>静态信息，不联网：列的是随包分发的三方组件及其真实许可与来源。</summary>
    private static ScrollViewer BuildLicenseContent()
    {
        var panel = new StackPanel { Spacing = 16, Width = 460 };

        panel.Children.Add(LicenseEntry(
            "FFmpeg — GPL v3",
            "随应用分发的 ffmpeg.exe / ffprobe.exe 是 --enable-gpl --enable-version3 的构建"
            + "（含 libx264、libx265 等 GPL 组件），因此整体按 GNU General Public License v3 分发，"
            + "而不是 LGPL。",
            "来源：BtbN/FFmpeg-Builds（https://github.com/BtbN/FFmpeg-Builds/releases）。",
            "再分发义务：随包提供 GPLv3 许可证全文，并按同样许可开放该二进制对应的源码"
            + "（或给出获取源码的明确指引）。"));

        panel.Children.Add(LicenseEntry(
            "CommunityToolkit.Mvvm — MIT",
            "MVVM 源生成器与 ObservableObject / RelayCommand 实现，按 MIT 许可分发。",
            "来源：https://github.com/CommunityToolkit/dotnet。"));

        panel.Children.Add(LicenseEntry(
            "Windows App SDK — MIT",
            "WinUI 3 应用框架与运行时，按 MIT 许可分发。",
            "来源：https://github.com/microsoft/WindowsAppSDK。"));

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 380,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private static StackPanel LicenseEntry(string title, params string[] lines)
    {
        var entry = new StackPanel { Spacing = 4 };
        entry.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var line in lines)
        {
            entry.Children.Add(new TextBlock
            {
                Text = line,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
            });
        }

        return entry;
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
