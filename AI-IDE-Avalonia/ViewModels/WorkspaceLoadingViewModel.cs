using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AI_IDE_Avalonia.ViewModels;

public partial class WorkspaceLoadingViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusLog = string.Empty;

    /// <summary>
    /// Appends <paramref name="message"/> as a new line in <see cref="StatusLog"/>.
    /// Must be called on the UI thread.
    /// </summary>
    public void AppendStatus(string message)
    {
        StatusLog = string.IsNullOrEmpty(StatusLog)
            ? message
            : StatusLog + Environment.NewLine + message;
    }
}
