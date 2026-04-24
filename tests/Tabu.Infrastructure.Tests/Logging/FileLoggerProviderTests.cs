using FluentAssertions;
using Microsoft.Extensions.Logging;
using Tabu.Infrastructure.Logging;
using Xunit;

namespace Tabu.Infrastructure.Tests.Logging;

/// <summary>
/// Verifies the contract of the lightweight file-backed
/// <see cref="ILoggerProvider"/> introduced in v1.7.0: daily file
/// rotation, level filtering, exception capture and resilience to
/// disposed state.
/// </summary>
public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly string _logDirectory;

    public FileLoggerProviderTests()
    {
        _logDirectory = Path.Combine(Path.GetTempPath(), "TabuLoggerTests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            try { Directory.Delete(_logDirectory, recursive: true); }
            catch { /* best-effort test cleanup */ }
        }
    }

    [Fact]
    public void Log_WritesEntryUnderTodaysFile()
    {
        using var provider = new FileLoggerProvider(_logDirectory);
        var logger = provider.CreateLogger("Test.Category");

        logger.LogInformation("hello {Name}", "world");

        var expectedFile = Path.Combine(_logDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
        File.Exists(expectedFile).Should().BeTrue();

        var contents = File.ReadAllText(expectedFile);
        contents.Should().Contain("hello world");
        contents.Should().Contain("Test.Category");
        contents.Should().Contain("INFO");
    }

    [Fact]
    public void Log_IncludesExceptionDetails()
    {
        using var provider = new FileLoggerProvider(_logDirectory);
        var logger = provider.CreateLogger("Update.IntegrityCheck");
        var ex = new InvalidOperationException("boom");

        logger.LogError(ex, "download failed");

        var file = Path.Combine(_logDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
        var contents = File.ReadAllText(file);
        contents.Should().Contain("ERROR");
        contents.Should().Contain("boom");
        contents.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void Log_BelowMinLevelIsSuppressed()
    {
        using var provider = new FileLoggerProvider(_logDirectory, LogLevel.Warning);
        var logger = provider.CreateLogger("Filtered");

        logger.LogInformation("should not appear");
        logger.LogWarning("must appear");

        var file = Path.Combine(_logDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
        var contents = File.ReadAllText(file);
        contents.Should().NotContain("should not appear");
        contents.Should().Contain("must appear");
    }

    [Fact]
    public void CreateLogger_ReturnsSameInstancePerCategory()
    {
        using var provider = new FileLoggerProvider(_logDirectory);
        var a = provider.CreateLogger("Same");
        var b = provider.CreateLogger("Same");

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Log_AfterDispose_DoesNotThrowOrWrite()
    {
        var provider = new FileLoggerProvider(_logDirectory);
        var logger = provider.CreateLogger("Disposed");
        provider.Dispose();

        var act = () => logger.LogInformation("post-dispose");
        act.Should().NotThrow();

        var file = Path.Combine(_logDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
        File.Exists(file).Should().BeFalse();
    }
}
