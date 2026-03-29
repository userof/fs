using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileManager.Models;
using FileManager.Services;

namespace FileManager.ViewModels;

public partial class FilePanelViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IPriorityService _priorityService;
    private readonly Stack<string> _history = new();

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileItem> _items = new();

    [ObservableProperty]
    private FileItem? _selectedItem;

    [ObservableProperty]
    private ObservableCollection<QuickAccessItem> _drives = new();

    [ObservableProperty]
    private QuickAccessItem? _selectedDrive;

    [ObservableProperty]
    private string _addressBarPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    [ObservableProperty]
    private bool _isEditingAddress;

    private static string? _clipboardPath;
    private static bool _clipboardIsDirectory;
    private static bool _clipboardIsCut;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private bool _isCreatingFolder;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private bool _isFilterVisible;

    [ObservableProperty]
    private string _filterText = string.Empty;

    private List<FileItem> _allItems = new();

    [ObservableProperty]
    private bool _isStatusBarVisible;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private SortColumn _sortColumn = SortColumn.Name;

    [ObservableProperty]
    private bool _sortAscending = true;

    // Header display texts with sort indicators
    [ObservableProperty]
    private string _nameHeader = "Name \u25B2";

    [ObservableProperty]
    private string _sizeHeader = "Size";

    [ObservableProperty]
    private string _modifiedHeader = "Modified";

    [ObservableProperty]
    private string _extHeader = "Ext";

    public FilePanelViewModel(IFileSystemService fileSystemService, IPriorityService priorityService)
    {
        _fileSystemService = fileSystemService;
        _priorityService = priorityService;
        foreach (var loc in _fileSystemService.GetQuickAccessLocations())
            Drives.Add(loc);
    }

    public void NavigateTo(string path)
    {
        if (!_fileSystemService.DirectoryExists(path))
            return;

        if (!string.IsNullOrEmpty(CurrentPath))
            _history.Push(CurrentPath);

        CurrentPath = path;
        AddressBarPath = path;
        UpdateBreadcrumbs(path);
        LoadDirectory(path);
        GoBackCommand.NotifyCanExecuteChanged();
    }

    private void UpdateBreadcrumbs(string path)
    {
        var items = new List<BreadcrumbItem>();
        var dir = new System.IO.DirectoryInfo(path);
        while (dir != null)
        {
            items.Insert(0, new BreadcrumbItem
            {
                Name = dir.Parent == null ? dir.FullName : dir.Name,
                FullPath = dir.FullName
            });
            dir = dir.Parent;
        }
        Breadcrumbs = new ObservableCollection<BreadcrumbItem>(items);
    }

    [RelayCommand]
    private void NavigateToBreadcrumb(BreadcrumbItem? item)
    {
        if (item != null)
            NavigateTo(item.FullPath);
    }

    [RelayCommand]
    private void StartEditAddress()
    {
        IsEditingAddress = true;
    }

    [RelayCommand]
    private void FinishEditAddress()
    {
        IsEditingAddress = false;
        if (_fileSystemService.DirectoryExists(AddressBarPath))
            NavigateTo(AddressBarPath);
        else
            AddressBarPath = CurrentPath;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (_history.Count == 0) return;
        var prev = _history.Pop();
        CurrentPath = prev;
        AddressBarPath = prev;
        LoadDirectory(prev);
        GoBackCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoBack() => _history.Count > 0;

    [RelayCommand]
    private void GoUp()
    {
        var parent = _fileSystemService.GetParentDirectory(CurrentPath);
        if (parent != null)
            NavigateTo(parent);
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadDirectory(CurrentPath);
    }

    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterVisible = !IsFilterVisible;
        if (!IsFilterVisible)
        {
            FilterText = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    [RelayCommand]
    private void NavigateToAddress()
    {
        if (_fileSystemService.DirectoryExists(AddressBarPath))
            NavigateTo(AddressBarPath);
        else
            AddressBarPath = CurrentPath;
    }

    public static event Action<FileItem>? FileOpened;

    [RelayCommand]
    private void Open(FileItem? item)
    {
        if (item == null) return;
        if (item.IsDirectory)
            NavigateTo(item.FullPath);
        else
        {
            try
            {
                _fileSystemService.OpenFile(item.FullPath);
            }
            catch (Exception) { }
            FileOpened?.Invoke(item);
        }
    }

    [RelayCommand]
    private void SortBy(string columnName)
    {
        var col = columnName switch
        {
            "Name" => SortColumn.Name,
            "Size" => SortColumn.Size,
            "Modified" => SortColumn.Modified,
            "Ext" => SortColumn.Extension,
            _ => SortColumn.Name
        };

        if (SortColumn == col)
            SortAscending = !SortAscending;
        else
        {
            SortColumn = col;
            SortAscending = true;
        }

        UpdateHeaders();
        ResortItems();
    }

    private void UpdateHeaders()
    {
        var asc = SortAscending ? " \u25B2" : " \u25BC";
        NameHeader = SortColumn == SortColumn.Name ? "Name" + asc : "Name";
        SizeHeader = SortColumn == SortColumn.Size ? "Size" + asc : "Size";
        ModifiedHeader = SortColumn == SortColumn.Modified ? "Modified" + asc : "Modified";
        ExtHeader = SortColumn == SortColumn.Extension ? "Ext" + asc : "Ext";
    }

    [RelayCommand]
    private void CyclePriority(FileItem? item)
    {
        if (item == null) return;
        var next = (item.Priority + 1) % 3;
        item.Priority = next;
        _priorityService.SetPriority(item.FullPath, next);
        ResortItems();
    }

    [RelayCommand]
    private void Copy()
    {
        if (SelectedItem == null) return;
        _clipboardPath = SelectedItem.FullPath;
        _clipboardIsDirectory = SelectedItem.IsDirectory;
        _clipboardIsCut = false;
    }

    [RelayCommand]
    private void Cut()
    {
        if (SelectedItem == null) return;
        _clipboardPath = SelectedItem.FullPath;
        _clipboardIsDirectory = SelectedItem.IsDirectory;
        _clipboardIsCut = true;
    }

    [RelayCommand]
    private void Paste()
    {
        if (string.IsNullOrEmpty(_clipboardPath)) return;

        try
        {
            if (_clipboardIsCut)
            {
                _fileSystemService.MoveTo(_clipboardPath, CurrentPath, _clipboardIsDirectory);
                _clipboardPath = null;
            }
            else
            {
                _fileSystemService.CopyTo(_clipboardPath, CurrentPath, _clipboardIsDirectory);
            }
            Refresh();
        }
        catch (Exception) { }
    }

    public void DropCopy(string sourcePath, bool isDirectory)
    {
        try
        {
            _fileSystemService.CopyTo(sourcePath, CurrentPath, isDirectory);
            Refresh();
        }
        catch (Exception) { }
    }

    public void DropMove(string sourcePath, bool isDirectory)
    {
        try
        {
            _fileSystemService.MoveTo(sourcePath, CurrentPath, isDirectory);
            Refresh();
        }
        catch (Exception) { }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem == null) return;
        try
        {
            _fileSystemService.Delete(SelectedItem.FullPath, SelectedItem.IsDirectory);
            Refresh();
        }
        catch (Exception) { }
    }

    [RelayCommand]
    private void StartRename()
    {
        if (SelectedItem == null) return;
        RenameText = SelectedItem.Name;
        IsRenaming = true;
    }

    [RelayCommand]
    private void ConfirmRename()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(RenameText))
        {
            CancelRename();
            return;
        }

        try
        {
            _fileSystemService.Rename(SelectedItem.FullPath, RenameText.Trim());
            IsRenaming = false;
            Refresh();
        }
        catch (Exception)
        {
            CancelRename();
        }
    }

    [RelayCommand]
    private void CancelRename()
    {
        IsRenaming = false;
        RenameText = string.Empty;
    }

    [RelayCommand]
    private void StartNewFolder()
    {
        NewFolderName = "New Folder";
        IsCreatingFolder = true;
    }

    [RelayCommand]
    private void ConfirmNewFolder()
    {
        if (string.IsNullOrWhiteSpace(NewFolderName))
        {
            CancelNewFolder();
            return;
        }

        try
        {
            _fileSystemService.CreateFolder(CurrentPath, NewFolderName.Trim());
            IsCreatingFolder = false;
            Refresh();
        }
        catch (Exception)
        {
            CancelNewFolder();
        }
    }

    [RelayCommand]
    private void CancelNewFolder()
    {
        IsCreatingFolder = false;
        NewFolderName = string.Empty;
    }

    // ── Shell context menu ──

    public void ShowWindowsContextMenu(string path, int screenX, int screenY)
    {
        if (!OperatingSystem.IsWindows()) return;
        var hwnd = GetMainWindowHandle();
        ShellContextMenuService.ShowContextMenu(path, hwnd, screenX, screenY);
    }

    public void ShowFolderWindowsContextMenu(int screenX, int screenY)
    {
        if (!OperatingSystem.IsWindows()) return;
        var hwnd = GetMainWindowHandle();
        ShellContextMenuService.ShowContextMenu(CurrentPath, hwnd, screenX, screenY);
    }

    private static IntPtr GetMainWindowHandle()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.TryGetPlatformHandle() is { } handle)
        {
            return handle.Handle;
        }
        return IntPtr.Zero;
    }

    // ── File operations ──

    [RelayCommand]
    private void OpenTerminal()
    {
        var path = SelectedItem is { IsDirectory: true } ? SelectedItem.FullPath : CurrentPath;
        _fileSystemService.OpenTerminal(path);
    }

    [RelayCommand]
    private void OpenVSCode()
    {
        var path = SelectedItem is { IsDirectory: true } ? SelectedItem.FullPath : CurrentPath;
        _fileSystemService.OpenVSCode(path);
    }

    [RelayCommand]
    private void ShowProperties()
    {
        if (SelectedItem == null) return;
        _fileSystemService.ShowProperties(SelectedItem.FullPath);
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (SelectedItem != null)
            _fileSystemService.OpenInExplorerAndSelect(SelectedItem.FullPath);
        else
            _fileSystemService.OpenFile(CurrentPath);
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedItem == null) return;
        var clipboard = GetClipboard();
        clipboard?.SetTextAsync(SelectedItem.FullPath);
    }

    [RelayCommand]
    private void CopyFolderPath()
    {
        var clipboard = GetClipboard();
        clipboard?.SetTextAsync(CurrentPath);
    }

    [RelayCommand]
    private void ShowFolderProperties()
    {
        _fileSystemService.ShowProperties(CurrentPath);
    }

    private static Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.Clipboard;
        return null;
    }

    partial void OnSelectedDriveChanged(QuickAccessItem? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.Path) && _fileSystemService.DirectoryExists(value.Path))
            NavigateTo(value.Path);
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilterAndSort();
    }

    private void LoadDirectory(string path)
    {
        var rawItems = _fileSystemService.GetDirectoryContents(path);

        // Batch lookup priorities for this directory
        var priorities = _priorityService.GetPrioritiesForDirectory(path);
        foreach (var item in rawItems)
        {
            var key = item.FullPath.Replace('\\', '/').ToLowerInvariant();
            item.Priority = priorities.TryGetValue(key, out var p) ? p : 0;
        }

        _allItems = rawItems.ToList();
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var filtered = string.IsNullOrEmpty(FilterText)
            ? _allItems
            : _allItems.Where(i => i.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();
        var sorted = SortItems(filtered);
        Items = new ObservableCollection<FileItem>(sorted);
        UpdateStatusText();
    }

    partial void OnSelectedItemChanged(FileItem? value)
    {
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var totalItems = _allItems.Count;
        var shownItems = Items.Count;
        var dirs = Items.Count(i => i.IsDirectory);
        var files = Items.Count(i => !i.IsDirectory);

        var parts = new List<string>();

        if (shownItems != totalItems)
            parts.Add($"{shownItems}/{totalItems} items");
        else
            parts.Add($"{totalItems} items");

        parts.Add($"{dirs} folders, {files} files");

        if (SelectedItem != null && !SelectedItem.IsDirectory)
            parts.Add($"Selected: {FormatSize(SelectedItem.Size)}");

        try
        {
            var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(CurrentPath)!);
            parts.Add($"Free: {FormatSize(driveInfo.AvailableFreeSpace)}");
        }
        catch { }

        StatusText = string.Join("  |  ", parts);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private void ResortItems()
    {
        var selected = SelectedItem;
        ApplyFilterAndSort();
        SelectedItem = selected;
    }

    private IEnumerable<FileItem> SortItems(IEnumerable<FileItem> items)
    {
        // Priority always first (star > smile > none), then dirs first
        var ordered = items
            .OrderByDescending(i => i.Priority)
            .ThenByDescending(i => i.IsDirectory);

        // Then by selected column
        return SortColumn switch
        {
            SortColumn.Name => SortAscending
                ? ordered.ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : ordered.ThenByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
            SortColumn.Size => SortAscending
                ? ordered.ThenBy(i => i.Size)
                : ordered.ThenByDescending(i => i.Size),
            SortColumn.Modified => SortAscending
                ? ordered.ThenBy(i => i.LastModified)
                : ordered.ThenByDescending(i => i.LastModified),
            SortColumn.Extension => SortAscending
                ? ordered.ThenBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                  .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : ordered.ThenByDescending(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                  .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            _ => ordered.ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
        };
    }
}
