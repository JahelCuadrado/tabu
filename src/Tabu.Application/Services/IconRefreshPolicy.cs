namespace Tabu.Application.Services;

/// <summary>
/// Pure decision function that determines whether the icon for a tab
/// must be re-resolved on a given poll. Extracted from the WPF
/// <c>TabViewModel.UpdateFrom</c> so the rules — particularly the
/// special-cases for UWP / WinUI relaunches — can be unit-tested
/// without spinning up the dispatcher.
/// </summary>
public static class IconRefreshPolicy
{
    /// <summary>
    /// Snapshot captured by <c>TabViewModel</c> right before deciding
    /// whether to re-invoke the icon resolution pipeline.
    /// </summary>
    public readonly record struct State(
        bool HasAnyIcon,
        bool HasShellResolvedIcon,
        IntPtr LastSeenCoreWindow,
        IntPtr CurrentCoreWindow);

    /// <summary>
    /// <c>true</c> when at least one of the following holds:
    /// <list type="number">
    ///   <item>No icon was ever resolved for this tab.</item>
    ///   <item>A UWP CoreWindow handle just appeared on this poll
    ///         (the previous snapshot had Zero, the current one
    ///         doesn't). The construction-time call would have
    ///         fallen through to the generic ApplicationFrameHost
    ///         executable icon, which we can now upgrade.</item>
    ///   <item>The window currently exposes a CoreWindow but the
    ///         Shell never returned the package logo (typical race
    ///         right after a Calculator / Clock relaunch where the
    ///         AUMID takes a few ms to register).</item>
    /// </list>
    /// </summary>
    public static bool ShouldReloadIcon(State state)
    {
        if (!state.HasAnyIcon) return true;

        bool coreWindowJustAppeared =
            state.LastSeenCoreWindow == IntPtr.Zero
            && state.CurrentCoreWindow != IntPtr.Zero;

        if (coreWindowJustAppeared) return true;

        bool isUwpStillUnresolved =
            state.CurrentCoreWindow != IntPtr.Zero
            && !state.HasShellResolvedIcon;

        return isUwpStillUnresolved;
    }
}
