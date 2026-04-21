using Tabu.Domain.Entities;

namespace Tabu.Domain.Interfaces;

public interface IWindowDetector
{
    List<TrackedWindow> GetVisibleWindows();
    TrackedWindow? GetForegroundWindow();
    void BringToFront(TrackedWindow window);
    void MinimizeWindow(TrackedWindow window);
    void CloseWindow(TrackedWindow window);
    bool IsWindowAlive(IntPtr handle);

    /// <summary>
    /// Returns <c>true</c> when the handle still references a window
    /// the user can plausibly interact with — i.e. the window is alive
    /// AND either visible OR DWM-cloaked. A purely hidden window
    /// (<c>ShowWindow(SW_HIDE)</c>, used by apps such as Telegram for
    /// their media viewer) returns <c>false</c> so its tab can be
    /// dropped immediately, while a cloaked window (modern standby /
    /// virtual desktop swap) returns <c>true</c> so the tab survives
    /// the transient state.
    /// </summary>
    bool IsWindowVisibleToUser(IntPtr handle);

    string GetWindowTitle(IntPtr handle);
    List<ScreenInfo> GetAllScreens();
}
