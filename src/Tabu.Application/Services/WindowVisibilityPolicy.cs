namespace Tabu.Application.Services;

/// <summary>
/// Bit flags returned by the DWM <c>DWMWA_CLOAKED</c> attribute that
/// identify who hid a window from the user. Mirrors the Win32
/// constants verbatim so the infrastructure layer can pass the raw
/// integer straight through without an extra translation step.
/// </summary>
[Flags]
public enum CloakReason
{
    /// <summary>Not cloaked. The window is fully visible to the DWM.</summary>
    None = 0,

    /// <summary>
    /// The owning app cloaked the window itself (e.g. Telegram's media
    /// viewer when dismissed by clicking outside). A tab tracking such
    /// a window must be dropped because the user explicitly hid it.
    /// </summary>
    App = 0x00000001,

    /// <summary>
    /// The shell cloaked the window (modern standby, lock screen,
    /// display-off). Transient — tabs must survive the cloak so the
    /// bar does not get wiped after every idle period.
    /// </summary>
    Shell = 0x00000002,

    /// <summary>
    /// Cloak inherited from the owner window (virtual-desktop swap,
    /// suspended parent app). Transient — tabs must survive.
    /// </summary>
    Inherited = 0x00000004,
}

/// <summary>
/// Pure resolver that combines a window's <c>IsWindow</c>,
/// <c>IsWindowVisible</c> and DWM cloak reason into a single boolean
/// answer: "is this window currently meaningful for the user?".
/// <para>
/// Lives in the Application layer so the rule that distinguishes
/// app-initiated hides (drop) from system-initiated cloaks (keep) can
/// be exercised by unit tests without standing up a real WPF window.
/// </para>
/// </summary>
public static class WindowVisibilityPolicy
{
    /// <summary>
    /// Decides whether the supplied window state should keep its tab
    /// alive on the bar.
    /// </summary>
    /// <param name="isAlive">
    /// Result of <c>user32!IsWindow</c>. When <c>false</c> nothing
    /// else matters and the answer is always <c>false</c>.
    /// </param>
    /// <param name="isVisible">
    /// Result of <c>user32!IsWindowVisible</c>. A window that the OS
    /// reports as visible is always considered visible to the user
    /// regardless of the cloak state.
    /// </param>
    /// <param name="cloakReason">
    /// Raw <c>DWMWA_CLOAKED</c> bitfield. Zero when the window is not
    /// cloaked; otherwise a combination of <see cref="CloakReason"/>
    /// values indicating who hid it.
    /// </param>
    public static bool IsVisibleToUser(bool isAlive, bool isVisible, CloakReason cloakReason)
    {
        if (!isAlive) return false;
        if (isVisible) return true;
        if (cloakReason == CloakReason.None) return false;

        // App cloak only -> ghost window (Telegram media viewer pattern).
        // Shell / inherited cloak -> transient system state, keep alive.
        var transient = CloakReason.Shell | CloakReason.Inherited;
        return (cloakReason & transient) != 0;
    }
}
