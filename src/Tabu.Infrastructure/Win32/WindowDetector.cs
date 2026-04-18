using System.Diagnostics;
using System.Runtime.InteropServices;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Infrastructure.Win32;

public sealed class WindowDetector : IWindowDetector
{
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInputHost",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "LockApp",
        "SystemSettings",
        "ApplicationFrameHost",
        "ScreenClippingHost",
        "WindowsInternal.ComposableShell.Experiences.TextInput.InputApp",
        "CompactOverlay",
        "Video.UI",
        "People",
        "MicrosoftEdgeUpdate"
    };

    /// <summary>
    /// Per-process metadata cache. Process name and executable path do not
    /// change for the lifetime of a PID, so we resolve them once instead of
    /// re-spawning a <see cref="Process"/> wrapper on every poll. Cache is
    /// pruned lazily when entries become stale (the PID no longer exists).
    /// </summary>
    private readonly Dictionary<int, ProcessMetadata> _processCache = new();

    private readonly record struct ProcessMetadata(string Name, string ExecutablePath, bool Excluded);

    public List<TrackedWindow> GetVisibleWindows()
    {
        var windows = new List<TrackedWindow>();
        var currentPid = Environment.ProcessId;
        var seenPids = new HashSet<int>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsTopLevelAppWindow(hWnd)) return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out int pid);
            if (pid == currentPid) return true;

            var title = NativeMethods.GetWindowText(hWnd);
            if (string.IsNullOrWhiteSpace(title)) return true;

            seenPids.Add(pid);
            var metadata = ResolveMetadata(pid);
            if (metadata.Excluded) return true;

            windows.Add(new TrackedWindow
            {
                Handle = hWnd,
                ProcessId = pid,
                ProcessName = metadata.Name,
                ExecutablePath = metadata.ExecutablePath,
                Title = title,
                MonitorHandle = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST)
            });

            return true;
        }, IntPtr.Zero);

        PruneCache(seenPids);
        return windows;
    }

    private ProcessMetadata ResolveMetadata(int pid)
    {
        if (_processCache.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        string name = string.Empty;
        string path = string.Empty;
        try
        {
            // Process implements IDisposable (SafeProcessHandle). Failing to
            // dispose leaks one kernel handle per call; with a 500ms poll
            // timer that compounds quickly into a process-wide handle
            // exhaustion crash.
            using var proc = Process.GetProcessById(pid);
            name = proc.ProcessName;
            try { path = proc.MainModule?.FileName ?? string.Empty; }
            catch { /* MainModule is denied for protected processes */ }
        }
        catch
        {
            // The PID may have died between EnumWindows and the lookup.
        }

        var metadata = new ProcessMetadata(name, path, ExcludedProcessNames.Contains(name));
        _processCache[pid] = metadata;
        return metadata;
    }

    private void PruneCache(HashSet<int> seenPids)
    {
        if (_processCache.Count <= seenPids.Count) return;
        var stale = _processCache.Keys.Where(k => !seenPids.Contains(k)).ToList();
        foreach (var pid in stale) _processCache.Remove(pid);
    }

    public TrackedWindow? GetForegroundWindow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out int pid);

        return new TrackedWindow
        {
            Handle = hwnd,
            ProcessId = pid,
            Title = NativeMethods.GetWindowText(hwnd)
        };
    }

    public void BringToFront(TrackedWindow window)
    {
        var hwnd = window.Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return;

        // Restore if minimized
        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        // Attach input threads so SetForegroundWindow works reliably
        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        if (foregroundThread != currentThread)
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.SetFocus(hwnd);
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
        else
        {
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.SetFocus(hwnd);
        }
    }

    public void MinimizeWindow(TrackedWindow window)
    {
        if (window.Handle == IntPtr.Zero) return;
        NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_MINIMIZE);
    }

    public void CloseWindow(TrackedWindow window)
    {
        if (window.Handle == IntPtr.Zero || !NativeMethods.IsWindow(window.Handle)) return;
        NativeMethods.PostMessageW(window.Handle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public bool IsWindowAlive(IntPtr handle)
    {
        return handle != IntPtr.Zero && NativeMethods.IsWindow(handle);
    }

    public string GetWindowTitle(IntPtr handle)
    {
        return NativeMethods.GetWindowText(handle);
    }

    public List<ScreenInfo> GetAllScreens()
    {
        var screens = new List<ScreenInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };

            if (NativeMethods.GetMonitorInfoW(hMonitor, ref info))
            {
                screens.Add(new ScreenInfo
                {
                    Handle = hMonitor,
                    Left = info.rcMonitor.Left,
                    Top = info.rcMonitor.Top,
                    Width = info.rcMonitor.Right - info.rcMonitor.Left,
                    Height = info.rcMonitor.Bottom - info.rcMonitor.Top,
                    WorkLeft = info.rcWork.Left,
                    WorkTop = info.rcWork.Top,
                    WorkWidth = info.rcWork.Right - info.rcWork.Left,
                    WorkHeight = info.rcWork.Bottom - info.rcWork.Top,
                    IsPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0
                });
            }

            return true;
        }, IntPtr.Zero);

        return screens;
    }

    private static bool IsTopLevelAppWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd)) return false;

        // Must have no owner (top-level)
        var owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
        if (owner != IntPtr.Zero) return false;

        // Must be root-owned
        var root = NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOTOWNER);
        if (root != hWnd) return false;

        // Filter out tool windows
        long exStyle = NativeMethods.GetWindowLongPtrW(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;

        return true;
    }
}
