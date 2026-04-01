using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_IDE_Avalonia.ViewModels;

public partial class WorkspaceSelectorViewModel : ViewModelBase, IDisposable
{
    private readonly RecentFoldersService _service = new();
    private readonly TaskCompletionSource<string?> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Injected by the code-behind after the window has a StorageProvider.
    private Func<Task<string?>>? _folderPickerFunc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentFolders))]
    private ObservableCollection<RecentFolderEntry> _recentFolders;

    /// <summary>Awaited by <c>App.axaml.cs</c> to receive the chosen workspace path (or <c>null</c> to skip).</summary>
    public Task<string?> SelectionTask => _tcs.Task;

    public bool HasRecentFolders => RecentFolders.Count > 0;

    public WorkspaceSelectorViewModel()
    {
        var entries = _service.Load();
        _recentFolders = new ObservableCollection<RecentFolderEntry>(entries);
    }

    /// <summary>Injects the platform folder-picker delegate provided by the code-behind.</summary>
    internal void WireFolderPicker(Func<Task<string?>> folderPicker) =>
        _folderPickerFunc = folderPicker;

    /// <summary>Opens a folder-picker dialog and, on success, completes the selection.</summary>
    [RelayCommand]
    private async Task BrowseFolder()
    {
        if (_folderPickerFunc is null) return;

        var path = await _folderPickerFunc();
        if (path is not null)
            SetResult(path);
    }

    /// <summary>Selects a folder from the recent-folders list.</summary>
    [RelayCommand]
    private void OpenRecentFolder(RecentFolderEntry entry) => SetResult(entry.Path);

    /// <summary>Closes the window without selecting a workspace.</summary>
    [RelayCommand]
    private void Skip() => _tcs.TrySetResult(null);

    /// <summary>Exits the application entirely (triggered by the ✕ button).</summary>
    [RelayCommand]
    private void Exit()
    {
        ExitRequested = true;
        _tcs.TrySetResult(null);
    }

    /// <summary>True when the user chose to close the app rather than skip.</summary>
    public bool ExitRequested { get; private set; }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void SetResult(string path)
    {
        _service.Add(path);
        _tcs.TrySetResult(path);
    }

    public void Dispose() => _folderPickerFunc = null;
}
