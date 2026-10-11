using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VideoConverter.Core.Models;
using VideoConverter.ViewModels;

namespace VideoConverter.Views;

public sealed partial class QueuePage : Page
{
    public QueuePage()
    {
        ViewModel = App.ViewModel;
        InitializeComponent();
    }

    /// <summary>页面绑定源：应用级共享 ViewModel（x:Bind 需要页面自身暴露该属性）。</summary>
    public MainViewModel ViewModel { get; }

    private void RevealOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: QueueItem item })
        {
            ViewModel.RevealOutputFor(item);
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