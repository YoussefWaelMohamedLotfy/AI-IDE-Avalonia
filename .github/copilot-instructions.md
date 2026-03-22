# Copilot Instructions

## Build & Run

```powershell
dotnet build
dotnet run --project AI-IDE-Avalonia
dotnet build -c Release
```

There are no tests yet. When tests are added, they should be run with `dotnet test`.

## Architecture

This is a **WinExe Avalonia UI desktop app** targeting `.NET 10.0`, structured as a standard MVVM application.

```
AI-IDE-Avalonia/
├── Models/          # Data models (currently empty)
├── ViewModels/      # CommunityToolkit.Mvvm ObservableObject subclasses
├── Views/           # Avalonia .axaml + .axaml.cs code-behind pairs
├── App.axaml(.cs)   # Application bootstrapping and theme setup
├── ViewLocator.cs   # Automatic ViewModel→View resolution via reflection
└── Program.cs       # Entry point — builds and starts Avalonia app
```

The app uses a **ViewLocator** registered as a global `IDataTemplate`. It resolves views by replacing `"ViewModel"` with `"View"` in the full type name (e.g., `AI_IDE_Avalonia.ViewModels.MainWindowViewModel` → `AI_IDE_Avalonia.Views.MainWindowView`). New ViewModel/View pairs must follow this naming convention exactly.

`App.axaml.cs` bootstraps the app: it creates `MainWindow` and assigns `MainWindowViewModel` as its `DataContext`, then disables Avalonia's built-in `DataAnnotationsValidationPlugin` to avoid duplicate validation with CommunityToolkit.Mvvm.

## Key Conventions

**Namespace:** Always use `AI_IDE_Avalonia` (underscores, not dots) — e.g., `namespace AI_IDE_Avalonia.ViewModels`.

**ViewModels:** All ViewModels inherit from `ViewModelBase`, which extends `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`. Use `[ObservableProperty]` source-generated attributes for bindable properties and declare the class `partial`.

**Compiled bindings:** `AvaloniaUseCompiledBindingsByDefault` is `true`. Every `.axaml` file that binds data **must** declare `x:DataType` pointing to the correct ViewModel type.

**Nullable:** Nullable reference types are enabled project-wide. All code must be null-safe.

**Avalonia diagnostics:** `Avalonia.Diagnostics` is included only in `Debug` builds. The dev tools overlay (`F12`) is available during development.

**Theme:** `FluentTheme` with `RequestedThemeVariant="Default"` (follows system light/dark preference).

**New Views:** Create an `.axaml` / `.axaml.cs` pair in `Views/`. The code-behind only needs `InitializeComponent()` unless additional logic is required. Always specify `x:DataType` in the AXAML root element.
