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

    public override bool Equals(object? obj)
        => obj is TrackedWindow other && Handle == other.Handle;

    public override int GetHashCode()
        => Handle.GetHashCode();
}
