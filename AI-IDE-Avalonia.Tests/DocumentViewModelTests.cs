using AI_IDE_Avalonia.ViewModels.Documents;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Unit tests for <see cref="DocumentViewModel"/> state-machine logic.
/// These tests exercise the dirty-tracking helpers and do not touch file I/O,
/// App.Services, or the Avalonia UI thread.
/// </summary>
public class DocumentViewModelTests
{
    [Test]
    public async Task NewDocument_IsNotModified()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc1",
            Title = "Untitled",
        });

        await Assert.That(doc.IsModified).IsFalse();
    }

    [Test]
    public async Task MarkModified_SetsIsModified()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc2",
            Title = "Untitled",
        });

        await AvaloniaDispatch.RunAsync(() => doc.MarkModified());

        await Assert.That(doc.IsModified).IsTrue();
    }

    [Test]
    public async Task MarkSaved_ClearsIsModified()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc3",
            Title = "Untitled",
        });

        await AvaloniaDispatch.RunAsync(() =>
        {
            doc.MarkModified();
            doc.MarkSaved();
        });

        await Assert.That(doc.IsModified).IsFalse();
    }

    [Test]
    public async Task MarkModified_IsIdempotent()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc4",
            Title = "Untitled",
        });

        var events = 0;
        doc.CommandBarsChanged += (_, _) => events++;

        await AvaloniaDispatch.RunAsync(() =>
        {
            doc.MarkModified(); // first call — raises event
            doc.MarkModified(); // second call — must be a no-op
        });

        await Assert.That(doc.IsModified).IsTrue();
        await Assert.That(events).IsEqualTo(1);
    }

    [Test]
    public async Task MarkSaved_IsIdempotent()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc5",
            Title = "Untitled",
        });

        var events = 0;
        doc.CommandBarsChanged += (_, _) => events++;

        await AvaloniaDispatch.RunAsync(() =>
        {
            doc.MarkModified();  // +1 event
            doc.MarkSaved();     // +1 event
            doc.MarkSaved();     // no-op
        });

        await Assert.That(events).IsEqualTo(2);
    }

    [Test]
    public async Task SaveAsync_WithNullFilePath_ReturnsFalse()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id       = "doc6",
            Title    = "Untitled",
            FilePath = null,
        });

        var result = await doc.SaveAsync();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SaveAsync_WritesContentToDisk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
            {
                Id           = tempFile,
                Title        = "temp.cs",
                FilePath     = tempFile,
                DocumentText = "// hello world",
            });

            var result = await doc.SaveAsync();

            await Assert.That(result).IsTrue();
            var written = await File.ReadAllTextAsync(tempFile);
            await Assert.That(written).IsEqualTo("// hello world");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task SaveAsync_Success_ClearsModifiedFlag()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
            {
                Id           = tempFile,
                Title        = "temp.cs",
                FilePath     = tempFile,
                DocumentText = "content",
            });

            await AvaloniaDispatch.RunAsync(() => doc.MarkModified());
            await doc.SaveAsync();

            await Assert.That(doc.IsModified).IsFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task GetCommandBars_ReturnsTwoEntries()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc7",
            Title = "Untitled",
        });

        var bars = doc.GetCommandBars();

        await Assert.That(bars.Count).IsEqualTo(2);
    }

    [Test]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var doc = await AvaloniaDispatch.RunAsync(() => new DocumentViewModel
        {
            Id    = "doc8",
            Title = "Untitled",
        });

        await doc.DisposeAsync();
        // No assertion needed — just verify no exception is thrown.
    }
}
