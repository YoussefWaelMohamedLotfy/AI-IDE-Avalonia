using System.Globalization;
using System.Resources;
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
        new("AI_IDE_Avalonia.Resources.Strings", typeof(LocalizationService).Assembly);

    private CultureInfo _currentCulture = new("en");

    /// <summary>The culture currently in use for string look-ups.</summary>
    public CultureInfo CurrentCulture => _currentCulture;

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
}
