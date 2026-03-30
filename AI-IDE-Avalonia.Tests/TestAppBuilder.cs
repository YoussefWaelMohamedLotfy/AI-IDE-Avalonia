using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(AI_IDE_Avalonia.Tests.TestAppBuilder))]

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Configures the Avalonia headless platform for all tests in this assembly.
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
}
