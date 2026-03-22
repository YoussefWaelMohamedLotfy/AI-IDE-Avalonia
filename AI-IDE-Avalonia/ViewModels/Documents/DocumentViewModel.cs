using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AI_IDE_Avalonia.Models.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.CommandBars;
using Dock.Model.Mvvm.Controls;
using OllamaSharp;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using IAiChatClient = Microsoft.Extensions.AI.IChatClient;

namespace AI_IDE_Avalonia.ViewModels.Documents;

public partial class DocumentViewModel : Document, IDockCommandBarProvider
{
    private const string OllamaEndpoint = "http://localhost:11434";
    private const string ModelName = "granite4:latest";

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
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        var userText = InputText.Trim();
        InputText = string.Empty;
        IsSending = true;

        var userMsg = new ChatMessage { IsUser = true, Content = userText };
        Messages.Add(userMsg);
        _chatHistory.Add(new AiChatMessage(AiChatRole.User, userText));

        var assistantMsg = new ChatMessage { IsUser = false };
        Messages.Add(assistantMsg);

        try
        {
            await foreach (var update in _chatClient.GetStreamingResponseAsync(_chatHistory, cancellationToken: ct))
            {
                if (update.Text is { Length: > 0 } token)
                    assistantMsg.Content += token;
            }

            _chatHistory.Add(new AiChatMessage(AiChatRole.Assistant, assistantMsg.Content));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            assistantMsg.Content = $"[Error: {ex.Message}]";
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

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
