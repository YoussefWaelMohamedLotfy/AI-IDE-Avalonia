using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI_IDE_Avalonia.ViewModels;

public partial class SplashScreenViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Token that is cancelled when the user clicks the Exit button.</summary>
    public CancellationToken CancellationToken => _cts.Token;

    [ObservableProperty]
    private string _loadingMessage = "Initializing…";

    [ObservableProperty]
    private double _progress;

    /// <summary>
    /// Cancels the loading process and requests application shutdown.
    /// Bound to the Exit button on the splash screen.
    /// </summary>
    [RelayCommand]
    private void Exit() => _cts.Cancel();

    /// <summary>
    /// Simulates the background start-up work the IDE needs to do before
    /// the main window is ready (loading themes, plug-ins, language servers …).
    /// Each step updates <see cref="LoadingMessage"/> and <see cref="Progress"/>
    /// so the splash screen can reflect what is happening.
    /// Throws <see cref="OperationCanceledException"/> if the user cancels.
    /// </summary>
    public async Task RunStartupTasksAsync(CancellationToken cancellationToken = default)
    {
        var steps = new (string Message, double ProgressAfter)[]
        {
            ("Loading themes…",           15),
            ("Loading language services…", 35),
            ("Loading extensions…",        55),
            ("Preparing editor…",          75),
            ("Loading workspace…",         90),
            ("Almost ready…",             100),
        };

        foreach (var (message, progressAfter) in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadingMessage = message;
            await Task.Delay(1500, cancellationToken);
            Progress = progressAfter;
        }
    }

    public void Dispose() => _cts.Dispose();
}
