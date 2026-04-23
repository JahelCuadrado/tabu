using FluentAssertions;
using NSubstitute;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// Covers the v1.5.0 additions to <see cref="WindowSwitcher"/>:
///  - per-tab notification flash propagation,
///  - active-tab preservation when the bar itself takes foreground,
///  - auto-clear on activation and pruning of dead handles.
/// </summary>
public sealed class WindowSwitcherNotificationTests
{
    private readonly IWindowDetector _detector = Substitute.For<IWindowDetector>();

    private static TrackedWindow MakeWindow(int handle)
    {
        return new TrackedWindow
        {
            Handle = new IntPtr(handle),
            Title = "App",
            ProcessId = 100,
            ProcessName = "test.exe",
            MonitorHandle = new IntPtr(1)
        };
    }

    [Fact]
    public void NotifyWindowFlashing_SetsHasNotificationOnMatchingWindow()
    {
        var target = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { target });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.NotifyWindowFlashing(new IntPtr(0xA1));

        sut.Windows.Single().HasNotification.Should().BeTrue();
    }

    [Fact]
    public void NotifyWindowFlashing_IgnoresTheOwnBarHandle()
    {
        var ownHandle = new IntPtr(0xBA1);
        var other = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { other });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.SetOwnHandle(ownHandle);
        sut.Refresh();

        sut.NotifyWindowFlashing(ownHandle);

        sut.Windows.Single().HasNotification.Should().BeFalse();
    }

    [Fact]
    public void NotifyWindowFlashing_DoesNothing_WhenTargetIsAlreadyActive()
    {
        var active = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { active });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA1));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.NotifyWindowFlashing(new IntPtr(0xA1));

        sut.Windows.Single().HasNotification.Should().BeFalse();
    }

    [Fact]
    public void Refresh_ClearsNotification_WhenWindowBecomesActive()
    {
        var target = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { target });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();
        sut.NotifyWindowFlashing(new IntPtr(0xA1));
        sut.Windows.Single().HasNotification.Should().BeTrue();

        _detector.GetForegroundWindow().Returns(MakeWindow(0xA1));
        sut.Refresh();

        sut.Windows.Single().HasNotification.Should().BeFalse();
    }

    [Fact]
    public void SwitchTo_ClearsNotificationOnTargetWindow()
    {
        var target = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { target });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();
        sut.NotifyWindowFlashing(new IntPtr(0xA1));

        sut.SwitchTo(sut.Windows.Single());

        sut.Windows.Single().HasNotification.Should().BeFalse();
    }

    [Fact]
    public void ClearNotification_RemovesBadgeExplicitly()
    {
        var target = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { target });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();
        sut.NotifyWindowFlashing(new IntPtr(0xA1));

        sut.ClearNotification(new IntPtr(0xA1));

        sut.Windows.Single().HasNotification.Should().BeFalse();
    }

    [Fact]
    public void Refresh_DropsPendingFlashes_ForWindowsThatNoLongerExist()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();
        sut.NotifyWindowFlashing(new IntPtr(0xA2));

        // w2 disappears between refreshes; its flash must not resurrect
        // the badge when a new window later reuses the same handle.
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { MakeWindow(0xA1) });
        sut.Refresh();
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { MakeWindow(0xA1), MakeWindow(0xA2) });
        sut.Refresh();

        sut.Windows.Should().OnlyContain(w => !w.HasNotification);
    }

    [Fact]
    public void Refresh_PreservesActiveTab_WhenBarTakesForeground()
    {
        var ownHandle = new IntPtr(0xBA1);
        var previouslyActive = MakeWindow(0xA1);
        var other = MakeWindow(0xA2);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { previouslyActive, other });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA1));

        var sut = new WindowSwitcher(_detector);
        sut.SetOwnHandle(ownHandle);
        sut.Refresh();
        sut.ActiveWindow!.Handle.Should().Be(new IntPtr(0xA1));

        // The user clicks the bar: foreground reports the bar's own HWND.
        // The active highlight MUST stay on the previously focused window.
        _detector.GetForegroundWindow().Returns(new TrackedWindow
        {
            Handle = ownHandle,
            Title = "Tabu",
            ProcessId = 0,
            ProcessName = string.Empty,
            MonitorHandle = IntPtr.Zero
        });
        sut.Refresh();

        sut.ActiveWindow!.Handle.Should().Be(new IntPtr(0xA1));
        sut.Windows.Single(w => w.IsActive).Handle.Should().Be(new IntPtr(0xA1));
    }
}
