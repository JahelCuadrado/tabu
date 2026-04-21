namespace Tabu.Application.Services;

/// <summary>
/// Decides whether the topmost real application window on a given monitor
/// covers the entire monitor bounds — the cue Tabu uses to hide its bar
/// during fullscreen apps (games, F11 browsers, video players).
/// </summary>
/// <remarks>
/// The actual Win32 enumeration lives in the UI layer; this policy is a
/// pure, allocation-light decision function so the rules introduced by
/// the v1.3.1 fix (reject WorkerW/Progman, tool windows, owned popups,
/// transient overlays) are unit-testable and cannot drift across
/// future refactors.
/// </remarks>
public static class FullscreenDetectionPolicy
{
    /// <summary>
    /// Minimum width AND height a candidate must have to be considered a
    /// real fullscreen surface. Filters out tooltips, IME composition
    /// windows, popups and other transient overlays that briefly sit at
    /// the top of the z-order without being meaningful content.
    /// </summary>
    public const int MinimumSurfaceSidePixels = 200;

    /// <summary>
    /// Walks the supplied z-order (front-to-back) and returns <c>true</c>
    /// when the first qualifying window on <paramref name="targetMonitor"/>
    /// fully covers <paramref name="monitorBounds"/>.
    /// </summary>
    /// <param name="zOrderFrontToBack">
    /// Top-level windows in z-order, front-most first. The first
    /// qualifying candidate decides the verdict; subsequent windows on
    /// the same monitor are not consulted, mirroring the production
    /// "topmost wins" semantics.
    /// </param>
    /// <param name="ownBarHandle">
    /// HWND of the bar itself; always skipped so the bar never causes
    /// itself to be hidden.
    /// </param>
    /// <param name="shellHandle">
    /// HWND returned by <c>GetShellWindow()</c> (Progman). Skipped by
    /// handle to short-circuit the class-name check.
    /// </param>
    /// <param name="desktopHandle">
    /// HWND returned by <c>GetDesktopWindow()</c>. Skipped by handle.
    /// </param>
    /// <param name="targetMonitor">
    /// HMONITOR of the bar whose visibility is being decided.
    /// </param>
    /// <param name="monitorBounds">
    /// Full monitor bounds in pixels (NOT the work area — fullscreen
    /// apps cover the whole screen including any AppBar reservation).
    /// </param>
    public static bool IsFullscreenOnMonitor(
        IEnumerable<FullscreenCandidate> zOrderFrontToBack,
        IntPtr ownBarHandle,
        IntPtr shellHandle,
        IntPtr desktopHandle,
        IntPtr targetMonitor,
        PixelRect monitorBounds)
    {
        ArgumentNullException.ThrowIfNull(zOrderFrontToBack);
        if (targetMonitor == IntPtr.Zero) return false;

        foreach (var candidate in zOrderFrontToBack)
        {
            if (!QualifiesAsRealAppOnMonitor(candidate, ownBarHandle, shellHandle, desktopHandle, targetMonitor))
            {
                continue;
            }

            // Transient overlays (popup tooltips, IMEs, splash screens)
            // briefly sit at the top of z-order without occupying any
            // meaningful surface. Skip them and continue walking.
            if (candidate.Bounds.Width < MinimumSurfaceSidePixels ||
                candidate.Bounds.Height < MinimumSurfaceSidePixels)
            {
                continue;
            }

            // Topmost qualifying candidate decides the verdict.
            return CoversMonitor(candidate.Bounds, monitorBounds);
        }

        return false;
    }

    private static bool QualifiesAsRealAppOnMonitor(
        in FullscreenCandidate candidate,
        IntPtr ownBarHandle,
        IntPtr shellHandle,
        IntPtr desktopHandle,
        IntPtr targetMonitor)
    {
        if (candidate.Handle == IntPtr.Zero) return false;
        if (candidate.Handle == ownBarHandle) return false;
        if (candidate.Handle == shellHandle) return false;
        if (candidate.Handle == desktopHandle) return false;
        if (!candidate.IsVisible) return false;
        if (candidate.IsCloaked) return false;
        if (candidate.IsToolWindow) return false;
        if (candidate.HasOwner) return false;
        if (ShellWindowClassifier.IsShellOrDesktopClass(candidate.ClassName)) return false;
        if (candidate.MonitorHandle != targetMonitor) return false;
        return true;
    }

    private static bool CoversMonitor(in PixelRect window, in PixelRect monitor)
    {
        return window.Left <= monitor.Left &&
               window.Top <= monitor.Top &&
               window.Right >= monitor.Right &&
               window.Bottom >= monitor.Bottom;
    }
}
