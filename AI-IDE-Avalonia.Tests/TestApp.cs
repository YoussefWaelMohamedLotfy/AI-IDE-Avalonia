using Avalonia;
using Avalonia.Headless;

// Wire up the headless Avalonia platform for tests that render UI.
[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(AI_IDE_Avalonia.Tests.TestApp))]

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Minimal Avalonia <see cref="Application"/> used exclusively by the test suite.
/// The <c>BuildAvaloniaApp()</c> static method is discovered by
/// <see cref="Avalonia.Headless.AvaloniaTestApplicationAttribute"/> to boot the
/// headless renderer without any XAML resources or DI setup.
/// </summary>
internal sealed class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
