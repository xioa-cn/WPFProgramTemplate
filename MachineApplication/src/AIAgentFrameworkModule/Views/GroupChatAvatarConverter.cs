using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AIAgentFrameworkModule.Views;

public sealed class GroupChatAvatarConverter : IValueConverter
{
    private static readonly Brush[] Palette =
    [
        CreateBrush(0x56, 0x7B, 0xA1), CreateBrush(0x83, 0x6B, 0xA4),
        CreateBrush(0x38, 0x86, 0x7D), CreateBrush(0xAD, 0x73, 0x51),
        CreateBrush(0xA5, 0x62, 0x7B), CreateBrush(0x66, 0x7D, 0x4B)
    ];

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        SolidColorBrush brush = new(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string name = (value as string)?.Trim() ?? "";
        if (parameter is "Background")
        {
            uint hash = 0;
            foreach (char character in name) hash = unchecked(hash * 31 + character);
            return Palette[hash % (uint)Palette.Length];
        }
        return name.Length == 0 ? "AI" : StringInfo.GetNextTextElement(name);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
