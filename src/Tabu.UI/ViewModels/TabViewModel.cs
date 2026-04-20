using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tabu.Domain.Entities;
using Tabu.UI.Helpers;

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
        LoadIcon(model.Handle, model.ExecutablePath);
    }

    public void UpdateFrom(TrackedWindow updated)
    {
        DisplayName = Truncate(updated.Title, 28);
        IsActive = updated.IsActive;
    }

    /// <summary>
    /// Resolves the tab icon using a layered strategy that gracefully
    /// degrades when the source process is protected or elevated:
    ///   1. <c>WM_GETICON</c> (small / big / small2) — asks the window
    ///      itself; works regardless of process access rights.
    ///   2. <c>GetClassLongPtr</c> with <c>GCLP_HICONSM</c> / <c>GCLP_HICON</c>
    ///      — falls back to the class-registered icon.
    ///   3. <c>ExtractAssociatedIcon</c> on the executable path — last
    ///      resort when the previous calls returned nothing and the path
    ///      is known.
    /// Every successful HICON is converted into a frozen
    /// <see cref="BitmapSource"/> and the GDI handle is released
    /// immediately to avoid leaking handles (see DestroyIcon comment
    /// further down).
    /// </summary>
    private void LoadIcon(IntPtr windowHandle, string executablePath)
    {
        try
        {
            if (TryLoadFromWindow(windowHandle, out var fromWindow))
            {
                Icon = fromWindow;
                return;
            }

            if (TryLoadFromExecutable(executablePath, out var fromExe))
            {
                Icon = fromExe;
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
