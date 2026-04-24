using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tabu.Application;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Tabu.Infrastructure;
using Tabu.UI.Helpers;
using Tabu.UI.Services;
using Tabu.UI.ViewModels;
using Tabu.UI.Views;

namespace Tabu.UI;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private MainWindow? _primaryBar;
    private MainViewModel? _primaryViewModel;
    private MultiMonitorBarManager? _barManager;
    private readonly ThemeManager _themeManager = new();
    private readonly LocalizationManager _localizationManager = new();
    private readonly AccentColorManager _accentColorManager = new();
    private ISettingsRepository _settingsRepository = null!;
    private ShellHookListener? _shellHook;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddApplicationServices();
                services.AddInfrastructureServices();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // The WPF lifecycle requires a synchronous override; doing the work
        // as `async void` (the previous shape) means any unobserved
        // exception during startup would terminate the process without a
        // chance for CrashLogger to record it. We block here on a regular
        // Task so the exception surfaces through normal channels.
        try
        {
            OnStartupAsync(e).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("App.OnStartup", ex);
            throw;
        }
    }

    private async Task OnStartupAsync(StartupEventArgs e)
    {
        CrashLogger.Attach();

        await _host.StartAsync();

        var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
        _settingsRepository = _host.Services.GetRequiredService<ISettingsRepository>();
        _barManager = new MultiMonitorBarManager(switcher);

        var saved = _settingsRepository.Load();

        _primaryViewModel = new MainViewModel(switcher)
        {
            IsBarOnAllMonitors = saved.IsBarOnAllMonitors,
            IsDetectSameScreenOnly = saved.IsDetectSameScreenOnly,
            BarOpacity = saved.BarOpacity,
            UseFixedTabWidth = saved.UseFixedTabWidth,
            ShowBranding = saved.ShowBranding,
            Language = saved.Language,
            AccentColor = saved.AccentColor,
            AutoHideBar = saved.AutoHideBar,
            LaunchAtStartup = saved.LaunchAtStartup,
            ShowClock = saved.ShowClock,
            ClockSize = Enum.TryParse<ClockSize>(saved.ClockSize, out var clockSize) ? clockSize : ClockSize.Small,
            BarSize = Enum.TryParse<BarSize>(saved.BarSize, out var size) ? size : BarSize.Small,
            UseBlurEffect = saved.UseBlurEffect,
            BlurMode = saved.BlurMode,
            AutoCheckUpdates = saved.AutoCheckUpdates,
            ShowNotificationBadges = saved.ShowNotificationBadges,
            NotificationDotSize = saved.NotificationDotSize,
            NotificationDotColor = saved.NotificationDotColor,
            ActiveTabColor = saved.ActiveTabColor,
            ActiveTabOpacity = saved.ActiveTabOpacity
        };

        _primaryViewModel.BarPlacementChangeRequested += OnBarPlacementChangeRequested;
        _primaryViewModel.DetectionModeChangeRequested += OnDetectionModeChangeRequested;
        _primaryViewModel.ThemeChangeRequested += OnThemeChangeRequested;
        _primaryViewModel.OpacityChangeRequested += OnOpacityChangeRequested;
        _primaryViewModel.TabWidthChangeRequested += OnTabWidthChangeRequested;
        _primaryViewModel.BrandingChangeRequested += OnBrandingChangeRequested;
        _primaryViewModel.LanguageChangeRequested += OnLanguageChangeRequested;
        _primaryViewModel.AccentColorChangeRequested += OnAccentColorChangeRequested;
        _primaryViewModel.AutoHideChangeRequested += OnAutoHideChangeRequested;
        _primaryViewModel.LaunchAtStartupChangeRequested += OnLaunchAtStartupChangeRequested;
        _primaryViewModel.ClockVisibilityChangeRequested += OnClockVisibilityChangeRequested;
        _primaryViewModel.ClockSizeChangeRequested += OnClockSizeChangeRequested;
        _primaryViewModel.BarSizeChangeRequested += OnBarSizeChangeRequested;
        _primaryViewModel.BlurEffectChangeRequested += OnBlurEffectChangeRequested;
        _primaryViewModel.AutoCheckUpdatesChangeRequested += OnAutoCheckUpdatesChangeRequested;
        _primaryViewModel.NotificationBadgesChangeRequested += OnNotificationBadgesChangeRequested;
        _primaryViewModel.NotificationDotSizeChangeRequested += OnNotificationDotSizeChangeRequested;
        _primaryViewModel.NotificationDotColorChangeRequested += OnNotificationDotColorChangeRequested;
        _primaryViewModel.ActiveTabColorChangeRequested += OnActiveTabColorChangeRequested;
        _primaryViewModel.ActiveTabOpacityChangeRequested += OnActiveTabOpacityChangeRequested;
        _primaryViewModel.ManualUpdateCheckRequested += OnManualUpdateCheckRequested;

        // Reconcile the Run registry value with the persisted preference so that
        // moving/renaming the executable still keeps autostart consistent.
        StartupRegistration.SetEnabled(saved.LaunchAtStartup);

        var theme = Enum.TryParse<AppTheme>(saved.AppTheme, out var parsed) ? parsed : AppTheme.System;
        _primaryViewModel.AppTheme = theme;
        _themeManager.Apply(theme);

        _localizationManager.Apply(saved.Language);

        _accentColorManager.Apply(saved.AccentColor, IsDarkTheme(theme));
        ApplyNotificationDotBrush(saved.NotificationDotColor);
        ApplyActiveTabBrush(saved.ActiveTabColor, saved.ActiveTabOpacity);

        if (saved.IsBarOnAllMonitors)
        {
            ActivateAllMonitorBars();
            if (saved.IsDetectSameScreenOnly)
            {
                ApplyDetectionMode(true);
            }
        }

        _primaryBar = new MainWindow(_primaryViewModel);
        _primaryBar.Show();

        // Subscribe to shell-level taskbar flash events so we can render
        // notification dots on tabs whose owning app called FlashWindowEx.
        // Created lazily after the primary bar so the WPF dispatcher and
        // global resources are fully initialised.
        try
        {
            _shellHook = new ShellHookListener();
            _shellHook.WindowFlashed += OnWindowFlashed;
        }
        catch (Exception ex)
        {
            // Shell hook registration can fail on locked-down sessions
            // (services, sandboxed accounts). Notification badges then
            // simply stay dark — no other functionality is impacted.
            CrashLogger.Log("ShellHook.Register", ex);
        }

        // Fire-and-forget update check; never blocks startup. Gated by
        // the user preference so opting out fully disables network I/O
        // until the user explicitly invokes a manual check.
        if (saved.AutoCheckUpdates)
        {
            var updater = new UpdateOrchestrator(_host.Services.GetRequiredService<IUpdateService>());
            updater.RunInBackground();
        }

        base.OnStartup(e);
    }

    private void OnBarPlacementChangeRequested(bool allMonitors)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (allMonitors)
                ActivateAllMonitorBars();
            else
                DeactivateSecondaryBars();
        });
        PersistSettings();
    }

    private void OnDetectionModeChangeRequested(bool sameScreenOnly)
    {
        Dispatcher.BeginInvoke(() => ApplyDetectionMode(sameScreenOnly));
        PersistSettings();
    }

    private void OnThemeChangeRequested(AppTheme theme)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _themeManager.Apply(theme);
            _accentColorManager.ReapplyForTheme(IsDarkTheme(theme));
        });
        PersistSettings();
    }

    private void OnOpacityChangeRequested(double opacity)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.BarOpacity = opacity));
        PersistSettings();
    }

    private void OnTabWidthChangeRequested(bool useFixed)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.UseFixedTabWidth = useFixed));
        PersistSettings();
    }

    private void OnBrandingChangeRequested(bool show)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.ShowBranding = show));
        PersistSettings();
    }

    private void OnLanguageChangeRequested(string language)
    {
        Dispatcher.BeginInvoke(() => _localizationManager.Apply(language));
        PersistSettings();
    }

    private void OnAccentColorChangeRequested(string colorCode)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var theme = _primaryViewModel?.AppTheme ?? AppTheme.System;
            _accentColorManager.Apply(colorCode, IsDarkTheme(theme));
            // The dot brush either follows the accent (empty user color)
            // or stays on its hex override. Re-evaluate so the change is
            // visible immediately when the user is on accent-follow mode.
            ApplyNotificationDotBrush(_primaryViewModel?.NotificationDotColor ?? string.Empty);
            ApplyActiveTabBrush(
                _primaryViewModel?.ActiveTabColor ?? string.Empty,
                _primaryViewModel?.ActiveTabOpacity ?? 100);
        });
        PersistSettings();
    }

    private void OnAutoHideChangeRequested(bool autoHide)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.AutoHideBar = autoHide));
        PersistSettings();
    }

    private void OnLaunchAtStartupChangeRequested(bool enabled)
    {
        StartupRegistration.SetEnabled(enabled);
        PersistSettings();
    }

    private void OnClockVisibilityChangeRequested(bool show)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.ShowClock = show));
        PersistSettings();
    }

    private void OnBarSizeChangeRequested(BarSize size)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.BarSize = size));
        PersistSettings();
    }

    private void OnClockSizeChangeRequested(ClockSize size)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.ClockSize = size));
        PersistSettings();
    }

    private void OnNotificationBadgesChangeRequested(bool show)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.ShowNotificationBadges = show));
        PersistSettings();
    }

    private void OnNotificationDotSizeChangeRequested(double size)
    {
        Dispatcher.BeginInvoke(() => _barManager?.Broadcast(vm => vm.NotificationDotSize = size));
        PersistSettings();
    }

    private void OnNotificationDotColorChangeRequested(string hex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyNotificationDotBrush(hex);
            _barManager?.Broadcast(vm => vm.NotificationDotColor = hex);
        });
        PersistSettings();
    }

    /// <summary>
    /// User changed the active-tab tint. Re-evaluates the global brush
    /// and broadcasts the new value to every secondary bar so a multi-
    /// monitor setup updates atomically.
    /// </summary>
    private void OnActiveTabColorChangeRequested(string hex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyActiveTabBrush(hex, _primaryViewModel?.ActiveTabOpacity ?? 100);
            _barManager?.Broadcast(vm => vm.ActiveTabColor = hex);
        });
        PersistSettings();
    }

    /// <summary>
    /// User dragged the active-tab opacity slider. Same flow as the
    /// color handler but only the alpha channel changes.
    /// </summary>
    private void OnActiveTabOpacityChangeRequested(double opacity)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyActiveTabBrush(_primaryViewModel?.ActiveTabColor ?? string.Empty, opacity);
            _barManager?.Broadcast(vm => vm.ActiveTabOpacity = opacity);
        });
        PersistSettings();
    }

    /// <summary>
    /// Updates the global <c>NotificationDotBrush</c> resource. When
    /// <paramref name="hex"/> is empty or invalid the brush mirrors the
    /// current <c>AccentBrush</c>; otherwise the parsed color wins.
    /// </summary>
    private static void ApplyNotificationDotBrush(string hex)
    {
        var resources = System.Windows.Application.Current.Resources;
        System.Windows.Media.Brush brush;
        if (!string.IsNullOrWhiteSpace(hex) && TryParseHexColor(hex, out var color))
        {
            brush = new System.Windows.Media.SolidColorBrush(color);
        }
        else if (resources["AccentBrush"] is System.Windows.Media.Brush accent)
        {
            brush = accent;
        }
        else
        {
            brush = System.Windows.Media.Brushes.DodgerBlue;
        }
        resources["NotificationDotBrush"] = brush;
    }

    /// <summary>
    /// Updates the global <c>ActiveTabBackgroundBrush</c> resource. The
    /// alpha channel is derived from the opacity slider (0–100). When
    /// <paramref name="hex"/> is empty the active tab follows the current
    /// accent color, replicating the original "no override" behaviour.
    /// </summary>
    private static void ApplyActiveTabBrush(string hex, double opacityPercent)
    {
        var resources = System.Windows.Application.Current.Resources;
        var alpha = (byte)Math.Clamp((int)Math.Round(opacityPercent * 2.55), 0, 255);

        System.Windows.Media.Color baseColor;
        if (!string.IsNullOrWhiteSpace(hex) && TryParseHexColor(hex, out var parsed))
        {
            baseColor = parsed;
        }
        else if (resources["AccentBrush"] is System.Windows.Media.SolidColorBrush accent)
        {
            baseColor = accent.Color;
        }
        else
        {
            baseColor = System.Windows.Media.Colors.DodgerBlue;
        }

        var finalColor = System.Windows.Media.Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        var brush = new System.Windows.Media.SolidColorBrush(finalColor);
        resources["ActiveTabBackgroundBrush"] = brush;
        // Blur mode uses a dedicated brush so the selected tab keeps a
        // legible contrast over the acrylic backdrop. Mirror the user's
        // tint there as well — otherwise the slider/color appears to do
        // nothing while blur is on.
        resources["ActiveTabBackgroundBlurBrush"] = brush;
    }

    private static bool TryParseHexColor(string hex, out System.Windows.Media.Color color)
        => ColorParser.TryParse(hex, out color);

    private void OnBlurEffectChangeRequested(bool enabled)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Acrylic only renders correctly over the dark palette; light
            // colors disappear into the blurred backdrop. Force-switch
            // and lock the theme to Dark while blur is on so the bar
            // stays legible regardless of the user's previous choice.
            if (enabled && _primaryViewModel is not null && _primaryViewModel.AppTheme != AppTheme.Dark)
            {
                _primaryViewModel.AppTheme = AppTheme.Dark;
            }

            _barManager?.Broadcast(vm => vm.UseBlurEffect = enabled);
        });
        PersistSettings();
    }

    private void OnAutoCheckUpdatesChangeRequested(bool enabled)
    {
        // No live propagation needed — the toggle only affects the next
        // startup. We just persist so the change survives restarts.
        _ = enabled;
        PersistSettings();
    }

    private void OnManualUpdateCheckRequested()
    {
        // Always honor a manual check regardless of the auto-update flag;
        // this is the user's explicit intent to look for a new release.
        var updater = new UpdateOrchestrator(_host.Services.GetRequiredService<IUpdateService>());
        updater.RunManualCheck();
    }

    private void ApplyDetectionMode(bool sameScreenOnly)
    {
        if (_primaryViewModel is null || _barManager is null) return;
        _barManager.ApplyDetectionMode(sameScreenOnly, _primaryViewModel);
    }

    private void ActivateAllMonitorBars()
    {
        if (_primaryViewModel is null || _barManager is null) return;
        _barManager.ActivateAll(_primaryViewModel);
    }

    private void DeactivateSecondaryBars()
    {
        _barManager?.Deactivate(_primaryViewModel);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            OnExitAsync(e).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("App.OnExit", ex);
        }
    }

    private async Task OnExitAsync(ExitEventArgs e)
    {
        PersistSettings();
        UnsubscribePrimaryViewModel();
        DeactivateSecondaryBars();
        if (_shellHook is not null)
        {
            _shellHook.WindowFlashed -= OnWindowFlashed;
            _shellHook.Dispose();
            _shellHook = null;
        }
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Tears down every event subscription registered against
    /// <see cref="_primaryViewModel"/> during <see cref="OnStartupAsync"/>.
    /// Without this the view-model is rooted by 21 long-lived delegates,
    /// preventing GC and leaking the entire window graph on app shutdown
    /// (visible as the process lingering in Task Manager after exit).
    /// </summary>
    private void UnsubscribePrimaryViewModel()
    {
        if (_primaryViewModel is null) return;

        _primaryViewModel.BarPlacementChangeRequested -= OnBarPlacementChangeRequested;
        _primaryViewModel.DetectionModeChangeRequested -= OnDetectionModeChangeRequested;
        _primaryViewModel.ThemeChangeRequested -= OnThemeChangeRequested;
        _primaryViewModel.OpacityChangeRequested -= OnOpacityChangeRequested;
        _primaryViewModel.TabWidthChangeRequested -= OnTabWidthChangeRequested;
        _primaryViewModel.BrandingChangeRequested -= OnBrandingChangeRequested;
        _primaryViewModel.LanguageChangeRequested -= OnLanguageChangeRequested;
        _primaryViewModel.AccentColorChangeRequested -= OnAccentColorChangeRequested;
        _primaryViewModel.AutoHideChangeRequested -= OnAutoHideChangeRequested;
        _primaryViewModel.LaunchAtStartupChangeRequested -= OnLaunchAtStartupChangeRequested;
        _primaryViewModel.ClockVisibilityChangeRequested -= OnClockVisibilityChangeRequested;
        _primaryViewModel.ClockSizeChangeRequested -= OnClockSizeChangeRequested;
        _primaryViewModel.BarSizeChangeRequested -= OnBarSizeChangeRequested;
        _primaryViewModel.BlurEffectChangeRequested -= OnBlurEffectChangeRequested;
        _primaryViewModel.AutoCheckUpdatesChangeRequested -= OnAutoCheckUpdatesChangeRequested;
        _primaryViewModel.NotificationBadgesChangeRequested -= OnNotificationBadgesChangeRequested;
        _primaryViewModel.NotificationDotSizeChangeRequested -= OnNotificationDotSizeChangeRequested;
        _primaryViewModel.NotificationDotColorChangeRequested -= OnNotificationDotColorChangeRequested;
        _primaryViewModel.ActiveTabColorChangeRequested -= OnActiveTabColorChangeRequested;
        _primaryViewModel.ActiveTabOpacityChangeRequested -= OnActiveTabOpacityChangeRequested;
        _primaryViewModel.ManualUpdateCheckRequested -= OnManualUpdateCheckRequested;
    }

    /// <summary>
    /// Forwards a shell-hook flash event to the switcher so the matching
    /// tab gets a notification dot. Runs on the dispatcher because the
    /// hook callback already arrives on the UI thread; no marshalling
    /// needed beyond the null-check guard.
    /// </summary>
    private void OnWindowFlashed(IntPtr hwnd)
    {
        if (_primaryViewModel is null || hwnd == IntPtr.Zero) return;
        try
        {
            var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
            switcher.NotifyWindowFlashing(hwnd);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("ShellHook.Flash", ex);
        }
    }

    /// <summary>
    /// Sends <c>WM_CLOSE</c> to every tracked top-level window. The bar's
    /// own HWND is filtered out by <see cref="WindowSwitcher"/>, so Tabu
    /// itself is never targeted. Each app receives a graceful close
    /// request and may show its own \"Save changes?\" dialog \u2014 nothing
    /// is force-terminated.
    /// </summary>
    /// <returns>The number of close requests dispatched.</returns>
    public int RequestCloseAllTrackedWindows()
    {
        try
        {
            var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
            return switcher.CloseAll();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("CloseAll", ex);
            return 0;
        }
    }

    /// <summary>
    /// Snapshot count of currently tracked windows, so the View can
    /// preview the impact of "Close all" in a confirmation dialog
    /// without reaching into the application layer directly.
    /// </summary>
    public int GetTrackedWindowCount()
    {
        try
        {
            var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
            return switcher.Windows.Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Force-terminates the OS process owning <paramref name="window"/>.
    /// The View is responsible for asking the user first; this entry
    /// point performs no additional checks beyond the per-window try/catch
    /// already provided by the detector.
    /// </summary>
    public void KillTrackedWindowProcess(TrackedWindow window)
    {
        try
        {
            var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
            switcher.KillProcess(window);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("KillProcess", ex);
        }
    }

    private static bool IsDarkTheme(AppTheme theme) => theme switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => ThemeManager.IsSystemDarkMode()
    };

    private void PersistSettings()
    {
        if (_primaryViewModel is null) return;

        var settings = new UserSettings
        {
            IsBarOnAllMonitors = _primaryViewModel.IsBarOnAllMonitors,
            IsDetectSameScreenOnly = _primaryViewModel.IsDetectSameScreenOnly,
            AppTheme = _primaryViewModel.AppTheme.ToString(),
            BarOpacity = _primaryViewModel.BarOpacity,
            UseFixedTabWidth = _primaryViewModel.UseFixedTabWidth,
            ShowBranding = _primaryViewModel.ShowBranding,
            Language = _primaryViewModel.Language,
            AccentColor = _primaryViewModel.AccentColor,
            AutoHideBar = _primaryViewModel.AutoHideBar,
            LaunchAtStartup = _primaryViewModel.LaunchAtStartup,
            ShowClock = _primaryViewModel.ShowClock,
            ClockSize = _primaryViewModel.ClockSize.ToString(),
            ShowNotificationBadges = _primaryViewModel.ShowNotificationBadges,
            NotificationDotSize = (int)_primaryViewModel.NotificationDotSize,
            NotificationDotColor = _primaryViewModel.NotificationDotColor,
            BarSize = _primaryViewModel.BarSize.ToString(),
            UseBlurEffect = _primaryViewModel.UseBlurEffect,
            BlurMode = _primaryViewModel.BlurMode,
            AutoCheckUpdates = _primaryViewModel.AutoCheckUpdates,
            ActiveTabColor = _primaryViewModel.ActiveTabColor,
            ActiveTabOpacity = (int)_primaryViewModel.ActiveTabOpacity
        };

        Task.Run(() =>
        {
            try { _settingsRepository.Save(settings); }
            catch { /* Silently ignore file write failures */ }
        });
    }
}
