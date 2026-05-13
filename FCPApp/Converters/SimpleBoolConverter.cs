using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FCPApp.Converters;

public class SimpleBoolConverter : IValueConverter
{
    public static readonly SimpleBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Brushes.Green : Brushes.Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}