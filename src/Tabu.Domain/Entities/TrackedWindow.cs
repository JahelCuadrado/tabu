namespace Tabu.Domain.Entities;

public sealed class TrackedWindow
{
    public IntPtr Handle { get; init; }
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IntPtr MonitorHandle { get; set; }

    /// <summary>
    /// For UWP / WinUI windows hosted by ApplicationFrameHost, this is the
    /// HWND of the inner <c>Windows.UI.Core.CoreWindow</c> owned by the
    /// real app process. Non-zero only for UWP windows; consumers (icon
    /// loaders, etc.) should prefer this handle when querying app-level
    /// resources because the host frame does not expose them.
    /// </summary>
    public IntPtr CoreWindowHandle { get; init; }

    public override bool Equals(object? obj)
        => obj is TrackedWindow other && Handle == other.Handle;

    public override int GetHashCode()
        => Handle.GetHashCode();
}
