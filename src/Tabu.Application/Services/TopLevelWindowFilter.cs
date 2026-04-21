namespace Tabu.Application.Services;

/// <summary>
/// Pure decision function that determines whether a top-level window
/// returned by <c>EnumWindows</c> is a candidate to be tracked as a
/// tab in the bar.
/// <para>
/// Extracted from <c>WindowDetector.IsTopLevelAppWindow</c> so the
/// rules — particularly the special-case for the UWP host
/// <c>ApplicationFrameWindow</c> — can be unit-tested in isolation
/// without invoking the Win32 enumeration surface.
/// </para>
/// </summary>
public static class TopLevelWindowFilter
{
    /// <summary>
    /// Win32 class name of the chrome window that hosts every UWP /
    /// WinUI app (Calculator, Clock, Photos, Settings, Sticky Notes…).
    /// On Windows 11 24H2 this surface is frequently DWM-cloaked even
    /// while the app is running and visible to the user (suspended /
    /// just-launched / background-focus states), so the generic cloak
    /// filter must not apply to it.
    /// </summary>
    public const string UwpFrameHostClassName = "ApplicationFrameWindow";

    /// <summary>
    /// Snapshot of every Win32 attribute the policy needs to evaluate.
    /// Captured up-front by the infrastructure layer so the decision
    /// itself stays pure and trivially testable.
    /// </summary>
    public readonly record struct WindowSnapshot(
        bool IsVisible,
        bool IsCloaked,
        bool HasOwner,
        bool IsRootOwner,
        bool IsToolWindow,
        string? ClassName);

    /// <summary>
    /// Returns <c>true</c> when the window should be tracked as a tab.
    /// </summary>
    /// <remarks>
    /// The cloak check is intentionally bypassed for UWP host windows
    /// because Windows 11 24H2 reports them as cloaked under perfectly
    /// normal conditions (Calculator, Clock and other store apps would
    /// otherwise vanish from the bar). Duplication between
    /// <c>ApplicationFrameWindow</c> and the inner <c>CoreWindow</c>
    /// is already prevented by the <c>GA_ROOTOWNER</c> check below
    /// plus the PID-level dedupe in <c>WindowDetector</c>.
    /// </remarks>
    public static bool IsCandidateAppWindow(WindowSnapshot snapshot)
    {
        if (!snapshot.IsVisible) return false;

        // Cloak filter: skip for the UWP frame host, enforce for
        // everything else (mostly to drop the inner CoreWindow of UWP
        // apps and freshly-created windows still being painted).
        if (snapshot.IsCloaked && !IsUwpFrameHost(snapshot.ClassName))
        {
            return false;
        }

        if (snapshot.HasOwner) return false;
        if (!snapshot.IsRootOwner) return false;
        if (snapshot.IsToolWindow) return false;

        return true;
    }

    /// <summary>
    /// Whether the supplied class name identifies the system-owned
    /// UWP / WinUI frame host. Public so the infrastructure layer can
    /// reuse the same comparison without duplicating the constant.
    /// </summary>
    public static bool IsUwpFrameHost(string? className) =>
        !string.IsNullOrEmpty(className)
        && string.Equals(className, UwpFrameHostClassName, StringComparison.Ordinal);
}
