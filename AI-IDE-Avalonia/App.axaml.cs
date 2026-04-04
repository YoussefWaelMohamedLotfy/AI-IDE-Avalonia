using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Avalonia.Diagnostics.Controls;
using Dock.Avalonia.Diagnostics;
using Dock.Avalonia.Themes;
using Dock.Avalonia.Themes.Fluent;
using AI_IDE_Avalonia.ViewModels;
using AI_IDE_Avalonia.Views;
using AI_IDE_Avalonia.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace AI_IDE_Avalonia;

public partial class App : Application
{
    public static IDockThemeManager? ThemeManager;

    /// <summary>
    /// The application-wide dependency injection container.
    /// Available after <see cref="OnFrameworkInitializationCompleted"/> begins.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        ThemeManager = new DockFluentThemeManager();
#if DOCK_USE_GENERATED_APP_INITIALIZE_COMPONENT
        InitializeComponent();
#else
        AvaloniaXamlLoader.Load(this);
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build the DI container before anything else.
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        // DockManager.s_enableSplitToWindow = true;

        var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();

        // Inject the theme preset ComboBox into the ribbon BEFORE setting DataContext.
        // RebuildTabs() fires when the TabsSource binding resolves (on DataContext assignment),
        // so Content must be populated beforehand or it will be cloned as null.
        if (ThemeManager is { } themeManager)
        {
            var comboBox = new ComboBox
            {
                Width = 160,
                Height = 24,
                FontSize = 11,
                Padding = new Thickness(6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                ItemsSource = themeManager.PresetNames,
                SelectedIndex = themeManager.CurrentPresetIndex
            };
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedIndex >= 0)
                    themeManager.SwitchPreset(comboBox.SelectedIndex);
            };
            mainWindowViewModel.WireThemeContent(comboBox);
        }

        // Inject the language selector ComboBox into the ribbon BEFORE setting DataContext.
        var locService = Services.GetRequiredService<LocalizationService>();
        var langOptions = new[] { "English", "العربية", "Français", "Deutsch", "Español", "Polski" };
        var langComboBox = new ComboBox
        {
            Width = 120,
            Height = 24,
            FontSize = 11,
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = langOptions,
            SelectedIndex = 0,
        };
        langComboBox.SelectionChanged += (_, _) =>
        {
            var culture = langComboBox.SelectedIndex switch
            {
                1 => "ar",
                2 => "fr",
                3 => "de",
                4 => "es",
                5 => "pl",
                _ => "en",
            };
            locService.SetCulture(culture);
        };
        mainWindowViewModel.WireLangContent(langComboBox);

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktopLifetime:
            {
                var splashViewModel = new SplashScreenViewModel();
                var splashWindow = new SplashScreenWindow
                {
                    DataContext = splashViewModel,
                };

                desktopLifetime.MainWindow = splashWindow;
                splashWindow.Show();

                // Flush logging on exit.
                desktopLifetime.Exit += (_, _) =>
                {
                    try
                    {
                        if (Services is IDisposable disposable)
                            disposable.Dispose();
                    }
                    catch
                    {
                        // Best effort — prevent shutdown from failing.
                    }
                };

                // Run background start-up work, then swap to the main window.
                _ = RunStartupAndOpenMainWindowAsync(
                    desktopLifetime, splashWindow, splashViewModel, mainWindowViewModel);

                break;
            }
            case ISingleViewApplicationLifetime singleViewLifetime:
            {
                var mainView = new MainView();
                mainView.DataContext = mainWindowViewModel;

                singleViewLifetime.MainView = mainView;

                break;
            }
        }

        base.OnFrameworkInitializationCompleted();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Configuration — built from appsettings.json next to the executable.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Logging — Serilog reads its config from the "Serilog" section.
        var serilogLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
        Log.Logger = serilogLogger;

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(serilogLogger, dispose: true);
        });

        // Application services.
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<AIProviderService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<RecentFoldersService>();

        // ViewModels.
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<WorkspaceSelectorViewModel>();
    }

    private static async Task RunStartupAndOpenMainWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        SplashScreenWindow splashWindow,
        SplashScreenViewModel splashViewModel,
        MainWindowViewModel mainWindowViewModel)
    {
        try
        {
            // Perform background initialisation while the splash is visible.
            await splashViewModel.RunStartupTasksAsync(splashViewModel.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // User pressed Exit — close the splash and shut the application down cleanly.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                splashWindow.Close();
                desktopLifetime.TryShutdown();
            });
            splashViewModel.Dispose();
            return;
        }
        catch (Exception ex)
        {
            // Unexpected failure during startup — log and still open the main window.
            Services.GetRequiredService<ILogger<App>>()
                .LogError(ex, "Splash startup error");
        }

        // Swap the splash for the workspace-selector window.
        var workspaceViewModel = Services.GetRequiredService<WorkspaceSelectorViewModel>();
        WorkspaceSelectorWindow? workspaceWindow = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            workspaceWindow = new WorkspaceSelectorWindow { DataContext = workspaceViewModel };
            desktopLifetime.MainWindow = workspaceWindow;
            workspaceWindow.Show();
            splashWindow.Close();
        });

        // Wait for the user to pick a folder or skip (this is off the UI thread).
        var selectedWorkspace = await workspaceViewModel.SelectionTask;

        // User pressed ✕ — close the workspace window and shut down cleanly.
        if (workspaceViewModel.ExitRequested)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                workspaceWindow?.Close();
                desktopLifetime.TryShutdown();
            });
            splashViewModel.Dispose();
            workspaceViewModel.Dispose();
            return;
        }

        // If a workspace was chosen, show the loading window while the tree is built.
        WorkspaceLoadingWindow? loadingWindow = null;

        if (selectedWorkspace is not null)
        {
            var loadingViewModel = new WorkspaceLoadingViewModel();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                loadingWindow = new WorkspaceLoadingWindow { DataContext = loadingViewModel };
                desktopLifetime.MainWindow = loadingWindow;
                loadingWindow.Show();
                workspaceWindow?.Close();
                workspaceWindow = null;
            });

            // Progress<T> must be created on the UI thread so its callback marshals back automatically.
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var progress = new Progress<string>(msg => loadingViewModel.AppendStatus(msg));
                await mainWindowViewModel.LoadWorkspaceAsync(selectedWorkspace, progress);
            });
        }

        // All work is done — switch to the main window on the UI thread.
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = mainWindowViewModel;

#if DEBUG
            mainWindow.AttachDockDebug(() => mainWindowViewModel.Layout, new KeyGesture(Key.F11));
            mainWindow.AttachDockDebugOverlay(new KeyGesture(Key.F9));
#endif

            mainWindow.Closing += (_, _) => mainWindowViewModel.CloseLayout();

            desktopLifetime.Exit += (_, _) => mainWindowViewModel.CloseLayout();

            desktopLifetime.MainWindow = mainWindow;
            mainWindow.Show();

            loadingWindow?.Close();
            workspaceWindow?.Close();
        });

        splashViewModel.Dispose();
        workspaceViewModel.Dispose();
    }
}
