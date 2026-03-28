using System.Collections.Generic;

namespace FileManager.Models;

public class Profile
{
    public string Name { get; set; } = string.Empty;
    public List<LevelState> Levels { get; set; } = new();
    public List<string> OpenedFiles { get; set; } = new();
}

public class LevelState
{
    public List<PanelState> Panels { get; set; } = new();
}
