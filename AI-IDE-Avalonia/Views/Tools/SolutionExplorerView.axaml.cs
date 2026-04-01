using System;
using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AI_IDE_Avalonia.Views.Tools;

public partial class SolutionExplorerView : UserControl
{
    public SolutionExplorerView()
    {
        InitializeComponent();

        var tree = this.FindControl<TreeView>("TreeView");
        if (tree is null) return;

        tree.AddHandler(TreeViewItem.ExpandedEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (e.Source is TreeViewItem { DataContext: Models.TreeNode node })
                Debug.WriteLine($"[Expanded]  {node.Name}");
        });

        tree.AddHandler(TreeViewItem.CollapsedEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (e.Source is TreeViewItem { DataContext: Models.TreeNode node })
                Debug.WriteLine($"[Collapsed] {node.Name}");
        });
    }

    private void OnExpandAll(object? sender, RoutedEventArgs e)
    {
        var tree = this.FindControl<TreeView>("TreeView");
        if (tree is not null) SetExpansion(tree, true);
    }

    private void OnCollapseAll(object? sender, RoutedEventArgs e)
    {
        var tree = this.FindControl<TreeView>("TreeView");
        if (tree is not null) SetExpansion(tree, false);
    }

    private static void SetExpansion(ItemsControl parent, bool expanded)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ContainerFromItem(item) is not TreeViewItem container) continue;
            container.IsExpanded = expanded;
            SetExpansion(container, expanded);
        }
    }
}
