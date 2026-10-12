using VideoConverter.ViewModels;

namespace VideoConverter.Helpers;

/// <summary>
/// XAML 里访问应用级 ViewModel 的间接层。<c>App.ViewModel</c> 是静态属性，没法直接写成
/// <c>{StaticResource}</c>；这个实例在解析 App.xaml 时就存在，真正取值时才转发到静态属性。
///
/// 用途：DataTemplate 里的 <c>x:Bind</c> 根是列表项模型（<c>QueueItem</c>），看不到页面上的
/// ViewModel 属性，而选中行的详情区要展示的是 <c>MainViewModel</c> 的选中项展示字段。
/// DataTemplate 内用经典 <c>{Binding}</c> 没有这个限制（既有 ThumbnailBrush 转换器读
/// <c>App.ViewModel</c> 是同一类做法，这里换成显式的属性路径，绑定点更清楚）。
/// </summary>
public sealed class ViewModelLocator
{
    public MainViewModel ViewModel => App.ViewModel;
}
