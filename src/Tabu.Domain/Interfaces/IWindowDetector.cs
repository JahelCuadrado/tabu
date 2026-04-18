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
    string GetWindowTitle(IntPtr handle);
    List<ScreenInfo> GetAllScreens();
}
