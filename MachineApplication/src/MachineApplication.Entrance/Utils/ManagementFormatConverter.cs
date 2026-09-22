using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MachineApplication.Entrance.Utils;

/// <summary>格式和参数任一变化时重新计算，支持运行时切换语言。</summary>
public sealed class ManagementFormatConverter : IMultiValueConverter
{
    /// <summary>使用当前文化格式化动态参数；绑定尚未就绪时返回空文本。</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // 多重绑定先传入本地化格式，再传入数量；未就绪时不尝试格式化。
        if (values.Length < 2 || values[0] is not string format || values[1] == DependencyProperty.UnsetValue)
            return "";
        return string.Format(culture, format, values[1]);
    }

    /// <summary>此转换仅用于显示，不支持将文本反向写回多个绑定源。</summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
