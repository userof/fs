using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileManager.Services;

namespace FileManager.ViewModels;

/// <summary>
/// Wraps multiple FilePanelViewModels as tabs within a single panel slot.
/// </summary>
public partial class TabPanelViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IPriorityService _priorityService;

    [ObservableProperty]
    private ObservableCollection<FilePanelViewModel> _tabs = new();

    [ObservableProperty]
    private FilePanelViewModel? _selectedTab;

    public TabPanelViewModel(IFileSystemService fileSystemService, IPriorityService priorityService)
    {
        _fileSystemService = fileSystemService;
        _priorityService = priorityService;
    }

    /// <summary>
    /// Add a tab navigated to the given path (or clone current tab's path).
    /// </summary>
    [RelayCommand]
    private void AddTab()
    {
        var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
        var path = SelectedTab?.CurrentPath
            ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        panel.NavigateTo(path);
        Tabs.Add(panel);
        SelectedTab = panel;
    }

    /// <summary>
    /// Close a tab. If it's the last tab, don't close.
    /// </summary>
    [RelayCommand]
    private void SelectTab(FilePanelViewModel? tab)
    {
        if (tab != null)
            SelectedTab = tab;
    }

    /// <summary>Fired when the last tab is closed — the parent level should remove this panel.</summary>
    public event Action<TabPanelViewModel>? LastTabClosed;

    [RelayCommand]
    private void CloseTab(FilePanelViewModel? tab)
    {
        if (tab == null) return;

        // Last tab — request panel removal from parent level
        if (Tabs.Count <= 1)
        {
            LastTabClosed?.Invoke(this);
            return;
        }

        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (SelectedTab == tab)
            SelectedTab = Tabs[System.Math.Min(idx, Tabs.Count - 1)];
    }

    /// <summary>
    /// Create initial tab navigated to the given path.
    /// </summary>
    public void AddInitialTab(string path)
    {
        var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
        panel.NavigateTo(path);
        Tabs.Add(panel);
        SelectedTab = panel;
    }

    /// <summary>
    /// Get the current path (from selected tab) for state persistence.
    /// </summary>
    public string CurrentPath => SelectedTab?.CurrentPath ?? string.Empty;

    /// <summary>
    /// Tab display name for the tab header.
    /// </summary>
    public static string GetTabName(FilePanelViewModel panel)
    {
        if (string.IsNullOrEmpty(panel.CurrentPath)) return "New Tab";
        var name = System.IO.Path.GetFileName(panel.CurrentPath);
        return string.IsNullOrEmpty(name) ? panel.CurrentPath : name;
    }
}
