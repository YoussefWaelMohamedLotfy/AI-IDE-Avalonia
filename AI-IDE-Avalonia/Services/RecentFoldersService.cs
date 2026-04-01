using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AI_IDE_Avalonia.Models;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Persists and retrieves the list of recently opened workspace folders.
/// Data is stored in JSON at <c>%AppData%/AI-IDE-Avalonia/recent-folders.json</c>.
/// </summary>
public class RecentFoldersService
{
    private const int MaxEntries = 10;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AI-IDE-Avalonia",
        "recent-folders.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Loads and returns the persisted recent-folder list (most-recent first).</summary>
    public List<RecentFolderEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return [];

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<RecentFolderEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Adds <paramref name="folderPath"/> as the most-recently-used entry and persists the list.
    /// If the path is already present it is moved to the top.
    /// </summary>
    public void Add(string folderPath)
    {
        var entries = Load();
        entries.RemoveAll(e => string.Equals(e.Path, folderPath, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, new RecentFolderEntry { Path = folderPath, LastAccessedUtc = DateTime.UtcNow });

        if (entries.Count > MaxEntries)
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

        Save(entries);
    }

    private static void Save(List<RecentFolderEntry> entries)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir is null) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch { /* best effort – non-critical */ }
    }
}
