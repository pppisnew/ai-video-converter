using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using VideoConverter.Core.Models;

namespace VideoConverter.Helpers;

public sealed class NotNullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not true;
}

/// <summary>队列项状态 -> 图标字符（Segoe Fluent Icons）。</summary>
public sealed class QueueStateToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            QueueItemState.Pending => "\uE892",      // 时钟
            QueueItemState.Converting => "\uE768",   // 播放
            QueueItemState.Completed => "\uE73E",    // 对勾
            QueueItemState.Failed => "\uEA39",       // 错误
            _ => "\uE9CE",
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>队列项状态 -> 颜色。</summary>
public sealed class QueueStateToBrushConverter : IValueConverter
{
    private static readonly Brush Pending = new SolidColorBrush(Color.FromArgb(255, 138, 138, 138));
    private static readonly Brush Converting = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
    private static readonly Brush Completed = new SolidColorBrush(Color.FromArgb(255, 16, 124, 65));
    private static readonly Brush Failed = new SolidColorBrush(Color.FromArgb(255, 197, 62, 22));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            QueueItemState.Converting => Converting,
            QueueItemState.Completed => Completed,
            QueueItemState.Failed => Failed,
            _ => Pending,
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>bool -> Visibility（x:Bind 不隐式转换，显式走转换器）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
