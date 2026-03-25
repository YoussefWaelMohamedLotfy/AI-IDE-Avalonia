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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using OllamaSharp;
using Ai = Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using IAiChatClient = Microsoft.Extensions.AI.IChatClient;

namespace AI_IDE_Avalonia.ViewModels.Tools;

public partial class Tool5ViewModel : Tool, IAsyncDisposable
{
    private const string OllamaEndpoint = "http://localhost:11434";
    private const string ModelName = "granite4:latest";
    private const int MaxToolIterations = 10;

    private const string SystemInstructions =
        "You are an AI assistant embedded in an IDE. " +
        "You have access to tools that manage the project file tree shown in the explorer panel. " +
        "Use search_tree_nodes to find nodes by name, add_tree_node to create new files or folders, " +
        "and delete_tree_node to remove nodes. " +
        "Use write_to_document to write generated code or text directly into the editor — " +
        "always prefer this tool when the user asks you to write, generate, or create code. " +
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

    public Tool5ViewModel()
    {
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

    [ObservableProperty]
    private string _selectedProvider = AIProviderService.Instance.SelectedProvider;

    public IReadOnlyList<string> AvailableProviders { get; } = AIProviderService.AvailableProviders;

    partial void OnSelectedProviderChanged(string value)
    {
        if (AIProviderService.Instance.SelectedProvider != value)
            AIProviderService.Instance.SelectedProvider = value;
    }

    private void UpdateProviderLabels()
    {
        var provider = AIProviderService.Instance.SelectedProvider;

        // Keep the combo in sync if the service was changed from outside.
        if (SelectedProvider != provider)
            SelectedProvider = provider;

        if (provider == "Github Copilot")
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
                    var argsDisplay = input.ToolArgs is null ? string.Empty
                        : FormatToolArgsJson(input.ToolName, JsonSerializer.Serialize(input.ToolArgs));
                    var resultJson = input.ToolResult is null ? string.Empty
                        : JsonSerializer.Serialize(input.ToolResult);

                    var toolMsg = new ChatMessage
                    {
                        IsUser = false,
                        Kind = ChatMessageKind.ToolCall,
                        Content = $"🔧 {input.ToolName}({argsDisplay})\n→ {resultJson}",
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

    [RelayCommand]
    private void CancelSend() => SendCommand.Cancel();

    [RelayCommand]
    private void Clear()
    {
        Messages.Clear();
        _chatHistory.Clear();
        _chatHistory.Add(new AiChatMessage(AiChatRole.System, SystemInstructions));
    }

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

                // If the model produced no text (only tool calls), remove the empty placeholder bubble.
                if (assistantText.Length == 0)
                    Dispatcher.UIThread.Post(() => Messages.Remove(assistantMsg));

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
                            result = await func.InvokeAsync(new(call.Arguments!), ct);
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
                        Content = $"🔧 {call.Name}({FormatToolArgs(call.Name, call.Arguments)})\n→ {result}"
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

            // If the agent only made tool calls and produced no text, remove the empty placeholder.
            if (assistantText.Length == 0)
                Dispatcher.UIThread.Post(() => Messages.Remove(assistantMsg));
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
        var tools = new List<Ai.AITool>();

        if (tool1 is not null)
        {
            tools.Add(Ai.AIFunctionFactory.Create(
                new Func<string, string>(query => tool1.SearchNodes(query)),
                "search_tree_nodes",
                "Search the project file tree for nodes whose name contains the given query string. " +
                "Returns matching node paths separated by '/'."));

            tools.Add(Ai.AIFunctionFactory.Create(
                new Func<string, string, bool, string>((parentPath, nodeName, isFolder) =>
                    tool1.AddNode(parentPath, nodeName, isFolder)),
                "add_tree_node",
                "Add a new file or folder to the project tree. " +
                "Use parentPath='' to add at the root level. " +
                "Set isFolder=true to create a folder, false for a file."));

            tools.Add(Ai.AIFunctionFactory.Create(
                new Func<string, string>(nodePath => tool1.DeleteNode(nodePath)),
                "delete_tree_node",
                "Delete a node from the project tree by its full path (e.g. 'MyAIProject/src/Agents/ChatAgent.cs')."));
        }

        tools.Add(Ai.AIFunctionFactory.Create(
            new Func<string, string?, Task<string>>(async (text, title) =>
            {
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var doc = DocumentService.Instance.GetOrCreateDocument(title);
                    if (title is not null)
                        doc.Title = title;
                    doc.DocumentText = UnescapeText(text);
                    return $"Written {doc.DocumentText.Length} characters to document '{doc.Title}'.";
                });
            }),
            "write_to_document",
            "Write or overwrite text and code in the active document editor. " +
            "Opens a new document tab automatically if none is open. " +
            "Provide 'title' to name the document (e.g. 'Program.cs', 'notes.md'). " +
            "Always use this tool when the user asks you to write, generate, or create any code or text."));

        return tools;
    }

    private static string FormatToolArgs(string toolName, IDictionary<string, object?>? args)
    {
        if (args is null or { Count: 0 }) return string.Empty;

        // For write_to_document, replace the full text payload with a compact summary.
        if (toolName == "write_to_document")
        {
            var parts = new List<string>();
            foreach (var kv in args)
            {
                if (kv.Key == "text")
                {
                    var len = kv.Value?.ToString()?.Length ?? 0;
                    parts.Add($"text: <{len} chars>");
                }
                else
                {
                    parts.Add($"{kv.Key}: {kv.Value}");
                }
            }
            return string.Join(", ", parts);
        }

        return string.Join(", ", args.Select(kv => $"{kv.Key}: {kv.Value}"));
    }

    /// <summary>
    /// Same redaction logic for the Copilot path, which provides args as a JSON string.
    /// </summary>
    private static string FormatToolArgsJson(string toolName, string argsJson)
    {
        if (toolName != "write_to_document") return argsJson;

        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var parts = new List<string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "text")
                {
                    var len = prop.Value.GetString()?.Length ?? 0;
                    parts.Add($"text: <{len} chars>");
                }
                else
                {
                    parts.Add($"{prop.Name}: {prop.Value}");
                }
            }
            return string.Join(", ", parts);
        }
        catch
        {
            return argsJson; // fall back to raw JSON if parsing fails
        }
    }

    /// <summary>
    /// Converts literal escape sequences (e.g. the two characters '\' + 'n') that some
    /// AI models emit inside tool-call arguments into their real Unicode equivalents.
    /// Sequences that are already proper characters are left untouched.
    /// </summary>
    private static string UnescapeText(string text)
    {
        // Fast-path: if there is no backslash at all, nothing to do.
        if (!text.Contains('\\'))
            return text;

        return text
            .Replace("\\r\\n", "\r\n")   // CRLF first so it isn't split by the rules below
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");      // un-double any escaped backslashes last
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
}
