using System.Diagnostics;
using System.Runtime.InteropServices;
using Tabu.Application.Services;
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
        "ScreenClippingHost",
        "WindowsInternal.ComposableShell.Experiences.TextInput.InputApp",
        "CompactOverlay",
        "Video.UI",
        "People",
        "MicrosoftEdgeUpdate"
    };

    /// <summary>
    /// Process name of the system host that owns every UWP / WinUI top-level
    /// window (Calculator, Settings, Photos, Sticky Notes, etc.). It is NOT
    /// excluded because doing so would hide every UWP app from Tabu; instead
    /// we resolve the real owning process by looking up the hosted
    /// <c>Windows.UI.Core.CoreWindow</c> child.
    /// </summary>
    private const string UwpHostProcessName = "ApplicationFrameHost";
    private const string UwpCoreWindowClass = "Windows.UI.Core.CoreWindow";

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

            var metadata = ResolveMetadata(pid);
            IntPtr coreWindowHandle = IntPtr.Zero;

            // UWP / WinUI apps are hosted by ApplicationFrameHost; the real
            // app process lives behind a CoreWindow child. Swap the PID and
            // metadata for the actual app so the user sees "Calculator"
            // instead of "ApplicationFrameHost" and tab grouping works.
            if (metadata.Name.Equals(UwpHostProcessName, StringComparison.OrdinalIgnoreCase))
            {
                var (realPid, coreHwnd) = ResolveUwpAppPid(hWnd, pid);
                if (realPid != pid)
                {
                    pid = realPid;
                    metadata = ResolveMetadata(pid);
                    coreWindowHandle = coreHwnd;
                }
            }

            seenPids.Add(pid);
            if (metadata.Excluded) return true;

            windows.Add(new TrackedWindow
            {
                Handle = hWnd,
                ProcessId = pid,
                ProcessName = metadata.Name,
                ExecutablePath = metadata.ExecutablePath,
                Title = title,
                MonitorHandle = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST),
                CoreWindowHandle = coreWindowHandle
            });

            return true;
        }, IntPtr.Zero);

        PruneCache(seenPids);
        return windows;
    }

    /// <summary>
    /// Walks the children of an <c>ApplicationFrameHost</c> top-level window
    /// looking for the hosted <c>Windows.UI.Core.CoreWindow</c>. The PID of
    /// that child belongs to the actual UWP / WinUI app (e.g. Calculator),
    /// which is what users expect to see, and the handle itself is the one
    /// that exposes app-level resources such as the window icon.
    /// </summary>
    /// <returns>
    ///   The real app PID and CoreWindow HWND, or <paramref name="hostPid"/>
    ///   plus <see cref="IntPtr.Zero"/> when no CoreWindow child is found.
    /// </returns>
    private static (int Pid, IntPtr CoreWindowHandle) ResolveUwpAppPid(IntPtr frameHwnd, int hostPid)
    {
        int realPid = hostPid;
        IntPtr coreHwnd = IntPtr.Zero;

        NativeMethods.EnumChildWindows(frameHwnd, (childHwnd, _) =>
        {
            if (!NativeMethods.GetClassName(childHwnd).Equals(UwpCoreWindowClass, StringComparison.Ordinal))
            {
                return true; // keep enumerating
            }

            NativeMethods.GetWindowThreadProcessId(childHwnd, out int childPid);
            if (childPid != 0 && childPid != hostPid)
            {
                realPid = childPid;
                coreHwnd = childHwnd;
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        return (realPid, coreHwnd);
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

    /// <inheritdoc />
    public bool IsWindowVisibleToUser(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle)) return false;

        // Cloaked windows survive (modern standby / display-off /
        // virtual-desktop swap). A window that the owning app actively
        // hid via ShowWindow(SW_HIDE) — Telegram's media viewer being
        // the canonical case — has IsWindowVisible == false AND is not
        // cloaked, so it is filtered out and its tab can be dropped.
        return NativeMethods.IsWindowVisible(handle) || NativeMethods.IsCloaked(handle);
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
        // Capture the Win32 surface up-front and delegate the verdict to
        // the pure Application-layer policy. Keeps the rules unit-testable
        // and ensures UWP host windows (Calculator, Clock, Photos…) are
        // tracked even when DWM reports them as cloaked, which Windows 11
        // 24H2 does under perfectly normal conditions.
        var owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
        var root = NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOTOWNER);
        long exStyle = NativeMethods.GetWindowLongPtrW(hWnd, NativeMethods.GWL_EXSTYLE);

        var snapshot = new TopLevelWindowFilter.WindowSnapshot(
            IsVisible: NativeMethods.IsWindowVisible(hWnd),
            IsCloaked: NativeMethods.IsCloaked(hWnd),
            HasOwner: owner != IntPtr.Zero,
            IsRootOwner: root == hWnd,
            IsToolWindow: (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0,
            ClassName: NativeMethods.GetClassName(hWnd));

        return TopLevelWindowFilter.IsCandidateAppWindow(snapshot);
    }
}
