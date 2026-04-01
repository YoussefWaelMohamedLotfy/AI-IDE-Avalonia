using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace AI_IDE_Avalonia.ViewModels;

public partial class SplashScreenViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _loadingMessage = "Initializing…";

    [ObservableProperty]
    private double _progress;

    /// <summary>
    /// Simulates the background start-up work the IDE needs to do before
    /// the main window is ready (loading themes, plug-ins, language servers …).
    /// Each step updates <see cref="LoadingMessage"/> and <see cref="Progress"/>
    /// so the splash screen can reflect what is happening.
    /// </summary>
    public async Task RunStartupTasksAsync()
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
            LoadingMessage = message;
            await Task.Delay(300);
            Progress = progressAfter;
        }
    }
}
