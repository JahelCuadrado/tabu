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

    public List<TrackedWindow> GetVisibleWindows()
    {
        var windows = new List<TrackedWindow>();
        var currentPid = Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsTopLevelAppWindow(hWnd)) return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out int pid);
            if (pid == currentPid) return true;

            var title = NativeMethods.GetWindowText(hWnd);
            if (string.IsNullOrWhiteSpace(title)) return true;

            string processName = string.Empty;
            string executablePath = string.Empty;
            try
            {
                var proc = Process.GetProcessById(pid);
                processName = proc.ProcessName;
                executablePath = proc.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // Access denied for some system processes
            }

            if (ExcludedProcessNames.Contains(processName)) return true;

            windows.Add(new TrackedWindow
            {
                Handle = hWnd,
                ProcessId = pid,
                ProcessName = processName,
                ExecutablePath = executablePath,
                Title = title,
                MonitorHandle = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST)
            });

            return true;
        }, IntPtr.Zero);

        return windows;
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
