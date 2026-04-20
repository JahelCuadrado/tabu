namespace Tabu.Domain.Entities;

/// <summary>
/// Discrete vertical sizes the top bar can take.
/// Values are expressed in DIPs (also used as raw pixels when reserving
/// workspace through the Windows AppBar API, matching the legacy behavior
/// at 100% DPI).
/// </summary>
public enum BarSize
{
    Small = 36,
    Medium = 44,
    Large = 52
}
