namespace FileManager.Models;

public class QuickAccessItem
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool IsSeparator { get; init; }

    public override string ToString() => Name;
}
