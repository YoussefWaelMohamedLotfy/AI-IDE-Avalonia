using System;
using System.Collections.ObjectModel;
using AI_IDE_Avalonia.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels.Tools;

public partial class Tool1ViewModel : Tool
{
    private readonly ObservableCollection<TreeNode> _allNodes = TreeNode.CreateSampleProject();

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<TreeNode> FilteredNodes { get; } = new();
    public ObservableCollection<TreeNode> SelectedNodes { get; } = new();

    public Tool1ViewModel() => ApplyFilter();

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredNodes.Clear();
        var filter = FilterText.Trim();

        foreach (var node in _allNodes)
        {
            var result = string.IsNullOrEmpty(filter)
                ? node
                : FilterNode(node, filter);

            if (result is not null)
                FilteredNodes.Add(result);
        }
    }

    private static TreeNode? FilterNode(TreeNode node, string filter)
    {
        bool nameMatches = node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (node.Children is null || node.Children.Count == 0)
            return nameMatches ? node : null;

        var matchingChildren = new ObservableCollection<TreeNode>();
        foreach (var child in node.Children)
        {
            var filtered = FilterNode(child, filter);
            if (filtered is not null)
                matchingChildren.Add(filtered);
        }

        if (matchingChildren.Count > 0)
            return new TreeNode(node.Name, node.IsFolder, matchingChildren);

        return nameMatches ? new TreeNode(node.Name, node.IsFolder) : null;
    }
}
