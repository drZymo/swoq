using Avalonia.Data.Converters;
using System.Globalization;

namespace Swoq.MapGeneratorTester;

public class NullableIntConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int valueAsInt ? valueAsInt.ToString(culture) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(value?.ToString()))
        {
            return null;
        }
        if (int.TryParse(value?.ToString(), out int result))
        {
            return result;
        }
        return null;
    }
}
