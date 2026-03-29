namespace FileManager.Models;

public class PanelState
{
    public string CurrentPath { get; set; } = string.Empty;
    public double Width { get; set; } = 1.0;
    public bool IsFilterVisible { get; set; }
    public string FilterText { get; set; } = string.Empty;
}
