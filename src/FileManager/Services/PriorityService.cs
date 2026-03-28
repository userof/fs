using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace FileManager.Services;

public class PriorityService : IPriorityService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly Dictionary<string, int> _priorities;
    private readonly object _lock = new();
    private Timer? _saveTimer;
    private bool _dirty;

    public PriorityService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileManager");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "priorities.json");
        _priorities = Load();
    }

    public Dictionary<string, int> GetPrioritiesForDirectory(string dirPath)
    {
        var prefix = Normalize(dirPath);
        if (!prefix.EndsWith('/'))
            prefix += '/';

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        lock (_lock)
        {
            foreach (var kvp in _priorities)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var remaining = kvp.Key.AsSpan(prefix.Length);
                    if (remaining.IndexOf('/') < 0)
                        result[kvp.Key] = kvp.Value;
                }
            }
        }
        return result;
    }

    public event Action? StarredChanged;

    public List<string> GetAllStarred()
    {
        lock (_lock)
        {
            return _priorities
                .Where(kvp => kvp.Value == 2)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }

    public void SetPriority(string filePath, int priority)
    {
        var key = Normalize(filePath);
        bool wasStarred, isStarred;
        lock (_lock)
        {
            wasStarred = _priorities.TryGetValue(key, out var old) && old == 2;
            isStarred = priority == 2;

            if (priority == 0)
                _priorities.Remove(key);
            else
                _priorities[key] = priority;
            _dirty = true;
        }

        _saveTimer?.Dispose();
        _saveTimer = new Timer(_ => FlushSave(), null, 500, Timeout.Infinite);

        if (wasStarred != isStarred)
            StarredChanged?.Invoke();
    }

    public void FlushSave()
    {
        lock (_lock)
        {
            if (!_dirty) return;
            _dirty = false;
            Save();
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();

    private Dictionary<string, int> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions)
                    ?? new Dictionary<string, int>();
            }
        }
        catch { }
        return new Dictionary<string, int>();
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_priorities, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}
