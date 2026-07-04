using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ZenIT.Mac.App.Converters;

/// <summary>
/// The view models expose colors as hex strings (shared convention with the Windows app);
/// this converter turns them into brushes for Avalonia bindings.
/// </summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                return Brush.Parse(text);
            }
            catch (FormatException)
            {
                return Brushes.Gray;
            }
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
