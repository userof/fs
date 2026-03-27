using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FileManager.Models;

namespace FileManager.Services;

public class FileSystemService : IFileSystemService
{
    public IReadOnlyList<FileItem> GetDirectoryContents(string path)
    {
        var items = new List<FileItem>();

        try
        {
            var dirInfo = new DirectoryInfo(path);

            foreach (var dir in dirInfo.EnumerateDirectories())
            {
                try
                {
                    items.Add(new FileItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dir.LastWriteTime
                    });
                }
                catch (UnauthorizedAccessException) { }
            }

            foreach (var file in dirInfo.EnumerateFiles())
            {
                try
                {
                    items.Add(new FileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTime
                    });
                }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (Exception) { }

        return items;
    }

    public IReadOnlyList<QuickAccessItem> GetQuickAccessLocations()
    {
        var items = new List<QuickAccessItem>();

        // Special folders
        AddFolder(items, "Desktop", Environment.SpecialFolder.Desktop);
        AddFolder(items, "Documents", Environment.SpecialFolder.MyDocuments);
        AddFolder(items, "Downloads", null);
        AddFolder(items, "Music", Environment.SpecialFolder.MyMusic);
        AddFolder(items, "Pictures", Environment.SpecialFolder.MyPictures);
        AddFolder(items, "Videos", Environment.SpecialFolder.MyVideos);
        AddFolder(items, "User", Environment.SpecialFolder.UserProfile);

        // Separator-like empty item
        items.Add(new QuickAccessItem { Name = "───────", Path = "" });

        // Drives
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? drive.Name.TrimEnd('\\')
                : $"{drive.Name.TrimEnd('\\')} {drive.VolumeLabel}";
            items.Add(new QuickAccessItem { Name = label, Path = drive.Name });
        }

        return items;
    }

    private static void AddFolder(List<QuickAccessItem> items, string name, Environment.SpecialFolder? folder)
    {
        string path;
        if (folder.HasValue)
        {
            path = Environment.GetFolderPath(folder.Value);
        }
        else if (name == "Downloads")
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
        else
        {
            return;
        }

        if (Directory.Exists(path))
            items.Add(new QuickAccessItem { Name = name, Path = path });
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string? GetParentDirectory(string path) => Directory.GetParent(path)?.FullName;

    public void OpenFile(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void Delete(string path, bool isDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            // Send to recycle bin via shell
            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0', // double-null terminated
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };
            SHFileOperation(ref fileOp);
        }
        else
        {
            if (isDirectory)
                Directory.Delete(path, true);
            else
                File.Delete(path);
        }
    }

    public void Rename(string oldPath, string newName)
    {
        var dir = Path.GetDirectoryName(oldPath)!;
        var newPath = Path.Combine(dir, newName);

        if (Directory.Exists(oldPath))
            Directory.Move(oldPath, newPath);
        else
            File.Move(oldPath, newPath);
    }

    public string CreateFolder(string parentPath, string name)
    {
        var path = Path.Combine(parentPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void CopyTo(string sourcePath, string destDir, bool isDirectory)
    {
        var name = Path.GetFileName(sourcePath);
        var destPath = GetUniqueDestPath(destDir, name);

        if (isDirectory)
            CopyDirectory(sourcePath, destPath);
        else
            File.Copy(sourcePath, destPath);
    }

    public void MoveTo(string sourcePath, string destDir, bool isDirectory)
    {
        var name = Path.GetFileName(sourcePath);
        var destPath = GetUniqueDestPath(destDir, name);

        if (isDirectory)
            Directory.Move(sourcePath, destPath);
        else
            File.Move(sourcePath, destPath);
    }

    public void OpenInExplorerAndSelect(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }

    public void OpenTerminal(string directoryPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "wt.exe",
            Arguments = $"-d \"{directoryPath}\"",
            UseShellExecute = true
        });
    }

    public void OpenVSCode(string directoryPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "code",
            Arguments = $"\"{directoryPath}\"",
            UseShellExecute = true
        });
    }

    public void ShowProperties(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        ShellExecuteProperties(path);
    }

    private static string GetUniqueDestPath(string destDir, string name)
    {
        var destPath = Path.Combine(destDir, name);
        if (!File.Exists(destPath) && !Directory.Exists(destPath))
            return destPath;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        int i = 2;
        do
        {
            destPath = Path.Combine(destDir, $"{nameWithoutExt} ({i}){ext}");
            i++;
        } while (File.Exists(destPath) || Directory.Exists(destPath));

        return destPath;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string lpVerb;
        public string lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    // ── Recycle bin ──

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    // ── Properties dialog ──

    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

    private static void ShellExecuteProperties(string path)
    {
        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            lpVerb = "properties",
            lpFile = path,
            fMask = SEE_MASK_INVOKEIDLIST,
            nShow = 5 // SW_SHOW
        };
        ShellExecuteEx(ref info);
    }
}
