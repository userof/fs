using System.Collections.Generic;
using FileManager.Models;

namespace FileManager.Services;

public interface IProfileService
{
    IReadOnlyList<Profile> LoadAllProfiles();
    void SaveProfile(Profile profile);
    void DeleteProfile(string name);
}
