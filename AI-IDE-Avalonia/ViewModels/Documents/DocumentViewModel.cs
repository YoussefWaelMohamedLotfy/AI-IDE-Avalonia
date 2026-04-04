using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.CommandBars;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI_IDE_Avalonia.ViewModels.Documents;

public partial class DocumentViewModel : Document, IDockCommandBarProvider, IAsyncDisposable
{
    private int _renameCounter;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _closeCommand;

    // The clean tab title (without the '*' dirty indicator).
    private string _baseTitle = string.Empty;

    // Cached logger resolved lazily to avoid accessing App.Services before it is ready.
    private ILogger<DocumentViewModel>? _logger;
    private ILogger<DocumentViewModel> Logger =>
        _logger ??= App.Services.GetRequiredService<ILogger<DocumentViewModel>>();

    // ── External-change watching ──────────────────────────────────────────────

    private IDisposable? _fileWatchSubscription;

    /// <summary>
    /// Set to <see langword="true"/> immediately before writing the file from <see cref="SaveAsync"/>
    /// so that the resulting filesystem event is silently ignored.
    /// </summary>
    private bool _suppressNextExternalChange;

    /// <summary>The tab title without the unsaved-changes indicator ('*').</summary>
    public string BaseTitle => _baseTitle;

    [ObservableProperty]
    private string _documentText = "";

    [ObservableProperty]
    private string _selectedLanguageExtension = ".cs";

    /// <summary>
    /// Absolute path of the file on disk, or <see langword="null"/> for an in-memory document.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Raised by <see cref="DisposeAsync"/> just before the ViewModel is torn down.
    /// The view subscribes to this to trigger its own cleanup.
    /// </summary>
    public event EventHandler? Disposing;

    public event EventHandler? CommandBarsChanged;

    public DocumentViewModel()
    {
        _renameCommand = new RelayCommand(RenameDocument);
        _closeCommand  = new RelayCommand(CloseDocument);

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Title))
                _baseTitle = Title ?? string.Empty;
        };
    }

    // ── Dirty-state helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Marks the document as having unsaved changes.
    /// The Dock framework's <c>DocumentControl</c> renders its own modified indicator
    /// when <see cref="Dock.Model.Core.IDockable.IsModified"/> is <see langword="true"/>,
    /// so we do not touch the tab <see cref="Dock.Model.Core.IDockable.Title"/>.
    /// Safe to call multiple times; no-ops when already modified.
    /// </summary>
    public void MarkModified()
    {
        if (IsModified) return;

        IsModified = true;
        RaiseCommandBarsChanged();
    }

    /// <summary>
    /// Clears the modified flag after a successful save.
    /// </summary>
    public void MarkSaved()
    {
        if (!IsModified) return;

        IsModified = false;
        RaiseCommandBarsChanged();
    }

    // ── File I/O ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists <see cref="DocumentText"/> to <see cref="FilePath"/>.
    /// Returns <see langword="true"/> when the file was written successfully,
    /// or <see langword="false"/> when <see cref="FilePath"/> is <see langword="null"/>
    /// (the caller should trigger a Save-As dialog instead).
    /// </summary>
    public async Task<bool> SaveAsync()
    {
        if (FilePath is null)
            return false;

        try
        {
            // Suppress the filesystem change event that our own write will trigger.
            _suppressNextExternalChange = true;
            await File.WriteAllTextAsync(FilePath, DocumentText);
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            _suppressNextExternalChange = false;
            Logger.LogError(ex, "Could not write '{FilePath}'", FilePath);
            return false;
        }
    }

    // ── External-change watcher ───────────────────────────────────────────────

    /// <summary>
    /// Starts observing <see cref="FilePath"/> for changes made by external processes.
    /// When the file changes and the document has no unsaved edits, the content is reloaded
    /// automatically.  When the file is deleted, the modified flag is set so the user is
    /// aware the backing store is gone.
    /// Call this once after the document is opened.
    /// </summary>
    internal void StartWatchingFile(Services.FileSystemWatcherService watcher)
    {
        if (FilePath is null) return;

        // Watch for content changes — auto-reload only if the document is clean.
        var changedSub = watcher.Changed
            .Where(e => string.Equals(e.FullPath, FilePath, StringComparison.OrdinalIgnoreCase))
            .Subscribe(async _ =>
            {
                if (_suppressNextExternalChange)
                {
                    _suppressNextExternalChange = false;
                    return;
                }

                if (IsModified) return; // user has unsaved edits — leave them alone

                try
                {
                    var newText = await File.ReadAllTextAsync(FilePath);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DocumentText = newText;
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Could not reload '{FilePath}'", FilePath);
                }
            });

        // Watch for deletion — mark modified so the user knows the file is gone.
        var deletedSub = watcher.Deleted
            .Where(e => string.Equals(e.FullPath, FilePath, StringComparison.OrdinalIgnoreCase))
            .Subscribe(_ =>
            {
                Dispatcher.UIThread.InvokeAsync(MarkModified);
            });

        // Combine both subscriptions so they are disposed together.
        _fileWatchSubscription = new CompositeDisposable(changedSub, deletedSub);
    }

    // ── IDockCommandBarProvider ───────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        _fileWatchSubscription?.Dispose();

        Disposing?.Invoke(this, EventArgs.Empty);

        // Clear all event subscribers so nothing holds references to this ViewModel.
        Disposing = null;
        CommandBarsChanged = null;

        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<DockCommandBarDefinition> GetCommandBars()
    {
        var displayTitle = IsModified ? $"{_baseTitle}*" : Title;

        var menuItems = new List<DockCommandBarItem>
        {
            new("_Document")
            {
                Items =
                [
                    new($"Active: {displayTitle}") { Order = 0 },
                    new("_Rename") { Command = _renameCommand, Order = 1 },
                    new(null) { IsSeparator = true, Order = 2 },
                    new("_Close") { Command = _closeCommand, Order = 3 }
                ]
            }
        };

        var toolItems = new List<DockCommandBarItem>
        {
            new("Rename") { Command = _renameCommand, Order = 0 },
            new("Close")  { Command = _closeCommand,  Order = 1 }
        };

        return
        [
            new("DocumentMenu", DockCommandBarKind.Menu)
            {
                Order = 0,
                Items = menuItems
            },
            new("DocumentToolBar", DockCommandBarKind.ToolBar)
            {
                Order = 1,
                Items = toolItems
            }
        ];
    }

    private void RenameDocument()
    {
        _renameCounter++;
        _baseTitle = $"{Id} ({_renameCounter})";
        Title = IsModified ? _baseTitle + "*" : _baseTitle;
        RaiseCommandBarsChanged();
    }

    private void CloseDocument()
    {
        if (!CanClose)
            return;

        Factory?.CloseDockable(this);
    }

    private void RaiseCommandBarsChanged()
    {
        CommandBarsChanged?.Invoke(this, EventArgs.Empty);
    }
}
