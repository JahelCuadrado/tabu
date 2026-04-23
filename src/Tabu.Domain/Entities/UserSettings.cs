namespace Tabu.Domain.Entities;

public sealed class UserSettings
{
    public bool IsBarOnAllMonitors { get; set; } = true;
    public bool IsDetectSameScreenOnly { get; set; } = true;
    public string AppTheme { get; set; } = "System";
    public double BarOpacity { get; set; } = 1.0;
    public bool UseFixedTabWidth { get; set; } = true;
    public bool ShowBranding { get; set; } = true;
    public string Language { get; set; } = "en";
    public string AccentColor { get; set; } = "blue";
    public bool AutoHideBar { get; set; }
    public bool LaunchAtStartup { get; set; }
    public bool ShowClock { get; set; } = true;
    public string ClockSize { get; set; } = "Small";
    public bool ShowNotificationBadges { get; set; } = true;

    /// <summary>
    /// Diameter (in DIPs) of the per-tab notification dot. Allowed range 4–12.
    /// </summary>
    public int NotificationDotSize { get; set; } = 7;

    /// <summary>
    /// Optional fill color for the notification dot, in <c>#RRGGBB</c> format.
    /// Empty string means “follow the current accent color”.
    /// </summary>
    public string NotificationDotColor { get; set; } = string.Empty;

    public string BarSize { get; set; } = "Small";
    public bool UseBlurEffect { get; set; }

    /// <summary>
    /// Backdrop variant requested by the user when <see cref="UseBlurEffect"/>
    /// is enabled. Recognised values: <c>Acrylic</c> (default, Fluent acrylic),
    /// <c>Gaussian</c> (Aero kernel-mode blur, used as a fallback on Windows 11
    /// Enterprise hosts where the acrylic API is silently disabled by security
    /// baselines) and <c>Disabled</c>. Resolution lives in
    /// <c>Tabu.Application.Services.BackdropPolicy</c>.
    /// </summary>
    public string BlurMode { get; set; } = "Acrylic";

    public bool AutoCheckUpdates { get; set; } = true;
}
