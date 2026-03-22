using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AI_IDE_Avalonia.Models.Documents;

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _content = string.Empty;

    public bool IsUser { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
