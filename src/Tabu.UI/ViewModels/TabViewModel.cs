using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.UI.Helpers;
using Tabu.UI.Services;

namespace Tabu.UI.ViewModels;

public sealed class TabViewModel : ObservableObject
{
    // --- Native interop for icon resolution -------------------------------
    // Some windows (Task Manager, Registry Editor, any elevated/protected
    // process) deny access to Process.MainModule, so the executable path is
    // unknown and ExtractAssociatedIcon cannot be used. For those we ask the
    // window itself for its icon, which works regardless of UAC level.

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongW")]
    private static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

    private const uint WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;
    private const int GCLP_HICONSM = -34;
    private const int GCLP_HICON = -14;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint IconQueryTimeoutMs = 100;

    private string _displayName = string.Empty;
    private bool _isActive;
    private ImageSource? _icon;
    private IntPtr _lastSeenCoreWindow;

    /// <summary>
    /// True once the Shell handed us the authoritative package logo for a
    /// UWP / WinUI window. While false, the tab is showing either no icon
    /// or a generic fallback (executable icon of ApplicationFrameHost,
    /// stock pixmap…) and we keep retrying the Shell resolution on every
    /// poll plus through a short burst of fast retries so the real logo
    /// appears within ~150 ms of the app showing up.
    /// </summary>
    private bool _hasShellResolvedIcon;

    /// <summary>
    /// Fast-retry timer fired only when a UWP-looking window has not yet
    /// surfaced its CoreWindow / AUMID. Stops itself the moment we either
    /// resolve the Shell icon or run out of attempts.
    /// </summary>
    private DispatcherTimer? _iconRetryTimer;
    private int _iconRetryAttempt;
    private const int MaxFastIconRetries = 6;
    private static readonly TimeSpan FastIconRetryInterval = TimeSpan.FromMilliseconds(40);

    public TrackedWindow Model { get; }
    public IntPtr Handle => Model.Handle;

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public TabViewModel(TrackedWindow model)
    {
        Model = model;
        _displayName = Truncate(model.Title, 28);
        _isActive = model.IsActive;
        _lastSeenCoreWindow = model.CoreWindowHandle;
        LoadIcon(model.Handle, model.CoreWindowHandle, model.ExecutablePath);
        ScheduleFastIconRetriesIfNeeded(model.Handle, model.ExecutablePath);
    }

    public void UpdateFrom(TrackedWindow updated)
    {
        DisplayName = Truncate(updated.Title, 28);
        IsActive = updated.IsActive;

        // Re-resolve the icon when the pure policy says so. Keeps the
        // UI thin and the rules unit-testable in IconRefreshPolicyTests.
        var refreshState = new IconRefreshPolicy.State(
            HasAnyIcon: Icon is not null,
            HasShellResolvedIcon: _hasShellResolvedIcon,
            LastSeenCoreWindow: _lastSeenCoreWindow,
            CurrentCoreWindow: updated.CoreWindowHandle);

        if (IconRefreshPolicy.ShouldReloadIcon(refreshState))
        {
            LoadIcon(updated.Handle, updated.CoreWindowHandle, updated.ExecutablePath);
            ScheduleFastIconRetriesIfNeeded(updated.Handle, updated.ExecutablePath);
        }

        _lastSeenCoreWindow = updated.CoreWindowHandle;
    }

    /// <summary>
    /// Spins up a short burst of fast retries (a handful of attempts
    /// spaced ~40 ms apart) when the icon is missing or the Shell still
    /// hasn't yielded a UWP package logo. This bridges the typical
    /// 100-300 ms gap between a UWP app's frame becoming visible and
    /// its AUMID being queryable, so the real logo appears almost
    /// instantly after a Calculator / Clock relaunch instead of
    /// waiting up to a full poll cycle (500 ms).
    /// </summary>
    private void ScheduleFastIconRetriesIfNeeded(IntPtr windowHandle, string executablePath)
    {
        // Already happy: nothing to do. This is the steady-state branch
        // executed on every UpdateFrom for the rest of the tab's life.
        if (_hasShellResolvedIcon) return;
        if (_iconRetryTimer is not null) return;

        _iconRetryAttempt = 0;
        _iconRetryTimer = new DispatcherTimer { Interval = FastIconRetryInterval };
        _iconRetryTimer.Tick += (_, _) =>
        {
            _iconRetryAttempt++;

            // Re-walk the Win32 surface to pick up the CoreWindow as
            // soon as the host registers it. We look the child up
            // through UwpIconResolver itself to avoid plumbing the
            // detector all the way down here.
            var fromShell = UwpIconResolver.TryResolve(windowHandle);
            if (fromShell is not null)
            {
                Icon = fromShell;
                _hasShellResolvedIcon = true;
                StopIconRetryTimer();
                return;
            }

            if (_iconRetryAttempt >= MaxFastIconRetries)
            {
                StopIconRetryTimer();
            }
        };
        _iconRetryTimer.Start();
    }

    private void StopIconRetryTimer()
    {
        if (_iconRetryTimer is null) return;
        _iconRetryTimer.Stop();
        _iconRetryTimer = null;
    }

