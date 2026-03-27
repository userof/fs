using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileManager.Models;

namespace FileManager.Services;

public class ProfileService : IProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _profileDir;

    public ProfileService()
    {
        _profileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileManager", "profiles");
        Directory.CreateDirectory(_profileDir);
    }

    public IReadOnlyList<Profile> LoadAllProfiles()
    {
        var profiles = new List<Profile>();

        if (!Directory.Exists(_profileDir))
            return profiles;

        foreach (var file in Directory.GetFiles(_profileDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
                if (profile != null)
                    profiles.Add(profile);
            }
            catch (Exception)
            {
                // Skip corrupted profile files
            }
        }

        return profiles.OrderBy(p => p.Name).ToList();
    }

    public void SaveProfile(Profile profile)
    {
        var safeName = string.Join("_", profile.Name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(_profileDir, safeName + ".json");
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void DeleteProfile(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(_profileDir, safeName + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }
}
