using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace AI_IDE_Avalonia.Services;

/// <summary>
/// Loads and exposes the application configuration built from appsettings.json.
/// </summary>
public static class AppConfiguration
{
    private static IConfiguration? _configuration;

    /// <summary>
    /// The loaded <see cref="IConfiguration"/> instance.
    /// Must not be accessed before <see cref="Initialize"/> has been called.
    /// </summary>
    public static IConfiguration Configuration =>
        _configuration ?? throw new InvalidOperationException(
            $"{nameof(AppConfiguration)} has not been initialized. Call {nameof(Initialize)} first.");

    /// <summary>
    /// Builds the configuration from <c>appsettings.json</c> located next to the executable.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public static void Initialize()
    {
        if (_configuration is not null)
            return;

        var basePath = AppContext.BaseDirectory;

        _configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }
}
