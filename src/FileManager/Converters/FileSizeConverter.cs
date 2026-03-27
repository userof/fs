using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileManager.Converters;

public class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();

    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long size || size <= 0)
            return "";

        var order = 0;
        var s = (double)size;
        while (s >= 1024 && order < Units.Length - 1)
        {
            order++;
            s /= 1024;
        }

        return $"{s:0.##} {Units[order]}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
