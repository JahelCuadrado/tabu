using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tabu.Application;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Tabu.Infrastructure;
using Tabu.UI.Services;
using Tabu.UI.ViewModels;
using Tabu.UI.Views;

namespace Tabu.UI;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private MainWindow? _primaryBar;
    private MainViewModel? _primaryViewModel;
    private readonly List<MainWindow> _secondaryBars = new();
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        CrashLogger.Attach();

        await _host.StartAsync();

        var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
        _settingsRepository = _host.Services.GetRequiredService<ISettingsRepository>();

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
            NotificationDotColor = saved.NotificationDotColor
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
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.BarOpacity = opacity;
                }
            }
        });
        PersistSettings();
    }

    private void OnTabWidthChangeRequested(bool useFixed)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.UseFixedTabWidth = useFixed;
                }
            }
        });
        PersistSettings();
    }

    private void OnBrandingChangeRequested(bool show)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.ShowBranding = show;
                }
            }
        });
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
        });
        PersistSettings();
    }

    private void OnAutoHideChangeRequested(bool autoHide)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.AutoHideBar = autoHide;
                }
            }
        });
        PersistSettings();
    }

    private void OnLaunchAtStartupChangeRequested(bool enabled)
    {
        StartupRegistration.SetEnabled(enabled);
        PersistSettings();
    }

    private void OnClockVisibilityChangeRequested(bool show)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.ShowClock = show;
                }
            }
        });
        PersistSettings();
    }

    private void OnBarSizeChangeRequested(BarSize size)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.BarSize = size;
                }
            }
        });
        PersistSettings();
    }

    private void OnClockSizeChangeRequested(ClockSize size)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.ClockSize = size;
                }
            }
        });
        PersistSettings();
    }

    private void OnNotificationBadgesChangeRequested(bool show)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.ShowNotificationBadges = show;
                }
            }
        });
        PersistSettings();
    }

    private void OnNotificationDotSizeChangeRequested(double size)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.NotificationDotSize = size;
                }
            }
        });
        PersistSettings();
    }

    private void OnNotificationDotColorChangeRequested(string hex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyNotificationDotBrush(hex);
            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.NotificationDotColor = hex;
                }
            }
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

    private static bool TryParseHexColor(string hex, out System.Windows.Media.Color color)
    {
        color = default;
        try
        {
            var converted = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            if (converted is System.Windows.Media.Color c)
            {
                color = c;
                return true;
            }
        }
        catch
        {
            // Swallow malformed hex values; the caller falls back to accent.
        }
        return false;
    }

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

            foreach (var bar in _secondaryBars)
            {
                if (bar.DataContext is MainViewModel vm)
                {
                    vm.UseBlurEffect = enabled;
                }
            }
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
        var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
        var screens = switcher.GetAllScreens();
        var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);

        if (_primaryViewModel is not null)
        {
            _primaryViewModel.MonitorFilter = sameScreenOnly && primaryScreen is not null
                ? primaryScreen.Handle
                : null;
        }

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

    private void ActivateAllMonitorBars()
    {
        var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
        var screens = switcher.GetAllScreens();
        var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);
        bool sameScreen = _primaryViewModel?.IsDetectSameScreenOnly ?? false;

        if (_primaryViewModel is not null && primaryScreen is not null)
        {
            _primaryViewModel.MonitorFilter = sameScreen ? primaryScreen.Handle : null;
        }

        foreach (var screen in screens.Where(s => !s.IsPrimary))
        {
            var vm = new MainViewModel(switcher, startPolling: false)
            {
                MonitorFilter = sameScreen ? screen.Handle : null,
                IsBarOnAllMonitors = true,
                IsDetectSameScreenOnly = sameScreen,
                BarOpacity = _primaryViewModel?.BarOpacity ?? 1.0,
                UseFixedTabWidth = _primaryViewModel?.UseFixedTabWidth ?? false,
                ShowBranding = _primaryViewModel?.ShowBranding ?? true,
                Language = _primaryViewModel?.Language ?? "en",
                AccentColor = _primaryViewModel?.AccentColor ?? "purple",
                AutoHideBar = _primaryViewModel?.AutoHideBar ?? false,
                ShowClock = _primaryViewModel?.ShowClock ?? true,
                ClockSize = _primaryViewModel?.ClockSize ?? ClockSize.Small,
                ShowNotificationBadges = _primaryViewModel?.ShowNotificationBadges ?? true,
                NotificationDotSize = _primaryViewModel?.NotificationDotSize ?? 7,
                NotificationDotColor = _primaryViewModel?.NotificationDotColor ?? string.Empty,
                BarSize = _primaryViewModel?.BarSize ?? BarSize.Small,
                UseBlurEffect = _primaryViewModel?.UseBlurEffect ?? false,
                BlurMode = _primaryViewModel?.BlurMode ?? "Acrylic",
                AutoCheckUpdates = _primaryViewModel?.AutoCheckUpdates ?? true
            };

            var bar = new MainWindow(vm) { TargetScreen = screen, IsPrimary = false };
            bar.Show();
            _secondaryBars.Add(bar);
        }
    }

    private void DeactivateSecondaryBars()
    {
        foreach (var bar in _secondaryBars)
        {
            bar.Close();
        }
        _secondaryBars.Clear();

        if (_primaryViewModel is not null)
        {
            _primaryViewModel.MonitorFilter = null;
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        PersistSettings();
        DeactivateSecondaryBars();
        _shellHook?.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
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
            AutoCheckUpdates = _primaryViewModel.AutoCheckUpdates
        };

        Task.Run(() =>
        {
            try { _settingsRepository.Save(settings); }
            catch { /* Silently ignore file write failures */ }
        });
    }
}
