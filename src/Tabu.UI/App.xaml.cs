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
            BarSize = Enum.TryParse<BarSize>(saved.BarSize, out var size) ? size : BarSize.Small
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
        _primaryViewModel.BarSizeChangeRequested += OnBarSizeChangeRequested;

        // Reconcile the Run registry value with the persisted preference so that
        // moving/renaming the executable still keeps autostart consistent.
        StartupRegistration.SetEnabled(saved.LaunchAtStartup);

        var theme = Enum.TryParse<AppTheme>(saved.AppTheme, out var parsed) ? parsed : AppTheme.System;
        _primaryViewModel.AppTheme = theme;
        _themeManager.Apply(theme);

        _localizationManager.Apply(saved.Language);

        _accentColorManager.Apply(saved.AccentColor, IsDarkTheme(theme));

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

        // Fire-and-forget update check; never blocks startup.
        var updater = new UpdateOrchestrator(_host.Services.GetRequiredService<IUpdateService>());
        updater.RunInBackground();

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
                BarSize = _primaryViewModel?.BarSize ?? BarSize.Small
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
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
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
            BarSize = _primaryViewModel.BarSize.ToString()
        };

        Task.Run(() =>
        {
            try { _settingsRepository.Save(settings); }
            catch { /* Silently ignore file write failures */ }
        });
    }
}
