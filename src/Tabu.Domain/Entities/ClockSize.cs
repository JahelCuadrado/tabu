namespace Tabu.Domain.Entities;

/// <summary>
/// Discrete font sizes (in DIPs) for the bar's wall-clock display. The
/// numeric values are used directly as <c>FontSize</c> in WPF, so adding
/// a new size only requires extending the enum.
/// </summary>
public enum ClockSize
{
    Small = 11,
    Medium = 14,
    Large = 18
}
