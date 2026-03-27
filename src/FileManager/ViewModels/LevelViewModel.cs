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
    private ObservableCollection<FilePanelViewModel> _panels = new();

    public LevelViewModel(IFileSystemService fileSystemService, IPriorityService priorityService)
    {
        _fileSystemService = fileSystemService;
        _priorityService = priorityService;
    }

    [RelayCommand]
    private void AddPanel()
    {
        var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
        var lastPath = Panels.LastOrDefault()?.CurrentPath;
        panel.NavigateTo(lastPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Panels.Add(panel);
    }

    [RelayCommand]
    private void RemovePanel(FilePanelViewModel? panel)
    {
        if (panel != null && Panels.Count > 1)
            Panels.Remove(panel);
    }
}
