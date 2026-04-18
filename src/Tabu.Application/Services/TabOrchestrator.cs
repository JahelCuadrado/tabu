using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Application.Services;

public sealed class WindowSwitcher
{
    private readonly IWindowDetector _detector;
    private List<TrackedWindow> _windows = new();
    private TrackedWindow? _activeWindow;
    private IntPtr _ownHandle;

    public IReadOnlyList<TrackedWindow> Windows => _windows.AsReadOnly();
    public TrackedWindow? ActiveWindow => _activeWindow;

    public event Action? WindowsChanged;

    public WindowSwitcher(IWindowDetector detector)
    {
        _detector = detector;
    }

    public void SetOwnHandle(IntPtr handle)
    {
        _ownHandle = handle;
    }

    public void Refresh()
    {
        var detected = _detector.GetVisibleWindows();

        // Filter out our own window
        if (_ownHandle != IntPtr.Zero)
        {
            detected.RemoveAll(w => w.Handle == _ownHandle);
        }

        // Update titles for existing windows
        foreach (var win in detected)
        {
            win.Title = _detector.GetWindowTitle(win.Handle);
        }

        // Detect active foreground window
        var fg = _detector.GetForegroundWindow();
        foreach (var win in detected)
        {
            win.IsActive = fg is not null && win.Handle == fg.Handle;
        }

        _windows = detected;
        _activeWindow = detected.FirstOrDefault(w => w.IsActive);
        WindowsChanged?.Invoke();
    }

    public void SwitchTo(TrackedWindow window)
    {
        _detector.BringToFront(window);
        foreach (var w in _windows) w.IsActive = w.Handle == window.Handle;
        _activeWindow = window;
        WindowsChanged?.Invoke();
    }

    public void SwitchToIndex(int index)
    {
        if (index >= 0 && index < _windows.Count)
        {
            SwitchTo(_windows[index]);
        }
    }

    public void SwitchNext()
    {
        if (_windows.Count < 2) return;
        var idx = _activeWindow is not null ? _windows.FindIndex(w => w.Handle == _activeWindow.Handle) : -1;
        var next = (idx + 1) % _windows.Count;
        SwitchTo(_windows[next]);
    }

    public void SwitchPrevious()
    {
        if (_windows.Count < 2) return;
        var idx = _activeWindow is not null ? _windows.FindIndex(w => w.Handle == _activeWindow.Handle) : 0;
        var prev = (idx - 1 + _windows.Count) % _windows.Count;
        SwitchTo(_windows[prev]);
    }

    public void MinimizeCurrent()
    {
        if (_activeWindow is not null)
        {
            _detector.MinimizeWindow(_activeWindow);
        }
    }

    public void CloseWindow(TrackedWindow window)
    {
        _detector.CloseWindow(window);
    }

    public List<ScreenInfo> GetAllScreens()
    {
        return _detector.GetAllScreens();
    }
}
