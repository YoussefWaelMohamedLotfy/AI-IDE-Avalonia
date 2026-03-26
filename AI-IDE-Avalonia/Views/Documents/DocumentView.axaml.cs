using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AI_IDE_Avalonia.Controls;
using AI_IDE_Avalonia.ViewModels.Documents;
using TextMateSharp.Grammars;

namespace AI_IDE_Avalonia.Views.Documents;

public partial class DocumentView : UserControl
{
    private readonly TextEditor _editor;
    private readonly BreakpointMargin _breakpointMargin;
    private readonly BreakpointLineHighlighter _breakpointHighlighter;
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private readonly ComboBox? _languageCombo;
    private readonly TextBlock? _caretPositionText;
    private readonly TextBlock? _breakpointCountText;
    private readonly TextBlock? _selectedLanguageText;
    private readonly ToggleButton? _wordWrapToggle;
    private readonly ToggleButton? _showLineNumbersToggle;
    private DocumentViewModel? _currentVm;
    private bool _disposed;

    public DocumentView()
    {
        InitializeComponent();

        _editor = this.FindControl<TextEditor>("Editor")
                  ?? throw new InvalidOperationException("TextEditor 'Editor' not found in AXAML.");
        _languageCombo         = this.FindControl<ComboBox>("LanguageCombo");
        _caretPositionText     = this.FindControl<TextBlock>("CaretPositionText");
        _breakpointCountText   = this.FindControl<TextBlock>("BreakpointCountText");
        _selectedLanguageText  = this.FindControl<TextBlock>("SelectedLanguageText");
        _wordWrapToggle        = this.FindControl<ToggleButton>("WordWrapToggle");
        _showLineNumbersToggle = this.FindControl<ToggleButton>("ShowLineNumbersToggle");

        _editor.ShowLineNumbers = true;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.AllowToggleOverstrikeMode = true;
        _editor.Options.EnableTextDragDrop = true;
        _editor.Options.ShowBoxForControlCharacters = true;
        _editor.Options.ColumnRulerPositions = [80, 120];
        _editor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(_editor.Options);
        _editor.TextArea.RightClickMovesCaret = true;
        _editor.TextArea.TextView.Margin = new Thickness(6, 0, 0, 0);

        _editor.AddHandler(PointerWheelChangedEvent, OnEditorPointerWheel,
            RoutingStrategies.Bubble, handledEventsToo: true);
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        WireContextMenu();

        if (_wordWrapToggle is not null)
            _wordWrapToggle.IsCheckedChanged += OnWordWrapChanged;
        if (_showLineNumbersToggle is not null)
            _showLineNumbersToggle.IsCheckedChanged += OnShowLineNumbersChanged;

        _breakpointMargin = new BreakpointMargin();
        _breakpointMargin.BreakpointsChanged += OnBreakpointsChanged;
        _editor.TextArea.LeftMargins.Insert(0, _breakpointMargin);

        _breakpointHighlighter = new BreakpointLineHighlighter(_breakpointMargin.BreakpointLines);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_breakpointHighlighter);

        try
        {
            var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            _registryOptions = new RegistryOptions(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
            _textMateInstallation = _editor.InstallTextMate(_registryOptions);
            _textMateInstallation.AppliedTheme += OnAppliedTheme;

            if (Application.Current is { } app)
                app.ActualThemeVariantChanged += OnAppThemeVariantChanged;
        }
        catch
        {
            // TextMate unavailable — editor still works without syntax highlighting
        }

        if (_languageCombo is not null && _registryOptions is not null)
        {
            _languageCombo.ItemsSource = _registryOptions.GetAvailableLanguages();
            _languageCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Id");
            _languageCombo.SelectionChanged += OnLanguageComboChanged;
        }

        DataContextChanged += OnDataContextChanged;

        if (DataContext is DocumentViewModel vm)
            BindViewModel(vm);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm is not null)
        {
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
            _currentVm.Disposing -= OnViewModelDisposing;
        }

        _currentVm = DataContext as DocumentViewModel;

