using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace FileManager.Converters;

/// <summary>
/// Converts a FilePanelViewModel's CurrentPath to a short tab name.
/// </summary>
public class TabNameConverter : IValueConverter
{
    public static readonly TabNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return "New Tab";

        var name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
