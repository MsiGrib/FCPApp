using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FCPApp.Converters;

public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string current && parameter is string target)
        {
            bool isEqual = string.Equals(current, target, StringComparison.OrdinalIgnoreCase);

            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return isEqual;

            return isEqual ? "Active" : string.Empty;
        }
        return targetType == typeof(bool) ? false : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}