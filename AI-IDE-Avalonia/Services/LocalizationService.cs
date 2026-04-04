using System.Globalization;
using System.Resources;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Singleton service that provides localized strings for the application.
/// Wraps the <see cref="ResourceManager"/> backed by <c>Resources/Strings.resx</c>
/// and its culture-specific counterparts (e.g. <c>Strings.ar.resx</c>).
/// Call <see cref="SetCulture"/> to switch languages at runtime; all bindings
/// that reference this service are automatically refreshed via
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// </summary>
public sealed class LocalizationService : ObservableObject
{
    private static readonly ResourceManager _rm =
        new("AI-IDE-Avalonia.Resources.Strings", typeof(LocalizationService).Assembly);

    private CultureInfo _currentCulture = new("en");

    /// <summary>The culture currently in use for string look-ups.</summary>
    public CultureInfo CurrentCulture => _currentCulture;

    /// <summary>
    /// Returns <see cref="FlowDirection.RightToLeft"/> for RTL cultures (e.g. Arabic, Hebrew)
    /// and <see cref="FlowDirection.LeftToRight"/> for all others.
    /// Bind this to the root <c>Window.FlowDirection</c> so the entire UI mirrors automatically.
    /// </summary>
    public FlowDirection FlowDirection =>
        _currentCulture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    /// <summary>
    /// Switches the active culture and notifies all bound properties.
    /// </summary>
    /// <param name="cultureName">A BCP-47 language tag such as "en" or "ar".</param>
    public void SetCulture(string cultureName)
    {
        _currentCulture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentUICulture = _currentCulture;
        // Raise PropertyChanged("") to signal that ALL properties have changed.
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Returns the localized string for the given resource key,
    /// or the key itself when no entry is found.
    /// </summary>
    public string this[string key] => _rm.GetString(key, _currentCulture) ?? key;

    // ── WorkspaceSelectorWindow strings ────────────────────────────────────

    public string WorkspaceSelectorTitle       => this[nameof(WorkspaceSelectorTitle)];
    public string WorkspaceSelectorSubtitle    => this[nameof(WorkspaceSelectorSubtitle)];
    public string OpenFolder                   => this[nameof(OpenFolder)];
    public string Recent                       => this[nameof(Recent)];
    public string NoRecentWorkspaces           => this[nameof(NoRecentWorkspaces)];
    public string ContinueWithoutWorkspace     => this[nameof(ContinueWithoutWorkspace)];

    // ── Solution Explorer context menu ─────────────────────────────────────

    public string CtxOpen                 => this[nameof(CtxOpen)];
    public string CtxOpenContainingFolder => this[nameof(CtxOpenContainingFolder)];
    public string CtxAdd                  => this[nameof(CtxAdd)];
    public string CtxNewFile              => this[nameof(CtxNewFile)];
    public string CtxNewFolder            => this[nameof(CtxNewFolder)];
    public string CtxAddExistingFile      => this[nameof(CtxAddExistingFile)];
    public string CtxCut                  => this[nameof(CtxCut)];
    public string CtxCopy                 => this[nameof(CtxCopy)];
    public string CtxPaste                => this[nameof(CtxPaste)];
    public string CtxRename               => this[nameof(CtxRename)];
    public string CtxDelete               => this[nameof(CtxDelete)];
    public string CtxCopyFullPath         => this[nameof(CtxCopyFullPath)];
    public string CtxProperties           => this[nameof(CtxProperties)];

    // ── Solution Explorer tool window ──────────────────────────────────────

    public string FilterWatermark     => this[nameof(FilterWatermark)];
    public string ExpandAll           => this[nameof(ExpandAll)];
    public string CollapseAll         => this[nameof(CollapseAll)];
    public string LoadingPlaceholder  => this[nameof(LoadingPlaceholder)];
    public string ItemsCount          => this[nameof(ItemsCount)];
    public string SelectedCount       => this[nameof(SelectedCount)];

    // ── AI Chat panel ──────────────────────────────────────────────────────

    public string ChatSend          => this[nameof(ChatSend)];
    public string ChatStop          => this[nameof(ChatStop)];
    public string ChatClear         => this[nameof(ChatClear)];
    public string ChatProviderLabel => this[nameof(ChatProviderLabel)];
    public string ChatToolCall      => this[nameof(ChatToolCall)];
}
