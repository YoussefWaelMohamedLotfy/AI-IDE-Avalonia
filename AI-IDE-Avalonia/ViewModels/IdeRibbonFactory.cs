using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using RibbonControl.Core.Enums;
using RibbonControl.Core.Icons;
using RibbonControl.Core.Models;
using RibbonControl.Core.Services;
using RibbonControl.Core.ViewModels;

namespace AI_IDE_Avalonia.ViewModels;

internal static class IdeRibbonFactory
{
    // Base IDE commands — handled with generic status callback.
    // Navigation, layout, window, and theme commands are registered
    // directly in MainWindowViewModel with their real implementations.
    private static readonly string[] AllCommandIds =
    [
        "new-file", "open-file", "save", "save-all",
        "undo", "redo", "cut", "copy", "paste",
        "find", "replace", "goto",
        "comment", "uncomment",
        "build", "rebuild", "clean",
        "start-debug", "stop-debug", "restart-debug",
        "toggle-explorer", "toggle-problems", "toggle-terminal",
        "about",
    ];

    public static RibbonViewModel BuildRibbon()
    {
        var ribbon = new RibbonViewModel { SelectedTabId = "home" };
        ribbon.Tabs.Add(BuildHomeTab());
        ribbon.Tabs.Add(BuildEditTab());
        ribbon.Tabs.Add(BuildViewTab());
        ribbon.Tabs.Add(BuildRunTab());
        ribbon.Tabs.Add(BuildHelpTab());
        return ribbon;
    }

    public static DictionaryRibbonCommandCatalog BuildCommandCatalog(Action<string> onExecuted)
    {
        var catalog = new DictionaryRibbonCommandCatalog();
        foreach (var id in AllCommandIds)
            catalog.Register(id, new RelayCommand(() => onExecuted(id)));
        return catalog;
    }

    public static ObservableCollection<RibbonItem> BuildQuickAccessItems() =>
    [
        new RibbonItem { Id = "qat-save",  Label = "Save",         IconPathData = FluentIconData.DocumentSave20Regular, CommandId = "save",         KeyTip = "QS", ScreenTip = "Save file",             Order = 0 },
        new RibbonItem { Id = "qat-undo",  Label = "Undo",         IconPathData = FluentIconData.ArrowUndo20Regular,    CommandId = "undo",         KeyTip = "QU", ScreenTip = "Undo last action",      Order = 1 },
        new RibbonItem { Id = "qat-build", Label = "Build",        IconPathData = FluentIconData.Wrench20Regular,       CommandId = "build",        KeyTip = "QB", ScreenTip = "Build project",         Order = 2 },
        new RibbonItem { Id = "qat-theme", Label = "Toggle Theme", IconPathData = FluentIconData.Settings20Regular,     CommandId = "toggle-theme", KeyTip = "QT", ScreenTip = "Toggle dark/light theme", Order = 3 },
    ];

    public static ObservableCollection<RibbonBackstageItem> BuildBackstageItems(
        ICommand newCmd, ICommand openCmd, ICommand saveCmd, ICommand saveAllCmd) =>
    [
        new RibbonBackstageItem { Id = "bs-new",      Label = "New",      IconPathData = FluentIconData.Document20Regular,     ShowChevron = false, Order = 0, Command = newCmd,     ExecuteCommandOnSelect = true, Content = "Create a new file." },
        new RibbonBackstageItem { Id = "bs-open",     Label = "Open",     IconPathData = FluentIconData.FolderOpen20Regular,   ShowChevron = false, Order = 1, Command = openCmd,    ExecuteCommandOnSelect = true, Content = "Open an existing file." },
        new RibbonBackstageItem { Id = "bs-save",     Label = "Save",     IconPathData = FluentIconData.DocumentSave20Regular, ShowChevron = false, Order = 2, Command = saveCmd,    ExecuteCommandOnSelect = true, Content = "Save the current file." },
        new RibbonBackstageItem { Id = "bs-save-all", Label = "Save All", IconPathData = FluentIconData.DocumentSave20Regular, ShowChevron = false, Order = 3, Command = saveAllCmd, ExecuteCommandOnSelect = true, Content = "Save all open files." },
    ];

