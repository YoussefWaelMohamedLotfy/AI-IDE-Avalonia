using System;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Bootstraps Serilog from the JSON configuration provided by
/// <see cref="AppConfiguration"/> and exposes an <see cref="ILoggerFactory"/>
/// that the rest of the application can use to obtain <see cref="ILogger{T}"/>
/// instances.
/// </summary>
public static class LoggingService
{
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// The application-wide <see cref="ILoggerFactory"/>.
    /// Must not be accessed before <see cref="Initialize"/> has been called.
    /// </summary>
    public static ILoggerFactory LoggerFactory =>
        _loggerFactory ?? throw new InvalidOperationException(
            $"{nameof(LoggingService)} has not been initialized. Call {nameof(Initialize)} first.");

    /// <summary>
    /// Configures Serilog from the <c>Serilog</c> section of <c>appsettings.json</c>
    /// and creates the shared <see cref="ILoggerFactory"/>.
    /// Must be called once at application startup, after <see cref="AppConfiguration.Initialize"/>.
    /// </summary>
    public static void Initialize()
    {
        if (_loggerFactory is not null)
            return;

        var serilogLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(AppConfiguration.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.Logger = serilogLogger;

        _loggerFactory = new SerilogLoggerFactory(serilogLogger, dispose: true);
    }

    /// <summary>
    /// Creates a typed <see cref="ILogger{T}"/> for the specified category.
    /// </summary>
    public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    /// <summary>
    /// Creates an <see cref="ILogger"/> for the specified category name.
    /// </summary>
    public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
        LoggerFactory.CreateLogger(categoryName);

    /// <summary>
    /// Flushes and closes the Serilog logger. Call once on application exit.
    /// Disposing the factory (created with <c>dispose: true</c>) also closes
    /// the underlying Serilog logger.
    /// </summary>
    public static void CloseAndFlush()
    {
        _loggerFactory?.Dispose();
    }
}