    /// <summary>
    /// Resolves the tab icon using a layered strategy that gracefully
    /// degrades depending on the kind of source window:
    /// <list type="bullet">
    ///   <item>UWP / WinUI windows (CoreWindow handle present): Shell
    ///         lookup via AppUserModelID is the only reliable path
    ///         because <c>WM_GETICON</c> against ApplicationFrameHost
    ///         returns nothing meaningful. We fall back to HICON / exe
    ///         only if the Shell call fails (e.g. unsigned dev apps).</item>
    ///   <item>Classic Win32 windows: <c>WM_GETICON</c> /
    ///         <c>GetClassLongPtr</c> first, then
    ///         <c>ExtractAssociatedIcon</c> over the executable path,
    ///         and Shell-by-AUMID only as a last resort.</item>
    /// </list>
    /// Every successful HICON is converted into a frozen
    /// <see cref="BitmapSource"/> and the GDI handle is released
    /// immediately to avoid leaking handles (see DestroyIcon comment
    /// further down).
    /// </summary>
    private void LoadIcon(IntPtr windowHandle, IntPtr coreWindowHandle, string executablePath)
    {
        try
        {
            bool isUwp = coreWindowHandle != IntPtr.Zero;

            if (isUwp)
            {
                var fromShell = UwpIconResolver.TryResolve(windowHandle);
                if (fromShell is not null)
                {
                    Icon = fromShell;
                    _hasShellResolvedIcon = true;
                    return;
                }
                // Some UWP windows still publish a class icon — try it.
                if (TryLoadFromWindow(coreWindowHandle, out var fromCore))
                {
                    Icon = fromCore;
                    return;
                }
            }

            if (TryLoadFromWindow(windowHandle, out var fromWindow))
            {
                Icon = fromWindow;
                return;
            }

            if (TryLoadFromExecutable(executablePath, out var fromExe))
            {
                Icon = fromExe;
                return;
            }

            // Final safety net for non-UWP windows that happen to have an
            // AUMID (e.g. WinUI 3 shells launched from packaged identity).
            if (!isUwp)
            {
                var fromShell = UwpIconResolver.TryResolve(windowHandle);
                if (fromShell is not null)
                {
                    Icon = fromShell;
                    _hasShellResolvedIcon = true;
                }
            }
        }
        catch
        {
            // Icon extraction is best-effort and must never break tab tracking.
        }
    }

    private static bool TryLoadFromWindow(IntPtr hwnd, out ImageSource? bitmap)
    {
        bitmap = null;
        if (hwnd == IntPtr.Zero) return false;

        // Order matters: ICON_SMALL2 returns the system-resized 16x16 icon
        // when the window does not provide its own small icon, which is the
        // best match for a 16x16 tab slot.
        IntPtr hIcon = QueryWindowIcon(hwnd, ICON_SMALL2);
        if (hIcon == IntPtr.Zero) hIcon = QueryWindowIcon(hwnd, ICON_SMALL);
        if (hIcon == IntPtr.Zero) hIcon = QueryWindowIcon(hwnd, ICON_BIG);
        if (hIcon == IntPtr.Zero) hIcon = GetClassIcon(hwnd, GCLP_HICONSM);
        if (hIcon == IntPtr.Zero) hIcon = GetClassIcon(hwnd, GCLP_HICON);
        if (hIcon == IntPtr.Zero) return false;

        bitmap = CreateFrozenBitmap(hIcon);
        // Class- and window-owned HICONs belong to the source window; we
        // only need to release the BitmapSource copy GDI created on our
        // behalf, NOT the original handle. CreateBitmapSourceFromHIcon
        // duplicates internally, so calling DestroyIcon here would corrupt
        // the source window's icon. Hence: do NOT destroy hIcon.
        return bitmap is not null;
    }

    private static IntPtr QueryWindowIcon(IntPtr hwnd, int iconType)
    {
        // SMTO_ABORTIFHUNG protects us from blocking the UI thread when the
        // target window is unresponsive (common during shutdown).
        SendMessageTimeout(hwnd, WM_GETICON, new IntPtr(iconType), IntPtr.Zero,
            SMTO_ABORTIFHUNG, IconQueryTimeoutMs, out var result);
        return result;
    }

    private static IntPtr GetClassIcon(IntPtr hwnd, int index)
    {
        return Environment.Is64BitProcess
            ? GetClassLongPtr64(hwnd, index)
            : new IntPtr(unchecked((int)GetClassLong32(hwnd, index)));
    }

    private static bool TryLoadFromExecutable(string executablePath, out ImageSource? bitmap)
    {
        bitmap = null;
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath)) return false;

        // The HICON returned by ExtractAssociatedIcon owns an unmanaged GDI
        // handle that WPF's CreateBitmapSourceFromHIcon does NOT take
        // ownership of, so we must explicitly dispose the managed wrapper
        // and call DestroyIcon afterwards. Failing to do so leaks one GDI
        // handle per tracked window and eventually crashes the process
        // (default per-process limit is 10,000).
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
        if (icon is null) return false;

        bitmap = CreateFrozenBitmap(icon.Handle);
        DestroyIcon(icon.Handle);
        return bitmap is not null;
    }

    private static ImageSource? CreateFrozenBitmap(IntPtr hIcon)
    {
        var bitmap = Imaging.CreateBitmapSourceFromHIcon(
            hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        bitmap.Freeze();
        return bitmap;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Window";
        return text.Length > maxLength ? string.Concat(text.AsSpan(0, maxLength - 3), "...") : text;
    }
}
