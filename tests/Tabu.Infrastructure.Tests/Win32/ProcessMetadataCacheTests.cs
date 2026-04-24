using FluentAssertions;
using Tabu.Infrastructure.Win32;
using Xunit;

namespace Tabu.Infrastructure.Tests.Win32;

/// <summary>
/// Unit tests for the cache extracted from <see cref="WindowDetector"/> in
/// the v1.5.1 audit refactor. Validates the behavioural contract that
/// matters at the seam: cache-hit returns the same metadata, prune trims
/// orphans, and unknown PIDs always yield empty (never throw).
/// </summary>
public class ProcessMetadataCacheTests
{
    private static readonly HashSet<string> NoExclusions = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Resolve_CurrentProcess_ReturnsRealName()
    {
        var sut = new ProcessMetadataCache();

        var result = sut.Resolve(Environment.ProcessId, NoExclusions);

        result.Name.Should().NotBeNullOrEmpty();
        result.Excluded.Should().BeFalse();
        result.StartTimeTicks.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Resolve_IsIdempotent_AndReturnsCachedSnapshot()
    {
        var sut = new ProcessMetadataCache();

        var first = sut.Resolve(Environment.ProcessId, NoExclusions);
        var second = sut.Resolve(Environment.ProcessId, NoExclusions);

        second.Should().Be(first, "subsequent resolves of the same PID must hit the cache");
        sut.Count.Should().Be(1);
    }

    [Fact]
    public void Resolve_FlagsExcludedProcessNames()
    {
        var sut = new ProcessMetadataCache();
        var realName = sut.Resolve(Environment.ProcessId, NoExclusions).Name;
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { realName };

        // Bypass the existing cache entry so the freshly applied exclusion
        // list is what populates the snapshot. (PID is the same; in the
        // real detector this only happens after PID recycling.)
        sut.Prune(new HashSet<int>());

        var result = sut.Resolve(Environment.ProcessId, blocked);

        result.Excluded.Should().BeTrue();
    }

    [Fact]
    public void Resolve_DeadPid_ReturnsEmptyMetadataInsteadOfThrowing()
    {
        var sut = new ProcessMetadataCache();
        const int impossiblePid = -1;

        var act = () => sut.Resolve(impossiblePid, NoExclusions);

        act.Should().NotThrow();
        var result = sut.Resolve(impossiblePid, NoExclusions);
        result.Name.Should().BeEmpty();
        result.ExecutablePath.Should().BeEmpty();
    }

    [Fact]
    public void Prune_RemovesEntriesAbsentFromLiveSet()
    {
        var sut = new ProcessMetadataCache();
        sut.Resolve(Environment.ProcessId, NoExclusions);
        sut.Resolve(-1, NoExclusions);
        sut.Count.Should().Be(2);

        sut.Prune(new HashSet<int> { Environment.ProcessId });

        sut.Count.Should().Be(1);
    }

    [Fact]
    public void Prune_NoOp_WhenLiveSetCoversCache()
    {
        var sut = new ProcessMetadataCache();
        sut.Resolve(Environment.ProcessId, NoExclusions);

        sut.Prune(new HashSet<int> { Environment.ProcessId, 99999 });

        sut.Count.Should().Be(1);
    }
}
