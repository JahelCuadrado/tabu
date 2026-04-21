using FluentAssertions;
using Tabu.Application.Services;
using Xunit;

namespace Tabu.Application.Tests.Services;

public sealed class ShellWindowClassifierTests
{
    [Theory]
    [InlineData("WorkerW")]
    [InlineData("Progman")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("Windows.UI.Core.CoreWindow")]
    [InlineData("MultitaskingViewFrame")]
    public void IsShellOrDesktopClass_ReturnsTrue_ForKnownShellSurfaces(string className)
    {
        ShellWindowClassifier.IsShellOrDesktopClass(className).Should().BeTrue();
    }

    [Theory]
    [InlineData("Notepad")]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("ApplicationFrameWindow")]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS")]
    [InlineData("HwndWrapper[Tabu.UI;;abc]")]
    public void IsShellOrDesktopClass_ReturnsFalse_ForRegularApplicationClasses(string className)
    {
        ShellWindowClassifier.IsShellOrDesktopClass(className).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsShellOrDesktopClass_ReturnsFalse_ForNullOrEmpty(string? className)
    {
        ShellWindowClassifier.IsShellOrDesktopClass(className).Should().BeFalse();
    }

    [Fact]
    public void IsShellOrDesktopClass_IsCaseSensitive_BecauseWin32ClassNamesAre()
    {
        // Win32 window class names are case-sensitive; "workerw" must
        // NOT match "WorkerW". Catching this guards against silent
        // copy/paste typos in the frozen set.
        ShellWindowClassifier.IsShellOrDesktopClass("workerw").Should().BeFalse();
        ShellWindowClassifier.IsShellOrDesktopClass("WorkerW").Should().BeTrue();
    }
}