        if (_currentVm is not null)
        {
            BindViewModel(_currentVm);
        }
        else
        {
            // DataContext set to null — document was closed. DockFactory.CloseDockable removes
            // the dockable before calling DisposeAsync, so this may fire before Disposing event.
            CleanUp();
        }
    }

    private void BindViewModel(DocumentViewModel vm)
    {
        _currentVm = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.Disposing += OnViewModelDisposing;
        ApplyViewModelState(vm);
    }

    /// <summary>
    /// Called from <see cref="DocumentViewModel.DisposeAsync"/> (via DockFactory.CloseDockable).
    /// This is the authoritative signal that the document has been closed.
    /// </summary>
    private void OnViewModelDisposing(object? sender, EventArgs e) => CleanUp();

    private void CleanUp()
    {
        if (_disposed) return;
        _disposed = true;

        // 1. Release external references — the VM outlives the view in some orderings,
        //    so we must unsubscribe to avoid the VM keeping the view alive.
        if (_currentVm is not null)
        {
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
            _currentVm.Disposing -= OnViewModelDisposing;
            _currentVm = null;
        }

        DataContextChanged -= OnDataContextChanged;

        // 2. Detach all editor event handlers to allow the editor tree to be GC'd cleanly.
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.RemoveHandler(PointerWheelChangedEvent, OnEditorPointerWheel);

        if (_wordWrapToggle is not null)
            _wordWrapToggle.IsCheckedChanged -= OnWordWrapChanged;
        if (_showLineNumbersToggle is not null)
            _showLineNumbersToggle.IsCheckedChanged -= OnShowLineNumbersChanged;

        if (_languageCombo is not null)
            _languageCombo.SelectionChanged -= OnLanguageComboChanged;

        // 3. Remove the breakpoint margin/highlighter and detach its event.
        _breakpointMargin.BreakpointsChanged -= OnBreakpointsChanged;
        _editor.TextArea.LeftMargins.Remove(_breakpointMargin);
        _editor.TextArea.TextView.BackgroundRenderers.Remove(_breakpointHighlighter);

        // 4. Dispose TextMate — releases grammar parsers and unregisters all line transformers
        //    that were registered on the editor's TextView. Without this, transformers accumulate.
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged -= OnAppThemeVariantChanged;

        if (_textMateInstallation is not null)
        {
            _textMateInstallation.AppliedTheme -= OnAppliedTheme;
            _textMateInstallation.Dispose();
            _textMateInstallation = null;
        }
    }

    // ── ViewModel sync ────────────────────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentViewModel vm) return;

        if (e.PropertyName == nameof(DocumentViewModel.SelectedLanguageExtension))
            ApplyLanguageByExtension(vm.SelectedLanguageExtension);

        if (e.PropertyName == nameof(DocumentViewModel.DocumentText)
            && vm.DocumentText != _editor.Text)
            _editor.Text = vm.DocumentText;
    }

    private void ApplyViewModelState(DocumentViewModel vm)
    {
        if (!string.IsNullOrEmpty(vm.DocumentText))
            _editor.Text = vm.DocumentText;

        ApplyThemeFromAppVariant();
        ApplyLanguageByExtension(vm.SelectedLanguageExtension);
        SyncLanguageCombo(vm.SelectedLanguageExtension);
    }

    // ── TextMate ──────────────────────────────────────────────────────────────

    private void OnAppThemeVariantChanged(object? sender, EventArgs e) => ApplyThemeFromAppVariant();

    private void ApplyThemeFromAppVariant()
    {
        if (_registryOptions is null || _textMateInstallation is null) return;
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        _textMateInstallation.SetTheme(_registryOptions.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus));
    }

    private void ApplyLanguageByExtension(string extension)
    {
        if (_registryOptions is null || _textMateInstallation is null) return;

        Language? lang = _registryOptions.GetLanguageByExtension(extension);
        if (lang is null) return;

        _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(lang.Id));

        if (_selectedLanguageText is not null)
            _selectedLanguageText.Text = lang.Id;

        SyncLanguageCombo(extension);
    }

    private void SyncLanguageCombo(string extension)
    {
        if (_languageCombo is null || _registryOptions is null) return;
        Language? lang = _registryOptions.GetLanguageByExtension(extension);
        if (lang is not null)
            _languageCombo.SelectedItem = lang;
    }

    private void OnLanguageComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_languageCombo?.SelectedItem is not Language lang) return;
        if (_registryOptions is null || _textMateInstallation is null) return;

        _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(lang.Id));

        if (_selectedLanguageText is not null)
            _selectedLanguageText.Text = lang.Id;

        if (_currentVm is not null && lang.Extensions?.Count > 0)
            _currentVm.SelectedLanguageExtension = lang.Extensions[0];
    }

    private void OnAppliedTheme(object? sender, TextMate.Installation installation)
    {
        if (!ApplyBrush(installation, "editor.background", b =>
        {
            _editor.Background = b;
            _editor.TextArea.Background = b;
        }))
        {
            // No theme color — keep AvaloniaEdit default background
        }

        ApplyBrush(installation, "editor.foreground", b => _editor.Foreground = b);
        ApplyBrush(installation, "editor.selectionBackground", b => _editor.TextArea.SelectionBrush = b);
        ApplyBrush(installation, "editorLineNumber.foreground", b => _editor.LineNumbersForeground = b);

        if (!ApplyBrush(installation, "editor.lineHighlightBackground", b =>
        {
            _editor.TextArea.TextView.CurrentLineBackground = b;
            _editor.TextArea.TextView.CurrentLineBorder = new Pen(b);
        }))
        {
            _editor.TextArea.TextView.SetDefaultHighlightLineColors();
        }

        if (!ApplyBrush(installation, "editorGutter.background", b => _breakpointMargin.BackgroundBrush = b))
            _breakpointMargin.UseDefaultBackground();
    }

    private static bool ApplyBrush(TextMate.Installation installation, string key, Action<IBrush> apply)
    {
        if (installation.TryGetThemeColor(key, out string? colorString)
            && Color.TryParse(colorString, out Color color))
        {
            apply(new SolidColorBrush(color));
            return true;
        }
        return false;
    }

    // ── Editor event handlers ─────────────────────────────────────────────────

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_caretPositionText is null) return;
        var pos = _editor.TextArea.Caret.Position;
        _caretPositionText.Text = $"Ln {pos.Line}, Col {pos.Column}";
    }

    private void OnBreakpointsChanged(object? sender, EventArgs e)
    {
        if (_breakpointCountText is not null)
            _breakpointCountText.Text = $"Breakpoints: {_breakpointMargin.BreakpointLines.Count}";

        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    private void OnEditorPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;
        _editor.FontSize = e.Delta.Y > 0
            ? _editor.FontSize + 1
            : Math.Max(1, _editor.FontSize - 1);
    }

    private void OnWordWrapChanged(object? sender, RoutedEventArgs e)
        => _editor.WordWrap = _wordWrapToggle?.IsChecked == true;

    private void OnShowLineNumbersChanged(object? sender, RoutedEventArgs e)
    {
        _editor.ShowLineNumbers = _showLineNumbersToggle?.IsChecked == true;
        // AvaloniaEdit always inserts its LineNumberMargin at index 0, which pushes
        // our BreakpointMargin to index 1. Re-pin it to the leftmost position.
        EnsureBreakpointMarginFirst();
    }

    private void EnsureBreakpointMarginFirst()
    {
        var margins = _editor.TextArea.LeftMargins;
        int idx = margins.IndexOf(_breakpointMargin);
        if (idx > 0)
        {
            margins.RemoveAt(idx);
            margins.Insert(0, _breakpointMargin);
        }
    }

    private void WireContextMenu()
    {
        var cut       = this.FindControl<MenuItem>("MenuCut");
        var copy      = this.FindControl<MenuItem>("MenuCopy");
        var paste     = this.FindControl<MenuItem>("MenuPaste");
        var selectAll = this.FindControl<MenuItem>("MenuSelectAll");

        TextArea area = _editor.TextArea;
        if (cut is not null)       cut.Click       += (_, _) => ApplicationCommands.Cut.Execute(null, area);
        if (copy is not null)      copy.Click      += (_, _) => ApplicationCommands.Copy.Execute(null, area);
        if (paste is not null)     paste.Click     += (_, _) => ApplicationCommands.Paste.Execute(null, area);
        if (selectAll is not null) selectAll.Click += (_, _) => ApplicationCommands.SelectAll.Execute(null, area);
    }
}
