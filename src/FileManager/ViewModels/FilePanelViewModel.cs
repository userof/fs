using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    // ── All items (unfiltered) and displayed items ──
    private List<FileItem> _allItems = new();

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileItem> _items = new();

    [ObservableProperty]
    private FileItem? _selectedItem;

    /// <summary>Multi-select: synced from code-behind SelectionChanged.</summary>
    public List<FileItem> SelectedItems { get; } = new();

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

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private bool _isCreatingFolder;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    // ── Sorting ──
    [ObservableProperty]
    private bool _isFilterVisible;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isStatusBarVisible;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private SortColumn _sortColumn = SortColumn.Name;

    [ObservableProperty]
    private bool _sortAscending = true;

    [ObservableProperty]
    private string _nameHeader = "Name ▲";

    [ObservableProperty]
    private string _sizeHeader = "Size";

    [ObservableProperty]
    private string _modifiedHeader = "Modified";

    [ObservableProperty]
    private string _extHeader = "Ext";

    // ── Error display ──
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    private Timer? _errorTimer;

    // ── Preview pane ──
    [ObservableProperty]
    private bool _showPreview;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _previewImage;

    [ObservableProperty]
    private string _previewName = string.Empty;

    [ObservableProperty]
    private string _previewDetails = string.Empty;

    [ObservableProperty]
    private string _previewText = string.Empty;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".webp", ".tiff", ".tif"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".cs", ".js", ".ts", ".json", ".xml", ".html", ".css",
        ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".log", ".sh", ".bat",
        ".cmd", ".ps1", ".py", ".rb", ".java", ".c", ".cpp", ".h", ".hpp",
        ".rs", ".go", ".sql", ".csv", ".env", ".gitignore", ".editorconfig",
        ".csproj", ".sln", ".slnx", ".axaml", ".xaml", ".razor", ".vue", ".svelte"
    };

    // ── FileSystemWatcher ──
    private FileSystemWatcher? _watcher;
    private Timer? _watcherDebounce;
    private readonly object _watcherLock = new();

    public FilePanelViewModel(IFileSystemService fileSystemService, IPriorityService priorityService)
    {
        _fileSystemService = fileSystemService;
        _priorityService = priorityService;
        foreach (var loc in _fileSystemService.GetQuickAccessLocations())
            Drives.Add(loc);
    }

    // ── Navigation ──

    public void NavigateTo(string path)
    {
        if (!_fileSystemService.DirectoryExists(path))
            return;

        if (!string.IsNullOrEmpty(CurrentPath))
            _history.Push(CurrentPath);

        CurrentPath = path;
        AddressBarPath = path;
        IsEditingAddress = false;
        UpdateBreadcrumbs(path);
        LoadDirectory(path);
        GoBackCommand.NotifyCanExecuteChanged();
    }

    private void UpdateBreadcrumbs(string path)
    {
        var items = new List<BreadcrumbItem>();
        var dir = new DirectoryInfo(path);
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
        IsEditingAddress = false;
        UpdateBreadcrumbs(prev);
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
    public void Refresh()
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
        {
            IsEditingAddress = false;
            NavigateTo(AddressBarPath);
        }
        else
        {
            ShowError($"Path not found: {AddressBarPath}");
            AddressBarPath = CurrentPath;
        }
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

    // ── Sorting ──

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
        var asc = SortAscending ? " ▲" : " ▼";
        NameHeader = SortColumn == SortColumn.Name ? "Name" + asc : "Name";
        SizeHeader = SortColumn == SortColumn.Size ? "Size" + asc : "Size";
        ModifiedHeader = SortColumn == SortColumn.Modified ? "Modified" + asc : "Modified";
        ExtHeader = SortColumn == SortColumn.Extension ? "Ext" + asc : "Ext";
    }

    // ── Priority ──

    [RelayCommand]
    private void CyclePriority(FileItem? item)
    {
        if (item == null) return;
        var next = (item.Priority + 1) % 3;
        item.Priority = next;
        _priorityService.SetPriority(item.FullPath, next);
        ResortItems();
    }

    // ── Multi-select clipboard operations (OS interop) ──

    [RelayCommand]
    private async Task Copy()
    {
        var selected = GetEffectiveSelection();
        if (selected.Count == 0) return;
        var paths = selected.Select(i => i.FullPath);
        await ClipboardService.CopyFiles(paths, isCut: false);
    }

    [RelayCommand]
    private async Task Cut()
    {
        var selected = GetEffectiveSelection();
        if (selected.Count == 0) return;
        var paths = selected.Select(i => i.FullPath);
        await ClipboardService.CopyFiles(paths, isCut: true);
    }

    [RelayCommand]
    private async Task Paste()
    {
        var (paths, isCut) = await ClipboardService.GetFiles();
        if (paths.Count == 0) return;

        int success = 0;
        foreach (var path in paths)
        {
            var isDir = Directory.Exists(path);
            try
            {
                if (isCut)
                    _fileSystemService.MoveTo(path, CurrentPath, isDir);
                else
                    _fileSystemService.CopyTo(path, CurrentPath, isDir);
                success++;
            }
            catch (Exception ex)
            {
                ShowError($"Failed: {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (isCut && success > 0)
            ClipboardService.ClearIfCut();

        Refresh();
    }

    public void DropCopy(string sourcePath, bool isDirectory)
    {
        try
        {
            _fileSystemService.CopyTo(sourcePath, CurrentPath, isDirectory);
            Refresh();
        }
        catch (Exception ex) { ShowError($"Copy failed: {ex.Message}"); }
    }

    public void DropMove(string sourcePath, bool isDirectory)
    {
        try
        {
            _fileSystemService.MoveTo(sourcePath, CurrentPath, isDirectory);
            Refresh();
        }
        catch (Exception ex) { ShowError($"Move failed: {ex.Message}"); }
    }

    // ── Delete (multi-select) ──

    [RelayCommand]
    private void Delete()
    {
        var selected = GetEffectiveSelection();
        if (selected.Count == 0) return;

        int fail = 0;
        foreach (var item in selected)
        {
            try
            {
                _fileSystemService.Delete(item.FullPath, item.IsDirectory);
            }
            catch (Exception ex)
            {
                fail++;
                ShowError($"Delete failed: {item.Name}: {ex.Message}");
            }
        }
        Refresh();
    }

    // ── Rename ──

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
        catch (Exception ex)
        {
            ShowError($"Rename failed: {ex.Message}");
            CancelRename();
        }
    }

    [RelayCommand]
    private void CancelRename()
    {
        IsRenaming = false;
        RenameText = string.Empty;
    }

    // ── New Folder ──

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
        catch (Exception ex)
        {
            ShowError($"Create folder failed: {ex.Message}");
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

    public void ShowWindowsContextMenu(string[] paths, int screenX, int screenY)
    {
        if (!OperatingSystem.IsWindows()) return;
        var hwnd = GetMainWindowHandle();
        // Show for first item (COM multi-select is complex)
        if (paths.Length > 0)
            ShellContextMenuService.ShowContextMenu(paths[0], hwnd, screenX, screenY);
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

    // ── Open in ... ──

    [RelayCommand]
    private void OpenTerminal()
    {
        var path = SelectedItem is { IsDirectory: true } ? SelectedItem.FullPath : CurrentPath;
        try { _fileSystemService.OpenTerminal(path); }
        catch (Exception ex) { ShowError($"Terminal failed: {ex.Message}"); }
    }

    [RelayCommand]
    private void OpenVSCode()
    {
        var path = SelectedItem is { IsDirectory: true } ? SelectedItem.FullPath : CurrentPath;
        try { _fileSystemService.OpenVSCode(path); }
        catch (Exception ex) { ShowError($"VS Code failed: {ex.Message}"); }
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
    private async Task CopyPath()
    {
        var selected = GetEffectiveSelection();
        if (selected.Count == 0) return;
        var text = string.Join(Environment.NewLine, selected.Select(i => i.FullPath));
        await ClipboardService.CopyText(text);
    }

    [RelayCommand]
    private async Task CopyFolderPath()
    {
        await ClipboardService.CopyText(CurrentPath);
    }

    [RelayCommand]
    private void ShowFolderProperties()
    {
        _fileSystemService.ShowProperties(CurrentPath);
    }

    // ── Preview pane ──

    [RelayCommand]
    private void TogglePreview()
    {
        ShowPreview = !ShowPreview;
        if (ShowPreview)
            UpdatePreview();
        else
            ClearPreview();
    }

    partial void OnSelectedItemChanged(FileItem? value)
    {
        if (ShowPreview)
            UpdatePreview();
        UpdateStatus();
    }

    private void UpdatePreview()
    {
        ClearPreview();

        var item = SelectedItem;
        if (item == null) return;

        PreviewName = item.Name;

        if (item.IsDirectory)
        {
            PreviewDetails = $"Folder\n{item.ChildCount} items\nModified: {item.LastModified:yyyy-MM-dd HH:mm}";
            return;
        }

        // File info
        PreviewDetails = $"{item.Extension.TrimStart('.')}{(item.Size > 0 ? $" — {FormatSize(item.Size)}" : "")}\nModified: {item.LastModified:yyyy-MM-dd HH:mm}";

        // Image preview
        var ext = item.Extension.ToLowerInvariant();
        if (ImageExtensions.Contains(ext))
        {
            try
            {
                using var stream = File.OpenRead(item.FullPath);
                PreviewImage = new Avalonia.Media.Imaging.Bitmap(stream);
            }
            catch { /* can't read image */ }
            return;
        }

        // Text preview
        if (TextExtensions.Contains(ext) || string.IsNullOrEmpty(ext))
        {
            try
            {
                if (item.Size < 512_000) // max 500KB
                {
                    var lines = File.ReadLines(item.FullPath).Take(50);
                    PreviewText = string.Join("\n", lines);
                }
                else
                {
                    PreviewText = "(File too large to preview)";
                }
            }
            catch { /* can't read file */ }
        }
    }

    private void ClearPreview()
    {
        PreviewImage = null;
        PreviewName = string.Empty;
        PreviewDetails = string.Empty;
        PreviewText = string.Empty;
    }

    // ── Filter ──

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilterAndSort();
    }

    // ── Error display ──

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        _errorTimer?.Dispose();
        _errorTimer = new Timer(_ =>
        {
            HasError = false;
            ErrorMessage = string.Empty;
        }, null, 4000, Timeout.Infinite);
    }

    // ── Status bar ──

    public void UpdateStatus()
    {
        var totalItems = _allItems.Count;
        var selectedCount = SelectedItems.Count;
        var selectedSize = SelectedItems.Where(i => !i.IsDirectory).Sum(i => i.Size);

        if (selectedCount > 0)
        {
            var sizeText = selectedSize > 0 ? $" ({FormatSize(selectedSize)})" : "";
            StatusText = $"{totalItems} items | {selectedCount} selected{sizeText}";
        }
        else
        {
            StatusText = $"{totalItems} items";
        }
    }

    private static string FormatSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var s = (double)size;
        while (s >= 1024 && order < units.Length - 1) { order++; s /= 1024; }
        return $"{s:0.##} {units[order]}";
    }

    // ── Helpers ──

    private List<FileItem> GetEffectiveSelection()
    {
        if (SelectedItems.Count > 0)
            return new List<FileItem>(SelectedItems);
        if (SelectedItem != null)
            return new List<FileItem> { SelectedItem };
        return new List<FileItem>();
    }

    partial void OnSelectedDriveChanged(QuickAccessItem? value)
    {
        if (value != null && !value.IsSeparator && !string.IsNullOrEmpty(value.Path)
            && _fileSystemService.DirectoryExists(value.Path))
            NavigateTo(value.Path);
    }

    // ── Directory loading ──

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

        _allItems = new List<FileItem>(rawItems);
        ApplyFilterAndSort();
        SetupWatcher(path);
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<FileItem> filtered = _allItems;

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var filter = FilterText.Trim();
            filtered = filtered.Where(i =>
                i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var sorted = SortItems(filtered);
        Items = new ObservableCollection<FileItem>(sorted);
        UpdateStatus();
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

    // ── FileSystemWatcher ──

    private void SetupWatcher(string path)
    {
        DisposeWatcher();

        try
        {
            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemChanged;
            _watcher.Changed += OnFileSystemChanged;
            _watcher.Error += (_, _) => DisposeWatcher();
        }
        catch
        {
            // Some directories can't be watched (network, restricted)
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: coalesce rapid changes into one refresh
        lock (_watcherLock)
        {
            _watcherDebounce?.Dispose();
            _watcherDebounce = new Timer(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (CurrentPath == ((FileSystemWatcher)sender).Path)
                        Refresh();
                });
            }, null, 300, Timeout.Infinite);
        }
    }

    private void DisposeWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        lock (_watcherLock)
        {
            _watcherDebounce?.Dispose();
            _watcherDebounce = null;
        }
    }
}
