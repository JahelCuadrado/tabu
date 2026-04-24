using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Tabu.UI.Services;

/// <summary>
/// Subscribes to the Windows shell hook protocol so the app can react
/// to taskbar-visible state transitions of foreign windows — most
/// importantly the <c>HSHELL_FLASH</c> notification that fires whenever
/// any application calls <c>FlashWindowEx</c> (Telegram, Slack, Outlook,
/// IDEs running long builds, etc.).
/// </summary>
/// <remarks>
/// The shell hook delivers a single, system-wide, very low-cost callback
/// — a stark contrast to <c>SetWinEventHook</c> which would require a
/// global hook DLL or an in-process callback for every event class. The
/// only requirement is that we register a top-level HWND through
/// <c>RegisterShellHookWindow</c> and listen for the dynamically
/// allocated <c>"SHELLHOOK"</c> message id.
/// </remarks>
public sealed partial class ShellHookListener : IDisposable
{
    /// <summary>HSHELL constants exposed by the shell hook protocol.</summary>
    private const int HSHELL_WINDOWCREATED = 1;
    private const int HSHELL_WINDOWDESTROYED = 2;
    private const int HSHELL_WINDOWACTIVATED = 4;
    private const int HSHELL_REDRAW = 6;
    private const int HSHELL_FLASH = 0x8006;
    private const int HSHELL_RUDEAPPACTIVATED = 0x8004;

    private readonly Window _carrier;
    private readonly HwndSource _source;
    private readonly uint _shellHookMessage;

    /// <summary>
    /// 0 while the listener is alive, 1 once <see cref="Dispose"/> has run.
    /// Manipulated through <see cref="Interlocked.Exchange(ref int, int)"/>
    /// so concurrent disposes from different threads cannot double-free the
    /// shell-hook registration or close the carrier window twice.
    /// </summary>
    private int _disposed;

    /// <summary>Raised on the UI thread whenever a tracked HWND flashes.</summary>
    public event Action<IntPtr>? WindowFlashed;

    /// <summary>Raised when an HWND is destroyed; consumers should clear state.</summary>
    public event Action<IntPtr>? WindowDestroyed;

    public ShellHookListener()
    {
        // The shell hook needs a real top-level HWND to deliver messages
        // to. We allocate a hidden zero-size carrier window and never show
        // it; this keeps the listener completely invisible and decoupled
        // from any user-visible WPF window lifetime.
        _carrier = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Opacity = 0,
            ShowActivated = false,
            Left = -32000,
            Top = -32000
        };
        _carrier.Show();
        _carrier.Hide();

        var helper = new WindowInteropHelper(_carrier);
        _source = HwndSource.FromHwnd(helper.Handle)
            ?? throw new InvalidOperationException("Could not acquire HwndSource for shell hook carrier.");

        _shellHookMessage = RegisterWindowMessage("SHELLHOOK");
        if (!RegisterShellHookWindow(helper.Handle))
        {
            throw new InvalidOperationException("RegisterShellHookWindow failed.");
        }

        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg != _shellHookMessage) return IntPtr.Zero;

        int code = wParam.ToInt32() & 0xFFFF;
        switch (code)
        {
            case HSHELL_FLASH:
                WindowFlashed?.Invoke(lParam);
                break;
            case HSHELL_WINDOWDESTROYED:
                WindowDestroyed?.Invoke(lParam);
                break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        try
        {
            DeregisterShellHookWindow(new WindowInteropHelper(_carrier).Handle);
        }
        catch { /* best-effort cleanup */ }

        _source.RemoveHook(WndProc);
        _carrier.Close();
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string lpString);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterShellHookWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeregisterShellHookWindow(IntPtr hWnd);
}
