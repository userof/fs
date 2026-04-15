using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FileManager.Converters;

public class PreviewToggleColorConverter : IValueConverter
{
    public static readonly PreviewToggleColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool show && show)
            return new SolidColorBrush(Color.Parse("#5580cc"));
        return new SolidColorBrush(Color.Parse("#b0b0c8"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
