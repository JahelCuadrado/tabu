namespace Tabu.Domain.Entities;

public sealed class UserSettings
{
    public bool IsBarOnAllMonitors { get; set; }
    public bool IsDetectSameScreenOnly { get; set; }
    public string AppTheme { get; set; } = "System";
    public double BarOpacity { get; set; } = 1.0;
    public bool UseFixedTabWidth { get; set; } = true;
    public bool ShowBranding { get; set; } = true;
    public string Language { get; set; } = "en";
    public string AccentColor { get; set; } = "blue";
    public bool AutoHideBar { get; set; }
    public bool LaunchAtStartup { get; set; }
    public bool ShowClock { get; set; } = true;
}
