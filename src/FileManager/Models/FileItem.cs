using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileManager.Models;

public partial class FileItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string Extension => IsDirectory ? "" : Path.GetExtension(Name);

    // 0 = none, 1 = like (on top), 2 = star (topper than like)
    [ObservableProperty]
    private int _priority;

    public string PriorityIcon => Priority switch
    {
        1 => "\U0001F60A",  // smile
        2 => "\U0001F31F",  // star
        _ => "\u2B50"       // dim star outline (placeholder)
    };

    partial void OnPriorityChanged(int value)
    {
        OnPropertyChanged(nameof(PriorityIcon));
    }
}
