using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI_IDE_Avalonia.Models.Documents;
using AI_IDE_Avalonia.ViewModels.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.CommandBars;
using Dock.Model.Mvvm.Controls;
using OllamaSharp;
using Ai = Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using IAiChatClient = Microsoft.Extensions.AI.IChatClient;

namespace AI_IDE_Avalonia.ViewModels.Documents;

public partial class DocumentViewModel : Document, IDockCommandBarProvider
{
    private const string OllamaEndpoint = "http://localhost:11434";
    private const string ModelName = "granite4:latest";
    private const int MaxToolIterations = 10;

    /// <summary>Shared Tool1 instance wired by DockFactory; used to create AI tree tools.</summary>
    internal static Tool1ViewModel? SharedTool1 { get; set; }

    private int _renameCounter;
    private readonly RelayCommand _toggleModifiedCommand;
    private readonly RelayCommand _renameCommand;
    private readonly RelayCommand _closeCommand;
    private readonly IAiChatClient _chatClient;
    private readonly List<AiChatMessage> _chatHistory = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public DocumentViewModel()
    {
        _toggleModifiedCommand = new RelayCommand(ToggleModified);
        _renameCommand = new RelayCommand(RenameDocument);
        _closeCommand = new RelayCommand(CloseDocument);

        var ollama = new OllamaApiClient(new Uri(OllamaEndpoint), ModelName);
        _chatClient = ollama;

        _chatHistory.Add(new AiChatMessage(AiChatRole.System,
            "You are an AI assistant embedded in an IDE. " +
            "You have access to tools that manage the project file tree shown in the explorer panel. " +
            "Use search_tree_nodes to find nodes by name, add_tree_node to create new files or folders, " +
            "and delete_tree_node to remove nodes. " +
            "Paths use '/' as separator (e.g. 'MyAIProject/src/Agents')."));
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        var userText = InputText.Trim();
        InputText = string.Empty;
        IsSending = true;

        var userMsg = new ChatMessage { IsUser = true, Kind = ChatMessageKind.User, Content = userText };
        Messages.Add(userMsg);
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

                await foreach (var update in _chatClient.GetStreamingResponseAsync(_chatHistory, options, ct))
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

                // Record assistant turn (text + any function-call requests)
                var historyContents = new List<Ai.AIContent>();
                if (assistantText.Length > 0)
                    historyContents.Add(new Ai.TextContent(assistantText.ToString()));
                historyContents.AddRange(pendingCalls);
                _chatHistory.Add(new AiChatMessage(AiChatRole.Assistant, historyContents));

                if (pendingCalls.Count == 0)
                    break; // No tool calls — conversation turn is complete

                // Execute each tool and feed results back
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
                                new Ai.AIFunctionArguments(call.Arguments!), ct);
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
            var lastAssistant = Messages.LastOrDefault(m => m.Kind == ChatMessageKind.Assistant);
            if (lastAssistant is not null)
            {
                lastAssistant.Content = string.IsNullOrWhiteSpace(lastAssistant.Content)
                    ? $"[Error: {ex.Message}]"
                    : lastAssistant.Content + $"\n\n[Error: {ex.Message}]";
            }
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

    // ── Tool construction ──────────────────────────────────────────────────────

    private static List<Ai.AITool> BuildTools()
    {
        var tool1 = SharedTool1;
        if (tool1 is null) return [];

        return
        [
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
            new DockCommandBarItem("_Document")
            {
                Items = new List<DockCommandBarItem>
                {
                    new DockCommandBarItem($"Active: {displayTitle}") { Order = 0 },
                    new DockCommandBarItem("_Toggle Modified") { Command = _toggleModifiedCommand, Order = 1 },
                    new DockCommandBarItem("_Rename") { Command = _renameCommand, Order = 2 },
                    new DockCommandBarItem(null) { IsSeparator = true, Order = 3 },
                    new DockCommandBarItem("_Close") { Command = _closeCommand, Order = 4 }
                }
            }
        };

        var toolItems = new List<DockCommandBarItem>
        {
            new DockCommandBarItem("Toggle Modified") { Command = _toggleModifiedCommand, Order = 0 },
            new DockCommandBarItem("Rename") { Command = _renameCommand, Order = 1 },
            new DockCommandBarItem("Close") { Command = _closeCommand, Order = 2 }
        };

        return new List<DockCommandBarDefinition>
        {
            new DockCommandBarDefinition("DocumentMenu", DockCommandBarKind.Menu)
            {
                Order = 0,
                Items = menuItems
            },
            new DockCommandBarDefinition("DocumentToolBar", DockCommandBarKind.ToolBar)
            {
                Order = 1,
                Items = toolItems
            }
        };
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

    private void RaiseCommandBarsChanged()
    {
        CommandBarsChanged?.Invoke(this, EventArgs.Empty);
    }
}
