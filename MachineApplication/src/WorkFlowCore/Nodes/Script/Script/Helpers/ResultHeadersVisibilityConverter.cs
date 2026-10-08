using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace CsxPad.Wpf.Helpers;

public sealed class ResultHeadersVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? DataGridHeadersVisibility.Column : DataGridHeadersVisibility.None;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
