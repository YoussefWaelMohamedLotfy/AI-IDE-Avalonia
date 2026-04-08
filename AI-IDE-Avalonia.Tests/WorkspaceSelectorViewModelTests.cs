using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.Services;
using AI_IDE_Avalonia.ViewModels;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Unit tests for <see cref="WorkspaceSelectorViewModel"/>.
/// Uses a stub <see cref="RecentFoldersService"/> to avoid touching the file system.
/// </summary>
public class WorkspaceSelectorViewModelTests
{
    // Stub service that holds in-memory entries only.
    private sealed class StubRecentFoldersService : RecentFoldersService
    {
        private readonly List<RecentFolderEntry> _entries;

        public StubRecentFoldersService(IEnumerable<RecentFolderEntry>? initial = null)
        {
            _entries = [.. (initial ?? [])];
        }

        public override List<RecentFolderEntry> Load() => [.. _entries];

        public override void Add(string folderPath)
        {
            _entries.RemoveAll(e =>
                string.Equals(e.Path, folderPath, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, new RecentFolderEntry { Path = folderPath });
        }
    }

    [Test]
    public async Task Constructor_WithNoRecentFolders_HasEmptyCollection()
    {
        var stub = new StubRecentFoldersService();
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        await Assert.That(vm.RecentFolders).IsEmpty();
        await Assert.That(vm.HasRecentFolders).IsFalse();
    }

    [Test]
    public async Task Constructor_WithRecentFolders_PopulatesCollection()
    {
        var entries = new[]
        {
            new RecentFolderEntry { Path = "/workspace/alpha" },
            new RecentFolderEntry { Path = "/workspace/beta"  },
        };
        var stub = new StubRecentFoldersService(entries);
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        await Assert.That(vm.RecentFolders.Count).IsEqualTo(2);
        await Assert.That(vm.HasRecentFolders).IsTrue();
    }

    [Test]
    public async Task SkipCommand_CompletesSelectionWithNull()
    {
        var stub = new StubRecentFoldersService();
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        vm.SkipCommand.Execute(null);

        var result = await vm.SelectionTask;
        await Assert.That(result).IsNull();
        await Assert.That(vm.ExitRequested).IsFalse();
    }

    [Test]
    public async Task ExitCommand_SetsExitRequestedAndCompletesWithNull()
    {
        var stub = new StubRecentFoldersService();
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        vm.ExitCommand.Execute(null);

        var result = await vm.SelectionTask;
        await Assert.That(result).IsNull();
        await Assert.That(vm.ExitRequested).IsTrue();
    }

    [Test]
    public async Task OpenRecentFolderCommand_CompletesWithChosenPath()
    {
        var entry = new RecentFolderEntry { Path = "/workspace/project" };
        var stub  = new StubRecentFoldersService([entry]);
        var loc   = new LocalizationService();
        var vm    = new WorkspaceSelectorViewModel(stub, loc);

        vm.OpenRecentFolderCommand.Execute(entry);

        var result = await vm.SelectionTask;
        await Assert.That(result).IsEqualTo("/workspace/project");
    }

    [Test]
    public async Task Loc_ExposesLocalizationService()
    {
        var stub = new StubRecentFoldersService();
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        await Assert.That(vm.Loc).IsSameReferenceAs(loc);
    }

    [Test]
    public async Task SelectionTask_IsNotCompletedBeforeUserAction()
    {
        var stub = new StubRecentFoldersService();
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        await Assert.That(vm.SelectionTask.IsCompleted).IsFalse();
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var stub = new StubRecentFoldersService();
        var loc  = new LocalizationService();
        var vm   = new WorkspaceSelectorViewModel(stub, loc);

        // Should not throw.
        vm.Dispose();
    }
}
