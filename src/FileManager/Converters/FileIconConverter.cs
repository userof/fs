using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using FileManager.Models;
using FileManager.Services;

namespace FileManager.Converters;

public class FileIconConverter : IValueConverter
{
    public static readonly FileIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FileItem item)
            return null;

        return FileIconService.GetIcon(item.FullPath, item.IsDirectory);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
