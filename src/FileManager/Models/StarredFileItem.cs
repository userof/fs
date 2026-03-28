using System.IO;
using Avalonia.Media.Imaging;
using FileManager.Services;

namespace FileManager.Models;

public class StarredFileItem
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public Bitmap? Icon => FileIconService.GetIcon(FullPath, IsDirectory);

    public static StarredFileItem? FromPath(string normalizedPath)
    {
        // Convert normalized (forward-slash, lowercase) path back to actual path
        var path = normalizedPath.Replace('/', '\\');

        // Try to find the actual casing on disk
        if (File.Exists(path))
            return new StarredFileItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = false
            };

        if (Directory.Exists(path))
            return new StarredFileItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = true
            };

        return null;
    }
}
