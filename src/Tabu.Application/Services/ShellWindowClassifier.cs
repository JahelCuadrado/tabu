using System.Collections.Frozen;

namespace Tabu.Application.Services;

/// <summary>
/// Classifies window class names into Windows shell / desktop surfaces
/// versus regular application windows. Used by the fullscreen detector
/// to ignore monitor-spanning shell components (wallpaper host, taskbar,
/// Start menu, etc.) that would otherwise be misidentified as fullscreen
/// applications and erroneously hide the bar (regression introduced in
/// v1.3.0 and fixed in v1.3.1).
/// </summary>
public static class ShellWindowClassifier
{
    /// <summary>
    /// Window class names that ALWAYS represent a shell/system surface
    /// rather than a user-launched application. Frozen at startup for
    /// O(1) lookup with the smallest possible memory footprint.
    /// </summary>
    private static readonly FrozenSet<string> ShellClassNames = new[]
    {
        "WorkerW",                                // Desktop wallpaper host
        "Progman",                                // Program Manager / desktop
        "Shell_TrayWnd",                          // Primary taskbar
        "Shell_SecondaryTrayWnd",                 // Per-monitor taskbar
        "Button",                                 // Start button
        "DV2ControlHost",                         // Legacy Start menu host
        "Windows.UI.Core.CoreWindow",             // Start / Search / Action Center
        "NotifyIconOverflowWindow",               // Tray overflow flyout
        "TaskListThumbnailWnd",                   // Alt+Tab thumbnails
        "TaskListOverlayWnd",                     // Alt+Tab overlay
        "MultitaskingViewFrame",                  // Task View
        "ForegroundStaging",                      // DWM staging surface
        "ApplicationManager_DesktopShellWindow",  // Win11 desktop shell
        "Shell_CharmWindow",                      // Win8 charms (legacy)
        "EdgeUiInputTopWndClass"                  // Win8 Edge UI (legacy)
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Returns <c>true</c> if the supplied window class name is a known
    /// shell/desktop surface that must not count as a fullscreen
    /// application.
    /// </summary>
    public static bool IsShellOrDesktopClass(string? className)
    {
        return !string.IsNullOrEmpty(className) && ShellClassNames.Contains(className);
    }
}
