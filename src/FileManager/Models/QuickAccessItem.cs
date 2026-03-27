namespace FileManager.Models;

public class QuickAccessItem
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;

    public override string ToString() => Name;
}
