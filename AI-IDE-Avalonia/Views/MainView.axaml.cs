using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Serializer.SystemTextJson;
using AI_IDE_Avalonia.Services;
using AI_IDE_Avalonia.ViewModels;
using AI_IDE_Avalonia.ViewModels.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace AI_IDE_Avalonia.Views;

public partial class MainView : UserControl
{
    private IDockSerializer? _serializer;
    private IDockState? _dockState;

    // Cached singleton resolved once App.Services is ready.
    private DocumentService? _documentService;
    private DocumentService DocumentSvc =>
        _documentService ??= App.Services.GetRequiredService<DocumentService>();
    
    public MainView()
    {
        InitializeComponent();
        _isDark = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;
        InitializeDockState();
        DataContextChanged += OnDataContextChanged;
    }

    private bool _isDark;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.WireLayoutIO(() => OpenLayout(), () => SaveLayout(), CloseLayout);
        vm.WireDocumentIO(() => SaveActiveDocument(), () => SaveAllDocuments());
        vm.WireToggleTheme(() =>
        {
            _isDark = !_isDark;
            App.ThemeManager?.Switch(_isDark ? 1 : 0);
        });
    }

    private void InitializeDockState()
    {
        _serializer = DockSystemTextJsonGenerated.CreateSerializer();
        _dockState = new DockState();

        if (DataContext is MainWindowViewModel mainWindowViewModel)
        {
            var layout = mainWindowViewModel.Layout;
            if (layout != null)
            {
                _dockState.Save(layout);
            }
        }
    }

    private async Task OpenLayout()
    {
        if (_serializer is null || _dockState is null)
        {
            return;
        }

        var storageProvider = (this.GetVisualRoot() as TopLevel)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open layout",
            FileTypeFilter = [new FilePickerFileType("Json") { Patterns = ["*.json"] }, new FilePickerFileType("All") { Patterns = ["*.*"] }],
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var layout = _serializer.Load<IRootDock?>(stream);
                if (layout is not null)
                {
                    _dockState.Restore(layout);

                    if (DataContext is MainWindowViewModel mainWindowViewModel)
                    {
                        mainWindowViewModel.Layout = layout;
                        mainWindowViewModel.InitLayout();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private async Task SaveLayout()
    {
        if (_serializer is null || _dockState is null)
        {
            return;
        }

        var storageProvider = (this.GetVisualRoot() as TopLevel)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save layout",
            FileTypeChoices = [new FilePickerFileType("Json") { Patterns = ["*.json"] }, new FilePickerFileType("All") { Patterns = ["*.*"] }],
            SuggestedFileName = "layout",
            DefaultExtension = "json",
            ShowOverwritePrompt = true
        });

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                
                if (DataContext is MainWindowViewModel mainWindowViewModel)
                {
                    _serializer.Save(stream, mainWindowViewModel.Layout);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    private void CloseLayout()
    {
        if (DataContext is MainWindowViewModel mainWindowViewModel)
        {
            mainWindowViewModel.CloseLayout();
            mainWindowViewModel.Layout = null;
        }
    }

    // ── Document save ──────────────────────────────────────────────────────────

    private async Task SaveActiveDocument()
    {
        var doc = DocumentSvc.ActiveDocument;
        if (doc is null) return;

        if (await doc.SaveAsync())
            return;

        // No file path — show a Save As dialog.
        var topLevel = this.GetVisualRoot() as TopLevel;
        var suggestedName = doc.BaseTitle.Length > 0 ? doc.BaseTitle : "untitled";
        var path = await AI_IDE_Avalonia.Services.StorageDialogHelper.PromptSavePathAsync(topLevel, suggestedName);
        if (path is not null)
        {
            doc.FilePath = path;
            await doc.SaveAsync();
        }
    }

    private async Task SaveAllDocuments()
    {
        foreach (DocumentViewModel doc in DocumentSvc.AllDocuments)
        {
            if (doc.FilePath is not null)
                await doc.SaveAsync();
            // In-memory documents are skipped for Save All — they require explicit Save As.
        }
    }

    /// <summary>Shows a Save File dialog and returns the chosen path, or <see langword="null"/> if cancelled.</summary>
    private Task<string?> PromptSavePathAsync(string suggestedName)
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        return AI_IDE_Avalonia.Services.StorageDialogHelper.PromptSavePathAsync(topLevel, suggestedName);
    }
}
