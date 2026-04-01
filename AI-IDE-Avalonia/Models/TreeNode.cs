using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AI_IDE_Avalonia.Models;

/// <summary>
/// Represents a single node (file or folder) in the workspace file tree.
/// Implements <see cref="ObservableObject"/> so that <see cref="IsExpanded"/> and
/// <see cref="IsLoading"/> changes are reflected in the UI without an intermediate ViewModel.
/// </summary>
public partial class TreeNode : ObservableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Display name of the file or folder.</summary>
    public string Name { get; }

    /// <summary><c>true</c> if this node represents a directory; <c>false</c> for a file.</summary>
    public bool IsFolder { get; }

    /// <summary>
    /// Full filesystem path used by lazy-loading logic to enumerate children on demand.
    /// <c>null</c> for in-memory / design-time nodes.
    /// </summary>
    public string? FullPath { get; }

    /// <summary>
    /// <c>true</c> for the transient "Loading…" placeholder inserted into folders
    /// whose children have not yet been fetched from the filesystem.
    /// </summary>
    public bool IsLoadingPlaceholder { get; }

    // ── Observable state ─────────────────────────────────────────────────────

    /// <summary>
    /// Whether the TreeViewItem for this node is currently expanded.
    /// Bound two-way via code-behind so user interactions propagate back to the model,
    /// and programmatic changes (e.g. Collapse All) propagate to the view.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Set to <c>true</c> while children are being loaded asynchronously so a progress
    /// indicator can be displayed in the row.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// <c>true</c> once the real children of this folder have been fetched from disk
    /// (or once it is confirmed the folder has no children).
    /// </summary>
    [ObservableProperty]
    private bool _childrenLoaded;

    // ── Children ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Child nodes.  Folders always own an <see cref="ObservableCollection{T}"/> (never <c>null</c>)
    /// so the TreeView shows an expand chevron.  Files have <c>null</c>.
    /// </summary>
    public ObservableCollection<TreeNode>? Children { get; }

    // ── Constructors ──────────────────────────────────────────────────────────

    public TreeNode(
        string name,
        bool isFolder = false,
        ObservableCollection<TreeNode>? children = null,
        string? fullPath = null,
        bool isLoadingPlaceholder = false,
        bool childrenLoaded = false)
    {
        Name = name;
        IsFolder = isFolder;
        FullPath = fullPath;
        IsLoadingPlaceholder = isLoadingPlaceholder;
        _childrenLoaded = childrenLoaded;
        Children = isFolder ? (children ?? new ObservableCollection<TreeNode>()) : children;
    }

    // ── Lazy-loading helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Singleton placeholder inserted as the sole child of an unloaded folder so that
    /// the TreeView renders an expand chevron before real children are fetched.
    /// </summary>
    public static readonly TreeNode LoadingPlaceholder =
        new("Loading\u2026", isFolder: false, isLoadingPlaceholder: true);

    // ── Design-time sample data ───────────────────────────────────────────────

    private static ObservableCollection<TreeNode> Nodes(params TreeNode[] nodes) =>
        new(nodes);

    public static ObservableCollection<TreeNode> CreateSampleProject() =>
        Nodes(
            new TreeNode("MyAIProject", isFolder: true, childrenLoaded: true, children: Nodes(
                new TreeNode("src", isFolder: true, childrenLoaded: true, children: Nodes(
                    new TreeNode("Agents", isFolder: true, childrenLoaded: true, children: Nodes(
                        new TreeNode("ChatAgent.cs"),
                        new TreeNode("SummaryAgent.cs"),
                        new TreeNode("CodeAgent.cs")
                    )),
                    new TreeNode("Models", isFolder: true, childrenLoaded: true, children: Nodes(
                        new TreeNode("ChatMessage.cs"),
                        new TreeNode("ConversationHistory.cs"),
                        new TreeNode("ModelSettings.cs")
                    )),
                    new TreeNode("Services", isFolder: true, childrenLoaded: true, children: Nodes(
                        new TreeNode("OpenAIService.cs"),
                        new TreeNode("AnthropicService.cs"),
                        new TreeNode("IModelService.cs")
                    )),
                    new TreeNode("Prompts", isFolder: true, childrenLoaded: true, children: Nodes(
                        new TreeNode("SystemPrompt.txt"),
                        new TreeNode("CodeReviewPrompt.txt")
                    ))
                )),
                new TreeNode("tests", isFolder: true, childrenLoaded: true, children: Nodes(
                    new TreeNode("AgentTests.cs"),
                    new TreeNode("ServiceTests.cs")
                )),
                new TreeNode("README.md"),
                new TreeNode("MyAIProject.csproj")
            ))
        );
}
