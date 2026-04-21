using FluentAssertions;
using Tabu.Application.Services;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// Locks the v1.3.1 fix in place: WorkerW/Progman/tool windows must
/// never be misidentified as fullscreen apps and hide the bar.
/// </summary>
public sealed class FullscreenDetectionPolicyTests
{
    private static readonly IntPtr Bar = new(0xBA1);
    private static readonly IntPtr Shell = new(0x5E11);
    private static readonly IntPtr Desktop = new(0xDE57);
    private static readonly IntPtr Monitor1 = new(0xA10);
    private static readonly IntPtr Monitor2 = new(0xA20);

    private static readonly PixelRect FullMonitor = new(0, 0, 1920, 1080);

    private static FullscreenCandidate App(
        int handleId,
        IntPtr? monitor = null,
        PixelRect? bounds = null,
        bool visible = true,
        bool cloaked = false,
        bool toolWindow = false,
        bool owned = false,
        string className = "Chrome_WidgetWin_1")
    {
        return new FullscreenCandidate(
            Handle: new IntPtr(handleId),
            MonitorHandle: monitor ?? Monitor1,
            ClassName: className,
            Bounds: bounds ?? FullMonitor,
            IsVisible: visible,
            IsCloaked: cloaked,
            IsToolWindow: toolWindow,
            HasOwner: owned);
    }

    private static bool Decide(params FullscreenCandidate[] zOrder)
    {
        return FullscreenDetectionPolicy.IsFullscreenOnMonitor(
            zOrder, Bar, Shell, Desktop, Monitor1, FullMonitor);
    }

    [Fact]
    public void True_WhenTopmostAppCoversTheEntireMonitor()
    {
        Decide(App(0xC1)).Should().BeTrue();
    }

    [Fact]
    public void False_WhenZOrderIsEmpty()
    {
        Decide().Should().BeFalse();
    }

    [Fact]
    public void False_WhenTargetMonitorIsZero_GuardClause()
    {
        FullscreenDetectionPolicy.IsFullscreenOnMonitor(
            new[] { App(0xC1) }, Bar, Shell, Desktop, IntPtr.Zero, FullMonitor)
            .Should().BeFalse();
    }

    [Fact]
    public void False_WhenAppCoversMonitorButLivesOnAnotherDisplay()
    {
        Decide(App(0xC1, monitor: Monitor2)).Should().BeFalse();
    }

    [Fact]
    public void False_WhenWindowedAppDoesNotCoverFullMonitor()
    {
        Decide(App(0xC1, bounds: new PixelRect(100, 100, 800, 600))).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsWorkerW_RegressionGuardForV131()
    {
        // Reproduces the v1.3.0 bug: with no real apps open on the
        // monitor, WorkerW (desktop wallpaper host) sits at the top of
        // z-order with monitor-spanning bounds. Must NOT count.
        Decide(App(0xC1, className: "WorkerW")).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsProgman()
    {
        Decide(App(0xC1, className: "Progman")).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsTaskbar()
    {
        Decide(App(0xC1, className: "Shell_TrayWnd")).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsTheBarItself()
    {
        Decide(App(handleId: 0xBA1)).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsCloaked()
    {
        Decide(App(0xC1, cloaked: true)).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsHidden()
    {
        Decide(App(0xC1, visible: false)).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsToolWindow()
    {
        Decide(App(0xC1, toolWindow: true)).Should().BeFalse();
    }

    [Fact]
    public void False_WhenTopmostIsOwnedPopup()
    {
        // A modal dialog owned by another window: don't count it as a
        // fullscreen app — the owner is what matters.
        Decide(App(0xC1, owned: true)).Should().BeFalse();
    }

    [Fact]
    public void True_WhenTransientOverlaySitsAboveARealFullscreenApp()
    {
        // Tooltip-sized window (50x50) on top must be skipped, allowing
        // the next qualifying window (a real fullscreen app) to win.
        var tinyOverlay = App(0xC1, bounds: new PixelRect(0, 0, 50, 50));
        var realFullscreenApp = App(0xC2);

        Decide(tinyOverlay, realFullscreenApp).Should().BeTrue();
    }

    [Fact]
    public void False_WhenFirstQualifyingAppIsWindowed_EvenIfALaterAppIsFullscreen()
    {
        // The "topmost qualifying" rule means a windowed app on top
        // protects the bar even if a fullscreen app exists below.
        var windowedTop = App(0xC1, bounds: new PixelRect(100, 100, 800, 600));
        var fullscreenBelow = App(0xC2);

        Decide(windowedTop, fullscreenBelow).Should().BeFalse();
    }

    [Fact]
    public void True_WhenAppExtendsBeyondMonitorBounds_ForBorderlessFullscreenGames()
    {
        // Many games render to a borderless window slightly larger than
        // the monitor. The coverage check uses ≤ / ≥ so this still
        // counts as fullscreen.
        var oversized = App(0xC1, bounds: new PixelRect(-1, -1, 1921, 1081));
        Decide(oversized).Should().BeTrue();
    }

    [Theory]
    [InlineData(199, 1080)] // width below threshold
    [InlineData(1920, 199)] // height below threshold
    public void TransientWithEitherDimensionBelowMinimum_IsSkipped(int width, int height)
    {
        var transient = App(0xC1, bounds: new PixelRect(0, 0, width, height));
        var realFs = App(0xC2);

        Decide(transient, realFs).Should().BeTrue("the transient overlay must not consume the verdict");
    }
}
