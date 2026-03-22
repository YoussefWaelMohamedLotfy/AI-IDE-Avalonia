# AI IDE Avalonia

A cross-platform AI-powered IDE prototype built with [Avalonia UI](https://avaloniaui.net/) and [Dock](https://github.com/wieslawsoltes/Dock), featuring a dockable workspace, a project file-tree explorer, and an AI chat assistant that can interact with the project tree via tool calls.

---

## Features

- **Dockable Layout** — Resizable, draggable tool windows and document tabs powered by the Dock framework.
- **Project File-Tree Explorer** — A filterable, hierarchical tree view of project files and folders with real-time search highlighting.
- **AI Chat Assistant** — Per-document chat panel that streams responses from a locally running [Ollama](https://ollama.com/) model (`granite4`).
- **AI Tool Use** — The AI can call built-in tools to search, add, and delete nodes in the project tree.
- **Fluent Theme** — Adapts to the system light/dark preference via Avalonia's `FluentTheme`.
- **Compiled Bindings** — All AXAML bindings are compiled at build time for type safety and performance.

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| Avalonia | 11.3.x |
| [Ollama](https://ollama.com/) (local AI runtime) | Latest |
| Ollama model | `granite4:latest` |

> **Note:** The AI chat panel requires Ollama to be running locally on `http://localhost:11434` with the `granite4:latest` model pulled. Pull the model with:
> ```powershell
> ollama pull granite4:latest
> ```

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
│   └── HighlightTextBlock.cs       # Custom TextBlock that highlights filter matches
├── Models/
│   ├── Documents/
│   │   ├── ChatMessage.cs          # Chat message model (User / Assistant / ToolCall)
│   │   └── DemoDocument.cs         # Placeholder document model
│   ├── Tools/
│   │   ├── Tool1.cs – Tool4.cs     # Placeholder tool models
│   ├── DemoData.cs                 # Placeholder demo data
│   └── TreeNode.cs                 # Hierarchical file-tree node with sample project data
├── ViewModels/
│   ├── Docks/
│   │   └── CustomDocumentDock.cs   # Custom document dock (Dock serialisation)
│   ├── Documents/
│   │   └── DocumentViewModel.cs    # AI chat logic, streaming, tool-call orchestration
│   ├── Tools/
│   │   ├── Tool1ViewModel.cs       # File-tree explorer (filterable TreeView)
│   │   ├── Tool2ViewModel.cs       # Placeholder tool panel
│   │   ├── Tool3ViewModel.cs       # Placeholder tool panel
│   │   └── Tool4ViewModel.cs       # Placeholder tool panel
│   ├── Views/
│   │   ├── DashboardViewModel.cs   # Dashboard view model
│   │   └── HomeViewModel.cs        # Root dock view model
│   ├── DockFactory.cs              # Constructs the full dockable layout
│   └── MainWindowViewModel.cs      # Top-level application ViewModel
├── Views/
│   ├── Documents/
│   │   └── DocumentView.axaml(.cs) # Chat UI with auto-scrolling message list
│   ├── Tools/
│   │   ├── Tool1View.axaml(.cs)    # File-tree explorer view
│   │   ├── Tool2View.axaml(.cs)    # Placeholder tool view
│   │   ├── Tool3View.axaml(.cs)    # Placeholder tool view
│   │   └── Tool4View.axaml(.cs)    # Placeholder tool view
│   ├── Views/
│   │   ├── DashboardView.axaml(.cs)
│   │   └── HomeView.axaml(.cs)
│   ├── DockableOptionsView.axaml(.cs)
│   ├── MainWindow.axaml(.cs)
│   ├── MainView.axaml(.cs)
│   └── ProportionalStackPanelView.axaml(.cs)
├── App.axaml(.cs)                  # Application bootstrap & theme setup
├── ViewLocator.cs                  # Automatic ViewModel → View resolution
└── Program.cs                      # Entry point & Dock JSON serialisation setup
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

`DocumentViewModel` connects to Ollama via `OllamaSharp` and routes messages through `Microsoft.Extensions.AI`. The chat loop supports multi-turn **tool use**:

| Tool | Description |
|---|---|
| `search_tree_nodes` | Finds nodes in the project tree by name |
| `add_tree_node` | Creates a new file or folder at a given path |
| `delete_tree_node` | Removes a node from the project tree |

Tool calls are executed against the live `Tool1ViewModel` tree and results are fed back to the model. The loop runs for up to **10 iterations** per user turn.

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
| `Microsoft.Agents.AI` 1.0.0-rc4 | `Microsoft.Extensions.AI` abstractions |
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