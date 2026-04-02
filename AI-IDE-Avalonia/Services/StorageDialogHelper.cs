using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Provides reusable file-picker helpers that require a <see cref="TopLevel"/> reference.
/// </summary>
internal static class StorageDialogHelper
{
    /// <summary>
    /// Shows a Save File dialog and returns the chosen local path,
    /// or <see langword="null"/> when the user cancels.
    /// </summary>
    internal static async Task<string?> PromptSavePathAsync(TopLevel? topLevel, string suggestedName)
    {
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = suggestedName,
            ShowOverwritePrompt = true,
        });

        return file?.Path?.LocalPath;
    }
}
