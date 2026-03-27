using System.Collections.Generic;
using FileManager.Models;

namespace FileManager.Services;

public interface IFileSystemService
{
    IReadOnlyList<FileItem> GetDirectoryContents(string path);
    IReadOnlyList<QuickAccessItem> GetQuickAccessLocations();
    bool DirectoryExists(string path);
    string? GetParentDirectory(string path);
    void OpenFile(string path);
    void Delete(string path, bool isDirectory);
    void Rename(string oldPath, string newName);
    string CreateFolder(string parentPath, string name);
    void CopyTo(string sourcePath, string destDir, bool isDirectory);
    void MoveTo(string sourcePath, string destDir, bool isDirectory);
    void ShowProperties(string path);
    void OpenInExplorerAndSelect(string path);
    void OpenTerminal(string directoryPath);
    void OpenVSCode(string directoryPath);
}
