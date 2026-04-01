using System;
using System.IO;

namespace AI_IDE_Avalonia.Models;

public class RecentFolderEntry
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The folder name extracted from <see cref="Path"/>.</summary>
    public string Name => GetFolderName(Path);

    /// <summary>Returns the display name (last path segment) for any folder path.</summary>
    public static string GetFolderName(string path) =>
        System.IO.Path.GetFileName(
            path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
        is { Length: > 0 } name ? name : path;
}
