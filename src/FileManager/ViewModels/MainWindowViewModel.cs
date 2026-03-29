using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileManager.Models;
using FileManager.Services;

namespace FileManager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IProfileService _profileService;
    private readonly IPriorityService _priorityService;

    [ObservableProperty]
    private ObservableCollection<LevelViewModel> _levels = new();

    [ObservableProperty]
    private ObservableCollection<Profile> _profiles = new();

    [ObservableProperty]
    private Profile? _selectedProfile;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StarredFileItem> _openedFiles = new();

    public MainWindowViewModel()
        : this(new FileSystemService(), new ProfileService(), new PriorityService()) { }

    public MainWindowViewModel(IFileSystemService fileSystemService, IProfileService profileService, IPriorityService priorityService)
    {
        _fileSystemService = fileSystemService;
        _profileService = profileService;
        _priorityService = priorityService;
        FilePanelViewModel.FileOpened += OnFileOpened;
        LoadProfiles();
        RestoreLastState();
    }

    private void OnFileOpened(FileItem item)
    {
        // If already there, remove it so we can re-insert at front
        var existing = OpenedFiles.FirstOrDefault(f => string.Equals(f.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            OpenedFiles.Remove(existing);

        OpenedFiles.Insert(0, new StarredFileItem
        {
            Name = item.Name,
            FullPath = item.FullPath,
            IsDirectory = item.IsDirectory
        });

        while (OpenedFiles.Count > 20)
            OpenedFiles.RemoveAt(OpenedFiles.Count - 1);
    }

    [RelayCommand]
    private void OpenOpenedFile(StarredFileItem? item)
    {
        if (item == null) return;
        try { _fileSystemService.OpenFile(item.FullPath); }
        catch (Exception) { }
    }

    [RelayCommand]
    private void ShowInExplorer(StarredFileItem? item)
    {
        if (item == null) return;
        try { _fileSystemService.OpenInExplorerAndSelect(item.FullPath); }
        catch (Exception) { }
    }

    [RelayCommand]
    private void RemoveOpenedFile(StarredFileItem? item)
    {
        if (item == null) return;
        OpenedFiles.Remove(item);
    }

    private string GetLastPath()
    {
        var lastLevel = Levels.LastOrDefault();
        var lastPanel = lastLevel?.Panels.LastOrDefault();
        return lastPanel?.CurrentPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    [RelayCommand]
    private void AddLevel()
    {
        var lastLevel = Levels.LastOrDefault();
        var level = new LevelViewModel(_fileSystemService, _priorityService);

        if (lastLevel != null && lastLevel.Panels.Count > 0)
        {
            foreach (var existingPanel in lastLevel.Panels)
            {
                var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
                panel.NavigateTo(existingPanel.CurrentPath);
                level.Panels.Add(panel);
            }
        }
        else
        {
            var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
            panel.NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            level.Panels.Add(panel);
        }

        Levels.Add(level);
    }

    [RelayCommand]
    private void RemoveLevel(LevelViewModel? level)
    {
        if (level != null && Levels.Count > 1)
            Levels.Remove(level);
    }

    [RelayCommand]
    private void SaveProfile()
    {
        var name = NewProfileName?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var profile = CaptureState(name);
        _profileService.SaveProfile(profile);
        NewProfileName = string.Empty;
        LoadProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == name);
    }

    [RelayCommand]
    private void LoadProfile()
    {
        if (SelectedProfile == null) return;
        ApplyProfile(SelectedProfile);
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        _profileService.DeleteProfile(SelectedProfile.Name);
        SelectedProfile = null;
        LoadProfiles();
    }

    public void SaveLastState()
    {
        var state = CaptureState("__last_state__");
        _profileService.SaveProfile(state);
    }

    public void FlushPriorities()
    {
        _priorityService.FlushSave();
    }

    private void RestoreLastState()
    {
        var all = _profileService.LoadAllProfiles();
        var lastState = all.FirstOrDefault(p => p.Name == "__last_state__");
        if (lastState != null && lastState.Levels.Count > 0)
        {
            ApplyProfile(lastState);
            RestoreOpenedFiles(lastState.OpenedFiles);
        }
        else
        {
            var level = new LevelViewModel(_fileSystemService, _priorityService);
            var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
            panel.NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            level.Panels.Add(panel);
            Levels.Add(level);
        }
    }

    private Profile CaptureState(string name)
    {
        return new Profile
        {
            Name = name,
            Levels = Levels.Select(l => new LevelState
            {
                Panels = l.Panels.Select(p => new PanelState
                {
                    CurrentPath = p.CurrentPath,
                    IsFilterVisible = p.IsFilterVisible,
                    FilterText = p.FilterText,
                    IsStatusBarVisible = p.IsStatusBarVisible
                }).ToList()
            }).ToList(),
            OpenedFiles = OpenedFiles.Select(f => f.FullPath).ToList()
        };
    }

    private void ApplyProfile(Profile profile)
    {
        Levels.Clear();

        foreach (var levelState in profile.Levels)
        {
            var level = new LevelViewModel(_fileSystemService, _priorityService);

            foreach (var panelState in levelState.Panels)
            {
                var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
                var path = panelState.CurrentPath;
                if (!_fileSystemService.DirectoryExists(path))
                    path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                panel.NavigateTo(path);
                panel.IsFilterVisible = panelState.IsFilterVisible;
                panel.FilterText = panelState.FilterText;
                panel.IsStatusBarVisible = panelState.IsStatusBarVisible;
                level.Panels.Add(panel);
            }

            if (level.Panels.Count == 0)
            {
                var panel = new FilePanelViewModel(_fileSystemService, _priorityService);
                panel.NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                level.Panels.Add(panel);
            }

            Levels.Add(level);
        }

        if (Levels.Count == 0)
            AddLevel();
    }

    private void RestoreOpenedFiles(List<string> paths)
    {
        OpenedFiles.Clear();
        foreach (var path in paths)
        {
            var item = StarredFileItem.FromPath(path);
            if (item != null)
                OpenedFiles.Add(item);
        }
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var p in _profileService.LoadAllProfiles().Where(p => p.Name != "__last_state__"))
            Profiles.Add(p);
    }
}
