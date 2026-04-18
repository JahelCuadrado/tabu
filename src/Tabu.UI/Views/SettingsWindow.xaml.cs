using System.Windows;
using Tabu.UI.ViewModels;

namespace Tabu.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _initialized;

    public SettingsWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        // Sync radio buttons with current state
        if (_viewModel.IsAllMonitors)
        {
            AllMonitorsRadio.IsChecked = true;
        }
        else
        {
            PrimaryOnlyRadio.IsChecked = true;
        }

        _initialized = true;
    }

    private void MonitorMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool allMonitors = AllMonitorsRadio.IsChecked == true;

        if (allMonitors != _viewModel.IsAllMonitors)
        {
            _viewModel.ToggleMonitorModeCommand.Execute(null);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
