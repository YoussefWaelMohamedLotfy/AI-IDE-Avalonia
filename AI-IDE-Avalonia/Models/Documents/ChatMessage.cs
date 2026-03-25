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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTokenUsage))]
    [NotifyPropertyChangedFor(nameof(TokenLabel))]
    private long? _inputTokens;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTokenUsage))]
    [NotifyPropertyChangedFor(nameof(TokenLabel))]
    private long? _outputTokens;

    public bool IsUser { get; init; }
    public ChatMessageKind Kind { get; init; } = ChatMessageKind.Assistant;
    public bool IsAssistant => !IsUser && Kind == ChatMessageKind.Assistant;
    public bool IsToolCall => Kind == ChatMessageKind.ToolCall;
    public bool IsWaiting => string.IsNullOrEmpty(Content);
    public bool IsNotWaiting => !string.IsNullOrEmpty(Content);
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public bool HasTokenUsage => InputTokens.HasValue || OutputTokens.HasValue;

    public string TokenLabel =>
        $"↑ {InputTokens?.ToString() ?? "?"} in  ↓ {OutputTokens?.ToString() ?? "?"} out";
}
