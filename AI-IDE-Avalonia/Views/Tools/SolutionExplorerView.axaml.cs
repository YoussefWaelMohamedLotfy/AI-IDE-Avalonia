using Avalonia.Controls;
using Avalonia.Interactivity;
using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.ViewModels.Tools;

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
    }

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