    private static RibbonTabViewModel BuildHomeTab() =>
        Tab("home", "Home", 0,
            Group("home-navigate", "Navigate", 0,
                Item("nav-back",      "Back",      FluentIconData.ArrowUndo20Regular,    0, "nav-back",      "NB", "Go back"),
                Item("nav-forward",   "Forward",   FluentIconData.ChevronRight20Regular, 1, "nav-forward",   "NF", "Go forward"),
                Item("nav-dashboard", "Dashboard", FluentIconData.Grid20Regular,          2, "nav-dashboard", "ND", "Open Dashboard"),
                Item("nav-home",      "Home",      FluentIconData.AppsList20Regular,      3, "nav-home",      "NH", "Navigate to home view")
            ),
            Group("home-file", "File", 1,
                Item("new-file",  "New",      FluentIconData.Document20Regular,     0, "new-file",  "N",  "Create a new file"),
                Item("open-file", "Open",     FluentIconData.FolderOpen20Regular,   1, "open-file", "O",  "Open a file"),
                Item("save",      "Save",     FluentIconData.DocumentSave20Regular, 2, "save",      "S",  "Save the current file"),
                Item("save-all",  "Save All", FluentIconData.DocumentSave20Regular, 3, "save-all",  "SA", "Save all open files")
            ),
            Group("home-clipboard", "Clipboard", 2,
                Item("paste", "Paste", FluentIconData.ClipboardPaste20Regular, 0, "paste", "V", "Paste from clipboard"),
                Item("copy",  "Copy",  FluentIconData.Copy20Regular,           1, "copy",  "C", "Copy to clipboard"),
                Item("cut",   "Cut",   FluentIconData.ArrowExport20Regular,    2, "cut",   "X", "Cut to clipboard")
            ),
            Group("home-history", "History", 3,
                Item("undo", "Undo", FluentIconData.ArrowUndo20Regular, 0, "undo", "Z", "Undo last action"),
                Item("redo", "Redo", FluentIconData.ArrowSync20Regular,  1, "redo", "Y", "Redo last undone action")
            )
        );

    private static RibbonTabViewModel BuildEditTab() =>
        Tab("edit", "Edit", 1,
            Group("edit-find", "Find", 0,
                Item("find",    "Find",    FluentIconData.Search20Regular,       0, "find",    "F", "Find in current file"),
                Item("replace", "Replace", FluentIconData.ArrowSync20Regular,    1, "replace", "H", "Find and replace"),
                Item("goto",    "Go To",   FluentIconData.ChevronRight20Regular, 2, "goto",    "G", "Go to line or symbol")
            ),
            Group("edit-code", "Code", 1,
                Item("comment",   "Comment",   FluentIconData.Comment20Regular, 0, "comment",   "LC", "Comment selected lines"),
                Item("uncomment", "Uncomment", FluentIconData.Comment20Regular, 1, "uncomment", "LU", "Uncomment selected lines")
            )
        );

    private static RibbonTabViewModel BuildViewTab() =>
        Tab("view", "View", 2,
            Group("view-panels", "Panels", 0,
                Item("toggle-explorer", "Explorer", FluentIconData.AppsList20Regular,       0, "toggle-explorer", "VE", "Toggle Solution Explorer"),
                Item("toggle-problems", "Problems", FluentIconData.TextBulletList20Regular,  1, "toggle-problems", "VP", "Toggle Problems panel"),
                Item("toggle-terminal", "Terminal", FluentIconData.Grid20Regular,            2, "toggle-terminal", "VT", "Toggle Terminal panel")
            ),
            Group("view-layout", "Layout", 1,
                Item("layout-new",   "New Layout",  FluentIconData.Document20Regular,     0, "layout-new",   "LN", "Create a new dock layout"),
                Item("layout-open",  "Open Layout", FluentIconData.FolderOpen20Regular,   1, "layout-open",  "LO", "Open a saved dock layout"),
                Item("layout-save",  "Save Layout", FluentIconData.DocumentSave20Regular, 2, "layout-save",  "LS", "Save the current dock layout"),
                Item("layout-close", "Close",       FluentIconData.ArrowExport20Regular,  3, "layout-close", "LC", "Close the current dock layout")
            ),
            Group("view-windows", "Windows", 2,
                Item("layout-show-windows", "Show Windows", FluentIconData.AppsList20Regular,      0, "layout-show-windows", "WS", "Show all floating windows"),
                Item("layout-exit-windows", "Exit Windows", FluentIconData.MoreHorizontal20Regular, 1, "layout-exit-windows", "WE", "Exit floating windows mode")
            ),
            Group("view-themes", "Themes", 3,
                new RibbonItemViewModel
                {
                    Id = "theme-preset",
                    Label = "Theme",
                    Primitive = RibbonItemPrimitive.Custom,
                    Size = RibbonItemSize.Small,
                    Order = 0,
                }
            )
        );

