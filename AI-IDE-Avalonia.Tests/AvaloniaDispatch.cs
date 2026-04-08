using Avalonia.Headless;

namespace AI_IDE_Avalonia.Tests;

/// <summary>
/// Provides a convenient way to execute code on the Avalonia UI dispatcher thread
/// from within TUnit tests, using the shared <see cref="HeadlessUnitTestSession"/>
/// that is started for this test assembly.
/// </summary>
internal static class AvaloniaDispatch
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(System.Reflection.Assembly.GetExecutingAssembly());

    /// <summary>Runs <paramref name="action"/> on the Avalonia UI thread.</summary>
    public static Task RunAsync(Action action) =>
        Session.Dispatch(action, CancellationToken.None);

    /// <summary>Runs <paramref name="func"/> on the Avalonia UI thread and returns its result.</summary>
    public static Task<T> RunAsync<T>(Func<T> func) =>
        Session.Dispatch(func, CancellationToken.None);

    /// <summary>Runs an async <paramref name="func"/> on the Avalonia UI thread and returns its result.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> func) =>
        Session.Dispatch(func, CancellationToken.None);
}
