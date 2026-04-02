using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.ViewModels.Tools;
using System.Windows.Input;

namespace AI_IDE_Avalonia.Views.Tools;

public partial class SolutionExplorerView : UserControl
{
    public SolutionExplorerView()
    {
        InitializeComponent();

        var tree = this.FindControl<TreeView>("TreeView");
        if (tree is null) return;

        // Round-trip TreeViewItem.IsExpanded changes back to TreeNode.IsExpanded so that
        // the ViewModel's lazy-loading observer fires when the user clicks the expand chevron.
        tree.AddHandler(TreeViewItem.ExpandedEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (e.Source is TreeViewItem { DataContext: TreeNode node })
                node.IsExpanded = true;
        });

        tree.AddHandler(TreeViewItem.CollapsedEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (e.Source is TreeViewItem { DataContext: TreeNode node })
                node.IsExpanded = false;
        });

        tree.AddHandler(
            InputElement.PointerReleasedEvent,
            OnTreePointerReleased,
            RoutingStrategies.Bubble);

        tree.AddHandler(
            Gestures.DoubleTappedEvent,
            OnTreeDoubleTapped,
            RoutingStrategies.Bubble);

        // Wire the Avalonia clipboard service once the view enters the visual tree.
        AttachedToVisualTree += (_, _) => TryWireClipboard();
        DataContextChanged    += (_, _) => TryWireClipboard();
    }

    private void TryWireClipboard()
    {
        if (DataContext is SolutionExplorerViewModel vm
            && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            vm.SystemClipboard = clipboard;
        }
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right) return;
        if (DataContext is not SolutionExplorerViewModel vm) return;

        var node = FindTreeNode(e.Source as Visual);
        if (node is null) return;

        var menu = BuildContextMenu(vm, node);
        menu.Open(sender as Control);
        e.Handled = true;
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not SolutionExplorerViewModel vm) return;

        var node = FindTreeNode(e.Source as Visual);
        if (node is null || node.IsFolder) return;

        vm.OpenNodeCommand.Execute(node);
        e.Handled = true;
    }

    private static TreeNode? FindTreeNode(Visual? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is TreeViewItem { DataContext: TreeNode n } && !n.IsLoadingPlaceholder)
                return n;
            current = current.GetVisualParent();
        }
        return null;
    }

    private static ContextMenu BuildContextMenu(SolutionExplorerViewModel vm, TreeNode node)
    {
        var menu = new ContextMenu();

        // ── Group 1: Open ─────────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Open",                    "Ctrl+O", IconFile,   vm.OpenNodeCommand,           node));
        menu.Items.Add(MakeItem("Open Containing Folder",  null,     IconFolder, vm.OpenContainingFolderCommand, node));
        menu.Items.Add(new Separator());

        // ── Group 2: Add (folders only) ───────────────────────────────────────
        if (node.IsFolder)
        {
            var addItem = new MenuItem { Header = "Add", Icon = MakeIcon(IconPlus) };
            addItem.Items.Add(MakeItem("New File...",           "Ctrl+N", IconFile,   vm.AddNewFileCommand,      node));
            addItem.Items.Add(MakeItem("New Folder...",         null,     IconFolder, vm.AddNewFolderCommand,    node));
            addItem.Items.Add(new Separator());
            addItem.Items.Add(MakeItem("Add Existing File...",  null,     IconFile,   vm.AddExistingFileCommand, node));
            menu.Items.Add(addItem);
            menu.Items.Add(new Separator());
        }

        // ── Group 3: Edit ─────────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Cut",   "Ctrl+X", IconCut,   vm.CutNodeCommand,   node));
        menu.Items.Add(MakeItem("Copy",  "Ctrl+C", IconCopy,  vm.CopyNodeCommand,  node));
        menu.Items.Add(MakeItem("Paste", "Ctrl+V", IconPaste, vm.PasteNodeCommand, node));
        menu.Items.Add(new Separator());

        // ── Group 4: Node operations ──────────────────────────────────────────
        menu.Items.Add(MakeItem("Rename", "F2",     null,       vm.RenameNodeCommand, node));
        menu.Items.Add(MakeItem("Delete", "Delete", IconDelete, vm.RemoveNodeCommand, node));
        menu.Items.Add(new Separator());

        // ── Group 5: Clipboard ────────────────────────────────────────────────
        menu.Items.Add(MakeItem("Copy Full Path", null, null, vm.CopyFullPathCommand, node));
        menu.Items.Add(new Separator());

        // ── Group 6: Properties ───────────────────────────────────────────────
        menu.Items.Add(MakeItem("Properties...", "Alt+Enter", IconProperties, vm.ShowPropertiesCommand, node));

        return menu;
    }

    private static MenuItem MakeItem(
        string header, string? gesture, string? iconPath,
        ICommand command, TreeNode node)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = node,
        };
        if (gesture is not null)
            item.InputGesture = KeyGesture.Parse(gesture);
        if (iconPath is not null)
            item.Icon = MakeIcon(iconPath);
        return item;
    }

    private static PathIcon MakeIcon(string data) =>
        new() { Width = 14, Height = 14, Data = Geometry.Parse(data) };

    // ── Icon path data (nominally 16 × 16 canvas, filled shapes) ─────────────

    private const string IconFile =
        "M3 1h7l4 4v10H3V1zM10 1l4 4h-4z";

    private const string IconFolder =
        "M2 6C2 4.895 2.895 4 4 4H7.172C7.702 4 8.21 4.21 8.586 4.586L9.414 5.414C9.789 5.789 10.298 6 10.828 6H12C13.105 6 14 6.895 14 8V12C14 13.105 13.105 14 12 14H4C2.895 14 2 13.105 2 12V6Z";

    private const string IconPlus =
        "M7 2h2v5h5v2H9v5H7V9H2V7h5z";

    private const string IconCut =
        "M4 3C2.9 3 2 3.9 2 5S2.9 7 4 7 6 6.1 6 5 5.1 3 4 3Z" +
        "M4 9C2.9 9 2 9.9 2 11S2.9 13 4 13 6 12.1 6 11 5.1 9 4 9Z" +
        "M5.8 5.5L14.5 2.5V4L7 6.5Z" +
        "M5.8 10.5L14.5 13.5V12L7 9.5Z";

    private const string IconCopy =
        "M4 1h6l3 3v9H4V1zM10 1l3 3h-3zM2 3H1v10h7v-1H2z";

    private const string IconPaste =
        "M4 4h8v10H4V4zM6 1h4v3H6V1z";

    private const string IconDelete =
        "M2 4h12v1H2V4z" +
        "M5 4V2h6v2H5z" +
        "M4.5 5.5l1 8h5l1-8z";

    private const string IconProperties =
        "M2 3h12v2H2zM2 8h12v2H2zM2 13h8v2H2z";

    // ── Toolbar button handlers ───────────────────────────────────────────────

    private void OnExpandAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SolutionExplorerViewModel vm)
            foreach (var node in vm.FilteredNodes)
                SetNodeExpansion(node, expanded: true);
    }

    private void OnCollapseAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SolutionExplorerViewModel vm)
            foreach (var node in vm.FilteredNodes)
                SetNodeExpansion(node, expanded: false);
    }

    /// <summary>
    /// Recursively sets <see cref="TreeNode.IsExpanded"/> on <paramref name="node"/> and all of
    /// its loaded descendants.  Because <see cref="SolutionExplorerView.axaml"/> binds
    /// <c>TreeViewItem.IsExpanded</c> to <c>TreeNode.IsExpanded</c>, the visual state updates
    /// automatically.  Expanding an unloaded folder also triggers lazy child loading via the
    /// <c>PropertyChanged</c> observer in the ViewModel.
    /// </summary>
    private static void SetNodeExpansion(TreeNode node, bool expanded)
    {
        if (!node.IsFolder || node.IsLoadingPlaceholder) return;

        node.IsExpanded = expanded;

        if (node.Children is null) return;
        foreach (var child in node.Children)
            SetNodeExpansion(child, expanded);
    }
}
