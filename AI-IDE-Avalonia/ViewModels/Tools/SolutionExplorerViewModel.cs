using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI_IDE_Avalonia.Collections;
using AI_IDE_Avalonia.Models;
using AI_IDE_Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace AI_IDE_Avalonia.ViewModels.Tools;

public partial class SolutionExplorerViewModel : Tool, IDisposable
{
    // Root nodes (one per open workspace root).
    private readonly ObservableCollection<TreeNode> _allNodes = TreeNode.CreateSampleProject();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private int _totalNodeCount;

    public int SelectedNodeCount => SelectedNodes.Count;

    /// <summary>True when more than one node is selected; drives the selected-count label visibility.</summary>
    public bool HasMultipleSelected => SelectedNodes.Count > 1;

    public BulkObservableCollection<TreeNode> FilteredNodes { get; } = new();
    public ObservableCollection<TreeNode> SelectedNodes { get; } = new();

    public SolutionExplorerViewModel()
    {
        // Observe the sample-data nodes so expand/collapse works for design-time preview.
        foreach (var node in _allNodes)
            ObserveNode(node);

        SelectedNodes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedNodeCount));
            OnPropertyChanged(nameof(HasMultipleSelected));
        };

        ApplyFilter();
    }

    // Cancels any in-progress async filter when the user types a new query.
    private CancellationTokenSource? _filterCts;

    // ── File-system watching ───────────────────────────────────────────────────

    private FileSystemWatcherService? _fsWatcher;
    private IDisposable? _fsCreatedSub;
    private IDisposable? _fsDeletedSub;
    private IDisposable? _fsRenamedSub;

    // Debounce delay: wait this long after the last keystroke before searching.
    private static readonly TimeSpan FilterDebounce = TimeSpan.FromMilliseconds(300);

    partial void OnFilterTextChanged(string value) => ScheduleFilter(debounce: true);

    // Kept so existing internal callers (workspace load, node add/delete) don't need changing.
    private void ApplyFilter() => ScheduleFilter(debounce: false);

    private void ScheduleFilter(bool debounce = false)
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        _filterCts = new CancellationTokenSource();
        _ = ApplyFilterAsync(_filterCts.Token, debounce);
    }

    private async Task ApplyFilterAsync(CancellationToken ct, bool debounce = false)
    {
        // Wait for the user to stop typing before hitting the filesystem.
        if (debounce)
        {
            try { await Task.Delay(FilterDebounce, ct); }
            catch (OperationCanceledException) { return; }
        }

        var filter = FilterText.Trim();

        if (string.IsNullOrEmpty(filter))
        {
            FilteredNodes.Reset(_allNodes);
            TotalNodeCount = CountNodes(_allNodes);
            return;
        }

        // Snapshot _allNodes on the UI thread before handing off to the thread pool.
        var roots = _allNodes.ToList();

        List<TreeNode> results;
        try
        {
            results = await Task.Run(() =>
            {
                var list = new List<TreeNode>();
                foreach (var n in roots)
                {
                    ct.ThrowIfCancellationRequested();
                    var r = FilterNodeDeep(n, filter, ct);
                    if (r is not null) list.Add(r);
                }
                return list;
            }, ct);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a newer filter run — leave FilteredNodes as-is
        }

        // One final check: if the token fired between Task.Run completing and this line,
        // discard the stale results so the next scheduled run takes over cleanly.
        if (ct.IsCancellationRequested) return;

        // Expand all result nodes, then push the whole list to the UI in one Reset
        // to fire a single CollectionChanged notification instead of one per item.
        foreach (var node in results)
            ExpandAll(node);

        FilteredNodes.Reset(results);
        TotalNodeCount = CountNodes(results);
    }

    /// <summary>Recursively expands all folder nodes in a filter-result subtree.</summary>
    private static void ExpandAll(TreeNode node)
    {
        if (!node.IsFolder || node.Children is not { Count: > 0 }) return;
        node.IsExpanded = true;
        foreach (var child in node.Children)
            ExpandAll(child);
    }

    /// <summary>
    /// Recursively filters <paramref name="node"/> against <paramref name="filter"/>.
    /// For folder nodes whose children have not yet been loaded into memory, the filesystem
    /// is searched directly so that the filter always scans the full tree depth.
    /// </summary>
    private static TreeNode? FilterNodeDeep(TreeNode node, string filter, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (node.IsLoadingPlaceholder) return null;

        bool nameMatches = node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (!node.IsFolder)
            return nameMatches ? node : null;

        // ── Loaded folder: recurse through in-memory children ─────────────────
        if (node.ChildrenLoaded && node.Children is { Count: > 0 })
        {
            // Snapshot to avoid races with LoadNodeChildrenAsync running on the UI thread.
            var childSnapshot = node.Children.Where(c => !c.IsLoadingPlaceholder).ToList();
            var matching = new ObservableCollection<TreeNode>();
            foreach (var child in childSnapshot)
            {
                var r = FilterNodeDeep(child, filter, ct);
                if (r is not null)
                    matching.Add(r);
            }

            if (matching.Count > 0)
                return new TreeNode(node.Name, isFolder: true, children: matching,
                    fullPath: node.FullPath, childrenLoaded: true);
            return nameMatches
                ? new TreeNode(node.Name, isFolder: true, fullPath: node.FullPath, childrenLoaded: true)
                : null;
        }

        // ── Unloaded folder: search the filesystem directly ───────────────────
        if (node.FullPath is not null)
        {
            var fsMatches = SearchFilesystemDeep(node.FullPath, filter, ct);
            if (fsMatches.Count > 0)
                return new TreeNode(node.Name, isFolder: true, children: fsMatches,
                    fullPath: node.FullPath, childrenLoaded: false);
            return nameMatches
                ? new TreeNode(node.Name, isFolder: true, fullPath: node.FullPath, childrenLoaded: false)
                : null;
        }

        return nameMatches ? new TreeNode(node.Name, isFolder: true) : null;
    }

    /// <summary>
    /// Recursively walks the directory at <paramref name="dirPath"/> and returns
    /// a collection of <see cref="TreeNode"/> entries whose name (or a descendant's name)
    /// contains <paramref name="filter"/> (case-insensitive).
    /// </summary>
    private static ObservableCollection<TreeNode> SearchFilesystemDeep(
        string dirPath, string filter, CancellationToken ct)
    {
        var results = new ObservableCollection<TreeNode>();
        try
        {
            var dir = new DirectoryInfo(dirPath);
            if (!dir.Exists) return results;

            foreach (var subDir in dir.GetDirectories()
                                       .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                       .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                bool matches = subDir.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
                var children = SearchFilesystemDeep(subDir.FullName, filter, ct);

                if (matches || children.Count > 0)
                {
                    results.Add(new TreeNode(
                        subDir.Name,
                        isFolder: true,
                        children: children.Count > 0 ? children : null,
                        fullPath: subDir.FullName,
                        childrenLoaded: false));
                }
            }

            foreach (var file in dir.GetFiles()
                                     .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                     .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (file.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    results.Add(new TreeNode(file.Name, isFolder: false, fullPath: file.FullName));
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible directories */ }
        catch (IOException) { /* skip unreadable paths */ }
        return results;
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

        // ── Start watching for external filesystem changes ────────────────────
        StartWatchingWorkspace(folderPath);
    }

    // ── Synchronous wrapper ────────────────────────────────────────────────────

    /// <summary>
    /// Synchronous wrapper — delegates to <see cref="LoadWorkspaceAsync"/> and waits.
    /// Prefer calling the async overload directly from async callers.
    /// </summary>
    public void LoadWorkspace(string folderPath) =>
        LoadWorkspaceAsync(folderPath).GetAwaiter().GetResult();

    // ── File-system watcher lifecycle ──────────────────────────────────────────

    /// <summary>
    /// Starts (or restarts) the <see cref="FileSystemWatcherService"/> for
    /// <paramref name="rootPath"/> and subscribes to created/deleted/renamed events so that
    /// the in-memory tree stays in sync with the filesystem.
    /// Also notifies <see cref="DocumentService"/> so open documents can watch their files.
    /// </summary>
    private void StartWatchingWorkspace(string rootPath)
    {
        // Tear down any previous watcher.
        _fsCreatedSub?.Dispose();
        _fsDeletedSub?.Dispose();
        _fsRenamedSub?.Dispose();
        _fsWatcher?.Dispose();
        _fsWatcher = null;

        try
        {
            _fsWatcher = new FileSystemWatcherService(rootPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SolutionExplorer] Could not start watcher for '{rootPath}': {ex.Message}");
        }

        // Also tell DocumentService so newly-opened documents can track their files.
        DocumentService.Instance.SetWorkspaceWatcher(rootPath);

        if (_fsWatcher is null) return;

        _fsCreatedSub = _fsWatcher.Created
            .Subscribe(e => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => OnFsCreated(e)));

        _fsDeletedSub = _fsWatcher.Deleted
            .Subscribe(e => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => OnFsDeleted(e)));

        _fsRenamedSub = _fsWatcher.Renamed
            .Subscribe(e => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => OnFsRenamed(e)));
    }

    // ── Filesystem event handlers ──────────────────────────────────────────────

    private void OnFsCreated(System.IO.FileSystemEventArgs e)
    {
        var parentDir = Path.GetDirectoryName(e.FullPath);
        if (parentDir is null) return;

        var parentNode = FindNodeByPath(_allNodes, parentDir);
        if (parentNode is null || !parentNode.ChildrenLoaded) return;

        // Avoid duplicates (watcher can fire more than once for the same path).
        if (parentNode.Children!.Any(n => string.Equals(n.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase)))
            return;

        bool isDir = Directory.Exists(e.FullPath);
        TreeNode newNode;

        if (isDir)
        {
            newNode = new TreeNode(
                e.Name ?? Path.GetFileName(e.FullPath),
                isFolder: true,
                children: new ObservableCollection<TreeNode> { TreeNode.LoadingPlaceholder },
                fullPath: e.FullPath,
                childrenLoaded: false);
            ObserveNode(newNode);
        }
        else
        {
            newNode = new TreeNode(
                e.Name ?? Path.GetFileName(e.FullPath),
                isFolder: false,
                fullPath: e.FullPath);
        }

        // Insert in sorted order (folders first, then files — matching the existing convention).
        InsertNodeSorted(parentNode.Children!, newNode);
        ApplyFilter();
    }

    private void OnFsDeleted(System.IO.FileSystemEventArgs e)
    {
        var node = FindNodeByPath(_allNodes, e.FullPath);
        if (node is null) return;

        RemoveNodeFromTree(node, _allNodes);
        ApplyFilter();
    }

    private void OnFsRenamed(System.IO.RenamedEventArgs e)
    {
        // Treat rename as delete-old + create-new so the sorted position is correct.
        OnFsDeleted(new System.IO.FileSystemEventArgs(
            System.IO.WatcherChangeTypes.Deleted, Path.GetDirectoryName(e.OldFullPath) ?? "", e.OldName));
        OnFsCreated(new System.IO.FileSystemEventArgs(
            System.IO.WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath) ?? "", e.Name));
    }

    // ── Tree helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the node whose <see cref="TreeNode.FullPath"/> matches <paramref name="fullPath"/>
    /// (case-insensitive), searching the entire loaded tree.
    /// </summary>
    private static TreeNode? FindNodeByPath(IEnumerable<TreeNode> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            if (node.IsLoadingPlaceholder) continue;
            if (string.Equals(node.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return node;
            if (node.Children is { Count: > 0 })
            {
                var found = FindNodeByPath(node.Children, fullPath);
                if (found is not null) return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Inserts <paramref name="node"/> into <paramref name="collection"/> in alphabetical order,
    /// placing folder nodes before file nodes (matching the existing tree convention).
    /// </summary>
    private static void InsertNodeSorted(ObservableCollection<TreeNode> collection, TreeNode node)
    {
        // Find the insertion index: folders before files, then alphabetical within each group.
        int i = 0;
        for (; i < collection.Count; i++)
        {
            var existing = collection[i];
            if (existing.IsLoadingPlaceholder) continue;

            // node is a folder: comes before files; skip over other folders in alpha order.
            if (node.IsFolder && !existing.IsFolder) break;
            // node is a file: comes after folders.
            if (!node.IsFolder && existing.IsFolder) { i++; continue; }

            if (string.Compare(node.Name, existing.Name, StringComparison.OrdinalIgnoreCase) <= 0)
                break;
        }
        collection.Insert(i, node);
    }

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _fsCreatedSub?.Dispose();
        _fsDeletedSub?.Dispose();
        _fsRenamedSub?.Dispose();
        _fsWatcher?.Dispose();
        _filterCts?.Dispose();
    }

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

        // Fire-and-forget: the async method handles and logs its own exceptions.
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Expected filesystem errors — reset the placeholder so the user can retry.
            node.ChildrenLoaded = false;
            node.Children?.Clear();
            node.Children?.Add(TreeNode.LoadingPlaceholder);
        }
        catch (Exception ex)
        {
            // Unexpected errors — log for diagnostics, then reset.
            System.Diagnostics.Debug.WriteLine(
                $"[SolutionExplorer] Unexpected error loading '{node.FullPath}': {ex}");
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

    /// <summary>Recursively counts all non-placeholder nodes in <paramref name="nodes"/>.</summary>
    private static int CountNodes(IEnumerable<TreeNode> nodes)
    {
        int count = 0;
        foreach (var node in nodes)
        {
            if (node.IsLoadingPlaceholder) continue;
            count++;
            if (node.Children is { Count: > 0 })
                count += CountNodes(node.Children);
        }
        return count;
    }

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

    // ── Context-menu state ────────────────────────────────────────────────────

    private TreeNode? _nodeClipboard;
    private bool _nodeClipboardIsCut;

    /// <summary>Avalonia clipboard service — set by the View after attaching to the visual tree.</summary>
    public Avalonia.Input.Platform.IClipboard? SystemClipboard { get; set; }

    // ── Context-menu commands ─────────────────────────────────────────────────

    [RelayCommand]
    private void OpenNode(TreeNode? node)
    {
        if (node is null || node.IsLoadingPlaceholder) return;
        if (node.IsFolder) { node.IsExpanded = !node.IsExpanded; return; }

        DocumentService.Instance.OpenDocument(node.Name, node.FullPath);
    }

    [RelayCommand]
    private void OpenContainingFolder(TreeNode? node)
    {
        if (node is null) return;
        var path = node.IsFolder ? node.FullPath : Path.GetDirectoryName(node.FullPath);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SolutionExplorer] OpenContainingFolder: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyFullPath(TreeNode? node)
    {
        if (node?.FullPath is null || SystemClipboard is null) return;
        await SystemClipboard.SetTextAsync(node.FullPath);
    }

    [RelayCommand]
    private void AddNewFile(TreeNode? node)
    {
        if (node is null || !node.IsFolder) return;
        const string name = "NewFile.cs";
        node.Children?.Add(new TreeNode(
            name, isFolder: false,
            fullPath: node.FullPath is not null ? Path.Combine(node.FullPath, name) : null));
        node.IsExpanded = true;
        ApplyFilter();
    }

    [RelayCommand]
    private void AddNewFolder(TreeNode? node)
    {
        if (node is null || !node.IsFolder) return;
        const string name = "NewFolder";
        node.Children?.Add(new TreeNode(
            name, isFolder: true,
            children: new ObservableCollection<TreeNode>(),
            fullPath: node.FullPath is not null ? Path.Combine(node.FullPath, name) : null,
            childrenLoaded: true));
        node.IsExpanded = true;
        ApplyFilter();
    }

    [RelayCommand]
    private void AddExistingFile(TreeNode? node)
    {
        if (node is null || !node.IsFolder) return;
        // TODO: open StorageProvider file picker
        System.Diagnostics.Debug.WriteLine($"[SolutionExplorer] AddExistingFile to: {node.FullPath}");
    }

    [RelayCommand]
    private void CutNode(TreeNode? node)
    {
        if (node is null || node.IsLoadingPlaceholder) return;
        _nodeClipboard = node;
        _nodeClipboardIsCut = true;
    }

    [RelayCommand]
    private void CopyNode(TreeNode? node)
    {
        if (node is null || node.IsLoadingPlaceholder) return;
        _nodeClipboard = node;
        _nodeClipboardIsCut = false;
    }

    [RelayCommand]
    private void PasteNode(TreeNode? node)
    {
        if (_nodeClipboard is null || node is null || !node.IsFolder) return;
        node.Children?.Add(new TreeNode(
            _nodeClipboard.Name, _nodeClipboard.IsFolder,
            _nodeClipboard.IsFolder ? new ObservableCollection<TreeNode>() : null,
            _nodeClipboard.FullPath, _nodeClipboard.ChildrenLoaded));
        if (_nodeClipboardIsCut)
        {
            RemoveNodeFromTree(_nodeClipboard, _allNodes);
            _nodeClipboard = null;
        }
        node.IsExpanded = true;
        ApplyFilter();
    }

    [RelayCommand]
    private void RenameNode(TreeNode? node)
    {
        // TODO: trigger inline rename
        System.Diagnostics.Debug.WriteLine($"[SolutionExplorer] Rename: {node?.Name}");
    }

    [RelayCommand]
    private void RemoveNode(TreeNode? node)
    {
        if (node is null || node.IsLoadingPlaceholder) return;
        if (RemoveNodeFromTree(node, _allNodes))
            ApplyFilter();
    }

    [RelayCommand]
    private void ShowProperties(TreeNode? node)
    {
        // TODO: show properties panel
        System.Diagnostics.Debug.WriteLine($"[SolutionExplorer] Properties: {node?.FullPath ?? node?.Name}");
    }

    private static bool RemoveNodeFromTree(TreeNode target, IList<TreeNode> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], target)) { nodes.RemoveAt(i); return true; }
            if (nodes[i].Children is { } ch && RemoveNodeFromTree(target, ch)) return true;
        }
        return false;
    }
}
