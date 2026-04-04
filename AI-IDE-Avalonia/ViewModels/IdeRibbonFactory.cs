using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AI_IDE_Avalonia.Services;
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

    public static RibbonViewModel BuildRibbon(LocalizationService loc)
    {
        var ribbon = new RibbonViewModel { SelectedTabId = "home" };
        ribbon.Tabs.Add(BuildHomeTab(loc));
        ribbon.Tabs.Add(BuildEditTab(loc));
        ribbon.Tabs.Add(BuildViewTab(loc));
        ribbon.Tabs.Add(BuildRunTab(loc));
        ribbon.Tabs.Add(BuildHelpTab(loc));
        return ribbon;
    }

    public static DictionaryRibbonCommandCatalog BuildCommandCatalog(Action<string> onExecuted)
    {
        var catalog = new DictionaryRibbonCommandCatalog();
        foreach (var id in AllCommandIds)
            catalog.Register(id, new RelayCommand(() => onExecuted(id)));
        return catalog;
    }

    public static ObservableCollection<RibbonItem> BuildQuickAccessItems(LocalizationService loc) =>
    [
        new RibbonItem { Id = "qat-save",  Label = loc["ItemSave"],        IconPathData = FluentIconData.DocumentSave20Regular, CommandId = "save",         KeyTip = "QS", ScreenTip = loc["TipQatSave"],   Order = 0 },
        new RibbonItem { Id = "qat-undo",  Label = loc["ItemUndo"],        IconPathData = FluentIconData.ArrowUndo20Regular,    CommandId = "undo",         KeyTip = "QU", ScreenTip = loc["TipQatUndo"],   Order = 1 },
        new RibbonItem { Id = "qat-build", Label = loc["ItemBuild"],       IconPathData = FluentIconData.Wrench20Regular,       CommandId = "build",        KeyTip = "QB", ScreenTip = loc["TipQatBuild"],  Order = 2 },
        new RibbonItem { Id = "qat-theme", Label = loc["ItemToggleTheme"], IconPathData = FluentIconData.Settings20Regular,     CommandId = "toggle-theme", KeyTip = "QT", ScreenTip = loc["TipQatTheme"], Order = 3 },
    ];

    public static ObservableCollection<RibbonBackstageItem> BuildBackstageItems(
        LocalizationService loc,
        ICommand newCmd, ICommand openCmd, ICommand saveCmd, ICommand saveAllCmd) =>
    [
        new RibbonBackstageItem { Id = "bs-new",      Label = loc["ItemNew"],     IconPathData = FluentIconData.Document20Regular,     ShowChevron = false, Order = 0, Command = newCmd,     ExecuteCommandOnSelect = true, Content = loc["BsNew"]     },
        new RibbonBackstageItem { Id = "bs-open",     Label = loc["ItemOpen"],    IconPathData = FluentIconData.FolderOpen20Regular,   ShowChevron = false, Order = 1, Command = openCmd,    ExecuteCommandOnSelect = true, Content = loc["BsOpen"]    },
        new RibbonBackstageItem { Id = "bs-save",     Label = loc["ItemSave"],    IconPathData = FluentIconData.DocumentSave20Regular, ShowChevron = false, Order = 2, Command = saveCmd,    ExecuteCommandOnSelect = true, Content = loc["BsSave"]    },
        new RibbonBackstageItem { Id = "bs-save-all", Label = loc["ItemSaveAll"], IconPathData = FluentIconData.DocumentSave20Regular, ShowChevron = false, Order = 3, Command = saveAllCmd, ExecuteCommandOnSelect = true, Content = loc["BsSaveAll"] },
    ];

    /// <summary>
    /// Updates all localizable labels on an already-built <see cref="RibbonViewModel"/>
    /// without rebuilding it. Call this after <see cref="LocalizationService.SetCulture"/>.
    /// </summary>
    public static void RefreshRibbonLabels(RibbonViewModel ribbon, LocalizationService loc)
    {
        foreach (var tab in ribbon.Tabs)
        {
            tab.Header = loc[TabKey(tab.Id)];
            foreach (var group in tab.GroupsViewModel)
            {
                group.Header = loc[GroupKey(group.Id)];
                foreach (var item in group.ItemsViewModel)
                {
                    if (ItemLabelKey(item.Id) is { } labelKey)
                        item.Label = loc[labelKey];
                    if (ItemTipKey(item.Id) is { } tipKey)
                        item.ScreenTip = loc[tipKey];
                }
            }
        }
    }

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

    /// <summary>
    /// Sets the Content of the "lang-select" item in the Home tab's Language group.
    /// Must be called BEFORE DataContext is assigned to the window.
    /// </summary>
    public static void SetLanguageSelectorContent(RibbonViewModel ribbon, object? content)
    {
        var homeTab = ribbon.Tabs.FirstOrDefault(t => t.Id == "home");
        if (homeTab is null) return;
        var langGroup = homeTab.GroupsViewModel.FirstOrDefault(g => g.Id == "home-language");
        if (langGroup is null) return;
        var langItem = langGroup.ItemsViewModel.FirstOrDefault(i => i.Id == "lang-select");
        if (langItem is not null)
            langItem.Content = content;
    }

    // ── Tab builders ───────────────────────────────────────────────────────

    private static RibbonTabViewModel BuildHomeTab(LocalizationService loc) =>
        Tab("home", loc["TabHome"], 0,
            Group("home-navigate", loc["GroupNavigate"], 0,
                Item("nav-back",      loc["ItemBack"],      FluentIconData.ArrowUndo20Regular,    0, "nav-back",      "NB", loc["TipBack"]),
                Item("nav-forward",   loc["ItemForward"],   FluentIconData.ChevronRight20Regular, 1, "nav-forward",   "NF", loc["TipForward"]),
                Item("nav-dashboard", loc["ItemDashboard"], FluentIconData.Grid20Regular,          2, "nav-dashboard", "ND", loc["TipDashboard"]),
                Item("nav-home",      loc["ItemHome"],      FluentIconData.AppsList20Regular,      3, "nav-home",      "NH", loc["TipHome"])
            ),
            Group("home-file", loc["GroupFile"], 1,
                Item("new-file",  loc["ItemNew"],     FluentIconData.Document20Regular,     0, "new-file",  "N",  loc["TipNew"]),
                Item("open-file", loc["ItemOpen"],    FluentIconData.FolderOpen20Regular,   1, "open-file", "O",  loc["TipOpen"]),
                Item("save",      loc["ItemSave"],    FluentIconData.DocumentSave20Regular, 2, "save",      "S",  loc["TipSave"]),
                Item("save-all",  loc["ItemSaveAll"], FluentIconData.DocumentSave20Regular, 3, "save-all",  "SA", loc["TipSaveAll"])
            ),
            Group("home-clipboard", loc["GroupClipboard"], 2,
                Item("paste", loc["ItemPaste"], FluentIconData.ClipboardPaste20Regular, 0, "paste", "V", loc["TipPaste"]),
                Item("copy",  loc["ItemCopy"],  FluentIconData.Copy20Regular,           1, "copy",  "C", loc["TipCopy"]),
                Item("cut",   loc["ItemCut"],   FluentIconData.ArrowExport20Regular,    2, "cut",   "X", loc["TipCut"])
            ),
            Group("home-history", loc["GroupHistory"], 3,
                Item("undo", loc["ItemUndo"], FluentIconData.ArrowUndo20Regular, 0, "undo", "Z", loc["TipUndo"]),
                Item("redo", loc["ItemRedo"], FluentIconData.ArrowSync20Regular, 1, "redo", "Y", loc["TipRedo"])
            ),
            Group("home-language", loc["GroupLanguage"], 4,
                new RibbonItemViewModel
                {
                    Id        = "lang-select",
                    Label     = loc["ItemLanguage"],
                    ScreenTip = loc["TipLanguage"],
                    Primitive = RibbonItemPrimitive.Custom,
                    Size      = RibbonItemSize.Small,
                    Order     = 0,
                }
            )
        );

    private static RibbonTabViewModel BuildEditTab(LocalizationService loc) =>
        Tab("edit", loc["TabEdit"], 1,
            Group("edit-find", loc["GroupFind"], 0,
                Item("find",    loc["ItemFind"],    FluentIconData.Search20Regular,       0, "find",    "F", loc["TipFind"]),
                Item("replace", loc["ItemReplace"], FluentIconData.ArrowSync20Regular,    1, "replace", "H", loc["TipReplace"]),
                Item("goto",    loc["ItemGoTo"],    FluentIconData.ChevronRight20Regular, 2, "goto",    "G", loc["TipGoTo"])
            ),
            Group("edit-code", loc["GroupCode"], 1,
                Item("comment",   loc["ItemComment"],   FluentIconData.Comment20Regular, 0, "comment",   "LC", loc["TipComment"]),
                Item("uncomment", loc["ItemUncomment"], FluentIconData.Comment20Regular, 1, "uncomment", "LU", loc["TipUncomment"])
            )
        );

    private static RibbonTabViewModel BuildViewTab(LocalizationService loc) =>
        Tab("view", loc["TabView"], 2,
            Group("view-panels", loc["GroupPanels"], 0,
                Item("toggle-explorer", loc["ItemExplorer"], FluentIconData.AppsList20Regular,       0, "toggle-explorer", "VE", loc["TipExplorer"]),
                Item("toggle-problems", loc["ItemProblems"], FluentIconData.TextBulletList20Regular,  1, "toggle-problems", "VP", loc["TipProblems"]),
                Item("toggle-terminal", loc["ItemTerminal"], FluentIconData.Grid20Regular,            2, "toggle-terminal", "VT", loc["TipTerminal"])
            ),
            Group("view-layout", loc["GroupLayout"], 1,
                Item("layout-new",   loc["ItemNewLayout"],  FluentIconData.Document20Regular,     0, "layout-new",   "LN", loc["TipNewLayout"]),
                Item("layout-open",  loc["ItemOpenLayout"], FluentIconData.FolderOpen20Regular,   1, "layout-open",  "LO", loc["TipOpenLayout"]),
                Item("layout-save",  loc["ItemSaveLayout"], FluentIconData.DocumentSave20Regular, 2, "layout-save",  "LS", loc["TipSaveLayout"]),
                Item("layout-close", loc["ItemClose"],      FluentIconData.ArrowExport20Regular,  3, "layout-close", "LC", loc["TipCloseLayout"])
            ),
            Group("view-windows", loc["GroupWindows"], 2,
                Item("layout-show-windows", loc["ItemShowWindows"], FluentIconData.AppsList20Regular,       0, "layout-show-windows", "WS", loc["TipShowWindows"]),
                Item("layout-exit-windows", loc["ItemExitWindows"], FluentIconData.MoreHorizontal20Regular, 1, "layout-exit-windows", "WE", loc["TipExitWindows"])
            ),
            Group("view-themes", loc["GroupThemes"], 3,
                new RibbonItemViewModel
                {
                    Id        = "theme-preset",
                    Label     = loc["ItemTheme"],
                    Primitive = RibbonItemPrimitive.Custom,
                    Size      = RibbonItemSize.Small,
                    Order     = 0,
                }
            )
        );

    private static RibbonTabViewModel BuildRunTab(LocalizationService loc) =>
        Tab("run", loc["TabRun"], 3,
            Group("run-build", loc["GroupBuild"], 0,
                Item("build",   loc["ItemBuild"],   FluentIconData.Wrench20Regular,         0, "build",   "B",  loc["TipBuild"]),
                Item("rebuild", loc["ItemRebuild"], FluentIconData.ArrowSync20Regular,       1, "rebuild", "RB", loc["TipRebuild"]),
                Item("clean",   loc["ItemClean"],   FluentIconData.Settings20Regular,        2, "clean",   "CL", loc["TipClean"])
            ),
            Group("run-debug", loc["GroupDebug"], 1,
                Item("start-debug",   loc["ItemStart"],   FluentIconData.Pulse20Regular,          0, "start-debug",   "F5", loc["TipStart"]),
                Item("stop-debug",    loc["ItemStop"],    FluentIconData.MoreHorizontal20Regular,  1, "stop-debug",    "SF", loc["TipStop"]),
                Item("restart-debug", loc["ItemRestart"], FluentIconData.ArrowSync20Regular,       2, "restart-debug", "RF", loc["TipRestart"])
            )
        );

    private static RibbonTabViewModel BuildHelpTab(LocalizationService loc) =>
        Tab("help", loc["TabHelp"], 4,
            Group("help-support", loc["GroupSupport"], 0,
                Item("about", loc["ItemAbout"], FluentIconData.CheckmarkCircle20Regular, 0, "about", "HA", loc["TipAbout"])
            )
        );

    // ── Helpers ────────────────────────────────────────────────────────────

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
            Id           = id,
            Label        = label,
            IconPathData = iconPathData,
            Order        = order,
            CommandId    = commandId,
            KeyTip       = keyTip,
            ScreenTip    = screenTip,
            Size         = RibbonItemSize.Small,
        };

    // ── ID → resource-key maps used by RefreshRibbonLabels ────────────────

    private static readonly Dictionary<string, string> _tabKeys = new()
    {
        ["home"] = "TabHome",
        ["edit"] = "TabEdit",
        ["view"] = "TabView",
        ["run"]  = "TabRun",
        ["help"] = "TabHelp",
    };

    private static readonly Dictionary<string, string> _groupKeys = new()
    {
        ["home-navigate"]  = "GroupNavigate",
        ["home-file"]      = "GroupFile",
        ["home-clipboard"] = "GroupClipboard",
        ["home-history"]   = "GroupHistory",
        ["home-language"]  = "GroupLanguage",
        ["edit-find"]      = "GroupFind",
        ["edit-code"]      = "GroupCode",
        ["view-panels"]    = "GroupPanels",
        ["view-layout"]    = "GroupLayout",
        ["view-windows"]   = "GroupWindows",
        ["view-themes"]    = "GroupThemes",
        ["run-build"]      = "GroupBuild",
        ["run-debug"]      = "GroupDebug",
        ["help-support"]   = "GroupSupport",
    };

    private static readonly Dictionary<string, (string Label, string Tip)> _itemKeys = new()
    {
        ["nav-back"]            = ("ItemBack",         "TipBack"),
        ["nav-forward"]         = ("ItemForward",      "TipForward"),
        ["nav-dashboard"]       = ("ItemDashboard",    "TipDashboard"),
        ["nav-home"]            = ("ItemHome",         "TipHome"),
        ["new-file"]            = ("ItemNew",          "TipNew"),
        ["open-file"]           = ("ItemOpen",         "TipOpen"),
        ["save"]                = ("ItemSave",         "TipSave"),
        ["save-all"]            = ("ItemSaveAll",      "TipSaveAll"),
        ["paste"]               = ("ItemPaste",        "TipPaste"),
        ["copy"]                = ("ItemCopy",         "TipCopy"),
        ["cut"]                 = ("ItemCut",          "TipCut"),
        ["undo"]                = ("ItemUndo",         "TipUndo"),
        ["redo"]                = ("ItemRedo",         "TipRedo"),
        ["find"]                = ("ItemFind",         "TipFind"),
        ["replace"]             = ("ItemReplace",      "TipReplace"),
        ["goto"]                = ("ItemGoTo",         "TipGoTo"),
        ["comment"]             = ("ItemComment",      "TipComment"),
        ["uncomment"]           = ("ItemUncomment",    "TipUncomment"),
        ["toggle-explorer"]     = ("ItemExplorer",     "TipExplorer"),
        ["toggle-problems"]     = ("ItemProblems",     "TipProblems"),
        ["toggle-terminal"]     = ("ItemTerminal",     "TipTerminal"),
        ["layout-new"]          = ("ItemNewLayout",    "TipNewLayout"),
        ["layout-open"]         = ("ItemOpenLayout",   "TipOpenLayout"),
        ["layout-save"]         = ("ItemSaveLayout",   "TipSaveLayout"),
        ["layout-close"]        = ("ItemClose",        "TipCloseLayout"),
        ["layout-show-windows"] = ("ItemShowWindows",  "TipShowWindows"),
        ["layout-exit-windows"] = ("ItemExitWindows",  "TipExitWindows"),
        ["theme-preset"]        = ("ItemTheme",        ""),
        ["lang-select"]         = ("ItemLanguage",     "TipLanguage"),
        ["build"]               = ("ItemBuild",        "TipBuild"),
        ["rebuild"]             = ("ItemRebuild",      "TipRebuild"),
        ["clean"]               = ("ItemClean",        "TipClean"),
        ["start-debug"]         = ("ItemStart",        "TipStart"),
        ["stop-debug"]          = ("ItemStop",         "TipStop"),
        ["restart-debug"]       = ("ItemRestart",      "TipRestart"),
        ["about"]               = ("ItemAbout",        "TipAbout"),
    };

    private static string TabKey(string tabId) =>
        _tabKeys.TryGetValue(tabId, out var k) ? k : tabId;

    private static string GroupKey(string groupId) =>
        _groupKeys.TryGetValue(groupId, out var k) ? k : groupId;

    private static string? ItemLabelKey(string itemId) =>
        _itemKeys.TryGetValue(itemId, out var k) ? k.Label : null;

    private static string? ItemTipKey(string itemId) =>
        _itemKeys.TryGetValue(itemId, out var k) && k.Tip.Length > 0 ? k.Tip : null;
}
