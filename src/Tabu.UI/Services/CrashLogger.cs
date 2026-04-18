using System.IO;
using System.Reflection;
using System.Text;

namespace Tabu.UI.Services;

/// <summary>
/// Centralised, fire-and-forget exception logger. Captures unhandled
/// exceptions from the UI dispatcher, the AppDomain and unobserved tasks
/// and persists them under <c>%LOCALAPPDATA%\Tabu\logs\</c> so we can
/// diagnose user-reported crashes after the fact without telemetry.
/// </summary>
public static class CrashLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tabu", "logs");

    private static bool _attached;

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

        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"crash-{DateTime.UtcNow:yyyyMMdd}.log");

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
