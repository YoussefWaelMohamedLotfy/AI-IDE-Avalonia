using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AI_IDE_Avalonia.Models.Documents;

public enum ChatMessageKind { User, Assistant, ToolCall }

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaiting))]
    [NotifyPropertyChangedFor(nameof(IsNotWaiting))]
    private string _content = string.Empty;

    public bool IsUser { get; init; }
    public ChatMessageKind Kind { get; init; } = ChatMessageKind.Assistant;
    public bool IsAssistant => !IsUser && Kind == ChatMessageKind.Assistant;
    public bool IsToolCall => Kind == ChatMessageKind.ToolCall;
    public bool IsWaiting => string.IsNullOrEmpty(Content);
    public bool IsNotWaiting => !string.IsNullOrEmpty(Content);
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
