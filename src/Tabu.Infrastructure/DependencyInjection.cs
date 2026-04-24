using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tabu.Domain.Interfaces;
using Tabu.Infrastructure.Logging;
using Tabu.Infrastructure.Persistence;
using Tabu.Infrastructure.Updates;
using Tabu.Infrastructure.Win32;

namespace Tabu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IWindowDetector, WindowDetector>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        // Structured logging: a daily rotated file under
        // %LOCALAPPDATA%\Tabu\logs\app-yyyyMMdd.log. Replaces the
        // legacy CrashLogger static for new code; the static keeps its
        // public surface as a thin facade for backwards compatibility.
        var logDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tabu", "logs");
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(logDirectory));
        });

        return services;
    }
}
