using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileManager.Converters;

public class PriorityOpacityConverter : IValueConverter
{
    public static readonly PriorityOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int priority && priority > 0)
            return 1.0;
        return 0.2;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
