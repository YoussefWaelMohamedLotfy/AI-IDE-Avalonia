using System;
using System.IO;
using R3;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Wraps <see cref="FileSystemWatcher"/> with R3 observables so that consumers can
/// subscribe to file-system events using standard reactive operators (debounce, filter, etc.).
/// Dispose this instance to stop watching and release the underlying watcher.
/// </summary>
public sealed class FileSystemWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    // ── Public observables ─────────────────────────────────────────────────────

    /// <summary>Fires when an existing file's content or attributes change.</summary>
    public Observable<FileSystemEventArgs> Changed { get; }

    /// <summary>Fires when a new file or directory is created.</summary>
    public Observable<FileSystemEventArgs> Created { get; }

    /// <summary>Fires when a file or directory is deleted.</summary>
    public Observable<FileSystemEventArgs> Deleted { get; }

    /// <summary>Fires when a file or directory is renamed.</summary>
    public Observable<RenamedEventArgs> Renamed { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts watching <paramref name="path"/> for file-system changes.
    /// </summary>
    /// <param name="path">Directory to watch.</param>
    /// <param name="filter">File-name filter (default: all files).</param>
    /// <param name="recursive">
    ///   <see langword="true"/> to include subdirectories (default: <see langword="true"/>).
    /// </param>
    public FileSystemWatcherService(string path, string filter = "*.*", bool recursive = true)
    {
        _watcher = new FileSystemWatcher(path, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite
                         | NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.Size,
            IncludeSubdirectories = recursive,
            // 64 KB buffer reduces the risk of Windows dropping events under heavy I/O.
            // The default of 8 KB can overflow quickly in large or busy workspaces.
            InternalBufferSize = 65536,
            EnableRaisingEvents = true
        };

        // File content changes are debounced more aggressively (500 ms) because editors
        // often trigger multiple rapid write events for a single logical save operation.
        Changed = Observable
            .FromEvent<FileSystemEventHandler, FileSystemEventArgs>(
                h => (_, e) => h(e),
                h => _watcher.Changed += h,
                h => _watcher.Changed -= h)
            .Debounce(TimeSpan.FromMilliseconds(500));

        // Structural changes (create/delete/rename) are less prone to bursts, so a shorter
        // debounce (200 ms) keeps the Solution Explorer responsive while still coalescing
        // rapid successive events from batch file operations.
        Created = Observable
            .FromEvent<FileSystemEventHandler, FileSystemEventArgs>(
                h => (_, e) => h(e),
                h => _watcher.Created += h,
                h => _watcher.Created -= h)
            .Debounce(TimeSpan.FromMilliseconds(200));

        Deleted = Observable
            .FromEvent<FileSystemEventHandler, FileSystemEventArgs>(
                h => (_, e) => h(e),
                h => _watcher.Deleted += h,
                h => _watcher.Deleted -= h)
            .Debounce(TimeSpan.FromMilliseconds(200));

        Renamed = Observable
            .FromEvent<RenamedEventHandler, RenamedEventArgs>(
                h => (_, e) => h(e),
                h => _watcher.Renamed += h,
                h => _watcher.Renamed -= h)
            .Debounce(TimeSpan.FromMilliseconds(200));
    }

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
