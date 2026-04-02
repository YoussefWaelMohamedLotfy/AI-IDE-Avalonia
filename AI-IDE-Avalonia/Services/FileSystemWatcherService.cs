using System;
using System.IO;
using System.Reactive.Linq;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Wraps <see cref="FileSystemWatcher"/> with Rx.NET observables so that consumers can
/// subscribe to file-system events using standard reactive operators (throttle, filter, etc.).
/// Dispose this instance to stop watching and release the underlying watcher.
/// </summary>
public sealed class FileSystemWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    // ── Public observables ─────────────────────────────────────────────────────

    /// <summary>Fires when an existing file's content or attributes change.</summary>
    public IObservable<FileSystemEventArgs> Changed { get; }

    /// <summary>Fires when a new file or directory is created.</summary>
    public IObservable<FileSystemEventArgs> Created { get; }

    /// <summary>Fires when a file or directory is deleted.</summary>
    public IObservable<FileSystemEventArgs> Deleted { get; }

    /// <summary>Fires when a file or directory is renamed.</summary>
    public IObservable<RenamedEventArgs> Renamed { get; }

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
            EnableRaisingEvents = true
        };

        // File content changes are throttled more aggressively (500 ms) because editors
        // often trigger multiple rapid write events for a single logical save operation.
        Changed = Observable
            .FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => _watcher.Changed += h,
                h => _watcher.Changed -= h)
            .Select(e => e.EventArgs)
            .Throttle(TimeSpan.FromMilliseconds(500));

        // Structural changes (create/delete/rename) are less prone to bursts, so a shorter
        // throttle (200 ms) keeps the Solution Explorer responsive while still coalescing
        // near-simultaneous events (e.g. atomic rename = delete + create).
        Created = Observable
            .FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => _watcher.Created += h,
                h => _watcher.Created -= h)
            .Select(e => e.EventArgs)
            .Throttle(TimeSpan.FromMilliseconds(200));

        Deleted = Observable
            .FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => _watcher.Deleted += h,
                h => _watcher.Deleted -= h)
            .Select(e => e.EventArgs)
            .Throttle(TimeSpan.FromMilliseconds(200));

        Renamed = Observable
            .FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                h => _watcher.Renamed += h,
                h => _watcher.Renamed -= h)
            .Select(e => e.EventArgs)
            .Throttle(TimeSpan.FromMilliseconds(200));
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
