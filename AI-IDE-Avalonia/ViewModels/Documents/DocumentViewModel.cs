using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.CommandBars;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels.Documents;

public partial class DocumentViewModel : Document, IDockCommandBarProvider, IAsyncDisposable
{
    private int _renameCounter;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _closeCommand;

    // The clean tab title (without the '*' dirty indicator).
    private string _baseTitle = string.Empty;

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
            {
                // Keep _baseTitle in sync whenever the title is set while the document is clean.
                if (!IsModified)
                    _baseTitle = Title ?? string.Empty;
            }
        };
    }

    // ── Dirty-state helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Marks the document as having unsaved changes and appends '*' to the tab title.
    /// Safe to call multiple times; no-ops when already modified.
    /// </summary>
    public void MarkModified()
    {
        if (IsModified) return;

        _baseTitle = Title ?? string.Empty;
        IsModified = true;
        Title = _baseTitle + "*";
        RaiseCommandBarsChanged();
    }

    /// <summary>
    /// Clears the modified flag and removes the '*' from the tab title.
    /// </summary>
    public void MarkSaved()
    {
        if (!IsModified) return;

        IsModified = false;
        Title = _baseTitle;
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
            await File.WriteAllTextAsync(FilePath, DocumentText);
            MarkSaved();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[DocumentViewModel] Could not write '{FilePath}': {ex.Message}");
            return false;
        }
    }

    // ── IDockCommandBarProvider ───────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
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
