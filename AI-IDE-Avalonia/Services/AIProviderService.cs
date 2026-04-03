using System;

namespace AI_IDE_Avalonia.Services;

public sealed class AIProviderService
{
    public static readonly string[] AvailableProviders = ["Ollama", "Github Copilot"];

    private string _selectedProvider = AvailableProviders[0];

    public AIProviderService() { }

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (_selectedProvider == value) return;
            _selectedProvider = value;
            ProviderChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ProviderChanged;
}
