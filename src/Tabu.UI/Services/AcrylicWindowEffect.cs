using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Tabu.UI.Services;

/// <summary>
/// Applies the system-rendered acrylic blur effect ("Fluent" backdrop)
/// to a WPF window via the undocumented but stable
/// <c>SetWindowCompositionAttribute</c> entry point exposed by
/// <c>user32.dll</c> on Windows 10 1809 and later.
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
    /// <param name="window">Target window. Must already have a HWND.</param>
    /// <param name="enabled">
    /// <c>true</c> to enable acrylic blur; <c>false</c> to revert to a
    /// regular composited surface.
    /// </param>
    /// <param name="tintColor">
    /// 32-bit ABGR tint blended over the blurred desktop. Use the alpha
    /// channel to control how much of the underlying content shows
    /// through (0 = fully transparent tint, 255 = solid).
    /// </param>
    public static void Apply(Window window, bool enabled, uint tintColor = 0x80202020)
    {
        if (window is null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        try
        {
            ApplyInternal(hwnd, enabled, tintColor);
        }
        catch
        {
            // Acrylic is a nice-to-have. Older builds, virtualization
            // layers and group-policy lockdowns can all reject the
            // composition attribute. We never let this break the bar.
        }
    }

    private static void ApplyInternal(IntPtr hwnd, bool enabled, uint tintColor)
    {
        var accent = new AccentPolicy
        {
            AccentState = enabled ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : AccentState.ACCENT_DISABLED,
            // GradientColor uses ABGR layout in this struct: 0xAARRGGBB
            // is reinterpreted by Windows as 0xAABBGGRR. Callers pass
            // ABGR directly to keep the API documented and unambiguous.
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
