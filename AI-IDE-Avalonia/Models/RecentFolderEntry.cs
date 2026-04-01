using System;
using System.IO;

namespace AI_IDE_Avalonia.Models;

public class RecentFolderEntry
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The folder name extracted from <see cref="Path"/>.</summary>
    public string Name =>
        System.IO.Path.GetFileName(
            Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
        ?? Path;
}
