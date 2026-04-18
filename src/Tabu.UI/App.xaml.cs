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
        _primaryViewModel.MonitorModeChangeRequested += OnMonitorModeChangeRequested;

        _primaryBar = new MainWindow(_primaryViewModel);
        _primaryBar.Show();

        base.OnStartup(e);
    }

    private void OnMonitorModeChangeRequested(bool allMonitors)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (allMonitors)
                ActivateAllMonitorBars();
            else
                DeactivateSecondaryBars();
        });
    }

    private void ActivateAllMonitorBars()
    {
        var switcher = _host.Services.GetRequiredService<WindowSwitcher>();
        var screens = switcher.GetAllScreens();
        var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);

        // Only update the filter — don't reposition the primary bar (AppBar handles it)
        if (_primaryViewModel is not null && primaryScreen is not null)
        {
            _primaryViewModel.MonitorFilter = primaryScreen.Handle;
        }

        foreach (var screen in screens.Where(s => !s.IsPrimary))
        {
            var vm = new MainViewModel(switcher, startPolling: false)
            {
                MonitorFilter = screen.Handle,
                IsAllMonitors = true
            };
            vm.MonitorModeChangeRequested += OnMonitorModeChangeRequested;

            var bar = new MainWindow(vm);
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

        // Only clear the filter — don't reposition (AppBar keeps it in place)
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
