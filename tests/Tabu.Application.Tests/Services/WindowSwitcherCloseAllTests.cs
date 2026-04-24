using FluentAssertions;
using NSubstitute;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// Behavioural guarantees for <see cref="WindowSwitcher.CloseAll"/>:
///  - dispatches a single graceful close per tracked window,
///  - never targets the bar's own HWND,
///  - keeps the broadcast going when one detector call throws,
///  - reports the count of dispatched requests for the UI.
/// </summary>
public sealed class WindowSwitcherCloseAllTests
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
    public void CloseAll_DispatchesWmCloseToEveryTrackedWindow()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        var w3 = MakeWindow(0xA3);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2, w3 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        var dispatched = sut.CloseAll();

        dispatched.Should().Be(3);
        _detector.Received(1).CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA1)));
        _detector.Received(1).CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA2)));
        _detector.Received(1).CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA3)));
    }

    [Fact]
    public void CloseAll_NeverTargetsTheOwnBarHandle()
    {
        var ownHandle = new IntPtr(0xBA1);
        var visible = MakeWindow(0xA1);
        // The detector reports the bar's HWND too; the switcher must
        // filter it out during Refresh so it never reaches CloseWindow.
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { visible, MakeWindow(0xBA1) });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);

        var sut = new WindowSwitcher(_detector);
        sut.SetOwnHandle(ownHandle);
        sut.Refresh();

        sut.CloseAll();

        _detector.DidNotReceive().CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == ownHandle));
        _detector.Received(1).CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA1)));
    }

    [Fact]
    public void CloseAll_ContinuesWhenOneDetectorCallThrows()
    {
        var w1 = MakeWindow(0xA1);
        var w2 = MakeWindow(0xA2);
        var w3 = MakeWindow(0xA3);
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow> { w1, w2, w3 });
        _detector.GetWindowTitle(Arg.Any<IntPtr>()).Returns(string.Empty);
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);
        _detector
            .When(d => d.CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA2))))
            .Do(_ => throw new InvalidOperationException("simulated"));

        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        var dispatched = sut.CloseAll();

        // The throwing window still counts as "attempted"; the broadcast
        // reaches the windows after it.
        dispatched.Should().Be(3);
        _detector.Received(1).CloseWindow(Arg.Is<TrackedWindow>(w => w.Handle == new IntPtr(0xA3)));
    }

    [Fact]
    public void CloseAll_ReturnsZero_WhenNoWindowsAreTracked()
    {
        _detector.GetVisibleWindows().Returns(new List<TrackedWindow>());
        _detector.GetForegroundWindow().Returns((TrackedWindow?)null);
        var sut = new WindowSwitcher(_detector);
        sut.Refresh();

        sut.CloseAll().Should().Be(0);
        _detector.DidNotReceive().CloseWindow(Arg.Any<TrackedWindow>());
    }
}
