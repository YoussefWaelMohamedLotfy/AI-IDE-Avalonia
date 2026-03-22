using System;
using System.Diagnostics;

using Avalonia.Controls;

namespace AI_IDE_Avalonia.Views.Tools;

public partial class Tool1View : UserControl
{
    public Tool1View()
    {
        InitializeComponent();

        var tree = this.FindControl<TreeView>("TreeView");
        if (tree is null) return;

        tree.AddHandler(TreeViewItem.ExpandedEvent, (object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        {
            if (e.Source is TreeViewItem { DataContext: Models.TreeNode node })
                Debug.WriteLine($"[Expanded]  {node.Name}");
        });

        tree.AddHandler(TreeViewItem.CollapsedEvent, (object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        {
            if (e.Source is TreeViewItem { DataContext: Models.TreeNode node })
                Debug.WriteLine($"[Collapsed] {node.Name}");
        });
    }
}
