using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_IDE_Avalonia.Models.Documents;
using AI_IDE_Avalonia.Services;
using AI_IDE_Avalonia.ViewModels.Tools;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.CommandBars;
using Dock.Model.Mvvm.Controls;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using OllamaSharp;
using Ai = Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using IAiChatClient = Microsoft.Extensions.AI.IChatClient;

namespace AI_IDE_Avalonia.ViewModels.Documents;

public partial class DocumentViewModel : Document, IDockCommandBarProvider, IAsyncDisposable
{
    private const string OllamaEndpoint = "http://localhost:11434";
    private const string ModelName = "granite4:latest";
    private const int MaxToolIterations = 10;

    private const string SystemInstructions =
        "You are an AI assistant embedded in an IDE. " +
        "You have access to tools that manage the project file tree shown in the explorer panel. " +
        "Use search_tree_nodes to find nodes by name, add_tree_node to create new files or folders, " +
        "and delete_tree_node to remove nodes. " +
        "Paths use '/' as separator (e.g. 'MyAIProject/src/Agents').";

    /// <summary>Shared Tool1 instance wired by DockFactory; used to create AI tree tools.</summary>
    internal static Tool1ViewModel? SharedTool1 { get; set; }

    // ── Ollama backend ──────────────────────────────────────────────────────────

    private readonly IAiChatClient _ollamaClient;
    private readonly List<AiChatMessage> _chatHistory = new();

    // ── GitHub Copilot backend ──────────────────────────────────────────────────

    private CopilotClient? _copilotClient;
    private GitHubCopilotAgent? _copilotAgent;
    private AgentSession? _copilotSession;

    // ── State ───────────────────────────────────────────────────────────────────

    private int _renameCounter;
    private readonly RelayCommand _toggleModifiedCommand;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _closeCommand;

    // ── Input history ────────────────────────────────────────────────────────────

    private readonly List<string> _inputHistory = new();
    private int _historyIndex = -1;
    private string _pendingInput = string.Empty;
    private bool _navigating;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    partial void OnInputTextChanged(string value)
    {
        if (!_navigating)
            _historyIndex = -1;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    [ObservableProperty]
    private string _currentModelLabel = ModelName;

    [ObservableProperty]
    private string _inputWatermark = $"Ask {ModelName} via Ollama anything";

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public DocumentViewModel()
    {
        _toggleModifiedCommand = new RelayCommand(ToggleModified);
        _renameCommand = new RelayCommand(RenameDocument);
        _closeCommand = new RelayCommand(CloseDocument);

        _ollamaClient = new OllamaApiClient(new Uri(OllamaEndpoint), ModelName);

        _chatHistory.Add(new AiChatMessage(AiChatRole.System, SystemInstructions));

        AIProviderService.Instance.ProviderChanged += OnProviderChanged;
    }

    private void OnProviderChanged(object? sender, EventArgs e) =>
        _ = HandleProviderChangedAsync();

    private async Task HandleProviderChangedAsync()
    {
        await DisposeGitHubCopilotAsync();

        _chatHistory.Clear();
        _chatHistory.Add(new AiChatMessage(AiChatRole.System, SystemInstructions));
        Messages.Clear();

        UpdateProviderLabels();
    }

    private void UpdateProviderLabels()
    {
        if (AIProviderService.Instance.SelectedProvider == "Github Copilot")
        {
            CurrentModelLabel = "GitHub Copilot";
            InputWatermark = "Ask GitHub Copilot anything";
        }
        else
        {
            CurrentModelLabel = ModelName;
            InputWatermark = $"Ask {ModelName} via Ollama anything";
        }
    }

    // ── GitHub Copilot lazy init ────────────────────────────────────────────────

    private const string CopilotCliPath =
        @"C:\Users\youss\AppData\Local\Microsoft\WinGet\Packages\GitHub.Copilot_Microsoft.Winget.Source_8wekyb3d8bbwe\copilot.exe";

    private async Task<(GitHubCopilotAgent agent, AgentSession session)> GetCopilotBackendAsync()
    {
        if (_copilotAgent != null && _copilotSession != null)
            return (_copilotAgent, _copilotSession);

        _copilotClient = new CopilotClient(new CopilotClientOptions { CliPath = CopilotCliPath });
        await _copilotClient.StartAsync();

        var sessionConfig = new SessionConfig
        {
            Model = "gpt-5-mini",
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = SystemInstructions,
            },
            Tools = [.. BuildTools().OfType<Ai.AIFunction>()],
            Hooks = new SessionHooks
            {
                OnPostToolUse = (input, _) =>
                {
                    var argsJson = input.ToolArgs is null ? string.Empty
                        : JsonSerializer.Serialize(input.ToolArgs);
                    var resultJson = input.ToolResult is null ? string.Empty
                        : JsonSerializer.Serialize(input.ToolResult);

                    var toolMsg = new ChatMessage
                    {
                        IsUser = false,
                        Kind = ChatMessageKind.ToolCall,
                        Content = $"🔧 {input.ToolName}({argsJson})\n→ {resultJson}",
                    };
                    Dispatcher.UIThread.Post(() => Messages.Add(toolMsg));
                    return Task.FromResult<PostToolUseHookOutput?>(new PostToolUseHookOutput());
                },
            },
        };

        _copilotAgent = new GitHubCopilotAgent(
            copilotClient: _copilotClient,
            sessionConfig: sessionConfig);

        _copilotSession = await _copilotAgent.CreateSessionAsync();
        return (_copilotAgent, _copilotSession);
    }

