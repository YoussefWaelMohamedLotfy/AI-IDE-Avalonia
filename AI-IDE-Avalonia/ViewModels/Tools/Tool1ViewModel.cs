using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using AI_IDE_Avalonia.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels.Tools;

public partial class Tool1ViewModel : Tool
{
    private const int MaxTreeDepth = 10;

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

    // ── AI-callable tools ──────────────────────────────────────────────────────

    // ── Workspace loading ──────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the current tree with the real filesystem content under
    /// <paramref name="folderPath"/>.  Folders are shown before files and
    /// both are sorted alphabetically.  Hidden and inaccessible paths are
    /// skipped silently.
    /// </summary>
    public void LoadWorkspace(string folderPath)
    {
        var di = new DirectoryInfo(folderPath);
        if (!di.Exists) return;

        _allNodes.Clear();

        var rootNode = BuildDirectoryNode(di, maxDepth: MaxTreeDepth, currentDepth: 0);
        if (rootNode is not null)
            _allNodes.Add(rootNode);

        ApplyFilter();
    }

    private static TreeNode? BuildDirectoryNode(DirectoryInfo dir, int maxDepth, int currentDepth)
    {
        if (currentDepth > maxDepth)
            return null;

        var children = new ObservableCollection<TreeNode>();

        try
        {
            foreach (var subDir in dir.GetDirectories()
                                      .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                      .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                var child = BuildDirectoryNode(subDir, maxDepth, currentDepth + 1);
                if (child is not null)
                    children.Add(child);
            }

            foreach (var file in dir.GetFiles()
                                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                children.Add(new TreeNode(file.Name, isFolder: false));
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible paths */ }

        return new TreeNode(dir.Name, isFolder: true, children: children);
    }

    /// <summary>Search the project tree for nodes whose name contains <paramref name="query"/>.</summary>
    public string SearchNodes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Query must not be empty.";

        var matches = new List<string>();
        foreach (var root in _allNodes)
            CollectMatchingPaths(root, query, root.Name, matches);

        if (matches.Count == 0)
            return $"No nodes matching \"{query}\" found.";

        var sb = new StringBuilder($"Found {matches.Count} node(s) matching \"{query}\":\n");
        foreach (var path in matches)
            sb.AppendLine($"  - {path}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Add a new node under the node at <paramref name="parentPath"/>.
    /// Use an empty string for <paramref name="parentPath"/> to add at the root level.
    /// </summary>
    public string AddNode(string parentPath, string nodeName, bool isFolder)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            return "Node name must not be empty.";

        if (string.IsNullOrWhiteSpace(parentPath))
        {
            _allNodes.Add(new TreeNode(nodeName, isFolder));
            ApplyFilter();
            return $"Added {(isFolder ? "folder" : "file")} '{nodeName}' at the root level.";
        }

        var parent = FindNode(_allNodes, parentPath.Split('/'));
        if (parent is null)
            return $"Parent path '{parentPath}' not found.";

        if (!parent.IsFolder)
            return $"'{parentPath}' is a file, not a folder. Cannot add children to it.";

        parent.Children!.Add(new TreeNode(nodeName, isFolder));
        ApplyFilter();
        return $"Added {(isFolder ? "folder" : "file")} '{nodeName}' under '{parentPath}'.";
    }

    /// <summary>Delete the node at <paramref name="nodePath"/> (e.g. "MyAIProject/src/Agents/ChatAgent.cs").</summary>
    public string DeleteNode(string nodePath)
    {
        if (string.IsNullOrWhiteSpace(nodePath))
            return "Node path must not be empty.";

        var parts = nodePath.Split('/');

        // Top-level deletion
        if (parts.Length == 1)
        {
            var root = _allNodes.FirstOrDefault(n => n.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
            if (root is null) return $"Node '{nodePath}' not found.";
            _allNodes.Remove(root);
            ApplyFilter();
            return $"Deleted '{nodePath}'.";
        }

        // Navigate to the parent of the node to delete
        var parentPath = string.Join('/', parts[..^1]);
        var parent = FindNode(_allNodes, parts[..^1]);
        if (parent is null)
            return $"Parent path '{parentPath}' not found.";

        var nodeName = parts[^1];
        var target = parent.Children?.FirstOrDefault(n => n.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return $"Node '{nodePath}' not found.";

        parent.Children!.Remove(target);
        ApplyFilter();
        return $"Deleted '{nodePath}'.";
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static void CollectMatchingPaths(TreeNode node, string query, string currentPath, List<string> results)
    {
        if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            results.Add(currentPath);

        if (node.Children is null) return;
        foreach (var child in node.Children)
            CollectMatchingPaths(child, query, $"{currentPath}/{child.Name}", results);
    }

    private static TreeNode? FindNode(IEnumerable<TreeNode> nodes, string[] pathParts)
    {
        var current = nodes.FirstOrDefault(n => n.Name.Equals(pathParts[0], StringComparison.OrdinalIgnoreCase));
        if (current is null) return null;
        if (pathParts.Length == 1) return current;
        if (current.Children is null) return null;
        return FindNode(current.Children, pathParts[1..]);
    }
}
