using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tabu.Application;
using Tabu.Application.Services;
using Tabu.Infrastructure;
using Tabu.UI.ViewModels;
using Tabu.UI.Views;

namespace Tabu.UI;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private MainWindow? _primaryBar;
    private MainViewModel? _primaryViewModel;
    private readonly List<MainWindow> _secondaryBars = new();

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
        await _host.StartAsync();

        var switcher = _host.Services.GetRequiredService<WindowSwitcher>();

        _primaryViewModel = new MainViewModel(switcher);
        _primaryViewModel.BarPlacementChangeRequested += OnBarPlacementChangeRequested;
        _primaryViewModel.DetectionModeChangeRequested += OnDetectionModeChangeRequested;

        _primaryBar = new MainWindow(_primaryViewModel);
        _primaryBar.Show();

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
    }

    private void OnDetectionModeChangeRequested(bool sameScreenOnly)
    {
        Dispatcher.BeginInvoke(() => ApplyDetectionMode(sameScreenOnly));
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
                IsDetectSameScreenOnly = sameScreen
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
        DeactivateSecondaryBars();
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
