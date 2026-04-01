using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using AI_IDE_Avalonia.ViewModels;

namespace AI_IDE_Avalonia.Views;

public partial class WorkspaceSelectorWindow : Window
{
    public WorkspaceSelectorWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is WorkspaceSelectorViewModel vm)
        {
            vm.WireFolderPicker(PickFolderAsync);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Ensure SelectionTask always completes (e.g. if the user closes the window directly).
        (DataContext as WorkspaceSelectorViewModel)?.SkipCommand.Execute(null);
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async Task<string?> PickFolderAsync()
    {
        var results = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Workspace Folder",
            AllowMultiple = false,
        });

        return results.FirstOrDefault()?.Path.LocalPath;
    }
}
