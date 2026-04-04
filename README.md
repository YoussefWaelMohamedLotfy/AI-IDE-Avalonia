# AI IDE Avalonia

A cross-platform AI-powered IDE prototype built with [Avalonia UI](https://avaloniaui.net/) and [Dock](https://github.com/wieslawsoltes/Dock), featuring a dockable workspace, a project file-tree explorer, a Ribbon toolbar, full RTL/multi-language localization, and an AI chat assistant that can interact with the project tree via tool calls.

---

## Features

- **Dockable Layout** — Resizable, draggable tool windows and document tabs powered by the Dock framework.
- **Project File-Tree Explorer** — A filterable, hierarchical tree view of project files and folders with real-time search highlighting, lazy on-demand expansion, and a right-click context menu.
- **AI Chat Panel** — Dedicated tool panel ("AI Chat") that streams responses from a locally running [Ollama](https://ollama.com/) model or [GitHub Copilot](https://github.com/features/copilot). Switch providers and models at any time via in-panel dropdowns.
  - **Ollama** — Connects to a local Ollama instance; available models are fetched dynamically on startup.
  - **GitHub Copilot** — Connects via the [GitHub Copilot SDK](https://github.com/github/copilot-sdk-dotnet); available models are fetched dynamically via `ListModelsAsync()` on startup.
  - Both provider model lists are pre-loaded in parallel at IDE startup and cached — switching providers is instant with no UI freeze.
- **AI Tool Use** — The AI can call built-in tools to search, add, delete, and write to nodes in the project tree.
- **Ribbon Toolbar** — A full Ribbon UI (`IdeRibbonFactory`) with tabs for Home, Edit, and View actions. Labels update live on language switch.
- **Internationalization** — Full RTL/LTR multi-language support via `LocalizationService` backed by `.resx` resource files. Supported languages: **English**, **Arabic** (RTL), **German**, **French**, **Spanish**, **Polish**.
- **Fluent Theme** — Adapts to the system light/dark preference via Avalonia's `FluentTheme`.
- **Compiled Bindings** — All AXAML bindings are compiled at build time for type safety and performance.

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| Avalonia | 11.3.x |
| [Ollama](https://ollama.com/) *(optional — for Ollama provider)* | Latest |
| GitHub Copilot CLI *(optional — for GitHub Copilot provider)* | Latest |

> **Ollama:** Pull a model before using the Ollama provider. The default model is `gemma4:e2b`:
> ```powershell
> ollama pull gemma4:e2b
> ```

> **GitHub Copilot:** Requires the [GitHub Copilot CLI](https://docs.github.com/en/copilot/github-copilot-in-the-cli) to be installed and authenticated. Update `CopilotCliPath` in `Tool5ViewModel.cs` to match your installation path.

---

## Getting Started

### Clone

```powershell
git clone https://github.com/YoussefWaelMohamedLotfy/AI-IDE-Avalonia.git
cd AI-IDE-Avalonia
```

### Build

```powershell
dotnet build
```

### Run

```powershell
dotnet run --project AI-IDE-Avalonia
```

### Release Build

```powershell
dotnet build -c Release
```

---

## Project Structure

```
AI-IDE-Avalonia/
├── Controls/
│   └── HighlightTextBlock.cs           # Custom TextBlock that highlights filter matches
├── Models/
│   ├── Documents/
│   │   ├── ChatMessage.cs              # Chat message model (User / Assistant / ToolCall)
│   │   └── DemoDocument.cs             # Placeholder document model
│   ├── Tools/
│   │   └── Tool1.cs – Tool5.cs         # Tool panel models
│   └── TreeNode.cs                     # Hierarchical file-tree node
├── Resources/
│   ├── Strings.resx                    # English (default) string resources
│   ├── Strings.ar.resx                 # Arabic (RTL)
│   ├── Strings.de.resx                 # German
│   ├── Strings.fr.resx                 # French
│   ├── Strings.es.resx                 # Spanish
│   └── Strings.pl.resx                 # Polish
├── Services/
│   ├── AIProviderService.cs            # Manages the active AI provider selection
│   ├── DocumentService.cs              # Manages open documents
│   ├── LocalizationService.cs          # Runtime language switching with INotifyPropertyChanged
│   ├── RecentFoldersService.cs         # Persists recently opened workspace folders
│   └── StorageDialogHelper.cs          # Shared file-picker helper
├── ViewModels/
│   ├── Documents/
│   │   └── DocumentViewModel.cs        # Editor document with save/modify state
│   ├── Tools/
│   │   ├── SolutionExplorerViewModel.cs # Filterable file-tree with lazy expansion & context menus
│   │   ├── Tool5ViewModel.cs           # AI Chat panel — provider/model selection, streaming, tool calls
│   │   └── Tool2–4ViewModel.cs         # Placeholder tool panels
│   ├── DockFactory.cs                  # Constructs the full dockable layout
│   ├── IdeRibbonFactory.cs             # Builds the Ribbon toolbar
│   ├── MainWindowViewModel.cs          # Top-level ViewModel; handles localization & provider changes
│   └── WorkspaceSelectorViewModel.cs   # Workspace open/recent dialog ViewModel
├── Views/
│   ├── Tools/
│   │   ├── SolutionExplorerView.axaml(.cs) # File-tree view with right-click context menu
│   │   ├── Tool5View.axaml(.cs)        # AI Chat panel UI
│   │   └── Tool2–4View.axaml(.cs)      # Placeholder views
│   ├── MainWindow.axaml(.cs)           # Root window; FlowDirection bound for RTL support
│   └── WorkspaceSelectorWindow.axaml(.cs) # Workspace picker dialog
├── App.axaml(.cs)                      # DI container setup, bootstrap & theme
├── ViewLocator.cs                      # Automatic ViewModel → View resolution
└── Program.cs                          # Entry point & Dock JSON serialisation setup
```

---

## Architecture

This application follows the **MVVM** pattern using [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/).

### ViewLocator

A global `IDataTemplate` that resolves Views from ViewModels automatically by replacing `"ViewModel"` with `"View"` in the full type name. New ViewModel/View pairs must follow this naming convention:

```
AI_IDE_Avalonia.ViewModels.Foo.BarViewModel  →  AI_IDE_Avalonia.Views.Foo.BarView
```

### Docking (Dock Framework)

The layout is built in `DockFactory` and serialised to/from JSON via `Dock.Serializer.SystemTextJson`. The source-generation attributes in `Program.cs` register all serialisable dock types.

### AI Chat & Tool Use

`Tool5ViewModel` manages the AI Chat panel. It supports two backends — **Ollama** (via `OllamaSharp`) and **GitHub Copilot** (via `GitHub.Copilot.SDK`) — both routed through `Microsoft.Extensions.AI` abstractions. The active provider and model are user-selectable via in-panel dropdowns. The chat loop supports multi-turn **tool use**:

| Tool | Description |
|---|---|
| `search_tree_nodes` | Finds nodes in the project tree by name |
| `add_tree_node` | Creates a new file or folder at a given path |
| `delete_tree_node` | Removes a node from the project tree |
| `write_to_document` | Writes generated content into an open editor document |

Tool calls are executed against the live `SolutionExplorerViewModel` tree and results are fed back to the model. The loop runs for up to **10 iterations** per user turn.

### Localization

`LocalizationService` is a singleton `ObservableObject` wrapping a `ResourceManager` over the `.resx` files. Calling `SetCulture(cultureName)` switches the active language at runtime and raises `PropertyChanged("")`, which causes all compiled bindings that reference `Loc.*` properties to refresh automatically. The root `Window.FlowDirection` is bound to `LocalizationService.FlowDirection`, so the entire layout mirrors for RTL languages (Arabic).

---

## Key Dependencies

| Package | Purpose |
|---|---|
| `Avalonia` 11.3.x | Cross-platform UI framework |
| `Dock.Avalonia` 11.3.x | Dockable layout engine |
| `Dock.Model.Mvvm` | MVVM bindings for Dock |
| `Dock.Serializer.SystemTextJson` | JSON persistence of dock layout |
| `CommunityToolkit.Mvvm` 8.4.x | Source-generated MVVM helpers |
| `OllamaSharp` 5.4.x | Ollama API client |
| `GitHub.Copilot.SDK` 0.2.x | GitHub Copilot client (`CopilotClient`, `ListModelsAsync`) |
| `Microsoft.Agents.AI` 1.0.0-rc4 | `Microsoft.Extensions.AI` abstractions + GitHub Copilot agent |
| `Serilog` | Structured logging |
| `StaticViewLocator` 0.4.x | Compile-time ViewLocator source generator |

---

## Coding Conventions

- **Namespace:** Always `AI_IDE_Avalonia` (underscores, not dots).
- **ViewModels:** Inherit from `ViewModelBase` → `ObservableObject`. Use `[ObservableProperty]` and declare classes `partial`.
- **Compiled bindings:** Every `.axaml` file that binds data **must** declare `x:DataType`.
- **Nullable:** Nullable reference types are enabled project-wide. All code must be null-safe.
- **Avalonia Diagnostics:** Available only in `Debug` builds. Open the dev-tools overlay with `F12`.

---

## Contributing

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`.
3. Commit your changes and open a Pull Request targeting `main`.