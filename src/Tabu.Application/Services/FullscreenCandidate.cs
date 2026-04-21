namespace Tabu.Application.Services;

/// <summary>
/// Pure description of a candidate top-level window the fullscreen
/// detector evaluates. The UI layer collects these via Win32
/// (<c>EnumWindows</c> / <c>GetTopWindow</c>+<c>GetWindow</c>) and
/// hands them to <see cref="FullscreenDetectionPolicy"/> for the
/// fullscreen verdict, keeping every Win32 P/Invoke out of the
/// reusable / testable code path.
/// </summary>
/// <param name="Handle">HWND of the candidate.</param>
/// <param name="MonitorHandle">HMONITOR returned by MonitorFromWindow.</param>
/// <param name="ClassName">Win32 window class name.</param>
/// <param name="Bounds">Window screen-space bounds in pixels.</param>
/// <param name="IsVisible">Result of IsWindowVisible.</param>
/// <param name="IsCloaked">Result of DwmGetWindowAttribute(DWMWA_CLOAKED).</param>
/// <param name="IsToolWindow">Whether <c>WS_EX_TOOLWINDOW</c> is set.</param>
/// <param name="HasOwner">Whether GetWindow(GW_OWNER) returned a non-zero HWND.</param>
public readonly record struct FullscreenCandidate(
    IntPtr Handle,
    IntPtr MonitorHandle,
    string? ClassName,
    PixelRect Bounds,
    bool IsVisible,
    bool IsCloaked,
    bool IsToolWindow,
    bool HasOwner);

/// <summary>
/// Inclusive, screen-space pixel rectangle. Equivalent to Win32
/// <c>RECT</c> but framework-agnostic so it can live in the
/// Application layer without dragging WPF references along.
/// </summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}
