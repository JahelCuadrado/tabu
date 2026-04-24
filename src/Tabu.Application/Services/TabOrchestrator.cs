using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Application.Services;

public sealed class WindowSwitcher
{
    private readonly IWindowDetector _detector;
    private List<TrackedWindow> _windows = new();
    private TrackedWindow? _activeWindow;
    private IntPtr _ownHandle;
    private static readonly int _ownProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

    /// <summary>
    /// Set of HWNDs that currently have a pending notification flash. The
    /// shell hook reports flashes asynchronously; we accumulate them here
    /// and project the state into <see cref="TrackedWindow.HasNotification"/>
    /// during the next <see cref="Refresh"/>. Entries are cleared as soon
    /// as the user activates the corresponding window.
    /// </summary>
    private readonly HashSet<IntPtr> _flashingWindows = new();

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

    /// <summary>
    /// Records that <paramref name="hwnd"/> is requesting attention. The
    /// flag is reflected on the matching tab on the next refresh and is
    /// idempotent — additional flashes from the same window are no-ops
    /// until the user activates it.
    /// </summary>
    public void NotifyWindowFlashing(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == _ownHandle) return;

        // Skip flashes coming from the currently active window: the user
        // is already looking at it, no badge needed.
        if (_activeWindow?.Handle == hwnd) return;

        if (_flashingWindows.Add(hwnd))
        {
            var match = _windows.FirstOrDefault(w => w.Handle == hwnd);
            if (match is not null)
            {
                match.HasNotification = true;
                WindowsChanged?.Invoke();
            }
        }
    }

    /// <summary>Clears any pending flash for the given HWND.</summary>
    public void ClearNotification(IntPtr hwnd)
    {
        if (!_flashingWindows.Remove(hwnd)) return;
        var match = _windows.FirstOrDefault(w => w.Handle == hwnd);
        if (match is not null && match.HasNotification)
        {
            match.HasNotification = false;
            WindowsChanged?.Invoke();
        }
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

        // Detect active foreground window. When the foreground window is
        // our own bar (because the user clicked the clock, the settings
        // button, or any non-tab area), we must NOT clear the active
        // state on every tab — the user-perceived "active" app is still
        // the previously focused window. Carrying the previous handle
        // forward keeps the active tab highlighted while the user
        // interacts with the bar itself.
        var fg = _detector.GetForegroundWindow();
        IntPtr activeHandle = fg?.Handle ?? IntPtr.Zero;
        // Treat *any* of our own process windows (bar, Settings, dialogs,
        // popups…) as "bar foreground" so the highlighted tab survives
        // while the user is configuring Tabu — otherwise opening Settings
        // would clear the active-tab tint and prevent live preview.
        bool barIsForeground = activeHandle != IntPtr.Zero
            && (activeHandle == _ownHandle || (fg is not null && fg.ProcessId == _ownProcessId));
        if (barIsForeground)
        {
            activeHandle = _activeWindow?.Handle ?? IntPtr.Zero;
        }

        foreach (var win in detected)
        {
            win.IsActive = activeHandle != IntPtr.Zero && win.Handle == activeHandle;

            // Project pending flashes onto the matching window. Auto-clear
            // when the window becomes active — the user has now seen it.
            if (win.IsActive)
            {
                _flashingWindows.Remove(win.Handle);
                win.HasNotification = false;
            }
            else
            {
                win.HasNotification = _flashingWindows.Contains(win.Handle);
            }
        }

        // Drop pending flashes for windows that no longer exist so the set
        // does not grow unbounded across long sessions.
        if (_flashingWindows.Count > 0)
        {
            var liveHandles = detected.Select(w => w.Handle).ToHashSet();
            _flashingWindows.RemoveWhere(h => !liveHandles.Contains(h));
        }

        _windows = detected;
        _activeWindow = detected.FirstOrDefault(w => w.IsActive) ?? _activeWindow;
        WindowsChanged?.Invoke();
    }

    public void SwitchTo(TrackedWindow window)
    {
        _detector.BringToFront(window);
        _flashingWindows.Remove(window.Handle);
        foreach (var w in _windows)
        {
            w.IsActive = w.Handle == window.Handle;
            if (w.IsActive) w.HasNotification = false;
        }
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

    /// <summary>
    /// Force-terminates the OS process owning <paramref name="window"/>.
    /// This is the destructive variant of <see cref="CloseWindow"/> and
    /// must only be invoked after explicit user confirmation. Tabu's own
    /// process is filtered by <see cref="Refresh"/>, so the bar can never
    /// be killed through this path.
    /// </summary>
    public void KillProcess(TrackedWindow window)
    {
        _detector.KillProcess(window);
    }

    /// <summary>
    /// Sends a graceful close request (<c>WM_CLOSE</c>, same as the per-tab
    /// X button) to every currently tracked window. The bar's own HWND is
    /// already filtered out of <see cref="_windows"/> by <see cref="Refresh"/>,
    /// so this method never targets Tabu itself. Each app decides whether
    /// to honour the close (e.g. "Save changes?" dialogs) or veto it; no
    /// process is force-terminated.
    /// </summary>
    /// <returns>The number of close requests dispatched.</returns>
    public int CloseAll()
    {
        // Snapshot to avoid mutation while iterating: closing a window
        // can trigger an immediate Refresh on the next tick.
        var snapshot = _windows.ToArray();
        foreach (var window in snapshot)
        {
            try
            {
                _detector.CloseWindow(window);
            }
            catch
            {
                // Swallow per-window failures — one stuck app must not
                // abort the broadcast to the rest.
            }
        }
        return snapshot.Length;
    }

    public List<ScreenInfo> GetAllScreens()
    {
        return _detector.GetAllScreens();
    }

    /// <summary>
    /// Reports whether the given window handle still references a live
    /// top-level window. Used by the UI layer to distinguish a window
    /// that genuinely vanished from one that is only temporarily
    /// missing from <see cref="Refresh"/> (e.g. DWM-cloaked while the
    /// system is idle / display off / between virtual desktop swaps).
    /// </summary>
    public bool IsWindowAlive(IntPtr handle) => _detector.IsWindowAlive(handle);

    /// <summary>
    /// Reports whether the given window handle is alive AND currently
    /// visible to the user (either rendered or merely DWM-cloaked).
    /// Apps that explicitly hide their windows via
    /// <c>ShowWindow(SW_HIDE)</c> — Telegram's image viewer being the
    /// canonical case — return <c>false</c> here so their tab is
    /// dropped immediately rather than lingering on the bar.
    /// </summary>
    public bool IsWindowVisibleToUser(IntPtr handle) => _detector.IsWindowVisibleToUser(handle);
}
