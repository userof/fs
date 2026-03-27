using System.Collections.Generic;

namespace FileManager.Services;

public interface IPriorityService
{
    Dictionary<string, int> GetPrioritiesForDirectory(string dirPath);
    void SetPriority(string filePath, int priority);
    void FlushSave();
}
