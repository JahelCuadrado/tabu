using FluentAssertions;
using Tabu.Application.Services;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// Locks the tab-survival rules in place so the v1.3.2 fix (tabs vanishing
/// during DWM cloak transitions) cannot regress silently. Every branch of
/// <see cref="TabSyncPolicy.DecideTabFate"/> is exercised explicitly.
/// </summary>
public sealed class TabSyncPolicyTests
{
    private static readonly IntPtr Hwnd = new(0xA1);
    private static readonly IntPtr Other = new(0xB2);

    [Fact]
    public void Keep_WhenWindowIsInTheFilteredList()
    {
        var verdict = TabSyncPolicy.DecideTabFate(
            tabHandle: Hwnd,
            filteredHandles: new[] { Hwnd },
            unfilteredHandles: new[] { Hwnd, Other },
            hasMonitorFilter: true,
            isWindowAlive: _ => false); // intentionally false; should be ignored

        verdict.Should().Be(TabSyncPolicy.Decision.Keep);
    }

    [Fact]
    public void Drop_WhenWindowExistsButOnAnotherMonitor()
    {
        var verdict = TabSyncPolicy.DecideTabFate(
            tabHandle: Hwnd,
            filteredHandles: Array.Empty<IntPtr>(),
            unfilteredHandles: new[] { Hwnd },
            hasMonitorFilter: true,
            isWindowAlive: _ => true); // alive but off-monitor → drop

        verdict.Should().Be(TabSyncPolicy.Decision.Drop);
    }

    [Fact]
    public void Keep_WhenWindowIsAbsentButHandleIsStillAlive_RegressionGuardForCloakTransient()
    {
        // Models the v1.3.2 bug scenario: DWM cloaked every window during
        // modern standby so the enumeration came back empty, but the
        // HWNDs were still alive — we must NOT wipe the tabs.
        var verdict = TabSyncPolicy.DecideTabFate(
            tabHandle: Hwnd,
            filteredHandles: Array.Empty<IntPtr>(),
            unfilteredHandles: Array.Empty<IntPtr>(),
            hasMonitorFilter: false,
            isWindowAlive: h => h == Hwnd);

        verdict.Should().Be(TabSyncPolicy.Decision.Keep);
    }

    [Fact]
    public void Drop_WhenWindowIsAbsentAndHandleIsNoLongerAlive()
    {
        var verdict = TabSyncPolicy.DecideTabFate(
            tabHandle: Hwnd,
            filteredHandles: Array.Empty<IntPtr>(),
            unfilteredHandles: Array.Empty<IntPtr>(),
            hasMonitorFilter: false,
            isWindowAlive: _ => false);

        verdict.Should().Be(TabSyncPolicy.Decision.Drop);
    }

    [Fact]
    public void NoFilter_TreatsUnfilteredCheckAsNoop_AndStillKeepsAliveCloakedWindows()
    {
        // Without a monitor filter, the "off-monitor" branch must never
        // trigger; only liveness governs whether a missing tab survives.
        var verdict = TabSyncPolicy.DecideTabFate(
            tabHandle: Hwnd,
            filteredHandles: new[] { Other },
            unfilteredHandles: new[] { Other },
            hasMonitorFilter: false,
            isWindowAlive: _ => true);

        verdict.Should().Be(TabSyncPolicy.Decision.Keep);
    }

    [Fact]
    public void DoesNotInvokeIsAliveCallback_WhenDecisionAlreadyMadeByListMembership()
    {
        // Performance + correctness: the alive callback may invoke
        // user32!IsWindow, which is non-trivial. It must only be
        // invoked when truly necessary.
        int callbackInvocations = 0;
        bool Probe(IntPtr _)
        {
            callbackInvocations++;
            return true;
        }

        TabSyncPolicy.DecideTabFate(Hwnd, new[] { Hwnd }, new[] { Hwnd }, true, Probe);
        TabSyncPolicy.DecideTabFate(Hwnd, Array.Empty<IntPtr>(), new[] { Hwnd }, true, Probe);

        callbackInvocations.Should().Be(0);
    }

    [Theory]
    [InlineData(null, null)]
    public void Throws_WhenRequiredArgumentsAreNull(object? _, object? __)
    {
        FluentActions.Invoking(() => TabSyncPolicy.DecideTabFate(
            Hwnd, null!, Array.Empty<IntPtr>(), false, _ => true))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => TabSyncPolicy.DecideTabFate(
            Hwnd, Array.Empty<IntPtr>(), null!, false, _ => true))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => TabSyncPolicy.DecideTabFate(
            Hwnd, Array.Empty<IntPtr>(), Array.Empty<IntPtr>(), false, null!))
            .Should().Throw<ArgumentNullException>();
    }
}
