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
    /// <remarks>
    /// The cache key includes the process creation timestamp because Windows
    /// recycles PIDs aggressively (PID space is 32K-ish on most systems and
    /// wraps within minutes on a busy machine). Without the start-time
    /// validation, an excluded PID such as <c>TextInputHost</c> could be
    /// reassigned days later to a real user app — leaving the new app
    /// permanently filtered out until the bar is restarted. This was the
    /// root cause of the long-uptime bug where new windows stopped being
    /// shown as tabs after several days of continuous execution.
    /// <para>
    /// Concurrency: <see cref="GetVisibleWindows"/> is called from the UI
    /// poll timer while the shell-hook callback may resolve metadata for a
    /// destroyed window from a different thread. A
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>
    /// gives us atomic insert/remove without coarse locking on the hot path.
    /// </para>
    /// </remarks>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, ProcessMetadata> _processCache = new();

    private readonly record struct ProcessMetadata(
        string Name,
        string ExecutablePath,
        bool Excluded,
        long StartTimeTicks);

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
        long currentStartTicks = GetProcessStartTimeTicks(pid);

        if (_processCache.TryGetValue(pid, out var cached))
        {
            // Validate that the cached entry still belongs to the same OS
            // process. Windows reuses PIDs constantly; a stale cache entry
            // for a recycled PID would make new apps invisible (the most
            // common symptom: open programs stop appearing as tabs after
            // several days of uptime).
            if (currentStartTicks != 0 && cached.StartTimeTicks == currentStartTicks)
            {
                return cached;
            }

            _processCache.TryRemove(pid, out _);
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

        var metadata = new ProcessMetadata(
            name,
            path,
            ExcludedProcessNames.Contains(name),
            currentStartTicks);
        _processCache[pid] = metadata;
        return metadata;
    }

    /// <summary>
    /// Reads the kernel-level creation timestamp of a process via
    /// <c>OpenProcess</c> + <c>GetProcessTimes</c>. Returns <c>0</c> when
    /// the PID is gone or access is denied (protected / system process).
    /// </summary>
    /// <remarks>
    /// Uses <c>PROCESS_QUERY_LIMITED_INFORMATION</c> (0x1000) which is the
    /// least-privileged right that still permits <c>GetProcessTimes</c>;
    /// it succeeds even for elevated targets running as another user when
    /// Tabu itself is unelevated. The handle is always released — leaking
    /// it would re-introduce the v1.2.1 handle-exhaustion crash.
    /// </remarks>
    private static long GetProcessStartTimeTicks(int pid)
    {
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (handle == IntPtr.Zero) return 0;

        try
        {
            if (!GetProcessTimes(handle, out long creation, out _, out _, out _))
            {
                return 0;
            }
            return creation;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private void PruneCache(HashSet<int> seenPids)
    {
        if (_processCache.Count <= seenPids.Count) return;
        var stale = _processCache.Keys.Where(k => !seenPids.Contains(k)).ToList();
        foreach (var pid in stale) _processCache.TryRemove(pid, out _);
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

    /// <inheritdoc />
    public void KillProcess(TrackedWindow window)
    {
        if (window.ProcessId <= 0) return;

        try
        {
            using var process = Process.GetProcessById(window.ProcessId);
            if (process is null || process.HasExited) return;
            // Kill the entire process tree so child workers spawned by the
            // app (e.g. browser renderers, helper processes) also exit. The
            // OS still cleans up handles and freed memory; nothing leaks.
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Process already gone — nothing to do.
        }
        catch (InvalidOperationException)
        {
            // Race: process exited between GetProcessById and Kill.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Insufficient privileges (process running as a different user
            // / elevated). Silently ignore: there is nothing the user-mode
            // bar can do, and we already warned the user with the dialog.
        }
    }

    public bool IsWindowAlive(IntPtr handle)
    {
        return handle != IntPtr.Zero && NativeMethods.IsWindow(handle);
    }

    /// <inheritdoc />
    public bool IsWindowVisibleToUser(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        // Capture the Win32 surface up-front and delegate the verdict
        // to the pure Application-layer policy. Keeps the rules
        // unit-testable so a future regression on the cloak-reason
        // bit handling is caught immediately.
        return WindowVisibilityPolicy.IsVisibleToUser(
            isAlive: NativeMethods.IsWindow(handle),
            isVisible: NativeMethods.IsWindowVisible(handle),
            cloakReason: (CloakReason)NativeMethods.GetCloakReason(handle));
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
