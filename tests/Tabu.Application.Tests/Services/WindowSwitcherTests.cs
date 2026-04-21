using FluentAssertions;
using NSubstitute;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// Behavioural tests for <see cref="WindowSwitcher"/>. The detector is
/// fully mocked because every interesting behaviour (own-handle filtering,
/// active-window marking, switch wrap-around, alive check) is pure logic
/// driven by the data the detector returns.
/// </summary>
public sealed class WindowSwitcherTests
{
    private readonly IWindowDetector _detector = Substitute.For<IWindowDetector>();

    private static TrackedWindow MakeWindow(int handle, string title = "App", int pid = 100, IntPtr? monitor = null)
    {
        return new TrackedWindow
        {
            Handle = new IntPtr(handle),
            Title = title,
            ProcessId = pid,
            ProcessName = "test.exe",
            MonitorHandle = monitor ?? new IntPtr(1)
        };
    }

    [Fact]
    public void Refresh_DropsTheOwnBarHandleFromTheVisibleList()
    {
        var ownHandle = new IntPtr(0xBA1);
        var detected = new List<TrackedWindow>
        {
            MakeWindow(0xBA1),
            MakeWindow(0xA1),
            MakeWindow(0xA2)
        };
        _detector.GetVisibleWindows().Returns(detected);
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns("Updated");
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.SetOwnHandle(ownHandle);

        sut.Refresh();

        sut.Windows.Select(w => w.Handle).Should().BeEquivalentTo(
            new[] { new IntPtr(0xA1), new IntPtr(0xA2) });
    }

    [Fact]
    public void Refresh_RewritesTitlesUsingDetectorLookup()
    {
        var window = MakeWindow(0xC1, title: "stale");
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { window });
        _detector.GetWindowTitle(window.Handle).Returns("fresh title");
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.Windows.Single().Title.Should().Be("fresh title");
    }

    [Fact]
    public void Refresh_MarksOnlyTheForegroundWindowAsActive()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        var w3 = MakeWindow(0xA3);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2, w3 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(ci => ci.Arg<IntPtr>().ToString());
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA2));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.ActiveWindow.Should().NotBeNull();
        sut.ActiveWindow!.Handle.Should().Be(new IntPtr(0xA2));
        sut.Windows.Single(w => w.IsActive).Handle.Should().Be(new IntPtr(0xA2));
        sut.Windows.Where(w => !w.IsActive).Should().HaveCount(2);
    }

    [Fact]
    public void Refresh_LeavesActiveNullWhenForegroundIsNotInTheList()
    {
        var w1 = MakeWindow(0xA1);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xFFF));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.ActiveWindow.Should().BeNull();
        sut.Windows.Should().OnlyContain(w => !w.IsActive);
    }

    [Fact]
    public void Refresh_RaisesWindowsChangedEventExactlyOnce()
    {
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow>());
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        int notifications = 0;
        sut.WindowsChanged += () => notifications++;

        sut.Refresh();

        notifications.Should().Be(1);
    }

    [Fact]
    public void SwitchTo_DelegatesToDetectorAndUpdatesActiveStateAtomically()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA1));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.SwitchTo(sut.Windows[1]);

        _detector.Received(1).BringToFront(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA2)));
        sut.ActiveWindow!.Handle.Should().Be(new IntPtr(0xA2));
        sut.Windows.Single(w => w.IsActive).Handle.Should().Be(new IntPtr(0xA2));
    }

    [Fact]
    public void SwitchNext_WrapsFromLastWindowToFirst()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        var w3 = MakeWindow(0xA3);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2, w3 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA3));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.SwitchNext();

        sut.ActiveWindow!.Handle.Should().Be(new IntPtr(0xA1));
    }

    [Fact]
    public void SwitchPrevious_WrapsFromFirstWindowToLast()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        var w3 = MakeWindow(0xA3);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2, w3 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA1));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.SwitchPrevious();

        sut.ActiveWindow!.Handle.Should().Be(new IntPtr(0xA3));
    }

    [Fact]
    public void SwitchNext_NoOpWhenFewerThanTwoWindows()
    {
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { MakeWindow(0xA1) });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns(MakeWindow(0xA1));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();
        _detector.ClearReceivedCalls();

        sut.SwitchNext();

        _detector.DidNotReceive().BringToFront(Arg.Any<TrackedWindow>());
    }

    [Fact]
    public void MinimizeCurrent_NoOpWhenNoActiveWindow()
    {
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow>());
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.MinimizeCurrent();

        _detector.DidNotReceive().MinimizeWindow(Arg.Any<TrackedWindow>());
    }

    [Fact]
    public void IsWindowAlive_DelegatesToDetector()
    {
        var hwnd = new IntPtr(0xBEEF);
        _detector.IsWindowAlive(hwnd).Returns(true);
        var sut = new WindowSwitcher(_detector);

        sut.IsWindowAlive(hwnd).Should().BeTrue();
        _detector.Received(1).IsWindowAlive(hwnd);
    }

    [Fact]
    public void CloseWindow_DelegatesToDetector()
    {
        var sut = new WindowSwitcher(_detector);
        var window = MakeWindow(0xA1);

        sut.CloseWindow(window);

        _detector.Received(1).CloseWindow(window);
    }
}
