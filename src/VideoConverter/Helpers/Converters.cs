using Microsoft.UI;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
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
/// 缩略图占位底色：按文件名确定性地取一组柔和渐变（对应原型的 hsl 渐变），
/// 保证每行颜色不同且同一文件颜色稳定。
/// </summary>
public sealed class ThumbnailBrushConverter : IValueConverter
{
    private static readonly double[] Hues = { 205, 160, 275, 25, 330, 100 };
    private static readonly LinearGradientBrush[] Brushes = CreateBrushes();

    private static LinearGradientBrush[] CreateBrushes()
    {
        var brushes = new LinearGradientBrush[Hues.Length];
        for (var i = 0; i < Hues.Length; i++)
        {
            var hue = Hues[i];
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
            };
            brush.GradientStops.Add(new GradientStop { Offset = 0, Color = FromHsl(hue, 0.62, 0.46) });
            brush.GradientStops.Add(new GradientStop { Offset = 1, Color = FromHsl((hue + 45) % 360, 0.70, 0.28) });
            brushes[i] = brush;
        }

        return brushes;
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var name = value switch
        {
            QueueItem item => item.FileName,
            string text => text,
            _ => string.Empty,
        };

        var hash = 0;
        foreach (var ch in name)
        {
            hash = unchecked(hash * 31 + ch);
        }

        return Brushes[(hash & 0x7FFFFFFF) % Brushes.Length];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var h = hue / 60.0;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        var m = lightness - c / 2;
        return Color.FromArgb(
            255,
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
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
