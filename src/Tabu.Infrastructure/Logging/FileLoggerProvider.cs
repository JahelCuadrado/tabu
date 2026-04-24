using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tabu.Infrastructure.Logging;

/// <summary>
/// Lightweight, dependency-free <see cref="ILoggerProvider"/> that
/// appends entries to a daily rotated file under
/// <c>%LOCALAPPDATA%\Tabu\logs\app-yyyyMMdd.log</c>.
/// </summary>
/// <remarks>
/// Designed as a drop-in replacement for the v1.6.x <c>CrashLogger</c>
/// static. Log lines preserve the legacy format so user-shared logs
/// from older versions remain readable.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly LogLevel _minLevel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writeLock = new();
    private bool _disposed;

    public FileLoggerProvider(string logDirectory, LogLevel minLevel = LogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        if (_disposed || level < _minLevel) return;

        try
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
            var entry = FormatEntry(category, level, message, exception);

            lock (_writeLock)
            {
                File.AppendAllText(path, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never throw. A locked file or full disk is
            // not a recoverable scenario at this layer.
        }
    }

    private static string FormatEntry(string category, LogLevel level, string message, Exception? exception)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
        var sb = new StringBuilder()
            .Append('[').Append(DateTime.UtcNow.ToString("O")).Append("] ")
            .Append(LevelTag(level)).Append(' ')
            .Append(category).Append(' ')
            .Append('v').AppendLine(version)
            .AppendLine(message);

        if (exception is not null)
        {
            sb.AppendLine(exception.ToString());
        }

        sb.AppendLine(new string('-', 60));
        return sb.ToString();
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "NONE "
    };

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider._minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            _provider.Write(_category, logLevel, message, exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
