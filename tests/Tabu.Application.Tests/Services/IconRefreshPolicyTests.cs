using FluentAssertions;
using Tabu.Application.Services;
using Xunit;
using State = Tabu.Application.Services.IconRefreshPolicy.State;

namespace Tabu.Application.Tests.Services;

public sealed class IconRefreshPolicyTests
{
    private static readonly IntPtr Zero = IntPtr.Zero;
    private static readonly IntPtr Core1 = new(0x12345);
    private static readonly IntPtr Core2 = new(0x99999);

    [Fact]
    public void ReloadsWhenNoIconYet()
    {
        var state = new State(
            HasAnyIcon: false, HasShellResolvedIcon: false,
            LastSeenCoreWindow: Zero, CurrentCoreWindow: Zero);

        IconRefreshPolicy.ShouldReloadIcon(state).Should().BeTrue();
    }

    [Fact]
    public void ReloadsWhenCoreWindowJustAppeared_RegressionGuard_UwpRelaunch()
    {
        // Calculator / Clock case: the first poll only saw the
        // ApplicationFrameWindow and resolved a generic exe icon.
        // The second poll exposes the CoreWindow handle so the Shell
        // can now hand back the real package logo.
        var state = new State(
            HasAnyIcon: true, HasShellResolvedIcon: false,
            LastSeenCoreWindow: Zero, CurrentCoreWindow: Core1);

        IconRefreshPolicy.ShouldReloadIcon(state).Should().BeTrue();
    }

    [Fact]
    public void ReloadsWhenUwpIconStillNotShellResolved()
    {
        // CoreWindow has been visible for several polls but the Shell
        // is still returning nothing — keep retrying so the real logo
        // arrives as soon as the AUMID gets registered.
        var state = new State(
            HasAnyIcon: true, HasShellResolvedIcon: false,
            LastSeenCoreWindow: Core1, CurrentCoreWindow: Core1);

        IconRefreshPolicy.ShouldReloadIcon(state).Should().BeTrue();
    }

    [Fact]
    public void DoesNotReloadOnceShellResolved_SteadyState()
    {
        // Steady state for every UWP tab. Must short-circuit so we
        // don't hammer the Shell on every poll for the rest of the
        // tab's life.
        var state = new State(
            HasAnyIcon: true, HasShellResolvedIcon: true,
            LastSeenCoreWindow: Core1, CurrentCoreWindow: Core1);

        IconRefreshPolicy.ShouldReloadIcon(state).Should().BeFalse();
    }

    [Fact]
    public void DoesNotReloadForClassicWin32_OnceIconAcquired()
    {
        // Win32 apps never have a CoreWindow. After the first
        // resolution the tab keeps its icon for life.
        var state = new State(
            HasAnyIcon: true, HasShellResolvedIcon: false,
            LastSeenCoreWindow: Zero, CurrentCoreWindow: Zero);

        IconRefreshPolicy.ShouldReloadIcon(state).Should().BeFalse();
    }

    [Fact]
    public void DoesNotReloadWhenCoreWindowChanges_AfterShellResolved()
    {
        // Defensive: even if the host swaps the CoreWindow handle
        // (rare but theoretically possible during a UWP refresh),
        // the existing Shell icon is still the right one for the
        // package, so don't waste a refresh.
        var state = new State(
            HasAnyIcon: true, HasShellResolvedIcon: true,
            LastSeenCoreWindow: Core1, CurrentCoreWindow: Core2);

        IconRefreshPolicy.ShouldReloadIcon(state).Should().BeFalse();
    }
}
