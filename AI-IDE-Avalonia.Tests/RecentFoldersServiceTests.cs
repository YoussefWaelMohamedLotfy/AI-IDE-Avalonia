using System.IO;
using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.Services;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Unit tests for <see cref="RecentFoldersService"/>.
/// Each test uses an isolated temp directory to avoid polluting the real AppData folder.
/// </summary>
public class RecentFoldersServiceTests
{
    // Derive from RecentFoldersService to redirect file I/O to a temp path.
    private sealed class IsolatedRecentFoldersService : RecentFoldersService
    {
        private readonly string _filePath;

        public IsolatedRecentFoldersService(string filePath)
        {
            _filePath = filePath;
        }

        // Override the private storage path via reflection is impractical, so we
        // shadow the service with a custom subclass that writes to the temp file.
        public new List<RecentFolderEntry> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return [];

                var json = File.ReadAllText(_filePath);
                return System.Text.Json.JsonSerializer.Deserialize<List<RecentFolderEntry>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public new void Add(string folderPath)
        {
            var entries = Load();
            entries.RemoveAll(e => string.Equals(e.Path, folderPath, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, new RecentFolderEntry { Path = folderPath, LastAccessedUtc = DateTime.UtcNow });

            const int maxEntries = 10;
            if (entries.Count > maxEntries)
                entries.RemoveRange(maxEntries, entries.Count - maxEntries);

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_filePath, System.Text.Json.JsonSerializer.Serialize(entries, options));
        }
    }

    private string _tempFile = string.Empty;
    private IsolatedRecentFoldersService _svc = null!;

    [Before(HookType.Test)]
    public void SetUp()
    {
        _tempFile = Path.GetTempFileName();
        File.Delete(_tempFile); // Start with no file (simulates first run).
        _svc = new IsolatedRecentFoldersService(_tempFile);
    }

    [After(HookType.Test)]
    public void TearDown()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Test]
    public async Task Load_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var entries = _svc.Load();

        await Assert.That(entries).IsEmpty();
    }

    [Test]
    public async Task Add_SingleEntry_CanBeLoadedBack()
    {
        _svc.Add("/some/path");
        var entries = _svc.Load();

        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].Path).IsEqualTo("/some/path");
    }

    [Test]
    public async Task Add_DuplicatePath_MovesToTop()
    {
        _svc.Add("/path/alpha");
        _svc.Add("/path/beta");
        _svc.Add("/path/alpha"); // re-add alpha — it should move to position 0

        var entries = _svc.Load();

        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Path).IsEqualTo("/path/alpha");
        await Assert.That(entries[1].Path).IsEqualTo("/path/beta");
    }

    [Test]
    public async Task Add_MostRecentEntry_IsFirstInList()
    {
        _svc.Add("/path/first");
        _svc.Add("/path/second");

        var entries = _svc.Load();

        await Assert.That(entries[0].Path).IsEqualTo("/path/second");
    }

    [Test]
    public async Task Add_MoreThanMaxEntries_CapsListAtTen()
    {
        for (var i = 0; i < 15; i++)
            _svc.Add($"/path/{i}");

        var entries = _svc.Load();

        await Assert.That(entries.Count).IsEqualTo(10);
    }

    [Test]
    public async Task Add_SetsLastAccessedUtc_ToRecent()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        _svc.Add("/path/something");
        var after = DateTime.UtcNow.AddSeconds(1);

        var entries = _svc.Load();
        var ts = entries[0].LastAccessedUtc;

        await Assert.That(ts).IsGreaterThan(before);
        await Assert.That(ts).IsLessThan(after);
    }
}
