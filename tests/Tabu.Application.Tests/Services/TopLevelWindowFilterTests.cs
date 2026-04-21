using FluentAssertions;
using Tabu.Application.Services;
using Xunit;
using Snap = Tabu.Application.Services.TopLevelWindowFilter.WindowSnapshot;

namespace Tabu.Application.Tests.Services;

public sealed class TopLevelWindowFilterTests
{
    [Fact]
    public void Rejects_InvisibleWindow()
    {
        var snap = new Snap(
            IsVisible: false, IsCloaked: false, HasOwner: false,
            IsRootOwner: true, IsToolWindow: false, ClassName: "Notepad");

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeFalse();
    }

    [Fact]
    public void Rejects_CloakedWin32App()
    {
        var snap = new Snap(
            IsVisible: true, IsCloaked: true, HasOwner: false,
            IsRootOwner: true, IsToolWindow: false, ClassName: "Notepad");

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeFalse();
    }

    [Fact]
    public void Accepts_CloakedUwpFrameHost_RegressionGuard_v1_4_0()
    {
        // Windows 11 24H2 reports ApplicationFrameWindow as cloaked
        // for Calculator / Clock / Photos / Settings under normal
        // conditions. They MUST still be tracked as tabs.
        var snap = new Snap(
            IsVisible: true, IsCloaked: true, HasOwner: false,
            IsRootOwner: true, IsToolWindow: false,
            ClassName: TopLevelWindowFilter.UwpFrameHostClassName);

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeTrue();
    }

    [Fact]
    public void Accepts_VisibleNonCloakedUwpFrameHost()
    {
        var snap = new Snap(
            IsVisible: true, IsCloaked: false, HasOwner: false,
            IsRootOwner: true, IsToolWindow: false,
            ClassName: TopLevelWindowFilter.UwpFrameHostClassName);

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeTrue();
    }

    [Fact]
    public void Rejects_OwnedPopup()
    {
        var snap = new Snap(
            IsVisible: true, IsCloaked: false, HasOwner: true,
            IsRootOwner: true, IsToolWindow: false, ClassName: "Notepad");

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeFalse();
    }

    [Fact]
    public void Rejects_NonRootOwner()
    {
        var snap = new Snap(
            IsVisible: true, IsCloaked: false, HasOwner: false,
            IsRootOwner: false, IsToolWindow: false, ClassName: "Notepad");

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeFalse();
    }

    [Fact]
    public void Rejects_ToolWindow()
    {
        var snap = new Snap(
            IsVisible: true, IsCloaked: false, HasOwner: false,
            IsRootOwner: true, IsToolWindow: true, ClassName: "Notepad");

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeFalse();
    }

    [Fact]
    public void Accepts_VisibleNonCloakedWin32App()
    {
        var snap = new Snap(
            IsVisible: true, IsCloaked: false, HasOwner: false,
            IsRootOwner: true, IsToolWindow: false, ClassName: "Chrome_WidgetWin_1");

        TopLevelWindowFilter.IsCandidateAppWindow(snap).Should().BeTrue();
    }

    [Theory]
    [InlineData("ApplicationFrameWindow", true)]
    [InlineData("applicationframewindow", false)]
    [InlineData("Notepad", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsUwpFrameHost_IsCaseSensitive(string? className, bool expected)
    {
        TopLevelWindowFilter.IsUwpFrameHost(className).Should().Be(expected);
    }
}
