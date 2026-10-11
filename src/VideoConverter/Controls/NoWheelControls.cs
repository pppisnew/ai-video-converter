using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace VideoConverter.Controls;

/// <summary>
/// 修复 WinUI 的误触问题：ComboBox 未展开时也会响应滚轮改变选中项，
/// 用户滚动页面时参数被静默修改。改为仅在展开时响应滚轮。
/// </summary>
public sealed class NoWheelComboBox : ComboBox
{
    protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
    {
        if (!IsDropDownOpen)
        {
            e.Handled = true;
            return;
        }

        base.OnPointerWheelChanged(e);
    }
}

/// <summary>Slider 同理：仅在获得焦点时才响应滚轮，避免滚动页面时误改数值。</summary>
public sealed class NoWheelSlider : Microsoft.UI.Xaml.Controls.Slider
{
    protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
    {
        if (FocusState == Microsoft.UI.Xaml.FocusState.Unfocused)
        {
            e.Handled = true;
            return;
        }

        base.OnPointerWheelChanged(e);
    }
}

/// <summary>NumberBox 同理：仅在获得焦点时才响应滚轮。</summary>
public sealed class NoWheelNumberBox : NumberBox
{
    protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
    {
        if (FocusState == Microsoft.UI.Xaml.FocusState.Unfocused)
        {
            e.Handled = true;
            return;
        }

        base.OnPointerWheelChanged(e);
    }
}
