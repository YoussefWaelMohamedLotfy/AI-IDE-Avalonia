using System.Linq;
using AI_IDE_Avalonia.ViewModels.Documents;
using Dock.Model.Controls;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Provides AI tools and other services with access to the document dock,
/// so they can read, open, or create editor documents at runtime.
/// </summary>
public sealed class DocumentService
{
    public static readonly DocumentService Instance = new();

    private DocumentService() { }

    internal IDocumentDock? DocumentDock { get; set; }

    /// <summary>
    /// Returns the currently active document, or <see langword="null"/> if none is open.
    /// Must be called on the UI thread.
    /// </summary>
    public DocumentViewModel? ActiveDocument =>
        DocumentDock?.ActiveDockable as DocumentViewModel;

    /// <summary>
    /// Returns the active document if one is open; otherwise creates a new document tab,
    /// makes it active, and returns it.
    /// Must be called on the UI thread.
    /// </summary>
    public DocumentViewModel GetOrCreateDocument(string? title = null)
    {
        // Prefer the active document.
        if (DocumentDock?.ActiveDockable is DocumentViewModel active)
            return active;

        // Fall back to the first visible document.
        if (DocumentDock?.VisibleDockables?
                .OfType<DocumentViewModel>().FirstOrDefault() is { } first)
        {
            DocumentDock.Factory?.SetActiveDockable(first);
            return first;
        }

        // No document open — create one using the same pattern as CustomDocumentDock.
        var index = (DocumentDock?.VisibleDockables?.Count ?? 0) + 1;
        var doc = new DocumentViewModel
        {
            Id = $"Document{index}",
            Title = title ?? $"Document{index}"
        };

        DocumentDock?.Factory?.AddDockable(DocumentDock, doc);
        DocumentDock?.Factory?.SetActiveDockable(doc);
        DocumentDock?.Factory?.SetFocusedDockable(DocumentDock, doc);

        return doc;
    }
}
