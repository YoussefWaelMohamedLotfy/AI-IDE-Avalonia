using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
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

using GitHub.Copilot;
using Google.GenAI;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using Ai = Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using IAiChatClient = Microsoft.Extensions.AI.IChatClient;

namespace AI_IDE_Avalonia.ViewModels.Tools;

public sealed partial class Tool5ViewModel : Tool, IAsyncDisposable
{
    private const string OllamaEndpoint = "http://localhost:11434/v1";
    private const string OllamaNativeEndpoint = "http://localhost:11434";
    private const string DefaultOllamaModel = "gemma4:e2b";
    private const string DefaultCopilotModel = "gpt-5-mini";
    private const string DefaultGoogleModel = "gemini-2.5-flash";
    private const int MaxToolIterations = 10;

    private const string SystemInstructions = """
        You are an AI assistant embedded in an IDE.
        You have access to tools that manage the project file tree shown in the explorer panel.
        Use search_tree_nodes to find nodes by name, add_tree_node to create new files or folders, and delete_tree_node to remove nodes.
        Use write_to_document to write generated code or text directly into the editor — always prefer this tool when the user asks you to write, generate, or create code.
        Paths use '/' as separator (e.g. 'MyAIProject/src/Agents').
        IMPORTANT: When calling a tool, you must always provide a brief text explanation first. Do not make a tool call without explaining it in text first.
        """;

    /// <summary>Shared Solution Explorer instance wired by DockFactory; used to create AI tree tools.</summary>
    internal static SolutionExplorerViewModel? SharedSolutionExplorer { get; set; }

    // ── Model caches (populated once at startup) ─────────────────────────────────

    private readonly List<string> _cachedOllamaModels = [];
    private readonly List<string> _cachedCopilotModels = [];
    private readonly List<string> _cachedGoogleModels = [];

    // ── Active IChatClient (Ollama or Google Gemini) ─────────────────────────────

    private IAiChatClient? _activeClient;

    // ── Unified Agent & Session ─────────────────────────────────────────────────

    private AIAgent? _activeAgent;
    private AgentSession? _activeSession;

    // ── GitHub Copilot backend ──────────────────────────────────────────────────

    private CopilotClient? _copilotClient;

    // ── Google Gemini ───────────────────────────────────────────────────────────

    private readonly string _googleApiKey = "AQ.Ab8RN6JH1JwXsM9xM9CQHqzxjSsLvgCAmjUsAYsadzQtzITy8w";

    private readonly List<string> _inputHistory = [];
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
    private string _currentModelLabel = DefaultOllamaModel;

    [ObservableProperty]
    private string _inputWatermark = $"Ask {DefaultOllamaModel} via Ollama anything";

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ObservableCollection<string> AvailableModels { get; } = [];

    [ObservableProperty]
    private string _selectedModel = DefaultOllamaModel;

    [ObservableProperty]
    private bool _isLoadingModels;

    public Tool5ViewModel()
    {
        Title = Loc.AiChatTitle;
        Loc.PropertyChanged += (_, _) => Title = Loc.AiChatTitle;
        ProviderService.ProviderChanged += OnProviderChanged;
        _ = PreloadAllModelsAsync();
    }

    // Singleton services resolved once and cached. Lazy to avoid accessing App.Services
    // before it is initialized (e.g. during design-time preview).

    private AIProviderService ProviderService =>
        field ??= App.Services.GetRequiredService<AIProviderService>();

    private DocumentService DocumentSvc =>
        field ??= App.Services.GetRequiredService<DocumentService>();

    /// <summary>Provides localized strings for the AI chat panel.</summary>
    public LocalizationService Loc =>
        field ??= App.Services.GetRequiredService<LocalizationService>();

    /// <summary>Localized label for tool-call bubbles in the chat.</summary>
    public string ChatToolCallLabel => Loc.ChatToolCall;

    private void OnProviderChanged(object? sender, EventArgs e)
    {
        if (SelectedProvider != ProviderService.SelectedProvider)
        {
            SelectedProvider = ProviderService.SelectedProvider;
        }
        _ = HandleProviderChangedAsync();
    }

