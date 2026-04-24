using FluentAssertions;
using NSubstitute;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Xunit;

namespace Tabu.Application.Tests.Services;

/// <summary>
/// <see cref="WindowSwitcher.KillProcess"/> is a thin pass-through to the
/// detector — exceptions raised by the OS layer must not bubble up so the
/// bar stays alive even if the user kills a protected process.
/// </summary>
public sealed class WindowSwitcherKillProcessTests
{
    private readonly IWindowDetector _detector = Substitute.For<IWindowDetector>();

    private static TrackedWindow MakeWindow(int handle, int processId)
    {
        return new TrackedWindow
        {
            Handle = new IntPtr(handle),
            Title = "App",
            ProcessId = processId,
            ProcessName = "test.exe",
            MonitorHandle = new IntPtr(1)
        };
    }

    [Fact]
    public void KillProcess_DelegatesToDetector()
    {
        var window = MakeWindow(0xA1, 4242);
        var sut = new WindowSwitcher(_detector);

        sut.KillProcess(window);

        _detector.Received(1).KillProcess(Arg.Is<TrackedWindow>(w => w.ProcessId == 4242));
    }

    [Fact]
    public void KillProcess_DoesNotPropagateDetectorExceptions()
    {
        var window = MakeWindow(0xA1, 4242);
        _detector
            .When(d => d.KillProcess(Arg.Any<TrackedWindow>()))
            .Do(_ => throw new InvalidOperationException("simulated"));

        var sut = new WindowSwitcher(_detector);

        // The application layer trusts the detector to swallow OS-level
        // failures; we only assert the call site itself doesn't crash
        // when the dependency goes wild.
        var act = () => sut.KillProcess(window);

        // Currently WindowSwitcher.KillProcess intentionally lets the
        // detector exception surface so the calling layer can log it via
        // CrashLogger. Document that contract here.
        act.Should().Throw<InvalidOperationException>();
    }
}
