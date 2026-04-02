using System;
using System.IO;
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

    /// <summary>
    /// Opens a document tab for the given file node.
    /// <para>
    /// When <paramref name="filePath"/> is not <see langword="null"/>, any existing tab whose
    /// <see cref="DocumentViewModel.Id"/> matches is activated instead of opening a duplicate.
    /// The file's contents are read into the editor and the language is selected from the
    /// file extension.
    /// </para>
    /// <para>
    /// When <paramref name="filePath"/> is <see langword="null"/> (in-memory / demo node), a new
    /// blank tab is always created with the language inferred from the extension in
    /// <paramref name="title"/>.
    /// </para>
    /// Must be called on the UI thread.
    /// </summary>
    public DocumentViewModel OpenDocument(string title, string? filePath)
    {
        // Deduplicate: if a tab for this path is already open, just activate it.
        if (filePath is not null)
        {
            var existing = DocumentDock?.VisibleDockables?
                .OfType<DocumentViewModel>()
                .FirstOrDefault(d => d.Id == filePath);

            if (existing is not null)
            {
                DocumentDock?.Factory?.SetActiveDockable(existing);
                DocumentDock?.Factory?.SetFocusedDockable(DocumentDock, existing);
                return existing;
            }
        }

        var index = (DocumentDock?.VisibleDockables?.Count ?? 0) + 1;
        var doc = new DocumentViewModel
        {
            Id    = filePath ?? $"Document{index}",
            Title = title,
            SelectedLanguageExtension = Path.GetExtension(filePath ?? title),
        };

        if (filePath is not null && File.Exists(filePath))
        {
            try { doc.DocumentText = File.ReadAllText(filePath); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DocumentService] Could not read '{filePath}': {ex.Message}");
            }
        }

        DocumentDock?.Factory?.AddDockable(DocumentDock, doc);
        DocumentDock?.Factory?.SetActiveDockable(doc);
        DocumentDock?.Factory?.SetFocusedDockable(DocumentDock, doc);

        return doc;
    }
}