    private async Task HandleProviderChangedAsync()
    {
        await DisposeGitHubCopilotAsync();
        DisposeActiveClient();
        _activeAgent = null;
        _activeSession = null;
        Messages.Clear();
        PopulateAvailableModels(ProviderService.SelectedProvider);
        UpdateProviderLabels();
    }

    [ObservableProperty]
    private string _selectedProvider = AIProviderService.AvailableProviders[0];

    public IReadOnlyList<string> AvailableProviders { get; } = AIProviderService.AvailableProviders;

    partial void OnSelectedProviderChanged(string value)
    {
        if (ProviderService.SelectedProvider != value)
            ProviderService.SelectedProvider = value;
    }

    partial void OnSelectedModelChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (ProviderService.SelectedProvider == "Github Copilot")
        {
            _ = DisposeGitHubCopilotAsync();
        }
        else
        {
            DisposeActiveClient();
            _activeClient = CreateChatClient(ProviderService.SelectedProvider, value);
        }
        
        _activeAgent = null;
        _activeSession = null;

        UpdateProviderLabels();
    }

    private async Task PreloadAllModelsAsync(CancellationToken ct = default)
    {
        IsLoadingModels = true;
        try
        {
            var ollamaTask = FetchOllamaModelsAsync(ct);
            var copilotTask = FetchCopilotModelsAsync(ct);
            var googleTask = FetchGoogleModelsAsync(ct);
            await Task.WhenAll(ollamaTask, copilotTask, googleTask);

            // Populate the UI list for whichever provider is currently active.
            PopulateAvailableModels(ProviderService.SelectedProvider);
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    private async Task FetchOllamaModelsAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(OllamaNativeEndpoint) };
            var json = await http.GetFromJsonAsync<JsonElement>("/api/tags", ct);
            _cachedOllamaModels.Clear();
            if (json.TryGetProperty("models", out var arr))
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var name))
                        _cachedOllamaModels.Add(name.GetString()!);
                }
            }
        }
        catch
        {
            // Ollama not running — leave cache empty; fallback applied in PopulateAvailableModels.
        }
    }

    private async Task FetchCopilotModelsAsync(CancellationToken ct)
    {
        try
        {
            using var client = new CopilotClient(new CopilotClientOptions { WorkingDirectory = CopilotCliPath });
            await client.StartAsync(ct);
            var models = await client.ListModelsAsync(ct);
            _cachedCopilotModels.Clear();
            foreach (var m in models)
                _cachedCopilotModels.Add(m.Id);
            await client.StopAsync();
        }
        catch
        {
            // Copilot CLI not running — leave cache empty; fallback applied in PopulateAvailableModels.
        }
    }

    private async Task FetchGoogleModelsAsync(CancellationToken ct)
    {
        _cachedGoogleModels.Clear();
        _cachedGoogleModels.Add("gemini-2.5-flash");
        _cachedGoogleModels.Add("gemini-2.5-pro");
        _cachedGoogleModels.Add("gemini-2.0-flash");
        await Task.CompletedTask;
    }

    private void PopulateAvailableModels(string provider)
    {
        AvailableModels.Clear();

        var source = provider == "Github Copilot" ? _cachedCopilotModels : 
                     provider == "Google Gemini" ? _cachedGoogleModels : 
                     _cachedOllamaModels;
        var fallback = provider == "Github Copilot" ? DefaultCopilotModel : 
                       provider == "Google Gemini" ? DefaultGoogleModel : 
                       DefaultOllamaModel;

        foreach (var m in source)
            AvailableModels.Add(m);

        if (AvailableModels.Count == 0)
            AvailableModels.Add(fallback);

        if (!AvailableModels.Contains(SelectedModel))
            SelectedModel = AvailableModels[0];
    }

    private void UpdateProviderLabels()
    {
        var provider = ProviderService.SelectedProvider;

        // Keep the combo in sync if the service was changed from outside.
        if (SelectedProvider != provider)
            SelectedProvider = provider;

        if (provider == "Github Copilot")
        {
            CurrentModelLabel = SelectedModel;
            InputWatermark = $"Ask GitHub Copilot ({SelectedModel}) anything";
        }
        else if (provider == "Google Gemini")
        {
            CurrentModelLabel = SelectedModel;
            InputWatermark = $"Ask Google Gemini ({SelectedModel}) anything";
        }
        else
        {
            CurrentModelLabel = SelectedModel;
            InputWatermark = $"Ask {SelectedModel} via Ollama anything";
        }
    }

    // ── Unified Agent Creation ──────────────────────────────────────────────────
    private static readonly string CopilotExeFileName =
        RuntimeInformation.RuntimeIdentifier != "win-x64" ? "copilot" : "copilot.exe";
    private static readonly string CopilotCliPath =
        @$".\runtimes\{RuntimeInformation.RuntimeIdentifier}\native\{CopilotExeFileName}";

    private async Task<(AIAgent agent, AgentSession session)> GetOrCreateAgentAsync()
    {
        if (_activeAgent != null && _activeSession != null)
            return (_activeAgent, _activeSession);

        if (ProviderService.SelectedProvider == "Github Copilot")
        {
            _copilotClient = new CopilotClient(new CopilotClientOptions { WorkingDirectory = CopilotCliPath });
            await _copilotClient.StartAsync();

            var sessionConfig = new SessionConfig
            {
                Model = SelectedModel,
                ReasoningEffort = "low",
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

            var copilotAgent = new GitHubCopilotAgent(
                copilotClient: _copilotClient,
                sessionConfig: sessionConfig);

            _activeSession = await copilotAgent.CreateSessionAsync();
            _activeAgent = copilotAgent;
        }
        else
        {
            _activeClient ??= CreateChatClient(ProviderService.SelectedProvider, SelectedModel);
            var tools = BuildTools().OfType<Ai.AITool>().ToList();
            _activeAgent = new ChatClientAgent(_activeClient, "AIAssistant", "IDE Assistant", SystemInstructions, tools);
            _activeSession = await _activeAgent.CreateSessionAsync();
        }

        return (_activeAgent, _activeSession);
    }

    private async Task DisposeGitHubCopilotAsync()
    {
        if (_activeAgent is GitHubCopilotAgent copilotAgent)
        {
            await copilotAgent.DisposeAsync();
        }
        if (_copilotClient != null)
        {
            await _copilotClient.DisposeAsync();
            _copilotClient = null;
        }
        _activeAgent = null;
        _activeSession = null;
    }

    /// <summary>
    /// Creates an <see cref="IAiChatClient"/> for the given provider and model.
    /// Ollama uses the OpenAI SDK pointed at the local /v1 endpoint;
    /// Google Gemini uses the GenAI SDK.
    /// </summary>
    private IAiChatClient CreateChatClient(string provider, string model) => provider switch
    {
        "Google Gemini" => Ai.GoogleGenAIExtensions.AsIChatClient(new Client(apiKey: _googleApiKey), model),
        _ => CreateOllamaClient(model),
    };

    private static IAiChatClient CreateOllamaClient(string model)
    {
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(OllamaEndpoint) });

        return Ai.OpenAIClientExtensions.AsIChatClient(openAiClient.GetChatClient(model));
    }

    private void DisposeActiveClient()
    {
        if (_activeClient is IDisposable disposable)
            disposable.Dispose();
        _activeClient = null;
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
            var (agent, session) = await GetOrCreateAgentAsync();

            ChatMessage? currentAssistantMsg = new ChatMessage { IsUser = false, Kind = ChatMessageKind.Assistant };
            Messages.Add(currentAssistantMsg);

            var assistantText = new StringBuilder();

            await foreach (var update in agent.RunStreamingAsync(userText, session, null, ct))
            {
                // Append text chunks. If we previously added a tool call bubble, we need a new assistant bubble for the final answer.
                if (update.Text is { Length: > 0 })
                {
                    if (currentAssistantMsg == null)
                    {
                        currentAssistantMsg = new ChatMessage { IsUser = false, Kind = ChatMessageKind.Assistant };
                        Dispatcher.UIThread.Post(() => Messages.Add(currentAssistantMsg));
                        assistantText.Clear();
                    }

                    assistantText.Append(update.Text);
                    var snapshot = assistantText.ToString();
                    Dispatcher.UIThread.Post(() => currentAssistantMsg.Content = snapshot);
                }

                // Extract tool calls and usage info from the raw contents
                foreach (var content in update.Contents)
                {
                    if (content is Ai.UsageContent uc)
                    {
                        var inputCount = uc.Details.InputTokenCount;
                        var outputCount = uc.Details.OutputTokenCount;
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (currentAssistantMsg != null)
                            {
                                currentAssistantMsg.InputTokens = inputCount;
                                currentAssistantMsg.OutputTokens = outputCount;
                            }
                        });
                    }
                    else if (content is Ai.FunctionCallContent fcc)
                    {
                        var toolMsg = new ChatMessage
                        {
                            IsUser = false,
                            Kind = ChatMessageKind.ToolCall,
                            Content = $"🔧 {fcc.Name}({FormatToolArgs(fcc.Name, fcc.Arguments)})"
                        };
                        Dispatcher.UIThread.Post(() => Messages.Add(toolMsg));
                        
                        // Set currentAssistantMsg to null so any text after this tool call gets its own bubble BELOW the tool call.
                        currentAssistantMsg = null;
                    }
                    else if (content is Ai.FunctionResultContent frc)
                    {
                        // Append the result to the last tool bubble
                        Dispatcher.UIThread.Post(() =>
                        {
                            var lastTool = Messages.LastOrDefault(m => m.Kind == ChatMessageKind.ToolCall);
                            if (lastTool != null)
                                lastTool.Content += $"\n→ {frc.Result}";
                        });
                    }
                }
            }

            if (assistantText.Length == 0 && currentAssistantMsg != null)
            {
                // Truly empty response or tool-call-only response
                var hasToolCall = Messages.Any(m => m.Kind == ChatMessageKind.ToolCall);
                if (!hasToolCall)
                {
                    Dispatcher.UIThread.Post(() =>
                        currentAssistantMsg.Content = "No response received. The model may still be loading — please send your message again.");
                }
                else
                {
                    Dispatcher.UIThread.Post(() => Messages.Remove(currentAssistantMsg));
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            AppendErrorToLastAssistant(ex.Message);
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
        _activeSession = null; // Forces a new session on next message
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



    // ── Helpers ────────────────────────────────────────────────────────────────

    private void AppendErrorToLastAssistant(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var last = Messages.LastOrDefault(m => m.Kind == ChatMessageKind.Assistant);
            if (last is null) return;
            last.Content = string.IsNullOrWhiteSpace(last.Content)
                ? $"[Error: {message}]"
                : last.Content + $"\n\n[Error: {message}]";
        });
    }

    private List<Ai.AITool> BuildTools()
    {
        var solutionExplorer = SharedSolutionExplorer;
        var tools = new List<Ai.AITool>();

        if (solutionExplorer is not null)
        {
            tools.Add(Ai.AIFunctionFactory.Create(
                new Func<string, string>(query => solutionExplorer.SearchNodes(query)),
                "search_tree_nodes",
                "Search the project file tree for nodes whose name contains the given query string. " +
                "Returns a formatted string containing the matching node paths."));

            tools.Add(Ai.AIFunctionFactory.Create(
                new Func<string, string, bool, string>((parentPath, nodeName, isFolder) =>
                    solutionExplorer.AddNode(parentPath, nodeName, isFolder)),
                "add_tree_node",
                "Add a new file or folder to the project tree. " +
                "Use parentPath='' to add at the root level. " +
                "Set isFolder=true to create a folder, false for a file."));

            tools.Add(Ai.AIFunctionFactory.Create(
                new Func<string, string>(nodePath => solutionExplorer.DeleteNode(nodePath)),
                "delete_tree_node",
                "Delete a node from the project tree by its full path (e.g. 'MyAIProject/src/Agents/ChatAgent.cs')."));
        }

        tools.Add(Ai.AIFunctionFactory.Create(
            new Func<string, string?, Task<string>>(async (text, title) =>
            {
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var doc = DocumentSvc.GetOrCreateDocument(title);
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
        ProviderService.ProviderChanged -= OnProviderChanged;
        await DisposeGitHubCopilotAsync();
        DisposeActiveClient();
    }
}
