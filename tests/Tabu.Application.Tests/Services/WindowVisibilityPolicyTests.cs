using FluentAssertions;
using Tabu.Application.Services;
using Xunit;

namespace Tabu.Application.Tests.Services;

public sealed class WindowVisibilityPolicyTests
{
    [Fact]
    public void NotAlive_AlwaysReturnsFalse()
    {
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: false, isVisible: true, cloakReason: CloakReason.None)
            .Should().BeFalse();
    }

    [Fact]
    public void Visible_ReturnsTrue_RegardlessOfCloak()
    {
        // Defensive: when the OS reports the window as visible we trust
        // it even if some cloak bit is set, because the user clearly
        // sees pixels on screen.
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: true, isVisible: true, cloakReason: CloakReason.App)
            .Should().BeTrue();
    }

    [Fact]
    public void HiddenAndNotCloaked_ReturnsFalse()
    {
        // Classic SW_HIDE: not visible, not cloaked -> drop.
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: true, isVisible: false, cloakReason: CloakReason.None)
            .Should().BeFalse();
    }

    [Fact]
    public void Cloaked_ByApp_ReturnsFalse_RegressionGuard_TelegramMediaViewer()
    {
        // Telegram dismisses its image viewer with an app-initiated
        // cloak. Tab MUST be dropped.
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: true, isVisible: false, cloakReason: CloakReason.App)
            .Should().BeFalse();
    }

    [Fact]
    public void Cloaked_ByShell_ReturnsTrue_RegressionGuard_ModernStandby()
    {
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: true, isVisible: false, cloakReason: CloakReason.Shell)
            .Should().BeTrue();
    }

    [Fact]
    public void Cloaked_ByInheritance_ReturnsTrue_RegressionGuard_VirtualDesktopSwap()
    {
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: true, isVisible: false, cloakReason: CloakReason.Inherited)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(CloakReason.App | CloakReason.Shell, true)]
    [InlineData(CloakReason.App | CloakReason.Inherited, true)]
    [InlineData(CloakReason.Shell | CloakReason.Inherited, true)]
    [InlineData(CloakReason.App | CloakReason.Shell | CloakReason.Inherited, true)]
    public void Cloaked_WithAnyTransientFlag_ReturnsTrue_EvenWhenAppFlagAlsoSet(
        CloakReason combo, bool expected)
    {
        // Defensive: in the rare case Windows reports both an app
        // cloak and a transient cloak, we side with "keep the tab"
        // because dropping during standby is a worse user experience
        // than carrying a slightly-stale tab for a few seconds.
        WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: true, isVisible: false, cloakReason: combo)
            .Should().Be(expected);
    }
}
