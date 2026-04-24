using Tabu.Application.Services;
using Tabu.UI.ViewModels;
using Tabu.UI.Views;

namespace Tabu.UI.Services;

/// <summary>
/// Owns the lifecycle of the Tabu bars displayed on every non-primary
/// monitor. Extracted from the v1.5.0 <c>App</c> God Object so the
/// per-monitor logic — creating clones of the primary view-model,
/// reflecting them on screen, and broadcasting subsequent setting changes
/// — has a single home and a unit-testable seam.
/// </summary>
/// <remarks>
/// Concurrency: every public method is expected to run on the WPF UI
/// dispatcher. The class itself owns no synchronisation primitives because
/// touching <c>Window</c> instances from a background thread would crash
/// regardless.
/// </remarks>
internal sealed class MultiMonitorBarManager
{
    private readonly WindowSwitcher _switcher;
    private readonly List<MainWindow> _secondaryBars = new();

    public MultiMonitorBarManager(WindowSwitcher switcher)
    {
        ArgumentNullException.ThrowIfNull(switcher);
        _switcher = switcher;
    }

    /// <summary>
    /// Read-only view of the secondary bars currently on screen. Exposed
    /// so the host can probe state for diagnostics; mutation must go
    /// through <see cref="ActivateAll"/> / <see cref="Deactivate"/>.
    /// </summary>
    public IReadOnlyList<MainWindow> SecondaryBars => _secondaryBars;

    /// <summary>
    /// Spawns a Tabu bar on every non-primary screen mirroring the
    /// settings of <paramref name="primary"/>. Idempotent: a second call
    /// without an intervening <see cref="Deactivate"/> creates duplicates,
    /// so callers should ensure the previous set has been disposed.
    /// </summary>
    public void ActivateAll(MainViewModel primary)
    {
        ArgumentNullException.ThrowIfNull(primary);

        var screens = _switcher.GetAllScreens();
        var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);
        var sameScreen = primary.IsDetectSameScreenOnly;

        if (primaryScreen is not null)
        {
            primary.MonitorFilter = sameScreen ? primaryScreen.Handle : null;
        }

        foreach (var screen in screens.Where(s => !s.IsPrimary))
        {
            var vm = CloneViewModel(primary, screen.Handle, sameScreen);
            var bar = new MainWindow(vm) { TargetScreen = screen, IsPrimary = false };
            bar.Show();
            _secondaryBars.Add(bar);
        }
    }

    /// <summary>
    /// Closes every secondary bar, clears the list and resets the primary
    /// view-model's monitor filter so it falls back to "all screens".
    /// </summary>
    public void Deactivate(MainViewModel? primary = null)
    {
        foreach (var bar in _secondaryBars)
        {
            bar.Close();
        }
        _secondaryBars.Clear();

        if (primary is not null)
        {
            primary.MonitorFilter = null;
        }
    }

    /// <summary>
    /// Toggles the same-screen-only detection mode across the primary VM
    /// and every secondary bar atomically. Centralising it here keeps the
    /// "what monitor am I filtering" rule consistent across the fleet.
    /// </summary>
    public void ApplyDetectionMode(bool sameScreenOnly, MainViewModel primary)
    {
        ArgumentNullException.ThrowIfNull(primary);

        var screens = _switcher.GetAllScreens();
        var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);

        primary.MonitorFilter = sameScreenOnly && primaryScreen is not null
            ? primaryScreen.Handle
            : null;

        foreach (var bar in _secondaryBars)
        {
            if (bar.DataContext is MainViewModel vm)
            {
                vm.MonitorFilter = sameScreenOnly && bar.TargetScreen is not null
                    ? bar.TargetScreen.Handle
                    : null;
            }
        }
    }

    /// <summary>
    /// Fans a setting change out to every secondary bar's view-model.
    /// Replaces the previous "foreach (var bar in _secondaryBars) { if
    /// (bar.DataContext is MainViewModel vm) { vm.X = value; } }" pattern
    /// duplicated 13× in the host.
    /// </summary>
    public void Broadcast(Action<MainViewModel> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        foreach (var bar in _secondaryBars)
        {
            if (bar.DataContext is MainViewModel vm)
            {
                apply(vm);
            }
        }
    }

    /// <summary>
    /// Builds a new <see cref="MainViewModel"/> mirroring every persisted
    /// setting of the primary instance. Polling is disabled on secondary
    /// view-models because the primary owns the timer; the secondaries
    /// react to <c>WindowsChanged</c> events instead.
    /// </summary>
    private MainViewModel CloneViewModel(MainViewModel primary, IntPtr screenHandle, bool sameScreen)
        => new(_switcher, startPolling: false)
        {
            MonitorFilter = sameScreen ? screenHandle : null,
            IsBarOnAllMonitors = true,
            IsDetectSameScreenOnly = sameScreen,
            BarOpacity = primary.BarOpacity,
            UseFixedTabWidth = primary.UseFixedTabWidth,
            ShowBranding = primary.ShowBranding,
            Language = primary.Language,
            AccentColor = primary.AccentColor,
            AutoHideBar = primary.AutoHideBar,
            ShowClock = primary.ShowClock,
            ClockSize = primary.ClockSize,
            ShowNotificationBadges = primary.ShowNotificationBadges,
            NotificationDotSize = primary.NotificationDotSize,
            NotificationDotColor = primary.NotificationDotColor,
            BarSize = primary.BarSize,
            UseBlurEffect = primary.UseBlurEffect,
            BlurMode = primary.BlurMode,
            AutoCheckUpdates = primary.AutoCheckUpdates,
            ActiveTabColor = primary.ActiveTabColor,
            ActiveTabOpacity = primary.ActiveTabOpacity
        };
}
