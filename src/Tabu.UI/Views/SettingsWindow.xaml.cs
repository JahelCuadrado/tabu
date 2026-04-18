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
        if (_viewModel.IsBarOnAllMonitors)
        {
            AllBarsRadio.IsChecked = true;
        }
        else
        {
            PrimaryBarRadio.IsChecked = true;
        }

        if (_viewModel.IsDetectSameScreenOnly)
        {
            DetectSameScreenRadio.IsChecked = true;
        }
        else
        {
            DetectAllRadio.IsChecked = true;
        }

        _initialized = true;
    }

    private void BarPlacement_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool allMonitors = AllBarsRadio.IsChecked == true;

        if (allMonitors != _viewModel.IsBarOnAllMonitors)
        {
            _viewModel.IsBarOnAllMonitors = allMonitors;
        }
    }

    private void DetectionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool sameScreen = DetectSameScreenRadio.IsChecked == true;

        if (sameScreen != _viewModel.IsDetectSameScreenOnly)
        {
            _viewModel.IsDetectSameScreenOnly = sameScreen;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
