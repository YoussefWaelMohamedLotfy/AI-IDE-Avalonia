using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Microsoft.Extensions.Logging;
using RibbonControl.Core.Contracts;
using RibbonControl.Core.Enums;
using RibbonControl.Core.Models;
using RibbonControl.Core.Services;
using RibbonControl.Core.ViewModels;

namespace AI_IDE_Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFactory? _factory;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly LocalizationService _loc;
    private IRootDock? _layout;
    private string _globalStatus = "Global: (none)";
    private readonly DictionaryRibbonCommandCatalog _catalogImpl;

    // Custom ribbon item contents preserved across tab rebuilds.
    private object? _themeContent;
    private object? _langContent;

    // Delegates set lazily by the view once it has a visual root (required for file dialogs / theme).
    private Func<Task>? _openLayoutFunc;
    private Func<Task>? _saveLayoutFunc;
    private Action? _closeLayoutFunc;
    private Action? _toggleThemeAction;
    private Func<Task>? _saveDocumentFunc;
    private Func<Task>? _saveAllDocumentsFunc;

    [ObservableProperty] private bool _isBackstageOpen;
    [ObservableProperty] private string _appTitle = "Avalonia AI IDE";
    [ObservableProperty] private RibbonQuickAccessPlacement _quickAccessPlacement = RibbonQuickAccessPlacement.Below;
    [ObservableProperty] private RibbonStateOwnershipMode _stateOwnershipMode = RibbonStateOwnershipMode.Synchronized;
    [ObservableProperty] private RibbonViewModel _ribbon = null!;

    public IRootDock? Layout
    {
        get => _layout;
        set => SetProperty(ref _layout, value);
    }

    public string GlobalStatus
    {
        get => _globalStatus;
        set => SetProperty(ref _globalStatus, value);
    }

    public ICommand NewLayout { get; }

    /// <summary>Saves the currently active document to disk (shows Save As if needed).</summary>
    public ICommand SaveDocumentCommand { get; }

    /// <summary>Saves all open documents that have a file path.</summary>
    public ICommand SaveAllDocumentsCommand { get; }

    /// <summary>
    /// The current layout direction derived from the active locale.
    /// Binding this to <c>Window.FlowDirection</c> (and/or the root UserControl) automatically
    /// mirrors every child — documents, tools, ribbon — for RTL languages such as Arabic.
    /// </summary>
    public FlowDirection FlowDirection => _loc.FlowDirection;

    public IRibbonCommandCatalog CommandCatalog => _catalogImpl;
    public IRibbonStateStore StateStore { get; }
    public ObservableCollection<RibbonItem> QuickAccessItems { get; }
    public ObservableCollection<RibbonBackstageItem> BackstageItems { get; }
    public IReadOnlyList<string> ActiveContextGroupIds { get; } = [];

    /// <summary>The Solution Explorer tool created during layout initialisation.</summary>
    public AI_IDE_Avalonia.ViewModels.Tools.SolutionExplorerViewModel? SolutionExplorer =>
        (_factory as DockFactory)?.SolutionExplorer;

    public MainWindowViewModel(DocumentService documentService, ILogger<MainWindowViewModel> logger, LocalizationService loc)
    {
        _logger = logger;
        _loc = loc;
        _factory = new DockFactory(new DemoData(), documentService);

        DebugFactoryEvents(_factory);

        var layout = _factory?.CreateLayout();
        if (layout is not null)
        {
            _factory?.InitLayout(layout);
        }
        Layout = layout;
        GlobalStatus = layout is null
            ? "Global: (none)"
            : FormatGlobalStatus(_factory?.GlobalDockTrackingState ?? GlobalDockTrackingState.Empty);

        if (Layout is { } root)
        {
            root.Navigate.Execute("Home");
        }

        NewLayout = new RelayCommand(ResetLayout);

        SaveDocumentCommand     = new AsyncRelayCommand(() => _saveDocumentFunc?.Invoke()     ?? Task.CompletedTask);
        SaveAllDocumentsCommand = new AsyncRelayCommand(() => _saveAllDocumentsFunc?.Invoke() ?? Task.CompletedTask);

        Ribbon = IdeRibbonFactory.BuildRibbon(loc);
        _catalogImpl = IdeRibbonFactory.BuildCommandCatalog(id => GlobalStatus = $"Ribbon: {id}");
        _catalogImpl.Register("nav-back",      new RelayCommand(() => Layout?.GoBack?.Execute(null)));
        _catalogImpl.Register("nav-forward",   new RelayCommand(() => Layout?.GoForward?.Execute(null)));
        _catalogImpl.Register("nav-dashboard", new RelayCommand(() => Layout?.Navigate?.Execute("Dashboard")));
        _catalogImpl.Register("nav-home",      new RelayCommand(() => Layout?.Navigate?.Execute(Layout?.DefaultDockable)));

        // Dock layout commands — file I/O delegates set later via WireLayoutIO (need visual root).
        _catalogImpl.Register("layout-new",   new RelayCommand(ResetLayout));
        _catalogImpl.Register("layout-open",  new AsyncRelayCommand(() => _openLayoutFunc?.Invoke()  ?? Task.CompletedTask));
        _catalogImpl.Register("layout-save",  new AsyncRelayCommand(() => _saveLayoutFunc?.Invoke()  ?? Task.CompletedTask));
        _catalogImpl.Register("layout-close", new RelayCommand(()         => _closeLayoutFunc?.Invoke()));

        // Document save commands — delegates set later via WireDocumentIO (need visual root).
        _catalogImpl.Register("save",     new AsyncRelayCommand(() => _saveDocumentFunc?.Invoke()     ?? Task.CompletedTask));
        _catalogImpl.Register("save-all", new AsyncRelayCommand(() => _saveAllDocumentsFunc?.Invoke() ?? Task.CompletedTask));

        // Window commands
        _catalogImpl.Register("layout-exit-windows", new RelayCommand(() => Layout?.ExitWindows?.Execute(null)));
        _catalogImpl.Register("layout-show-windows", new RelayCommand(() => Layout?.ShowWindows?.Execute(null)));

        // Theme toggle — delegate set later via WireToggleTheme.
        _catalogImpl.Register("toggle-theme", new RelayCommand(() => _toggleThemeAction?.Invoke()));

        StateStore = new InMemoryRibbonStateStore();
        QuickAccessItems = IdeRibbonFactory.BuildQuickAccessItems(loc);
        BackstageItems = IdeRibbonFactory.BuildBackstageItems(
            loc,
            newCmd:     new RelayCommand(() => { GlobalStatus = "Ribbon: New File";  IsBackstageOpen = false; }),
            openCmd:    new RelayCommand(() => { GlobalStatus = "Ribbon: Open File"; IsBackstageOpen = false; }),
            saveCmd:    new AsyncRelayCommand(async () => { IsBackstageOpen = false; await (_saveDocumentFunc?.Invoke()     ?? Task.CompletedTask); }),
            saveAllCmd: new AsyncRelayCommand(async () => { IsBackstageOpen = false; await (_saveAllDocumentsFunc?.Invoke() ?? Task.CompletedTask); })
        );

        _loc.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Notify the FlowDirection binding so the window mirrors for RTL languages.
        OnPropertyChanged(nameof(FlowDirection));

        // Build a fresh RibbonViewModel with updated strings and preserve the
        // current UI state (selected tab, minimized, key-tip mode).
        var selectedTabId = Ribbon.SelectedTabId;
        var isMinimized   = Ribbon.IsMinimized;
        var isKeyTipMode  = Ribbon.IsKeyTipMode;
        var newRibbon = IdeRibbonFactory.BuildRibbon(_loc);
        newRibbon.SelectedTabId = selectedTabId;
        newRibbon.IsMinimized   = isMinimized;
        newRibbon.IsKeyTipMode  = isKeyTipMode;

        // IMPORTANT: inject custom Content (ComboBox controls) into the new
        // viewmodel items BEFORE assigning Ribbon = newRibbon.
        //
        // Setting Ribbon = newRibbon fires PropertyChanged("Ribbon") which the
        // compiled binding (TabsSource="{CompiledBinding Ribbon.Tabs}") handles
        // synchronously: it sets TabsSourceProperty = newRibbon.Tabs, which
        // immediately triggers RebuildTabs() on the ribbon control.
        // RebuildTabs() clones every item via ToRibbonItem() which copies
        // node.Content into the cloned RibbonItem. If Content is not set yet,
        // the ComboBoxes will be absent from the cloned items and never appear.
        IdeRibbonFactory.SetThemeContent(newRibbon, _themeContent);
        IdeRibbonFactory.SetLanguageSelectorContent(newRibbon, _langContent);

        Ribbon = newRibbon;

        // Rebuild Quick Access Toolbar items with new strings.
        QuickAccessItems.Clear();
        foreach (var item in IdeRibbonFactory.BuildQuickAccessItems(_loc))
            QuickAccessItems.Add(item);

        // Rebuild Backstage items with new strings.
        BackstageItems.Clear();
        foreach (var item in IdeRibbonFactory.BuildBackstageItems(
            _loc,
            newCmd:     new RelayCommand(() => { GlobalStatus = "Ribbon: New File";  IsBackstageOpen = false; }),
            openCmd:    new RelayCommand(() => { GlobalStatus = "Ribbon: Open File"; IsBackstageOpen = false; }),
            saveCmd:    new AsyncRelayCommand(async () => { IsBackstageOpen = false; await (_saveDocumentFunc?.Invoke()     ?? Task.CompletedTask); }),
            saveAllCmd: new AsyncRelayCommand(async () => { IsBackstageOpen = false; await (_saveAllDocumentsFunc?.Invoke() ?? Task.CompletedTask); })))
            BackstageItems.Add(item);

        // Update tool window header titles with new strings.
        if (SolutionExplorer is { } se)
            se.Title = _loc.ToolSolutionExplorer;
    }

    internal void WireLayoutIO(Func<Task> open, Func<Task> save, Action close)    {
        _openLayoutFunc  = open;
        _saveLayoutFunc  = save;
        _closeLayoutFunc = close;
    }

    internal void WireDocumentIO(Func<Task> saveDocument, Func<Task> saveAllDocuments)
    {
        _saveDocumentFunc     = saveDocument;
        _saveAllDocumentsFunc = saveAllDocuments;
    }

    internal void WireToggleTheme(Action toggleTheme) => _toggleThemeAction = toggleTheme;

    /// <summary>
    /// Stores the theme-preset ComboBox and injects it into the ribbon's View → Themes group.
    /// Also re-injects after every language rebuild so the content is never lost.
    /// </summary>
    internal void WireThemeContent(object? content)
    {
        _themeContent = content;
        IdeRibbonFactory.SetThemeContent(Ribbon, content);
    }

    /// <summary>
    /// Stores the language-selector ComboBox and injects it into the ribbon's Home → Language group.
    /// Also re-injects after every language rebuild so the content is never lost.
    /// </summary>
    internal void WireLangContent(object? content)
    {
        _langContent = content;
        IdeRibbonFactory.SetLanguageSelectorContent(Ribbon, content);
    }

    /// <summary>
    /// Loads the filesystem tree for <paramref name="folderPath"/> into the Solution Explorer
    /// and updates the window title to reflect the opened workspace.
    /// </summary>
    public void LoadWorkspace(string folderPath)
    {
        SolutionExplorer?.LoadWorkspace(folderPath);
        AppTitle = $"Avalonia AI IDE — {AI_IDE_Avalonia.Models.RecentFolderEntry.GetFolderName(folderPath)}";
    }

    /// <summary>
    /// Asynchronously loads the filesystem tree for <paramref name="folderPath"/> into the Solution
    /// Explorer, reporting status messages via <paramref name="progress"/>, then updates the window title.
    /// </summary>
    public async Task LoadWorkspaceAsync(
        string folderPath,
        IProgress<string>? progress = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (SolutionExplorer is { } se)
            await se.LoadWorkspaceAsync(folderPath, progress, cancellationToken);

        AppTitle = $"Avalonia AI IDE — {AI_IDE_Avalonia.Models.RecentFolderEntry.GetFolderName(folderPath)}";
    }

    public void InitLayout()
    {
        if (Layout is null)
        {
            return;
        }

        _factory?.InitLayout(Layout);
    }

    public void CloseLayout()
    {
        if (Layout is IDock dock)
        {
            if (dock.Close.CanExecute(null))
            {
                dock.Close.Execute(null);
            }
        }
    }

    public void ResetLayout()
    {
        if (Layout is not null)
        {
            if (Layout.Close.CanExecute(null))
            {
                Layout.Close.Execute(null);
            }
        }

        var layout = _factory?.CreateLayout();
        if (layout is not null)
        {
            _factory?.InitLayout(layout);
            Layout = layout;
        }
    }

    private void DebugFactoryEvents(IFactory factory)
    {
        factory.ActiveDockableChanged += (_, args) =>
        {
            _logger.LogDebug("[ActiveDockableChanged] Title='{Title}', Root='{RootId}', Window='{WindowId}'",
                args.Dockable?.Title, args.RootDock?.Id, args.Window?.Id);
        };

        factory.FocusedDockableChanged += (_, args) =>
        {
            _logger.LogDebug("[FocusedDockableChanged] Title='{Title}', Root='{RootId}', Window='{WindowId}'",
                args.Dockable?.Title, args.RootDock?.Id, args.Window?.Id);
        };

        factory.GlobalDockTrackingChanged += (_, args) =>
        {
            GlobalStatus = FormatGlobalStatus(args.Current);
            _logger.LogDebug("[GlobalDockTrackingChanged] Reason='{Reason}', Dockable='{Title}', Root='{RootId}', Window='{WindowId}'",
                args.Reason, args.Current.Dockable?.Title, args.Current.RootDock?.Id, args.Current.Window?.Id);
        };

        factory.DockableAdded += (_, args) =>
        {
            _logger.LogDebug("[DockableAdded] Title='{Title}'", args.Dockable?.Title);
        };

        factory.DockableRemoved += (_, args) =>
        {
            _logger.LogDebug("[DockableRemoved] Title='{Title}'", args.Dockable?.Title);
        };

        factory.DockableClosed += (_, args) =>
        {
            _logger.LogDebug("[DockableClosed] Title='{Title}'", args.Dockable?.Title);
        };

        factory.DockableMoved += (_, args) =>
        {
            _logger.LogDebug("[DockableMoved] Title='{Title}'", args.Dockable?.Title);
        };

        factory.DockableDocked += (_, args) =>
        {
            _logger.LogDebug("[DockableDocked] Title='{Title}', Operation='{Operation}'",
                args.Dockable?.Title, args.Operation);
        };

        factory.DockableUndocked += (_, args) =>
        {
            _logger.LogDebug("[DockableUndocked] Title='{Title}', Operation='{Operation}'",
                args.Dockable?.Title, args.Operation);
        };

        factory.DockableSwapped += (_, args) =>
        {
            _logger.LogDebug("[DockableSwapped] Title='{Title}'", args.Dockable?.Title);
        };

        factory.DockablePinned += (_, args) =>
        {
            _logger.LogDebug("[DockablePinned] Title='{Title}'", args.Dockable?.Title);
        };

        factory.DockableUnpinned += (_, args) =>
        {
            _logger.LogDebug("[DockableUnpinned] Title='{Title}'", args.Dockable?.Title);
        };

        factory.WindowOpened += (_, args) =>
        {
            _logger.LogDebug("[WindowOpened] Title='{Title}'", args.Window?.Title);
        };

        factory.WindowClosed += (_, args) =>
        {
            _logger.LogDebug("[WindowClosed] Title='{Title}'", args.Window?.Title);
        };

        factory.WindowClosing += (_, args) =>
        {
            // NOTE: Set to True to cancel window closing.
#if false
                args.Cancel = true;
#endif
            _logger.LogDebug("[WindowClosing] Title='{Title}', Cancel={Cancel}",
                args.Window?.Title, args.Cancel);
        };

        factory.WindowAdded += (_, args) =>
        {
            _logger.LogDebug("[WindowAdded] Title='{Title}'", args.Window?.Title);
        };

        factory.WindowRemoved += (_, args) =>
        {
            _logger.LogDebug("[WindowRemoved] Title='{Title}'", args.Window?.Title);
        };

        factory.WindowMoveDragBegin += (_, args) =>
        {
            // NOTE: Set to True to cancel window dragging.
#if false
                args.Cancel = true;
#endif
            _logger.LogDebug("[WindowMoveDragBegin] Title='{Title}', Cancel={Cancel}, X='{X}', Y='{Y}'",
                args.Window?.Title, args.Cancel, args.Window?.X, args.Window?.Y);
        };

        factory.WindowMoveDrag += (_, args) =>
        {
            _logger.LogDebug("[WindowMoveDrag] Title='{Title}', X='{X}', Y='{Y}'",
                args.Window?.Title, args.Window?.X, args.Window?.Y);
        };

        factory.WindowMoveDragEnd += (_, args) =>
        {
            _logger.LogDebug("[WindowMoveDragEnd] Title='{Title}', X='{X}', Y='{Y}'",
                args.Window?.Title, args.Window?.X, args.Window?.Y);
        };

        factory.WindowActivated += (_, args) =>
        {
            _logger.LogDebug("[WindowActivated] Title='{Title}'", args.Window?.Title);
        };

        factory.DockableActivated += (_, args) =>
        {
            _logger.LogDebug("[DockableActivated] Title='{Title}'", args.Dockable?.Title);
        };

        factory.WindowDeactivated += (_, args) =>
        {
            _logger.LogDebug("[WindowDeactivated] Title='{Title}'", args.Window?.Title);
        };

        factory.DockableDeactivated += (_, args) =>
        {
            _logger.LogDebug("[DockableDeactivated] Title='{Title}'", args.Dockable?.Title);
        };
    }

    private static string FormatGlobalStatus(GlobalDockTrackingState state)
    {
        var dockableTitle = state.Dockable?.Title ?? "(none)";
        var rootId = state.RootDock?.Id ?? "(none)";
        var windowTitle = state.Window?.Title ?? "(main)";
        var host = state.HostWindow?.GetType().Name ?? "(main)";
        return $"Dockable: {dockableTitle} | Root: {rootId} | Window: {windowTitle} | Host: {host}";
    }
}
