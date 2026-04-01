using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI_IDE_Avalonia.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels.Tools;

public partial class SolutionExplorerViewModel : Tool
{
    // Root nodes (one per open workspace root).
    private readonly ObservableCollection<TreeNode> _allNodes = TreeNode.CreateSampleProject();

    [ObservableProperty]
    private string _filterText = string.Empty;

    public ObservableCollection<TreeNode> FilteredNodes { get; } = new();
    public ObservableCollection<TreeNode> SelectedNodes { get; } = new();

    public SolutionExplorerViewModel()
    {
        // Observe the sample-data nodes so expand/collapse works for design-time preview.
        foreach (var node in _allNodes)
            ObserveNode(node);

        ApplyFilter();
    }

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
        // Never surface internal placeholder nodes to the filtered view.
        if (node.IsLoadingPlaceholder) return null;

        bool nameMatches = node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (node.Children is null || node.Children.Count == 0)
            return nameMatches ? node : null;

        var matchingChildren = new ObservableCollection<TreeNode>();
        foreach (var child in node.Children)
        {
            if (child.IsLoadingPlaceholder) continue;
            var filtered = FilterNode(child, filter);
            if (filtered is not null)
                matchingChildren.Add(filtered);
        }

        if (matchingChildren.Count > 0)
            return new TreeNode(node.Name, node.IsFolder, matchingChildren);

        return nameMatches ? new TreeNode(node.Name, node.IsFolder) : null;
    }

    // ── Workspace loading ──────────────────────────────────────────────────────

    /// <summary>
    /// Asynchronously replaces the current tree with the real filesystem content under
    /// <paramref name="folderPath"/>, reporting status via <paramref name="progress"/>.
    /// Only the first level of the directory tree is built eagerly; sub-folders are loaded
    /// on demand when the user expands them (<see cref="LoadNodeChildrenAsync"/>).
    /// </summary>
    public async Task LoadWorkspaceAsync(
        string folderPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var di = new DirectoryInfo(folderPath);
        if (!di.Exists) return;

        progress?.Report($"Opening workspace: {di.Name}");
        progress?.Report("Scanning top-level directory\u2026");

        // Build the root node shallowly on a thread-pool thread so the UI stays responsive.
        var rootNode = await Task.Run(
            () => BuildShallowNode(di),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report("Building file tree\u2026");

        // Collection mutations must occur on the UI thread (ObservableCollection is not thread-safe).
        _allNodes.Clear();
        if (rootNode is not null)
        {
            rootNode.IsExpanded = true; // expand the workspace root by default
            ObserveNode(rootNode);
            _allNodes.Add(rootNode);
        }

        ApplyFilter();

        progress?.Report("Workspace loaded successfully.");
    }

    /// <summary>
    /// Synchronous wrapper — delegates to <see cref="LoadWorkspaceAsync"/> and waits.
    /// Prefer calling the async overload directly from async callers.
    /// </summary>
    public void LoadWorkspace(string folderPath) =>
        LoadWorkspaceAsync(folderPath).GetAwaiter().GetResult();

    // ── Shallow tree builder ───────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="dir"/> one level deep and returns a <see cref="TreeNode"/> whose
    /// children are either real file nodes or folder nodes that each carry a single
    /// <see cref="TreeNode.LoadingPlaceholder"/> child (so the TreeView expand chevron appears).
    /// Sub-directory contents are not scanned here — they are loaded lazily on first expand.
    /// </summary>
    private static TreeNode? BuildShallowNode(DirectoryInfo dir)
    {
        var children = new ObservableCollection<TreeNode>();

        try
        {
            foreach (var subDir in dir.GetDirectories()
                                      .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                      .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                // The placeholder child makes the expand arrow visible without scanning the sub-dir.
                var folderNode = new TreeNode(
                    subDir.Name,
                    isFolder: true,
                    children: new ObservableCollection<TreeNode> { TreeNode.LoadingPlaceholder },
                    fullPath: subDir.FullName,
                    childrenLoaded: false);

                children.Add(folderNode);
            }

            foreach (var file in dir.GetFiles()
                                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                children.Add(new TreeNode(file.Name, isFolder: false, fullPath: file.FullName));
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible paths */ }

        // Root (or any level-0 caller): direct children are considered loaded at this level.
        return new TreeNode(
            dir.Name,
            isFolder: true,
            children: children,
            fullPath: dir.FullName,
            childrenLoaded: true);
    }

    // ── Lazy loading ───────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribes to <see cref="TreeNode.IsExpanded"/> changes on <paramref name="node"/> and
    /// all of its currently-loaded descendants, so that expanding a folder triggers an async
    /// child load when its children have not yet been fetched.
    /// </summary>
    private void ObserveNode(TreeNode node)
    {
        if (!node.IsFolder) return;

        node.PropertyChanged += OnNodePropertyChanged;

        if (node.Children is null) return;
        foreach (var child in node.Children)
            ObserveNode(child);
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TreeNode.IsExpanded)) return;
        if (sender is not TreeNode node) return;
        if (!node.IsExpanded || node.ChildrenLoaded) return;

        // Fire-and-forget: exceptions are swallowed inside LoadNodeChildrenAsync.
        _ = LoadNodeChildrenAsync(node);
    }

    /// <summary>
    /// Loads the immediate children of <paramref name="node"/> from disk and replaces its
    /// placeholder child with the real entries.  Runs the filesystem scan on the thread pool
    /// and marshals collection updates back to the UI thread via the captured sync context.
    /// </summary>
    private async Task LoadNodeChildrenAsync(TreeNode node)
    {
        if (node.FullPath is null || node.ChildrenLoaded) return;

        // Mark immediately to prevent concurrent re-entry for the same node.
        node.ChildrenLoaded = true;
        node.IsLoading = true;

        try
        {
            var dir = new DirectoryInfo(node.FullPath);
            if (!dir.Exists)
            {
                node.Children?.Clear();
                return;
            }

            // Scan the directory on the thread pool so the UI stays responsive.
            var (subDirs, files) = await Task.Run(() =>
            {
                DirectoryInfo[] dirs;
                FileInfo[] fls;
                try
                {
                    dirs = dir.GetDirectories()
                              .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                              .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                              .ToArray();
                    fls = dir.GetFiles()
                             .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                             .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                             .ToArray();
                }
                catch (UnauthorizedAccessException)
                {
                    dirs = [];
                    fls = [];
                }
                return (dirs, fls);
            });

            // Replace the placeholder with real children.
            // This continuation runs on the UI thread because LoadNodeChildrenAsync is always
            // invoked from an event handler that fires on the UI thread.
            node.Children!.Clear();

            foreach (var subDir in subDirs)
            {
                var child = new TreeNode(
                    subDir.Name,
                    isFolder: true,
                    children: new ObservableCollection<TreeNode> { TreeNode.LoadingPlaceholder },
                    fullPath: subDir.FullName,
                    childrenLoaded: false);

                ObserveNode(child);
                node.Children.Add(child);
            }

            foreach (var file in files)
                node.Children.Add(new TreeNode(file.Name, isFolder: false, fullPath: file.FullName));

            // Refresh the filter so newly-loaded nodes appear in filtered results.
            if (!string.IsNullOrWhiteSpace(FilterText))
                ApplyFilter();
        }
        catch
        {
            // On failure, reset so the user can retry by collapsing and re-expanding.
            node.ChildrenLoaded = false;
            node.Children?.Clear();
            node.Children?.Add(TreeNode.LoadingPlaceholder);
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    // ── AI-callable tools ──────────────────────────────────────────────────────

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
        if (node.IsLoadingPlaceholder) return;

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
