using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tabu.Infrastructure.Win32;

/// <summary>
/// Thread-safe cache of immutable per-process metadata (executable name and
/// path) keyed by process id. Validates entries against the kernel-level
/// process creation timestamp so a recycled PID — Windows reuses them
/// aggressively, the entire space wraps within minutes on a busy machine —
/// can never serve stale data to <see cref="WindowDetector"/>. Was the root
/// cause of the long-uptime regression where new windows stopped appearing
/// as tabs after several days of continuous execution.
/// </summary>
/// <remarks>
/// Extracted from <see cref="WindowDetector"/> in v1.5.1 to satisfy SRP and
/// give the cache its own unit-test surface. The detector now owns just
/// window enumeration and delegates identity resolution here.
/// </remarks>
internal sealed partial class ProcessMetadataCache
{
    /// <summary>
    /// Win32 access flag granted by <c>PROCESS_QUERY_LIMITED_INFORMATION</c>.
    /// Smallest right that still permits <see cref="GetProcessTimes"/>;
    /// succeeds against elevated targets running as another user even when
    /// the host process itself is unelevated.
    /// </summary>
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>
    /// Concurrent storage so the UI poll thread and shell-hook callbacks
    /// can race safely on the same key without coarse locking.
    /// </summary>
    private readonly ConcurrentDictionary<int, ProcessMetadata> _entries = new();

    /// <summary>
    /// Returns an authoritative <see cref="ProcessMetadata"/> snapshot for
    /// <paramref name="pid"/>. Cache hits are returned only when the
    /// recorded creation timestamp still matches the live process, so a
    /// recycled PID transparently invalidates the prior entry.
    /// </summary>
    public ProcessMetadata Resolve(int pid, IReadOnlySet<string> excludedProcessNames)
    {
        long currentStartTicks = GetProcessStartTimeTicks(pid);

        if (_entries.TryGetValue(pid, out var cached))
        {
            if (currentStartTicks != 0 && cached.StartTimeTicks == currentStartTicks)
            {
                return cached;
            }

            _entries.TryRemove(pid, out _);
        }

        string name = string.Empty;
        string path = string.Empty;
        try
        {
            // Process implements IDisposable (SafeProcessHandle). Failing
            // to dispose leaks one kernel handle per call; with a 500ms
            // poll timer that compounds quickly into a process-wide
            // handle exhaustion crash (regression v1.2.1).
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
            excludedProcessNames.Contains(name),
            currentStartTicks);
        _entries[pid] = metadata;
        return metadata;
    }

    /// <summary>
    /// Drops every cached entry whose PID was not observed in the last
    /// enumeration pass. Bounds memory growth on workloads where many
    /// short-lived processes appear and disappear (CI runners, IDE
    /// integrated terminals, etc.).
    /// </summary>
    public void Prune(IReadOnlySet<int> liveProcessIds)
    {
        if (_entries.Count <= liveProcessIds.Count) return;
        foreach (var pid in _entries.Keys)
        {
            if (!liveProcessIds.Contains(pid))
            {
                _entries.TryRemove(pid, out _);
            }
        }
    }

    /// <summary>
    /// Diagnostics-only count of currently cached entries. Exposed for
    /// tests; the production code path never reads it.
    /// </summary>
    internal int Count => _entries.Count;

    /// <summary>
    /// Reads the kernel-level creation timestamp of a process via
    /// <c>OpenProcess</c> + <c>GetProcessTimes</c>. Returns <c>0</c> when
    /// the PID is gone or access is denied. The handle is always released
    /// — leaking it would re-introduce the v1.2.1 handle-exhaustion crash.
    /// </summary>
    private static long GetProcessStartTimeTicks(int pid)
    {
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

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);
}

/// <summary>
/// Immutable per-process metadata snapshot. The <see cref="StartTimeTicks"/>
/// field is the cache invalidation key for PID-recycle scenarios.
/// </summary>
internal readonly record struct ProcessMetadata(
    string Name,
    string ExecutablePath,
    bool Excluded,
    long StartTimeTicks);
