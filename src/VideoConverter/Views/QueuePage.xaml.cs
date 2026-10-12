using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoConverter.Core.Models;
using VideoConverter.ViewModels;

namespace VideoConverter.Views;

public sealed partial class QueuePage : Page
{
    private const double StatBreakpoint = 1100;

    public QueuePage()
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

    /// <summary>统计卡断点：<1100 DIP 收成两列（不依赖 VisualStateManager）。</summary>
    private void ApplyBreakpoint()
    {
        var width = XamlRoot?.Size.Width ?? 0;
        if (width <= 0)
        {
            return;
        }

        var twoColumns = width < StatBreakpoint;
        StatColumn3.Width = twoColumns ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        StatColumn4.Width = twoColumns ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(StatCompletedCard, twoColumns ? 1 : 0);
        Grid.SetColumn(StatCompletedCard, twoColumns ? 0 : 2);
        Grid.SetRow(StatFailedCard, twoColumns ? 1 : 0);
        Grid.SetColumn(StatFailedCard, twoColumns ? 1 : 3);

        // 两列时用行间距，四列时用列间距；另一方向的间距置 0，避免留下多余的空档
        StatGrid.RowSpacing = twoColumns ? 14 : 0;
        StatGrid.ColumnSpacing = twoColumns ? 0 : 14;
    }

    private void RevealOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.RevealOutputFor(item);
        }
    }

    private void RetryItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.RetryItem(item);
        }
    }

    private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.RemoveItem(item);
        }
    }
}
