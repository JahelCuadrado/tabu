using FluentAssertions;
using Tabu.Domain.Entities;
using Tabu.Infrastructure.Persistence;
using Xunit;

namespace Tabu.Infrastructure.Tests.Persistence;

/// <summary>
/// Round-trip and resilience tests for the JSON-backed settings store.
/// Each test isolates state in a unique temp file under
/// <see cref="Path.GetTempPath"/> and removes it on dispose so the suite
/// can run in parallel without cross-talk.
/// </summary>
public sealed class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonSettingsRepositoryTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"tabu-settings-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { File.Delete(_tempFile); } catch { /* best-effort */ }
    }

    [Fact]
    public void Load_ReturnsDefaultSettings_WhenFileDoesNotExist()
    {
        var sut = new JsonSettingsRepository(_tempFile);

        var settings = sut.Load();

        settings.Should().NotBeNull();
        settings.AppTheme.Should().Be("System");
        settings.BarOpacity.Should().Be(1.0);
        settings.AccentColor.Should().Be("blue");
        settings.AutoCheckUpdates.Should().BeTrue();
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryPropertyExactly()
    {
        var saved = new UserSettings
        {
            IsBarOnAllMonitors = true,
            IsDetectSameScreenOnly = true,
            AppTheme = "Dark",
            BarOpacity = 0.75,
            UseFixedTabWidth = false,
            ShowBranding = false,
            Language = "es",
            AccentColor = "purple",
            AutoHideBar = true,
            LaunchAtStartup = true,
            ShowClock = false,
            BarSize = "Large",
            UseBlurEffect = true,
            AutoCheckUpdates = false
        };
        var sut = new JsonSettingsRepository(_tempFile);

        sut.Save(saved);
        var loaded = new JsonSettingsRepository(_tempFile).Load();

        loaded.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public void Load_FallsBackToDefaults_WhenFileIsCorrupt_NoCrash()
    {
        File.WriteAllText(_tempFile, "{ this is not valid json");
        var sut = new JsonSettingsRepository(_tempFile);

        var settings = sut.Load();

        settings.Should().NotBeNull();
        settings.AppTheme.Should().Be("System");
    }

    [Fact]
    public void Load_FallsBackToDefaults_WhenFileContainsJsonNull()
    {
        File.WriteAllText(_tempFile, "null");
        var sut = new JsonSettingsRepository(_tempFile);

        sut.Load().Should().NotBeNull();
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenItDoesNotExist()
    {
        var nestedFile = Path.Combine(
            Path.GetTempPath(),
            $"tabu-tests-{Guid.NewGuid():N}",
            "deeper",
            "settings.json");
        try
        {
            var sut = new JsonSettingsRepository(nestedFile);
            sut.Save(new UserSettings());

            File.Exists(nestedFile).Should().BeTrue();
        }
        finally
        {
            var rootDir = Path.GetDirectoryName(Path.GetDirectoryName(nestedFile));
            if (rootDir is not null && Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Save_ProducesIndentedJsonForHumanInspection()
    {
        var sut = new JsonSettingsRepository(_tempFile);

        sut.Save(new UserSettings());
        var content = File.ReadAllText(_tempFile);

        content.Should().Contain("\n", "settings.json must remain readable by humans editing it manually");
    }

    [Fact]
    public void Constructor_ThrowsForNullOrWhiteSpacePath()
    {
        FluentActions.Invoking(() => new JsonSettingsRepository(""))
            .Should().Throw<ArgumentException>();

        FluentActions.Invoking(() => new JsonSettingsRepository("   "))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_ThrowsOnNullSettings()
    {
        var sut = new JsonSettingsRepository(_tempFile);

        FluentActions.Invoking(() => sut.Save(null!))
            .Should().Throw<ArgumentNullException>();
    }
}
