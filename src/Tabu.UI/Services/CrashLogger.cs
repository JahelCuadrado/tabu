using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tabu.UI.Services;

/// <summary>
/// Centralised, fire-and-forget exception logger. Captures unhandled
/// exceptions from the UI dispatcher, the AppDomain and unobserved tasks
/// and persists them under <c>%LOCALAPPDATA%\Tabu\logs\</c> so we can
/// diagnose user-reported crashes after the fact without telemetry.
/// </summary>
/// <remarks>
/// Since v1.7.0 this static is a thin facade over
/// <see cref="ILoggerFactory"/>: when the host has wired a factory via
/// <see cref="UseLoggerFactory"/> entries are routed there (and on to
/// the file provider configured in DI). The legacy direct file path is
/// preserved as a fallback when the factory has not been initialised
/// (very early startup, unit tests instantiating WPF types directly).
/// </remarks>
public static class CrashLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LegacyLogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tabu", "logs");

    private static ILoggerFactory _factory = NullLoggerFactory.Instance;
    private static bool _attached;

    /// <summary>
    /// Wires the static facade to the host-provided
    /// <see cref="ILoggerFactory"/>. Call once during <c>OnStartup</c>
    /// after the DI container is built.
    /// </summary>
    public static void UseLoggerFactory(ILoggerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public static void Attach()
    {
        if (_attached) return;
        _attached = true;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        if (System.Windows.Application.Current is { } app)
        {
            app.DispatcherUnhandledException += (_, args) =>
            {
                Log("Dispatcher.UnhandledException", args.Exception);
                // Best-effort: never let UI exceptions tear the bar down
                // mid-session; the user can always close from the system tray.
                args.Handled = true;
            };
        }
    }

    public static void Log(string source, Exception? exception)
    {
        if (exception is null) return;

        // Preferred path: route through ILoggerFactory so logs flow to
        // every registered provider (file, console, future telemetry).
        if (!ReferenceEquals(_factory, NullLoggerFactory.Instance))
        {
            try
            {
                var logger = _factory.CreateLogger(source);
                logger.LogError(exception, "{Source} reported an exception", source);
                return;
            }
            catch
            {
                // Fall through to legacy path so a misconfigured factory
                // never silences crash reporting.
            }
        }

        WriteLegacyEntry(source, exception);
    }

    /// <summary>
    /// Pre-DI fallback: writes a crash entry to the same file format as
    /// v1.6.x so logs from older builds remain readable side-by-side.
    /// </summary>
    private static void WriteLegacyEntry(string source, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LegacyLogDirectory);
            var path = Path.Combine(LegacyLogDirectory, $"crash-{DateTime.UtcNow:yyyyMMdd}.log");

            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
            var entry = new StringBuilder()
                .Append('[').Append(DateTime.UtcNow.ToString("O")).Append("]\n")
                .Append("source : ").AppendLine(source)
                .Append("version: ").AppendLine(version)
                .Append("os     : ").AppendLine(Environment.OSVersion.VersionString)
                .Append("clr    : ").AppendLine(Environment.Version.ToString())
                .AppendLine(exception.ToString())
                .AppendLine(new string('-', 60))
                .ToString();

            lock (SyncRoot)
            {
                File.AppendAllText(path, entry);
            }
        }
        catch
        {
            // Logging is best-effort; never throw from the crash logger.
        }
    }
}
