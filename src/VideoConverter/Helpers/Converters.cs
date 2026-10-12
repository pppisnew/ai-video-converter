using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VideoConverter.Core.Models;
using VideoConverter.ViewModels;
using Windows.Foundation;

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

/// <summary>队列项状态 -> 徽章底色（四态各自的柔和底色，与文字色同源）。</summary>
public sealed class QueueStateToBadgeBrushConverter : IValueConverter
{
    // 底色取主题令牌：浅色/深色/高对比都能各自解析，缺资源时退回等效硬编码值
    private static readonly Brush Pending = ThemeBrush("AppBadgeNeutralBackground", Color.FromArgb(26, 0, 0, 0));
    private static readonly Brush Converting = ThemeBrush("AppAccentSoftBackground", Color.FromArgb(26, 0, 120, 212));
    private static readonly Brush Completed = ThemeBrush("AppSuccessSoftBackground", Color.FromArgb(26, 15, 123, 15));
    private static readonly Brush Failed = ThemeBrush("AppDangerSoftBackground", Color.FromArgb(26, 196, 43, 28));

    private static Brush ThemeBrush(string key, Color fallback) =>
        Application.Current?.Resources is { } resources
        && resources.TryGetValue(key, out var value)
        && value is Brush brush
            ? brush
            : new SolidColorBrush(fallback);

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

/// <summary>容器扩展名 -> 展示标签（".mp4" -> "MP4"）。</summary>
public sealed class ExtensionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string text ? text.TrimStart('.').ToUpperInvariant() : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>任务筛选枚举 -> 筛选胶囊样式（ConverterParameter 为对应的筛选项名称）。</summary>
public sealed class TaskFilterToChipStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var selected = value is TaskFilter filter
            && parameter is string name
            && string.Equals(filter.ToString(), name, StringComparison.OrdinalIgnoreCase);

        var key = selected ? "FilterChipButtonSelectedStyle" : "FilterChipButtonStyle";
        if (Application.Current?.Resources is { } resources && resources.TryGetValue(key, out var style))
        {
            return style;
        }

        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}


/// <summary>
/// 缩略图缓存路径 -> BitmapImage。按路径缓存已解码的图片对象：列表滚动/虚拟化会反复
/// 调用 Convert，每次都新建 BitmapImage 会把同一张图重复解码。
/// 空路径、文件缺失或图片损坏一律返回 null——此时 Image 不渲染，底下的彩色占位块继续显示。
/// </summary>
public sealed class PathToImageSourceConverter : IValueConverter
{
    // 列表行缩略图只显示 96x56 DIP；按 240 物理像素解码（高 DPI 下仍清晰）
    // 而不是解出源视频分辨率，否则每行都是一张 1080p 位图，长队列会很吃内存。
    private const int DecodeWidth = 240;

    private static readonly Dictionary<string, BitmapImage> Decoded = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null!;
        }

        if (Decoded.TryGetValue(path, out var cached))
        {
            return cached;
        }

        BitmapImage bitmap;
        try
        {
            using var stream = File.OpenRead(path);
            bitmap = new BitmapImage { DecodePixelWidth = DecodeWidth };
            bitmap.SetSource(stream.AsRandomAccessStream());
        }
        catch (Exception)
        {
            return null!;
        }

        Decoded[path] = bitmap;
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>空字符串折叠：非空内容显示，空/空白内容隐藏（时长角标等可选小标记用）。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

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

/// <summary>bool -> Visibility 的反向版本：false 时显示（用于空态 / 文件态互斥切换）。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not Visibility.Visible;
}
