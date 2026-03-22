using System;
using System.Collections.Generic;
using AI_IDE_Avalonia.Models.Documents;
using AI_IDE_Avalonia.Models.Tools;
using AI_IDE_Avalonia.ViewModels.Docks;
using AI_IDE_Avalonia.ViewModels.Documents;
using AI_IDE_Avalonia.ViewModels.Tools;
using AI_IDE_Avalonia.ViewModels.Views;
using Dock.Avalonia.Controls;
using Dock.Settings;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels;

public class DockFactory : Factory
{
    private readonly object _context;
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;

    public DockFactory(object context)
    {
        _context = context;
    }

    public override IDocumentDock CreateDocumentDock() => new CustomDocumentDock();

    public override IRootDock CreateLayout()
    {
        var document1 = new DocumentViewModel {Id = "Document1", Title = "Document1"};
        var tool1 = new Tool1ViewModel {Id = "Tool1", Title = "Tool1", KeepPinnedDockableVisible = true};

        // Expose the tree to the AI agent so it can use the tree-node tools.
        DocumentViewModel.SharedTool1 = tool1;
        var tool2 = new Tool2ViewModel {Id = "Tool2", Title = "Tool2", KeepPinnedDockableVisible = true};
        var tool3 = new Tool3ViewModel {Id = "Tool3", Title = "Tool3", CanDrag = false };
        var tool4 = new Tool4ViewModel {Id = "Tool4", Title = "Tool4", CanDrag = false };

        var leftDock = new ProportionalDock
        {
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>
            (
                new ToolDock
                {
                    ActiveDockable = tool1,
                    VisibleDockables = CreateList<IDockable>(tool1, tool2),
                    Alignment = Alignment.Left,
                    // CanDrop = false
                },
                new ProportionalDockSplitter { CanResize = true, ResizePreview = true },
                new ToolDock
                {
                    ActiveDockable = tool3,
                    VisibleDockables = CreateList<IDockable>(tool3, tool4),
                    Alignment = Alignment.Bottom,
                    CanDrag = false,
                    CanDrop = false
                }
            ),
            // CanDrop = false
        };

        var rightDock = new ProportionalDock
        {
            // DockGroup = "RightDock",
            Proportion = 0.25,
            // MinWidth = 200,
            // MaxWidth = 400,
            Orientation = Orientation.Vertical,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>
            (
            ),
            // CanDrop = false
        };

        var documentDock = new CustomDocumentDock
        {
            // DockGroup = "CustomDocumentDock",
            IsCollapsable = false,
            ActiveDockable = document1,
            VisibleDockables = CreateList<IDockable>(document1),
            CanCreateDocument = true,
            // CanDrop = false,
            EnableWindowDrag = true,
            // CanCloseLastDockable = false,
        };

        var mainLayout = new ProportionalDock
        {
            // EnableGlobalDocking = false,
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>
            (
                leftDock,
                new ProportionalDockSplitter { ResizePreview = true },
                documentDock,
                new ProportionalDockSplitter(),
                rightDock
            )
        };

        var dashboardView = new DashboardViewModel
        {
            Id = "Dashboard",
            Title = "Dashboard"
        };

        var homeView = new HomeViewModel
        {
            Id = "Home",
            Title = "Home",
            ActiveDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout)
        };

        var rootDock = CreateRootDock();

        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = dashboardView;
        rootDock.DefaultDockable = homeView;
        rootDock.VisibleDockables = CreateList<IDockable>(dashboardView, homeView);

        rootDock.LeftPinnedDockables = CreateList<IDockable>();
        rootDock.RightPinnedDockables = CreateList<IDockable>();
        rootDock.TopPinnedDockables = CreateList<IDockable>();
        rootDock.BottomPinnedDockables = CreateList<IDockable>();

        rootDock.PinnedDock = null;

        _documentDock = documentDock;
        _rootDock = rootDock;
            
        return rootDock;
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);

        if (window != null)
        {
            window.Title = "Dock Avalonia Demo";
        }
        return window;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["Document1"] = () => new DemoDocument(),
            ["Tool1"] = () => new Tool1(),
            ["Tool2"] = () => new Tool2(),
            ["Tool3"] = () => new Tool3(),
            ["Tool4"] = () => new Tool4(),
            ["Dashboard"] = () => layout,
            ["Home"] = () => _context
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>()
        {
            ["Root"] = () => _rootDock,
            ["Documents"] = () => _documentDock
        };

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => DockSettings.UseManagedWindows ? new ManagedHostWindow() : new HostWindow()
        };

        base.InitLayout(layout);
    }
}