    private async Task DisposeGitHubCopilotAsync()
    {
        if (_copilotAgent != null)
        {
            await _copilotAgent.DisposeAsync();
            _copilotAgent = null;
        }
        if (_copilotClient != null)
        {
            await _copilotClient.DisposeAsync();
            _copilotClient = null;
        }
        _copilotSession = null;
    }

    // ── Send command ───────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        var userText = InputText.Trim();
        InputText = string.Empty;
        IsSending = true;

        if (_inputHistory.Count == 0 || _inputHistory[^1] != userText)
            _inputHistory.Add(userText);
        _historyIndex = -1;
        _pendingInput = string.Empty;

        var userMsg = new ChatMessage { IsUser = true, Kind = ChatMessageKind.User, Content = userText };
        Messages.Add(userMsg);

        try
        {
            if (AIProviderService.Instance.SelectedProvider == "Github Copilot")
                await SendWithGitHubCopilotAsync(userText, ct);
            else
                await SendWithOllamaAsync(userText, ct);
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

    internal void NavigateHistoryUp()
    {
        if (_inputHistory.Count == 0) return;

        if (_historyIndex == -1)
        {
            _pendingInput = InputText;
            _historyIndex = _inputHistory.Count - 1;
        }
        else if (_historyIndex > 0)
        {
            _historyIndex--;
        }

        _navigating = true;
        InputText = _inputHistory[_historyIndex];
        _navigating = false;
    }

    internal void NavigateHistoryDown()
    {
        if (_historyIndex == -1) return;

        if (_historyIndex < _inputHistory.Count - 1)
        {
            _historyIndex++;
            _navigating = true;
            InputText = _inputHistory[_historyIndex];
            _navigating = false;
        }
        else
        {
            _historyIndex = -1;
            _navigating = true;
            InputText = _pendingInput;
            _navigating = false;
        }
    }

    // ── Ollama send ────────────────────────────────────────────────────────────

    private async Task SendWithOllamaAsync(string userText, CancellationToken ct)
    {
        _chatHistory.Add(new AiChatMessage(AiChatRole.User, userText));

        var tools = BuildTools();
        var options = tools.Count > 0
            ? new Ai.ChatOptions { Tools = tools, ToolMode = Ai.ChatToolMode.Auto }
            : null;

        try
        {
            for (var iteration = 0; iteration < MaxToolIterations && !ct.IsCancellationRequested; iteration++)
            {
                var assistantMsg = new ChatMessage { IsUser = false, Kind = ChatMessageKind.Assistant };
                Messages.Add(assistantMsg);

                var pendingCalls = new List<Ai.FunctionCallContent>();
                var assistantText = new StringBuilder();

                await foreach (var update in _ollamaClient.GetStreamingResponseAsync(_chatHistory, options, ct))
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is Ai.TextContent tc && tc.Text is { Length: > 0 })
                        {
                            assistantText.Append(tc.Text);
                            assistantMsg.Content = assistantText.ToString();
                        }
                        else if (content is Ai.FunctionCallContent fcc)
                        {
                            pendingCalls.Add(fcc);
                        }
                    }
                }

                var historyContents = new List<Ai.AIContent>();
                if (assistantText.Length > 0)
                    historyContents.Add(new Ai.TextContent(assistantText.ToString()));
                historyContents.AddRange(pendingCalls);
                _chatHistory.Add(new AiChatMessage(AiChatRole.Assistant, historyContents));

                if (pendingCalls.Count == 0)
                    break;

                var toolFunctions = tools.OfType<Ai.AIFunction>().ToDictionary(f => f.Name);
                var toolResultContents = new List<Ai.AIContent>();

                foreach (var call in pendingCalls)
                {
                    object? result;
                    if (toolFunctions.TryGetValue(call.Name, out var func))
                    {
                        try
                        {
                            result = await func.InvokeAsync(
                                new(call.Arguments!), ct);
                        }
                        catch (Exception ex)
                        {
                            result = $"Error executing tool: {ex.Message}";
                        }
                    }
                    else
                    {
                        result = $"Error: tool '{call.Name}' not found.";
                    }

                    var toolMsg = new ChatMessage
                    {
                        IsUser = false,
                        Kind = ChatMessageKind.ToolCall,
                        Content = $"🔧 {call.Name}({FormatArgs(call.Arguments)})\n→ {result}"
                    };
                    Messages.Add(toolMsg);
                    toolResultContents.Add(new Ai.FunctionResultContent(call.CallId, result));
                }

                _chatHistory.Add(new AiChatMessage(AiChatRole.Tool, toolResultContents));
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            AppendErrorToLastAssistant(ex.Message);
        }
    }

    // ── GitHub Copilot send ────────────────────────────────────────────────────

    private async Task SendWithGitHubCopilotAsync(string userText, CancellationToken ct)
    {
        var assistantMsg = new ChatMessage { IsUser = false, Kind = ChatMessageKind.Assistant };
        Messages.Add(assistantMsg);

        try
        {
            var (agent, session) = await GetCopilotBackendAsync();
            var assistantText = new StringBuilder();

            await foreach (var update in agent.RunStreamingAsync(userText, session, null, ct))
            {
                var text = update.Text;
                if (text is { Length: > 0 })
                {
                    assistantText.Append(text);
                    assistantMsg.Content = assistantText.ToString();
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            AppendErrorToLastAssistant(ex.Message);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void AppendErrorToLastAssistant(string message)
    {
        var last = Messages.LastOrDefault(m => m.Kind == ChatMessageKind.Assistant);
        last?.Content = string.IsNullOrWhiteSpace(last.Content)
                ? $"[Error: {message}]"
                : last.Content + $"\n\n[Error: {message}]";
    }

    private static List<Ai.AITool> BuildTools()
    {
        var tool1 = SharedTool1;
        return tool1 is null
            ? []
            : [
            Ai.AIFunctionFactory.Create(
                new Func<string, string>(query => tool1.SearchNodes(query)),
                "search_tree_nodes",
                "Search the project file tree for nodes whose name contains the given query string. " +
                "Returns matching node paths separated by '/'."),

            Ai.AIFunctionFactory.Create(
                new Func<string, string, bool, string>((parentPath, nodeName, isFolder) =>
                    tool1.AddNode(parentPath, nodeName, isFolder)),
                "add_tree_node",
                "Add a new file or folder to the project tree. " +
                "Use parentPath='' to add at the root level. " +
                "Set isFolder=true to create a folder, false for a file."),

            Ai.AIFunctionFactory.Create(
                new Func<string, string>(nodePath => tool1.DeleteNode(nodePath)),
                "delete_tree_node",
                "Delete a node from the project tree by its full path (e.g. 'MyAIProject/src/Agents/ChatAgent.cs')."),
        ];
    }

    private static string FormatArgs(IDictionary<string, object?>? args)
    {
        if (args is null or { Count: 0 }) return string.Empty;
        return string.Join(", ", args.Select(kv => $"{kv.Key}: {kv.Value}"));
    }

    // ── Command bars ──────────────────────────────────────────────────────────

    public event EventHandler? CommandBarsChanged;

    public IReadOnlyList<DockCommandBarDefinition> GetCommandBars()
    {
        var displayTitle = IsModified ? $"{Title}*" : Title;

        var menuItems = new List<DockCommandBarItem>
        {
            new("_Document")
            {
                Items =
                [
                    new($"Active: {displayTitle}") { Order = 0 },
                    new("_Toggle Modified") { Command = _toggleModifiedCommand, Order = 1 },
                    new("_Rename") { Command = _renameCommand, Order = 2 },
                    new(null) { IsSeparator = true, Order = 3 },
                    new("_Close") { Command = _closeCommand, Order = 4 }
                ]
            }
        };

        var toolItems = new List<DockCommandBarItem>
        {
            new("Toggle Modified") { Command = _toggleModifiedCommand, Order = 0 },
            new("Rename") { Command = _renameCommand, Order = 1 },
            new("Close") { Command = _closeCommand, Order = 2 }
        };

        return
        [
            new("DocumentMenu", DockCommandBarKind.Menu)
            {
                Order = 0,
                Items = menuItems
            },
            new("DocumentToolBar", DockCommandBarKind.ToolBar)
            {
                Order = 1,
                Items = toolItems
            }
        ];
    }

    private void ToggleModified()
    {
        IsModified = !IsModified;
        RaiseCommandBarsChanged();
    }

    private void RenameDocument()
    {
        _renameCounter++;
        Title = $"{Id} ({_renameCounter})";
        RaiseCommandBarsChanged();
    }

    private void CloseDocument()
    {
        if (!CanClose)
            return;

        Factory?.CloseDockable(this);
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        AIProviderService.Instance.ProviderChanged -= OnProviderChanged;
        await DisposeGitHubCopilotAsync();
        if (_ollamaClient is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_ollamaClient is IDisposable disposable)
            disposable.Dispose();
    }

    private void RaiseCommandBarsChanged()
    {
        CommandBarsChanged?.Invoke(this, EventArgs.Empty);
    }
}