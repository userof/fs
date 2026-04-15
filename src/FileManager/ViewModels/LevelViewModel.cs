using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileManager.Services;

namespace FileManager.ViewModels;

public partial class LevelViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IPriorityService _priorityService;

    [ObservableProperty]
    private ObservableCollection<TabPanelViewModel> _panels = new();

    public LevelViewModel(IFileSystemService fileSystemService, IPriorityService priorityService)
    {
        _fileSystemService = fileSystemService;
        _priorityService = priorityService;
    }

    [RelayCommand]
    private void AddPanel()
    {
        var tabPanel = new TabPanelViewModel(_fileSystemService, _priorityService);
        var lastPath = Panels.LastOrDefault()?.CurrentPath;
        tabPanel.AddInitialTab(lastPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Panels.Add(tabPanel);
    }

    [RelayCommand]
    private void RemovePanel(TabPanelViewModel? panel)
    {
        if (panel != null && Panels.Count > 1)
            Panels.Remove(panel);
    }
}