    /// <summary>
    /// Sets the Content of the "theme-preset" item in the View tab's Themes group.
    /// Must be called BEFORE DataContext is assigned to the window so that
    /// RebuildTabs() captures the content when the TabsSource binding fires.
    /// </summary>
    public static void SetThemeContent(RibbonViewModel ribbon, object? content)
    {
        var viewTab = ribbon.Tabs.FirstOrDefault(t => t.Id == "view");
        if (viewTab is null) return;
        var themesGroup = viewTab.GroupsViewModel.FirstOrDefault(g => g.Id == "view-themes");
        if (themesGroup is null) return;
        var themeItem = themesGroup.ItemsViewModel.FirstOrDefault(i => i.Id == "theme-preset");
        if (themeItem is not null)
            themeItem.Content = content;
    }

    private static RibbonTabViewModel BuildRunTab() =>
        Tab("run", "Run", 3,
            Group("run-build", "Build", 0,
                Item("build",   "Build",   FluentIconData.Wrench20Regular,          0, "build",   "B",  "Build the project"),
                Item("rebuild", "Rebuild", FluentIconData.ArrowSync20Regular,        1, "rebuild", "RB", "Rebuild the project"),
                Item("clean",   "Clean",   FluentIconData.Settings20Regular,         2, "clean",   "CL", "Clean build outputs")
            ),
            Group("run-debug", "Debug", 1,
                Item("start-debug",   "Start",   FluentIconData.Pulse20Regular,           0, "start-debug",   "F5", "Start debugging"),
                Item("stop-debug",    "Stop",    FluentIconData.MoreHorizontal20Regular,   1, "stop-debug",    "SF", "Stop debugging"),
                Item("restart-debug", "Restart", FluentIconData.ArrowSync20Regular,        2, "restart-debug", "RF", "Restart debugging")
            )
        );

    private static RibbonTabViewModel BuildHelpTab() =>
        Tab("help", "Help", 4,
            Group("help-support", "Support", 0,
                Item("about", "About", FluentIconData.CheckmarkCircle20Regular, 0, "about", "HA", "About this application")
            )
        );

    private static RibbonTabViewModel Tab(string id, string header, int order, params RibbonGroupViewModel[] groups)
    {
        var tab = new RibbonTabViewModel { Id = id, Header = header, Order = order };
        foreach (var group in groups)
            tab.GroupsViewModel.Add(group);
        return tab;
    }

    private static RibbonGroupViewModel Group(string id, string header, int order, params RibbonItemViewModel[] items)
    {
        var group = new RibbonGroupViewModel
        {
            Id = id,
            Header = header,
            Order = order,
            ItemsLayoutMode = RibbonGroupItemsLayoutMode.Stacked,
            StackedRows = 2,
        };
        foreach (var item in items)
            group.ItemsViewModel.Add(item);
        return group;
    }

    // All items use Small size so the ribbon height fits the content compactly.
    private static RibbonItemViewModel Item(
        string id, string label, string iconPathData, int order,
        string commandId, string keyTip, string screenTip) =>
        new()
        {
            Id = id,
            Label = label,
            IconPathData = iconPathData,
            Order = order,
            CommandId = commandId,
            KeyTip = keyTip,
            ScreenTip = screenTip,
            Size = RibbonItemSize.Small,
        };
}
