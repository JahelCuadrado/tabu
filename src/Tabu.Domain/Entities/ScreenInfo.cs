namespace Tabu.Domain.Entities;

public sealed class ScreenInfo
{
    public IntPtr Handle { get; init; }
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int WorkLeft { get; init; }
    public int WorkTop { get; init; }
    public int WorkWidth { get; init; }
    public int WorkHeight { get; init; }
    public bool IsPrimary { get; init; }
}
