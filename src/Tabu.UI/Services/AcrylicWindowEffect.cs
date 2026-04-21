using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Tabu.Application.Services;

namespace Tabu.UI.Services;

/// <summary>
/// Applies a system-rendered backdrop effect ("Fluent" acrylic or
/// classic Aero gaussian blur) to a WPF window via the undocumented
/// but stable <c>SetWindowCompositionAttribute</c> entry point exposed
/// by <c>user32.dll</c> on Windows 10 1809 and later.
/// <para>
/// The window must be created with
/// <see cref="System.Windows.WindowStyle.None"/>,
/// <see cref="Window.AllowsTransparency"/> = <c>true</c> and a
/// transparent <see cref="Window.Background"/>; otherwise WPF will
/// composite over the blurred surface and hide the effect entirely.
/// </para>
/// <para>
/// All P/Invoke surface is encapsulated here so the rest of the UI
/// remains framework-agnostic and the effect can be silently disabled
/// on unsupported builds without leaking exceptions.
/// </para>
/// </summary>
internal static class AcrylicWindowEffect
{
    /// <summary>
    /// Toggles the acrylic blur on the supplied window. Safe to call
    /// repeatedly; failures are swallowed because the effect is purely
    /// cosmetic and must never interrupt the application.
    /// </summary>
    /// <remarks>
    /// Kept for backwards compatibility. New code should prefer
    /// <see cref="Apply(Window, BackdropMode, uint)"/> which exposes
    /// the gaussian-blur fallback used on Windows 11 Enterprise hosts
    /// where the acrylic accent is gated by security baselines.
    /// </remarks>
    public static void Apply(Window window, bool enabled, uint tintColor = 0x80202020)
    {
        Apply(window, enabled ? BackdropMode.Acrylic : BackdropMode.Disabled, tintColor);
    }

    /// <summary>
    /// Applies the requested <see cref="BackdropMode"/> to the window.
    /// </summary>
    /// <param name="window">Target window. Must already have a HWND.</param>
    /// <param name="mode">
    /// Concrete backdrop to apply. Resolved upstream by
    /// <see cref="BackdropPolicy.Resolve(bool, string?, int)"/>.
    /// </param>
    /// <param name="tintColor">
    /// 32-bit ABGR tint blended over the blurred desktop. Use the alpha
    /// channel to control how much of the underlying content shows
    /// through (0 = fully transparent tint, 255 = solid). Ignored when
    /// <paramref name="mode"/> is <see cref="BackdropMode.GaussianBlur"/>
    /// because Aero ignores the gradient colour entirely.
    /// </param>
    public static void Apply(Window window, BackdropMode mode, uint tintColor = 0x80202020)
    {
        if (window is null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        try
        {
            ApplyInternal(hwnd, mode, tintColor);
        }
        catch
        {
            // Acrylic / blur is a nice-to-have. Older builds, virtualization
            // layers and group-policy lockdowns can all reject the
            // composition attribute. We never let this break the bar.
        }
    }

    private static void ApplyInternal(IntPtr hwnd, BackdropMode mode, uint tintColor)
    {
        var accentState = mode switch
        {
            BackdropMode.Acrylic => AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            BackdropMode.GaussianBlur => AccentState.ACCENT_ENABLE_BLURBEHIND,
            _ => AccentState.ACCENT_DISABLED,
        };

        var accent = new AccentPolicy
        {
            AccentState = accentState,
            // GradientColor uses ABGR layout in this struct: 0xAARRGGBB
            // is reinterpreted by Windows as 0xAABBGGRR. Callers pass
            // ABGR directly to keep the API documented and unambiguous.
            // Aero blur (BLURBEHIND) ignores this field.
            GradientColor = tintColor,
            AccentFlags = 0,
            AnimationId = 0
        };

        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, buffer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = size,
                Data = buffer
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_INVALID_STATE = 5
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }
}
