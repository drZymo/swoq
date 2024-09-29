using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Swoq.Dashboard.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool maximize && targetType.IsAssignableTo(typeof(GridLength)))
        {
            double targetLength = 0;
            if (parameter is double doubleValue) { targetLength = doubleValue; }
            else if (parameter is int intValue) { targetLength = intValue; }
            else if (parameter is string stringValue) { targetLength = double.Parse(stringValue); }

            return new GridLength(maximize ? 0 : targetLength);
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
