namespace Tabu.Application.Services;

/// <summary>
/// Pure decision function that determines whether a tab tracking a given
/// window handle should be kept on the bar or dropped during a refresh.
/// Extracted from the UI layer so the policy is unit-testable without
/// pulling in WPF, and so the rules cannot drift between bars on
/// different monitors.
/// </summary>
public static class TabSyncPolicy
{
    /// <summary>
    /// Final verdict for a tab in the bar's current tab list.
    /// </summary>
    public enum Decision
    {
        /// <summary>
        /// Tab must remain — either because the window is still on this
        /// bar's monitor, or because it is temporarily missing from the
        /// enumeration (DWM cloak during standby / virtual-desktop swap)
        /// while still being a live top-level window.
        /// </summary>
        Keep,

        /// <summary>
        /// Tab must be removed — the window is alive but lives outside
        /// this bar's monitor scope, or the handle no longer references
        /// any live top-level window.
        /// </summary>
        Drop
    }

    /// <summary>
    /// Decides the fate of a single tab.
    /// </summary>
    /// <param name="tabHandle">HWND tracked by the tab.</param>
    /// <param name="filteredHandles">
    /// Handles returned by the latest enumeration AFTER applying the
    /// bar's monitor filter (i.e. windows the bar should currently
    /// display).
    /// </param>
    /// <param name="unfilteredHandles">
    /// Handles returned by the latest enumeration BEFORE applying the
    /// monitor filter. Used to detect a window that simply moved to
    /// another monitor versus one that genuinely vanished.
    /// </param>
    /// <param name="hasMonitorFilter">
    /// <c>true</c> when the bar is restricted to a specific monitor;
    /// when <c>false</c> the unfiltered set is identical to the
    /// filtered set and the comparison is skipped.
    /// </param>
    /// <param name="isWindowAlive">
    /// Callback that asks the OS whether the HWND still references a
    /// live top-level window. Used to differentiate a real close from
    /// a transient cloak.
    /// </param>
    public static Decision DecideTabFate(
        IntPtr tabHandle,
        IReadOnlyCollection<IntPtr> filteredHandles,
        IReadOnlyCollection<IntPtr> unfilteredHandles,
        bool hasMonitorFilter,
        Func<IntPtr, bool> isWindowAlive)
    {
        ArgumentNullException.ThrowIfNull(filteredHandles);
        ArgumentNullException.ThrowIfNull(unfilteredHandles);
        ArgumentNullException.ThrowIfNull(isWindowAlive);

        // 1. Window is in this bar's filtered scope: always keep.
        if (filteredHandles.Contains(tabHandle))
        {
            return Decision.Keep;
        }

        // 2. Window exists in the global enumeration but moved off this
        //    bar's monitor: drop, the appropriate bar will pick it up.
        if (hasMonitorFilter && unfilteredHandles.Contains(tabHandle))
        {
            return Decision.Drop;
        }

        // 3. Window absent from both lists. If the HWND is still alive
        //    it is a transient cloak (modern standby / display-off /
        //    virtual desktop swap) — keep the tab to avoid wiping the
        //    bar after every idle period.
        if (isWindowAlive(tabHandle))
        {
            return Decision.Keep;
        }

        // 4. HWND no longer alive: window was actually destroyed.
        return Decision.Drop;
    }
}
