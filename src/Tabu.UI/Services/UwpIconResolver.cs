using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tabu.UI.Services;

/// <summary>
/// Resolves the application icon for UWP / WinUI windows by going through
/// the Windows Shell — exactly the same pipeline the Start menu, taskbar
/// and Alt+Tab use to display package logos.
/// <para>
/// UWP top-level windows are hosted by ApplicationFrameHost and never
/// expose a meaningful HICON via <c>WM_GETICON</c> or
/// <c>GetClassLongPtr</c>. The icon shown by the system is the package
/// logo registered in the AppX manifest, and the canonical way to fetch
/// it is:
/// </para>
/// <list type="number">
///   <item><c>SHGetPropertyStoreForWindow</c> +
///         <c>PKEY_AppUserModel.ID</c> to discover the AUMID.</item>
///   <item><c>SHCreateItemFromParsingName("shell:AppsFolder\\&lt;AUMID&gt;")</c>
///         to obtain a Shell item pointing at the Start menu entry.</item>
///   <item><c>IShellItemImageFactory.GetImage</c> to render the package
///         logo as a HBITMAP.</item>
/// </list>
/// </summary>
internal static class UwpIconResolver
{
    /// <summary>Attempts to resolve the UWP package icon for a window.</summary>
    /// <param name="hwnd">Top-level window handle (the ApplicationFrame is fine).</param>
    /// <param name="size">Requested icon edge size in pixels.</param>
    /// <returns>A frozen <see cref="ImageSource"/> on success; otherwise <c>null</c>.</returns>
    public static ImageSource? TryResolve(IntPtr hwnd, int size = 32)
    {
        if (hwnd == IntPtr.Zero) return null;

        try
        {
            string? aumid = TryGetAppUserModelId(hwnd);
            if (string.IsNullOrEmpty(aumid)) return null;

            return TryGetShellImage(aumid, size);
        }
        catch
        {
            // Shell APIs throw COMException for missing packages, unsigned
            // apps or transient namespace failures. None of those should
            // ever break tab tracking, so swallow and fall through.
            return null;
        }
    }

    private static string? TryGetAppUserModelId(IntPtr hwnd)
    {
        if (SHGetPropertyStoreForWindow(hwnd, typeof(IPropertyStore).GUID, out object psObj) != 0
            || psObj is null)
        {
            return null;
        }

        var ps = (IPropertyStore)psObj;
        try
        {
            var key = PKEY_AppUserModel_ID;
            var pv = new PROPVARIANT();
            try
            {
                if (ps.GetValue(ref key, out pv) != 0) return null;

                // Use PropVariantToString to extract the string regardless of
                // the underlying VT (LPWSTR, BSTR, etc.). This is far more
                // robust than poking at the union by hand.
                var sb = new StringBuilder(512);
                if (PropVariantToString(ref pv, sb, (uint)sb.Capacity) != 0) return null;
                var aumid = sb.ToString();
                return string.IsNullOrEmpty(aumid) ? null : aumid;
            }
            finally
            {
                PropVariantClear(ref pv);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(ps);
        }
    }

    private static ImageSource? TryGetShellImage(string aumid, int size)
    {
        // shell:AppsFolder\<AUMID> is the parsing name that resolves to
        // the Start menu entry of the UWP / WinUI app. From there the
        // Shell knows how to render the package logo.
        if (SHCreateItemFromParsingName(
                $"shell:AppsFolder\\{aumid}", IntPtr.Zero,
                typeof(IShellItemImageFactory).GUID, out object factoryObj) != 0
            || factoryObj is null)
        {
            return null;
        }

        var factory = (IShellItemImageFactory)factoryObj;
        try
        {
            // SIIGBF_BIGGERSIZEOK lets Windows return a higher-resolution
            // asset that we then downscale to the requested edge with high
            // quality, which yields crisper icons at small sizes than
            // forcing the Shell to upscale a tiny variant.
            const int SIIGBF_RESIZETOFIT = 0x00;
            const int SIIGBF_BIGGERSIZEOK = 0x01;
            var requested = new SIZE { cx = size, cy = size };

            int hr = factory.GetImage(requested, SIIGBF_RESIZETOFIT | SIIGBF_BIGGERSIZEOK, out IntPtr hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero) return null;

            try
            {
                var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    // --- Win32 / Shell interop --------------------------------------------

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object propertyStore);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [DllImport("propsys.dll", CharSet = CharSet.Unicode)]
    private static extern int PropVariantToString(ref PROPVARIANT propvar, StringBuilder pszBuf, uint cchBuf);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>PKEY_AppUserModel.ID = {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, pid 5.</summary>
    private static PROPERTYKEY PKEY_AppUserModel_ID => new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    /// <summary>
    /// PROPVARIANT is 16 bytes on x86 and 24 bytes on x64 (the union is
    /// 8-byte aligned). We intentionally over-allocate by including two
    /// IntPtrs after the header so the runtime always passes enough
    /// memory regardless of bitness; the unused tail is harmless.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p1;
        public IntPtr p2;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }
}
